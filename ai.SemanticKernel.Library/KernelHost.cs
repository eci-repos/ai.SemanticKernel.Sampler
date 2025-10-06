using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

/// <summary>
/// Kernel Instance helper.
/// </summary>
public class KernelHost
{

   private Kernel _kernel;
   private object _config;

   public Kernel Instance
   {
      get { return _kernel; }
   }

   public object Config
   {
      get { return _config; }
   }

   /// <summary>
   /// Kernel instance constructor.
   /// </summary>
   /// <param name="config">configuration/settings based on KernelModelConfig</param>
   public KernelHost(object config)
   {
      _config = config;
      _kernel = PrepareKernel(config as KernelConfig);
   }

   /// <summary>
   /// Creates a KernelHost instance based on the provided configuration.
   /// </summary>
   /// <param name="config">configuration to be used</param>
   /// <returns>Instance of KernelHost is returned</returns>
   public static async Task<KernelHost> CreateAsync(object config)
   {
      var host = new KernelHost(config);
      await Task.CompletedTask;
      return host;
   }

   /// <summary>
   /// Retrieves an instance of an embedding generator for generating embeddings
   /// from string inputs.
   /// </summary>
   /// <remarks>The returned embedding generator is resolved from the underlying dependency 
   /// injection    /// container. Ensure that the required service is registered before calling
   /// this method.</remarks>
   /// <returns>An implementation of <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>
   /// where the input type is <see
   /// cref="string"/> and the embedding type is <see cref="Embedding{T}"/> 
   /// with <see cref="float"/> values.</returns>
   public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator()
   {
      return _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
   }

   /// <summary>
   /// Retrieves an instance of the <see cref="IChatCompletionService"/> from the service container.
   /// </summary>
   /// <remarks>The returned service instance is resolved using the dependency injection container. 
   /// Ensure that the service  is registered in the container before calling this method.</remarks>
   /// <returns>An instance of <see cref="IChatCompletionService"/>. This instance is resolved from 
   /// the service container.</returns>
   public IChatCompletionService GetChatCompletionService()
   {
      return _kernel.GetRequiredService<IChatCompletionService>();
   }

   /// <summary>
   /// Returns a rewrite function that can be used to create a function that rewrites text to be.
   /// </summary>
   /// <param name="prompt">optional prompt to use (default: null)</param>
   /// <returns>rewrite function is returned</returns>
   public KernelFunction GetRewriteFunction(string prompt = null)
   {
      if (String.IsNullOrWhiteSpace(prompt))
      {
         prompt = """
            You are a helpful editor.
            Rewrite the following text to be clear and concise. {{$input}}
            """;
      }

      var func = _kernel.CreateFunctionFromPrompt(
         promptTemplate: prompt,
         functionName: "rewrite");
      _kernel.Plugins.AddFromFunctions("writing", new[] { func });
      return func;
   }

   /// <summary>
   /// Using given configuration details build a kernel instance.
   /// </summary>
   /// <remarks>This method sets up the necessary components for context-based searches, 
   /// including a connection to a Qdrant vector store and integration with Ollama for chat 
   /// completions and embedding generation. The Qdrant client defaults to  localhost:6333 unless 
   /// otherwise specified in the <paramref name="config"/>The following components are configured: 
   /// <list type="bullet">
   ///    <item><description>Qdrant vector store for storing and retrieving embeddings.
   ///       </description>
   ///    </item>
   ///    <item><description>Ollama chat completion for generating conversational responses.
   ///       </description></item> 
   ///    <item><description>Ollama embedding generator for creating vector embeddings.
   ///       </description></item>
   /// </list>
   /// </remarks>
   /// <param name="config">The configuration settings used to initialize the context search 
   /// manager,  including model endpoints, embedding models, and vector store connection details.
   /// </param>
   /// <returns>instance of kernel returned</returns>
   public static Kernel PrepareKernel(KernelConfig? config)
   {

      // Build kernel with Ollama chat + embeddings
      IKernelBuilder builder = Kernel.CreateBuilder();

      // Chat completion 
      if (config.ModelNeeded)
      {
         builder.AddOllamaChatCompletion(
             modelId: config.ChatModel,
             endpoint: new Uri(config.Endpoint));
      }

      // Embeddings
      if (config.EmbeddingsNeeded)
      {
         builder.AddOllamaEmbeddingGenerator(
            modelId: config.EmbeddingModel,
            endpoint: new Uri(config.Endpoint));
      }

      // Register Qdrant Vector Store
      if (config.MemoryStoreNeeded)
      {
         // Qdrant connection (defaults to localhost:6333; add API key/URI if needed)
         var qdrantClient = new QdrantClient(host: config.StoreHost, port: config.StorePort);

         builder.Services.AddSingleton(qdrantClient);
         builder.Services.AddQdrantVectorStore(); // DI extension from SK Qdrant connector
      }

      // GetRewriteFunction();

      var kernel = builder.Build();

      return kernel;
   }

