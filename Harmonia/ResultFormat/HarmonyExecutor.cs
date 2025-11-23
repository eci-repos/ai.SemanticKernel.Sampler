using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;

public sealed class HarmonyExecutionResult
{
   public string FinalText { get; set; } = string.Empty;
   public Dictionary<string, object?> Vars { get; set; } = new();
}

// -------------------------------------------------------------------------------------------------

/// <summary>
/// Executes Harmony workflows by orchestrating chat-based operations, tool invocations, and
/// conditional logic using a provided kernel and chat completion service.
/// </summary>
/// <remarks>HarmonyExecutor coordinates the execution of multi-step workflows defined in Harmony 
/// envelopes, managing context variables, chat history, and plugin function calls. It is designed 
/// for scenarios where conversational AI and workflow automation are integrated, such as chatbots 
/// or virtual assistants. This class is intended to be used as a top-level executor and is not 
/// thread-safe; concurrent usage should be managed externally.</remarks>
public sealed class HarmonyExecutor
{
   private readonly Kernel _kernel;
   private readonly IChatCompletionService _chat;
   private readonly JsonSerializerOptions _jsonOpts;

   public HarmonyExecutor(Kernel kernel)
   {
      _kernel = kernel;
      _chat = _kernel.GetRequiredService<IChatCompletionService>();
      _jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
   }

   public async Task<HarmonyExecutionResult> ExecuteAsync(
       HarmonyEnvelope envelope,
       IDictionary<string, object?> input,
       CancellationToken ct = default)
   {
      var script = envelope.GetScript();

      // Initialize vars (from script.vars defaults)
      var vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
      if (script.Vars is not null)
      {
         foreach (var kvp in script.Vars)
         {
            vars[kvp.Key] = FromJsonElement(kvp.Value);
         }
      }

      // Build initial chat history from plain system/user messages
      var chatHistory = new ChatHistory();
      foreach (var (channel, content) in envelope.GetPlainSystemPrompts())
      {
         if (!string.IsNullOrWhiteSpace(content))
            chatHistory.AddSystemMessage(content);
      }

      var user = envelope.GetUserMessage();
      if (user is { } u && !string.IsNullOrWhiteSpace(u.Content))
      {
         chatHistory.AddUserMessage(u.Content);
      }

      // Prepare execution
      var execCtx = new ExecContext(_kernel, _chat, chatHistory, vars, input, _jsonOpts);

      // Execute steps sequentially
      foreach (var step in script.Steps)
      {
         var halted = await ExecuteStepAsync(execCtx, step, ct);
         if (halted) break;
      }

      // If no explicit final added, we can still ask the LLM to summarize
      if (string.IsNullOrWhiteSpace(execCtx.FinalText))
      {
         // As a fallback, ask LLM to compile a final answer from the context
         chatHistory.AddSystemMessage(
            "Summarize the results from the executed plan above for the user.");
         var result = await _chat.GetChatMessageContentsAsync(
            chatHistory, kernel: _kernel, cancellationToken: ct);
         execCtx.FinalText = string.Join("\n", result.Select(r => r.Content));
      }

      return new HarmonyExecutionResult
      {
         FinalText = execCtx.FinalText,
         Vars = new(execCtx.Vars)
      };
   }

   /// <summary>
   /// Normalize parameters to match expected types in function signature.
   /// </summary>
   /// <param name="func"></param>
   /// <param name="args"></param>
   /// <returns></returns>
   private KernelArguments NormalizeParameters(KernelFunction func, KernelArguments args)
   {
      var normalizedArgs = new KernelArguments();
      foreach (var key in args.Keys)
      {
         var val = args[key];
         // Try find matching param (case-insensitive)
         var param = func.Metadata.Parameters
             .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
         if (param != null && param.ParameterType == typeof(string[]))
         {
            if (val is string s)
            {
               var arr = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(x => x.Trim())
                          .ToArray();
               normalizedArgs[key] = arr;
               continue;
            }
            if (val is IEnumerable<object?> list)
            {
               // convert enumerable of items into string[] if necessary
               normalizedArgs[key] = list.Select(x => x?.ToString() ?? string.Empty).ToArray();
               continue;
            }
         }
         // default passthrough
         normalizedArgs[key] = val;
      }
      return normalizedArgs;
   }

