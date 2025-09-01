using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

/// <summary>
/// Kernel Instance helper.
/// </summary>
public class KernelInstance
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
   /// 
   /// </summary>
   /// <param name="config">configuration/settings based on KernelModelConfig</param>
   public KernelInstance(object config)
   {
      _config = config;
      _kernel = PrepareKernel(config as KernelModelConfig);
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
   public static Kernel PrepareKernel(KernelModelConfig? config)
   {

      // Build kernel with Ollama chat + embeddings
      IKernelBuilder builder = Kernel.CreateBuilder();

      // Chat completion 
      if (config.ModelNeeded)
      {
         builder.AddOllamaChatCompletion(
             modelId: config.Model,
             endpoint: new Uri(config.ModelEndpoint));
      }

      // Embeddings
      if (config.EmbeddingsNeeded)
      {
         builder.AddOllamaEmbeddingGenerator(
            modelId: config.EmbeddingModel,
            endpoint: new Uri(config.ModelEndpoint));
      }

      // Register Qdrant Vector Store
      if (config.MemoryStoreNeeded)
      {
         // Qdrant connection (defaults to localhost:6333; add API key/URI if needed)
         var qdrantClient = new QdrantClient(host: config.StoreHost, port: config.StorePort);

         builder.Services.AddSingleton(qdrantClient);
         builder.Services.AddQdrantVectorStore(); // DI extension from SK Qdrant connector
      }

      var kernel = builder.Build();

      return kernel;
   }

}
