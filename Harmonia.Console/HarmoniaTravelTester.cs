using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

using Harmonia;
using Harmonia.Executor;
using Harmonia.ResultFormat;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.Console;

public class HarmoniaTravelTester
{

   public static async Task RunExampleAsync(Kernel kernel, CancellationToken ct = default)
   {
      var example = new HarmonyExampleTester(kernel);
      await example.RunAsync(ct);
   }

   /// <summary>
   /// Demonstrates how to:
   /// 1. Build a HarmonyConversation for a complex scripted scenario.
   /// 2. Use SemanticKernelInterop to:
   ///    - Convert Harmony messages to a Semantic Kernel ChatHistory.
   ///    - Execute tool calls emitted by the model.
   /// 3. Drive a simple model/tool loop until a final answer is produced.
   /// </summary>
   public class HarmonyExampleTester
   {
      private readonly Kernel _kernel;
      private readonly IChatCompletionService _chatService;

      /// <summary>
      /// Initializes the example with a configured Semantic Kernel instance.
      /// The kernel must have:
      /// - A chat completion service registered.
      /// - Plugins providing the travel-related functions.
      /// </summary>
      /// <param name="kernel">The Semantic Kernel instance to use.</param>
      public HarmonyExampleTester(Kernel kernel)
      {
         _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));

         // Retrieve the default chat completion service from the kernel
         _chatService = _kernel.GetRequiredService<IChatCompletionService>();
      }

      /// <summary>
      /// Entry point that runs the full scenario:
      /// - Builds an initial HarmonyConversation, including a Harmony script.
      /// - Repeatedly calls the model and tools until a final answer is produced.
      /// - Prints the final assistant response to the console.
      /// </summary>
      /// <param name="ct">An optional cancellation token.</param>
      public async Task RunAsync(CancellationToken ct = default)
      {
         // 1. Create the initial Harmony conversation with system + script + user messages.
         var convo = CreateInitialHarmonyConversation();

         // 2. Run a simple loop:
         //    - Convert HarmonyConversation -> ChatHistory (using SemanticKernelInterop).
         //    - Ask the model for the next step.
         //    - Merge the model's reply back into the HarmonyConversation.
         //    - Execute any tool calls emitted by the model.
         //    - Repeat until we see a final assistant message.
         for (int step = 0; step < 8; step++)
         {
            ct.ThrowIfCancellationRequested();

            // Convert HarmonyConversation to ChatHistory for the model
            ChatHistory history = convo.ToChatHistory();

            // Ask the LLM for the next message(s)
            var response = await _chatService.GetChatMessageContentAsync(
               history,
               kernel: _kernel,
               cancellationToken: ct);

            // Add the model's reply back into the Harmony conversation.
            // In a real implementation, you would parse the Harmony Response Format
            // content coming from the model. For this demo, we treat the reply as
            // an assistant final message for simplicity, or as commentary that
            // may contain tool calls.
            convo.Messages.Add(new HarmonyMessage
            {
               Role = "assistant",
               Channel = HarmonyChannel.Commentary, // or Final, depending on your parsing
               Content = response.Content ?? string.Empty
            });

            // 3. Execute any tool calls that the model requested in commentary messages.
            //    This will append tool result messages back into the conversation.
            await convo.ExecuteToolCallsAsync(_kernel, ct);

            // 4. Check if we already have a final assistant answer in the conversation.
            var final = GetLastFinalAssistantMessage(convo);
            if (final != null)
            {
               System.Console.WriteLine("=== FINAL TRIP PLAN ===");
               System.Console.WriteLine(final.Content);
               return;
            }
         }

         System.Console.WriteLine("No final response was produced within the step limit.");
      }

      /// <summary>
      /// Creates the initial HarmonyConversation for the SmartTrip scenario.
      /// This wires:
      /// - A system message describing the agent.
      /// - A system message carrying the Harmony script (as JSON or a structured payload).
      /// - The user’s trip-planning request.
      /// </summary>
      private static HarmonyConversation CreateInitialHarmonyConversation()
      {
         var convo = new HarmonyConversation
         {
            Messages = new List<HarmonyMessage>()
         };

         // High-level system prompt
         convo.Messages.Add(new HarmonyMessage
         {
            Role = "system",
            Channel = HarmonyChannel.System,
            Content = "You are SmartTrip, an AI travel planner. Use the provided Harmony script to orchestrate tools and produce realistic, budget-aware itineraries."
         });

         // Harmony script, as in the example above. Here we embed it as raw JSON
         // in a system message with a custom content_type.
         var scriptJson = System.IO.File.ReadAllText("./Scripts/PlannerSampler.1.json");

         convo.Messages.Add(new HarmonyMessage
         {
            Role = "system",
            Channel = HarmonyChannel.System,
            ContentType = "harmony-script",
            Content = scriptJson
         });

         // The user request
         convo.Messages.Add(new HarmonyMessage
         {
            Role = "user",
            Channel = HarmonyChannel.User,
            Content = "Plan a 3-day weekend trip from San Francisco to Seattle next month with a total budget of $900. I care more about experiences than hotel luxury."
         });

         return convo;
      }

      /// <summary>
      /// Locates the last assistant message on the Final channel, if any.
      /// This is used as our stopping condition: once we have a Final message,
      /// we treat it as the completed answer to return to the user.
      /// </summary>
      /// <param name="convo">The Harmony conversation to inspect.</param>
      /// <returns>The last final assistant message, or null if none exist yet.</returns>
      private static HarmonyMessage? GetLastFinalAssistantMessage(HarmonyConversation convo)
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
   }

}