   /// <summary>
   /// Executes a single workflow step asynchronously within the provided execution context.
   /// </summary>
   /// <remarks>This method processes various step types, such as input extraction, tool invocation,
   /// conditional branching, assistant messaging, and halting. The behavior and side effects 
   /// depend on the specific step provided. The method may update context variables, chat history,
   /// or final output text as part of execution.</remarks>
   /// <param name="ctx">The execution context that maintains state, variables, and services 
   /// required for step execution.</param>
   /// <param name="step">The workflow step to execute. The type of step determines the specific 
   /// action performed.</param>
   /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.
   /// </param>
   /// <returns>A task that represents the asynchronous operation. The task result is 
   /// <see langword="true"/> if execution should be halted; otherwise, 
   /// <see langword="false"/>.</returns>
   /// <exception cref="InvalidOperationException">Thrown if a referenced plugin function cannot be 
   /// found during a tool call step.</exception>
   /// <exception cref="NotSupportedException">Thrown if the specified step type is not supported.
   /// </exception>
   private async Task<bool> ExecuteStepAsync(
      ExecContext ctx, HarmonyStep step, CancellationToken ct)
   {
      switch (step)
      {
         case ExtractInputStep s:
            foreach (var (varName, expr) in s.Output)
            {
               var value = ctx.EvalExpression(expr);
               ctx.Vars[varName] = value;
            }
            return false;

         case ToolCallStep s:
            {
               var (pluginName, functionName) = ParseRecipient(s.Recipient);
               var args = new KernelArguments();
               foreach (var (k, v) in s.Args)
               {
                  args[k] = ctx.EvalNode(v);
               }

               var func = ctx.Kernel.Plugins.GetFunction(pluginName, functionName)
                  ?? throw new InvalidOperationException(
                     $"Function '{pluginName}.{functionName}' not found.");

               // Normalize args to match expected parameter types
               var normalizedArgs = NormalizeParameters(func, args);

               var result = await ctx.Kernel.InvokeAsync(func, normalizedArgs, ct);
               object? value = result.GetValue<object>();
               ctx.Vars[s.SaveAs] = value;
               return false;
            }

         case IfStep s:
            {
               var condition = ctx.EvalBoolean(s.Condition);
               var branch = condition ? s.Then : s.Else;
               foreach (var inner in branch)
               {
                  var halted = await ExecuteStepAsync(ctx, inner, ct);
                  if (halted) return true;
               }
               return false;
            }

         case AssistantMessageStep s:
            {
               // Render content or template
               var text = !string.IsNullOrWhiteSpace(s.ContentTemplate)
                   ? ctx.RenderTemplate(s.ContentTemplate!)
                   : (s.Content ?? string.Empty);

               if (s.Channel?.Equals("analysis", StringComparison.OrdinalIgnoreCase) == true)
               {
                  // Analysis is typically internal; we can keep it in history
                  if (!string.IsNullOrWhiteSpace(text))
                     ctx.Chat.AddAssistantMessage(text);
                  return false;
               }

               if (s.Channel?.Equals("final", StringComparison.OrdinalIgnoreCase) == true)
               {
                  if (!string.IsNullOrWhiteSpace(text) && text != ".")
                  {
                     // If template produced material text, use it directly
                     ctx.FinalText = text;
                     return false;
                  }

                  // Otherwise, ask LLM to produce final answer given the history
                  var result = await ctx.ChatSvc.GetChatMessageContentsAsync(
                     ctx.Chat, kernel: ctx.Kernel, cancellationToken: ct);
                  ctx.FinalText = string.Join("\n", result.Select(r => r.Content));
                  return false;
               }

               // Any other channel -> append to chat
               if (!string.IsNullOrWhiteSpace(text))
                  ctx.Chat.AddAssistantMessage(text);

               return false;
            }

         case HaltStep:
            return true;

         default:
            throw new NotSupportedException($"Unsupported step type: {step.Type}");
      }
   }

   private static (string plugin, string function) ParseRecipient(string recipient)
   {
      // e.g. "functions.search_recipes" -> ("functions", "search_recipes")
      var idx = recipient.IndexOf('.');
      if (idx <= 0 || idx >= recipient.Length - 1)
         throw new InvalidOperationException(
            $"Invalid recipient '{recipient}'. Expected 'plugin.functionName'.");
      return (recipient[..idx], recipient[(idx + 1)..]);
   }

   private static object? FromJsonElement(JsonElement e)
   {
      return e.ValueKind switch
      {
         JsonValueKind.String => e.GetString(),
         JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
         JsonValueKind.True => true,
         JsonValueKind.False => false,
         JsonValueKind.Null => null,
         JsonValueKind.Object => JsonDocument.Parse(e.GetRawText()).RootElement.Clone(),
         JsonValueKind.Array => JsonDocument.Parse(e.GetRawText()).RootElement.Clone(),
         _ => e.GetRawText()
      };
   }

   // ----------------------------------------------------------------------------------------------