   /// <summary>
   /// Executes a workflow based on the provided payload and returns the result.
   /// </summary>
   /// <remarks>The method currently supports the "draft-and-rewrite" workflow, which generates a 
   /// draft based on  a topic and rewrites it in a specified style. If the "inputs" property is 
   /// provided in the payload, it may include the following: 
   ///    <list type="bullet">
   ///       <item>
   ///          <description> <c>topic</c>: A string representing the topic for the draft. 
   ///             Defaults to "a note" if not provided.
   ///          </description>
   ///       </item>
   ///       <item>
   ///          <description> <c>style</c>: A string specifying the writing style 
   ///             (e.g., "concise and friendly"). Defaults to  "concise and friendly" if not
   ///             provided.
   ///          </description>
   ///       </item>
   ///    </list>
   /// If the workflow name is not recognized, the method returns a failure result with an 
   /// appropriate  error message.
   /// </remarks>
   /// <param name="payload">A <see cref="JsonDocument"/> containing the workflow definition.
   /// The payload must include a  "name" property specifying the workflow to execute, and may 
   /// include additional properties  required by the workflow.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.
   /// </param>
   /// <returns>A <see cref="RequestResult"/> containing the outcome of the workflow execution. 
   /// If the workflow  is successfully executed, the result includes the workflow output. 
   /// If the workflow name is  unrecognized, the result indicates failure.</returns>
   public async Task<RequestResult> RunWorkflowAsync(JsonDocument payload, CancellationToken ct)
   {
      var root = payload.RootElement;
      var name = root.GetProperty("name").GetString()!;
      var inputs = root.TryGetProperty("inputs", out var inp) ? inp : default;

      switch (name)
      {
         case "draft-and-rewrite":
            // inputs: { topic: string, style?: string }
            var topic = root.TryGetProperty("topic", out var topicEl) ? 
               topicEl.GetString() : "a note";
            var style = root.TryGetProperty("style", out var styleEl) ? 
               styleEl.GetString() : "concise and friendly";
            var maxToks = payload.RootElement.TryGetProperty("max_tokens", out var mtEl) ? 
               mtEl.GetInt32() : (int?)null;

            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            history.AddSystemMessage($"Write a short draft about the topic in a {style} style.");
            history.AddUserMessage(topic!);

            PromptExecutionSettings settings = null; // optional

            if (maxToks.HasValue)
               settings = new OpenAIPromptExecutionSettings { MaxTokens = maxToks };

            var draft = await chat.GetChatMessageContentAsync(history, settings, _kernel, ct);

            var rewriter = _kernel.Plugins["writing"];
            var rewritten = await _kernel.InvokeAsync(
               rewriter["rewrite"], new() { ["input"] = draft.Content ?? string.Empty }, ct);

            return RequestResult.Okey(new
            {
               draft = draft.Content,
               final = rewritten.ToString()
            });

         default:
            return RequestResult.Fail($"Unknown workflow: {name}");
      }
   }

}

