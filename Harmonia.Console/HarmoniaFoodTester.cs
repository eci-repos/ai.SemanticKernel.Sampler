using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI; // or Azure, depending on your setup

using Harmonia.Executor;
using Harmonia.ResultFormat;
using HarmoniaFoodTools;
using ai.SemanticKernel.Library;
using System.Text.Json.Serialization;


// -------------------------------------------------------------------------------------------------
// EMPHASIS: is being placed on the JSON Harmony Request/Response Format parsing capabilities; find
//           JSON parsing-execution within the HarmonyExecutor class.
// TODO: Harmony Request/Response Format needs to be tested and documented.
// -------------------------------------------------------------------------------------------------
namespace HarmoniaFoodDemo;

/// <summary>
/// Runs the AsianFoodPrep Harmony script end-to-end using Semantic Kernel
/// and the AsianFoodTools plugin.
///
/// Responsibilities:
///  - Load the Harmony conversation JSON for the Asian food planner.
///  - Convert HarmonyConversation to ChatHistory via SemanticKernelInterop.
///  - Call the LLM, parse Harmony-formatted assistant messages, and append them.
///  - Execute tool calls using SemanticKernelInterop + AsianFoodTools.
///  - Stop when a final assistant message appears and print it.
/// </summary>
public sealed class HarmoniaFoodTester
{
   private readonly Kernel _kernel;
   private readonly IChatCompletionService _chat;

   /// <summary>
   /// Constructs the runner with a configured Semantic Kernel instance.
   /// The Kernel must:
   ///   - Expose an IChatCompletionService for the chosen model.
   ///   - Have the AsianFoodTools plugin imported as "functions".
   /// </summary>
   /// <param name="kernel">Configured Semantic Kernel instance.</param>
   public HarmoniaFoodTester(Kernel kernel)
   {
      _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
      _chat = _kernel.GetRequiredService<IChatCompletionService>();
   }

   /// <summary>
   /// Runs the AsianFoodPrep Harmony conversation until a final answer is produced
   /// or a maximum number of steps is reached.
   /// </summary>
   /// <param name="harmonyJsonPath">
   /// Path to the JSON file containing the Harmony conversation
   /// (for example, asian_food_prep_harmony.json).
   /// </param>
   /// <param name="ct">Optional cancellation token.</param>
   public async Task RunAsync(string harmonyJsonPath, CancellationToken ct = default)
   {
      if (string.IsNullOrWhiteSpace(harmonyJsonPath))
         throw new ArgumentException("JSON path must be provided.", nameof(harmonyJsonPath));

      // 1. Load the initial Harmony conversation from disk.
      HarmonyConversation convo = LoadHarmonyConversation(harmonyJsonPath);

      // 2. Fixed-step loop:
      //    - HarmonyConversation -> ChatHistory
      //    - LLM produces Harmony assistant messages
      //    - Merge messages into HarmonyConversation
      //    - Execute tool calls
      //    - Check for final answer
      const int maxSteps = 8;

      for (int step = 0; step < maxSteps; step++)
      {
         ct.ThrowIfCancellationRequested();

         // Convert HarmonyConversation to SK ChatHistory
         ChatHistory history = convo.ToChatHistory();

         // Ask the model for the next Harmony assistant output
         ChatMessageContent reply = await _chat.GetChatMessageContentAsync(
            history,
            kernel: _kernel,
            cancellationToken: ct);

         // Parse Harmony text into one or more HarmonyMessage instances.
         // NOTE: This is a placeholder. Replace ParseAssistantMessages with
         // a real Harmony parser when available.
         IReadOnlyList<HarmonyMessage> assistantMessages = ParseAssistantMessages(reply.Content);

         // Append assistant messages to the conversation
         foreach (var msg in assistantMessages)
         {
            convo.Messages.Add(msg);
         }

         // Execute any tool calls (assistant + commentary + Recipient) via SK
         await convo.ExecuteToolCallsAsync(_kernel, ct);

         // Check if we now have a final assistant answer
         HarmonyMessage? final = FindLastFinalAssistantMessage(convo);
         if (final != null)
         {
            Console.WriteLine("=== AsianFoodPrep FINAL (Harmony) ===");
            Console.WriteLine(final.Content);
            return;
         }
      }

      Console.WriteLine("No final message produced within the step limit.");
   }