   /// <summary>
   /// Provides contextual data and utility methods for executing chat-based operations, including
   /// access to kernel services, chat history, variable storage, and input parameters.
   /// </summary>
   /// <remarks>ExecContext encapsulates all resources and state required for evaluating 
   /// expressions, rendering templates, and managing chat interactions within an execution flow. 
   /// It is intended for internal use to coordinate execution logic and should not be instantiated
   /// directly by consumers. Thread safety is not guaranteed; concurrent access should be managed 
   /// externally if required.</remarks>
   private sealed class ExecContext
   {
      public Kernel Kernel { get; }
      public IChatCompletionService ChatSvc { get; }
      public ChatHistory Chat { get; }
      public Dictionary<string, object?> Vars { get; }
      public IDictionary<string, object?> Input { get; }
      public string FinalText { get; set; } = string.Empty;
      private readonly JsonSerializerOptions _opts;

      public ExecContext(Kernel kernel, IChatCompletionService chat, ChatHistory history,
                         Dictionary<string, object?> vars, IDictionary<string, object?> input,
                         JsonSerializerOptions opts)
      {
         Kernel = kernel;
         ChatSvc = chat;
         Chat = history;
         Vars = vars;
         Input = input;
         _opts = opts;
      }

      public object? EvalNode(JsonElement node)
      {
         if (node.ValueKind == JsonValueKind.String)
         {
            var s = node.GetString() ?? string.Empty;
            if (s.StartsWith("$")) return EvalExpression(s);
            return s;
         }
         return HarmonyExecutor.FromJsonElement(node);
      }

      /// <summary>
      /// Evaluates a simple expression and returns its computed value, supporting patterns such as 
      /// variable access, input property access, length calculation, and property mapping.
      /// </summary>
      /// <remarks>Supported patterns allow dynamic access to variables and input properties, as
      /// well as basic collection operations. If the expression does not match a supported 
      /// pattern, it is returned as-is. The method does not perform complex parsing and is 
      /// intended for simple evaluation scenarios.</remarks>
      /// <param name="expr">The expression to evaluate. Supported patterns include variable 
      /// references (e.g., "$vars.x"), input property access (e.g., "$input.x"), length calculation
      /// (e.g., "$len(collection)"), and property mapping (e.g., "$map(collection, 'property')").
      /// The expression must not be null.</param>
      /// <returns>The result of evaluating the expression. Returns the computed value for
      /// supported patterns, or the original expression string if no pattern is matched.</returns>
      /// <exception cref="InvalidOperationException">Thrown if the "$map" pattern is used with an
      /// argument list that does not contain exactly two elements:  
      ///    collection and a property name.</exception>
      public object? EvalExpression(string expr)
      {
         // Minimal evaluator for patterns: $vars.x, $input.x, $len(...), $map(...)
         expr = expr.Trim();
         if (!expr.StartsWith("$", StringComparison.Ordinal)) return expr;

         if (expr.StartsWith("$len(", StringComparison.Ordinal))
         {
            var inner = Between(expr, "$len(", ")");
            var value = EvalExpression(inner);
            if (value is JsonElement je)
            {
               return je.ValueKind == JsonValueKind.Array ? je.GetArrayLength() : 0;
            }
            if (value is IEnumerable<object?> list) return list.Count();
            if (value is string s) return s.Length;
            if (value is ICollection<object?> col) return col.Count;
            return 0;
         }

         if (expr.StartsWith("$map(", StringComparison.Ordinal))
         {
            // $map(vars.recipes, 'name')
            var inner = Between(expr, "$map(", ")");
            var parts = SplitArgs(inner);
            if (parts.Count != 2) throw new InvalidOperationException(
               "map expects two args: collection, 'prop'");
            var collection = EvalExpression(parts[0]);
            var prop = parts[1].Trim().Trim('\'', '"');

            var results = new List<object?>();
            if (collection is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
               foreach (var item in je.EnumerateArray())
               {
                  if (item.ValueKind == JsonValueKind.Object && 
                      item.TryGetProperty(prop, out var v))
                  {
                     results.Add(FromJsonElement(v));
                  }
               }
               return results;
            }
            if (collection is IEnumerable<object?> enumerable)
            {
               foreach (var item in enumerable)
               {
                  if (item is JsonElement o && o.ValueKind == 
                     JsonValueKind.Object && o.TryGetProperty(prop, out var v))
                     results.Add(FromJsonElement(v));
                  else if (item is IDictionary<string, object?> dict && 
                     dict.TryGetValue(prop, out var v2))
                     results.Add(v2);
               }
               return results;
            }
            return results;
         }

         if (expr.StartsWith("$input.", StringComparison.Ordinal))
         {
            var path = expr.Substring("$input.".Length);
            return ResolvePath(Input, path);
         }

