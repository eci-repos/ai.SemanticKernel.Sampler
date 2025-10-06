using ai.SemanticKernel.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Workflow.Sampler.Console.ProcessWorkflow;

public class Processor
{

   private KernelHost _kernelInstance;

   public Kernel Kernel
   {
      get { return _kernelInstance.Instance; }
   }

   public static KernelConfig GetConfig()
   {
      var config = new KernelConfig();
      return config;
   }

   /// <summary>
   /// Prepare a Kernel instance with a simple Sentiment Analysis plugin.
   /// </summary>
   /// <param name="config"></param>
   /// <returns>A Kernel instance is returned</returns>
   public Kernel PrepareKernel(KernelConfig? config = null)
   {
      if (config == null)
      {
         config = GetConfig();
      }

      _kernelInstance = new KernelHost(config);

      return Kernel;
   }

#pragma warning disable SKEXP0080
   public KernelProcess PrepareProcess()
   {
      // Define a workflow (Process) with three steps:
      //    - Gather info (stateless)
      //    - Generate draft (stateful; keeps chat history & last doc)
      //    - Publish (stateless)
      var processBuilder = new ProcessBuilder("ProductDocWorkflow");

      var gather = processBuilder.AddStepFromType<GatherProductInfoStep>();
      var draft = processBuilder.AddStepFromType<GenerateDocumentationStep>();
      var publish = processBuilder.AddStepFromType<PublishDocumentationStep>();

      // Wire up event-driven routing (this IS the “workflow”):
      // Start -> Gather -> Draft -> Publish
      processBuilder
          .OnInputEvent("Start")
          .SendEventTo(new ProcessFunctionTargetBuilder(gather));

      gather
          .OnFunctionResult()                                      // when Gather returns…
          .SendEventTo(new ProcessFunctionTargetBuilder(draft));   // …send its result to Draft

      draft
          .OnFunctionResult()                                      // ...returns a DocumentInfo…
          .SendEventTo(new ProcessFunctionTargetBuilder(publish)); // …send it to Publish

      // build the process...
      var process = processBuilder.Build();
      return process;
   }

   #region -- Workflow Steps

   /// <summary>
   /// STEP A: Stateless step that gathers structured product info
   /// </summary>
   public class GatherProductInfoStep : KernelProcessStep
   {
      [KernelFunction]
      public ProductInfo GatherProductInformation(string productName)
      {
         System.Console.WriteLine(
            $"[{nameof(GatherProductInfoStep)}] Gathering info for '{productName}'");

         return new ProductInfo
         {
            Title = productName,
            Content = """
                Product Description:
                GlowBrew an AI-driven coffee machine with programmable LEDs and a built-in grinder.

                Key Features:
                1. Luminous Brew Technology
                2. AI Taste Assistant
                3. Gourmet Aroma Diffusion

                Troubleshooting:
                - If LEDs malfunction, reset settings in the app and check internal connections.
                """
         };
      }
   }

   /// <summary>
   /// STEP B: Stateful step that maintains chat history and last generated doc. 
   /// Demonstrates built-in checkpointing/state across invocations.
   /// </summary>
   public class GenerateDocumentationStep : KernelProcessStep<GenerateDocumentationStep.State>
   {
      private State _state = new();

      private const string SystemPrompt = """
        You write clear, engaging customer-facing documentation using ONLY provided product info.
        If suggestions are provided later, incorporate them and rewrite.
        """;

      public override ValueTask ActivateAsync(KernelProcessStepState<State> state)
      {
         _state = state.State!;
         _state.Chat ??= new ChatHistory(SystemPrompt);
         return base.ActivateAsync(state);
      }

      [KernelFunction]
      public async Task<DocumentInfo> GenerateDocumentationAsync(
          Kernel kernel,
          KernelProcessStepContext context,
          ProductInfo product)
      {
         System.Console.WriteLine($"[{nameof(GenerateDocumentationStep)}] Drafting docs…");

         _state.Chat!.AddUserMessage($"Product Info:\n{product.Title}\n{product.Content}");

         var chat = kernel.GetRequiredService<IChatCompletionService>();
         var reply = await chat.GetChatMessageContentAsync(_state.Chat);

         var doc = new DocumentInfo
         {
            Id = Guid.NewGuid().ToString(),
            Title = $"Documentation: {product.Title}",
            Content = reply.Content ?? "(empty)"
         };

         _state.LastGenerated = doc;

         // You can optionally emit custom events (fan-out targets, hooks, etc.)
         await context.EmitEventAsync("DocumentationGenerated", doc);

         return doc;
      }

      public class State
      {
         public ChatHistory? Chat { get; set; }
         public DocumentInfo? LastGenerated { get; set; }
      }
   }

   /// <summary>
   /// STEP C: Stateless sink step (could publish to storage, CMS, etc.)
   /// </summary>
   public class PublishDocumentationStep : KernelProcessStep
   {
      [KernelFunction]
      public DocumentInfo Publish(DocumentInfo doc)
      {
         System.Console.WriteLine(
            $"[{nameof(PublishDocumentationStep)}] PUBLISHING\n{doc.Title}\n\n{doc.Content}\n");
         return doc;
      }
   }

   #endregion
   #region -- Data Transfer Objects (DTOs)

   public class ProductInfo
   {
      public string Title { get; set; } = "";
      public string Content { get; set; } = "";
   }

   public class DocumentInfo
   {
      public string Id { get; set; } = "";
      public string Title { get; set; } = "";
      public string Content { get; set; } = "";
   }

   #endregion

   /// <summary>
   /// Run the defined Process (workflow).
   /// </summary>
   /// <returns></returns>
   /// <exception cref="InvalidOperationException"></exception>
   public static async Task RunAsync()
   {
      var processor = new Processor();
      processor.PrepareKernel();

      var process = processor.PrepareProcess();
      if (process == null)
      {
         throw new InvalidOperationException(
            "Process not found. Make sure to call PrepareProcess() first.");
      }

      await process.StartAsync(
          processor.Kernel,
          new KernelProcessEvent { Id = "Start", Data = "GlowBrew" });
   }

}