   /// <summary>
   /// Loads a HarmonyConversation from a JSON file. The JSON must contain
   /// a 'messages' array as in the Asian food Harmony sample.
   /// </summary>
   private static HarmonyConversation LoadHarmonyConversation(string path)
   {
      string json = File.ReadAllText(path);

      var options = new JsonSerializerOptions
      {
         PropertyNameCaseInsensitive = true
      };
      options.Converters.Add(new JsonStringEnumConverter());

      HarmonyConversation convo;
      try
      {
         convo = JsonSerializer.Deserialize<HarmonyConversation>(json, options);
      }
      catch (JsonException ex)
      {
         // TODO: send messsage to LOG
         throw new InvalidOperationException("Failed to deserialize HarmonyConversation from JSON.", ex);
      }

      convo.Messages ??= new List<HarmonyMessage>();
      return convo;
   }

   /// <summary>
   /// Finds the last assistant message on the Final channel, if any.
   /// Used as the stopping condition for the runner.
   /// </summary>
   private static HarmonyMessage? FindLastFinalAssistantMessage(HarmonyConversation convo)
   {
      if (convo?.Messages == null) return null;

      for (int i = convo.Messages.Count - 1; i >= 0; i--)
      {
         var m = convo.Messages[i];
         if (string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) &&
             m.Channel == HarmonyChannel.Final)
         {
            return m;
         }
      }

      return null;
   }

   /// <summary>
   /// Placeholder Harmony parsing: wraps the entire reply as a single
   /// assistant commentary message. This is enough to exercise the
   /// SemanticKernelInterop workflow, but does NOT parse real Harmony syntax.
   ///
   /// Replace this with a real Harmony parser when you integrate the
   /// official Harmony renderer/decoder.
   /// </summary>
   private static IReadOnlyList<HarmonyMessage> ParseAssistantMessages(string? content)
   {
      return new[]
      {
         new HarmonyMessage
         {
            Role = "assistant",
            Channel = HarmonyChannel.Commentary,
            Content = JsonSerializer.SerializeToElement(content ?? string.Empty)
         }
      };
   }

   // ----------------------------------------------------------------------------------------------

   /// <summary>
   /// Example entry point: builds a Kernel, imports AsianFoodTools as 'functions',
   /// then runs the AsianFoodPrep scenario using the given JSON file path.
   /// </summary>
   public static async Task MainAsync(string[] args)
   {
      string jsonPath = args.Length > 0
         ? args[0]
         : "foodPlannerSample.1.json";

      Kernel kernel = BuildKernelWithTools();
      var runner = new HarmoniaFoodTester(kernel);
      await runner.RunAsync("Scripts/" + jsonPath);
   }

   /// <summary>
   /// Builds a Semantic Kernel instance that:
   ///  - Has a chat completion service configured.
   ///  - Imports the AsianFoodTools plugin as 'functions', so that
   ///    Harmony tool calls like 'functions.search_recipes' resolve.
   /// </summary>
   private static Kernel BuildKernelWithTools()
   {
      var host = KernelHost.PrepareKernelHost(null);

      // Import the AsianFoodTools plugin under the 'functions' namespace.
      // This ensures kernel.Plugins.TryGetFunction("functions", "search_recipes", ...)
      // will succeed inside SemanticKernelInterop.ExecuteToolCallsAsync.
      host.Instance.ImportPluginFromObject(new FoodTools(), "functions");

      return host.Instance;
   }

}
