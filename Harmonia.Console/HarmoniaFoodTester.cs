using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Harmonia.ResultFormat;
using Harmonia.Executor;                  // SemanticKernelInterop
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

// -------------------------------------------------------------------------------------------------
namespace HarmoniaFoodTester
{

   /// <summary>
   /// Entry point for running the AsianFoodPrep Harmony script end-to-end using Semantic Kernel.
   /// 
   /// Responsibilities:
   ///  - Load the Harmony conversation JSON for the Asian food planner.
   ///  - Drive a loop that:
   ///      * Converts HarmonyConversation to ChatHistory.
   ///      * Asks the LLM (via Semantic Kernel) for the next Harmony assistant messages.
   ///      * Executes any tool calls using SemanticKernelInterop.
   ///      * Stops when a final assistant message is produced.
   /// </summary>
   public sealed class AsianFoodPrepRunner
   {
      private readonly Kernel _kernel;
      private readonly IChatCompletionService _chat;

      /// <summary>
      /// Constructs the runner with a configured Semantic Kernel instance.
      /// The Kernel must:
      ///   - Have an IChatCompletionService registered.
      ///   - Register plugins/functions under the "functions" namespace:
      ///       search_recipes, check_pantry, generate_shopping_list,
      ///       build_prep_schedule, get_cultural_background.
      /// </summary>
      /// <param name="kernel">Configured Semantic Kernel instance.</param>
      public AsianFoodPrepRunner(Kernel kernel)
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
      /// (e.g., asian_food_prep_harmony.json).
      /// </param>
      /// <param name="ct">Optional cancellation token.</param>
      public async Task RunAsync(string harmonyJsonPath, CancellationToken ct = default)
      {
         if (string.IsNullOrWhiteSpace(harmonyJsonPath))
            throw new ArgumentException("JSON path must be provided.", nameof(harmonyJsonPath));

         // 1. Load and deserialize the Harmony conversation from JSON.
         HarmonyConversation convo = LoadHarmonyConversation(harmonyJsonPath);

         // 2. Simple fixed-step loop that:
         //    - feeds HarmonyConversation to the model,
         //    - integrates the model's Harmony assistant messages,
         //    - executes tool calls,
         //    - checks for a final assistant message.
         const int maxSteps = 8;

         for (int step = 0; step < maxSteps; step++)
         {
            ct.ThrowIfCancellationRequested();

            // Convert HarmonyConversation -> ChatHistory for Semantic Kernel
            ChatHistory history = convo.ToChatHistory();

            // Call the model. The model is expected to emit Harmony-formatted output
            // (with roles/channels/tools) encoded in the Content string.
            ChatMessageContent reply = await _chat.GetChatMessageContentAsync(
               history,
               kernel: _kernel,
               cancellationToken: ct);

            // Parse the Harmony-formatted reply text into one or more HarmonyMessage
            // instances. In a real system, you would use the OpenAI Harmony
            // renderer/parser here. For now, we treat the entire reply as a single
            // assistant commentary message so that ExecuteToolCallsAsync can see it.
            IReadOnlyList<HarmonyMessage> assistantMessages = ParseAssistantMessages(reply.Content);

            // Merge assistant messages into the conversation
            foreach (var msg in assistantMessages)
            {
               convo.Messages.Add(msg);
            }

            // Execute any tool calls (assistant + commentary + Recipient) via SemanticKernelInterop.
            await convo.ExecuteToolCallsAsync(_kernel, ct);

            // Check if we now have a final assistant message.
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
      /// Loads a HarmonyConversation from a JSON file on disk.
      /// Expects the file to contain the full 'messages' array as shown in the sample JSON.
      /// </summary>
      private static HarmonyConversation LoadHarmonyConversation(string path)
      {
         string json = File.ReadAllText(path);

         var options = new JsonSerializerOptions
         {
            PropertyNameCaseInsensitive = true
         };

         HarmonyConversation? convo = JsonSerializer.Deserialize<HarmonyConversation>(json, options);
         if (convo == null)
            throw new InvalidOperationException("Failed to deserialize HarmonyConversation from JSON.");

         convo.Messages ??= new List<HarmonyMessage>();
         return convo;
      }

      /// <summary>
      /// Finds the last assistant message on the Final channel, if any, which we treat
      /// as the completed response to show to the end user.
      /// </summary>
      /// <param name="convo">Conversation to inspect.</param>
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
      /// **Placeholder** Harmony parsing.
      /// 
      /// In a real implementation, you should:
      ///   - Use the official Harmony renderer/parser to turn the model's text
      ///     into a sequence of HarmonyMessage objects (roles, channels, tools, etc.).
      /// 
      /// For now, we just wrap the entire reply as a single assistant commentary
      /// message so you can wire up the loop and see end-to-end flow.
      /// </summary>
      private static IReadOnlyList<HarmonyMessage> ParseAssistantMessages(string? content)
      {
         return new[]
         {
            new HarmonyMessage
            {
               Role = "assistant",
               Channel = HarmonyChannel.Commentary,
               Content = content ?? string.Empty
            }
         };
      }

      // ------------------------------------------------------------------
      // Optional: small Program entry point example
      // ------------------------------------------------------------------

      /// <summary>
      /// Small helper to create a Kernel, construct the runner, and kick off the scenario.
      /// Adjust model/provider configuration as needed for your environment.
      /// </summary>
      public static async Task Main(string[] args)
      {
         // Path to the Harmony JSON file, defaulting if not passed on the command line.
         string jsonPath = args.Length > 0
            ? args[0]
            : "asian_food_prep_harmony.json";

         // TODO: configure your kernel properly here. The exact code depends on the
         // Semantic Kernel version and provider (OpenAI / Azure OpenAI / etc.).
         //
         // Example (pseudo-code):
         //
         // var builder = Kernel.CreateBuilder();
         // builder.AddOpenAIChatCompletion(
         //     modelId: "gpt-4.1-mini",
         //     apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
         //
         // // Register tool plugins under "functions" namespace
         // builder.Plugins.AddFromObject(new AsianFoodTools(), "functions");
         //
         // Kernel kernel = builder.Build();

         Kernel kernel = BuildKernelWithTools();

         var runner = new AsianFoodPrepRunner(kernel);
         await runner.RunAsync(jsonPath);
      }

      /// <summary>
      /// Stub for building a Kernel with your AsianFood tools.
      /// Replace this with your real SK configuration.
      /// </summary>
      private static Kernel BuildKernelWithTools()
      {
         var builder = Kernel.CreateBuilder();

         // TODO: configure your chat completion service
         // builder.AddOpenAIChatCompletion(
         //    modelId: "gpt-4.1-mini",
         //    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

         // TODO: register your tool plugin implementation under "functions"
         // builder.Plugins.AddFromObject(new AsianFoodTools(), "functions");

         return builder.Build();
      }
   }

   // ----------------------------------------------------------------------
   // Example tool plugin (stubs) to match the Harmony 'functions.*' tools.
   // ----------------------------------------------------------------------

   /// <summary>
   /// Example stub implementation of the Asian food preparation tools.
   /// Replace the bodies of these methods with real logic or data sources.
   /// Make sure the names and parameters line up with your Harmony tool
   /// definitions (search_recipes, check_pantry, etc.).
   /// </summary>
   public sealed class AsianFoodTools
   {
      // All of these method signatures are *examples*.
      // In an actual SK plugin you would decorate them with [KernelFunction]
      // and use appropriate parameter types.

      public Task<string> search_recipes(
         string cuisine_region,
         string[]? dietary_tags,
         int? max_total_time_minutes,
         int? max_dish_count,
         string? spice_level)
      {
         // TODO: return realistic JSON representing a set of recipes.
         return Task.FromResult(@"{ ""recipes"": [] }");
      }

      public Task<string> check_pantry(string free_text_inventory)
      {
         // TODO: parse inventory text and normalize ingredients.
         return Task.FromResult(@"{ ""ingredients"": [] }");
      }

      public Task<string> generate_shopping_list(object recipes, object pantry, bool allow_substitutions)
      {
         // TODO: compute missing ingredients and suggested substitutes.
         return Task.FromResult(@"{ ""shopping_list"": [] }");
      }

      public Task<string> build_prep_schedule(object recipes, string? desired_serving_time, string? experience_level)
      {
         // TODO: build a parallelizable prep schedule.
         return Task.FromResult(@"{ ""schedule"": [] }");
      }

      public Task<string> get_cultural_background(string[] items)
      {
         // TODO: return background notes for dishes/ingredients.
         return Task.FromResult(@"{ ""notes"": [] }");
      }
   }
}