         if (expr.StartsWith("$vars.", StringComparison.Ordinal))
         {
            var path = expr.Substring("$vars.".Length);
            return ResolvePath(Vars, path);
         }

         // Literal fall-through
         return expr;
      }

      public bool EvalBoolean(string condition)
      {
         // Support very simple comparisons: <, <=, ==, !=, >=, >
         // Left/right may be expressions like $len(vars.recipes)
         var m = Regex.Match(condition, @"^(?<left>.+?)\s*(?<op>==|!=|<=|>=|<|>)\s*(?<right>.+?)$");
         if (!m.Success)
         {
            // Single term truthiness?
            var single = EvalExpression(condition);
            return IsTruthy(single);
         }

         var left = EvalExpression(m.Groups["left"].Value);
         var right = EvalExpression(m.Groups["right"].Value);
         var op = m.Groups["op"].Value;

         int cmp = Compare(left, right);
         return op switch
         {
            "==" => AreEqual(left, right),
            "!=" => !AreEqual(left, right),
            "<" => cmp < 0,
            "<=" => cmp <= 0,
            ">" => cmp > 0,
            ">=" => cmp >= 0,
            _ => false
         };
      }

      public string RenderTemplate(string template)
      {
         // Very minimal mustache-like replacement: {{vars.key}}, {{input.key}}
         // Nested lookups by dot-path are supported.
         return Regex.Replace(template, @"\{\{\s*(?<path>[^}]+)\s*\}\}", m =>
         {
            var path = m.Groups["path"].Value.Trim();
            if (path.StartsWith("vars.", StringComparison.Ordinal))
            {
               var v = ResolvePath(Vars, path.Substring(5));
               return ToString(v);
            }
            if (path.StartsWith("input.", StringComparison.Ordinal))
            {
               var v = ResolvePath(Input, path.Substring(6));
               return ToString(v);
            }
            return m.Value;
         });
      }

      private static object? ResolvePath(IDictionary<string, object?> dict, string path)
      {
         var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
         object? current = dict;

         foreach (var part in parts)
         {
            if (current is IDictionary<string, object?> d)
            {
               if (!d.TryGetValue(part, out current)) return null;
               continue;
            }
            if (current is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
               if (!je.TryGetProperty(part, out var v)) return null;
               current = FromJsonElement(v);
               continue;
            }
            return null;
         }

         return current;
      }

      private static string Between(string s, string start, string end)
      {
         var i = s.IndexOf(start, StringComparison.Ordinal);
         if (i < 0) return string.Empty;
         i += start.Length;
         var j = s.IndexOf(end, i, StringComparison.Ordinal);
         if (j < 0) return string.Empty;
         return s.Substring(i, j - i);
      }

      private static List<string> SplitArgs(string s)
      {
         // split by comma outside quotes
         var list = new List<string>();
         var sb = new StringBuilder();
         bool inQuotes = false;

         foreach (var ch in s)
         {
            if (ch == '\'' || ch == '"') inQuotes = !inQuotes;
            if (ch == ',' && !inQuotes)
            {
               list.Add(sb.ToString());
               sb.Clear();
            }
            else sb.Append(ch);
         }
         if (sb.Length > 0) list.Add(sb.ToString());
         return list;
      }

      private static string ToString(object? v)
          => v switch
          {
             null => string.Empty,
             JsonElement je => je.ToString(),
             _ => v.ToString() ?? string.Empty
          };

      private static bool IsTruthy(object? v)
          => v switch
          {
             null => false,
             bool b => b,
             string s => !string.IsNullOrWhiteSpace(s),
             JsonElement je => je.ValueKind != JsonValueKind.Null &&
                je.ValueKind != JsonValueKind.Undefined,
                _ => true
          };

      private static bool AreEqual(object? a, object? b)
      {
         if (a is null && b is null) return true;
         if (a is null || b is null) return false;

         if (TryNumber(a, out var da) && TryNumber(b, out var db)) return da == db;
         return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
      }

      private static int Compare(object? a, object? b)
      {
         if (TryNumber(a, out var da) && TryNumber(b, out var db))
            return da.CompareTo(db);

         return string.Compare(a?.ToString(), b?.ToString(), StringComparison.Ordinal);
      }

      private static bool TryNumber(object? v, out double d)
      {
         if (v is double dd) { d = dd; return true; }
         if (v is float ff) { d = ff; return true; }
         if (v is long ll) { d = ll; return true; }
         if (v is int ii) { d = ii; return true; }
         if (v is string s && double.TryParse(s, out d)) return true;
         if (v is JsonElement je && je.ValueKind == JsonValueKind.Number)
         {
            if (je.TryGetInt64(out var l)) { d = l; return true; }
            d = je.GetDouble(); return true;
         }
         d = 0; return false;
      }
   }

}

