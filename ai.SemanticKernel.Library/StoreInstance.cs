using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public class StoreInstance
{

   private QdrantVectorStore? _vectorStore;
   private KernelInstance _kernelInstance;

   public Kernel Kernel
   {
      get { return _kernelInstance.Instance; }
   }

   public QdrantVectorStore? VectorStore
   {
      get { return _vectorStore; }
   }

   /// <summary>
   /// Gets the service responsible for generating embeddings from string inputs.
   /// </summary>
   /// <remarks>This property retrieves the embedding generation service from the dependency 
   /// injection container. Ensure that the required service is registered in the container before
   /// accessing this property.</remarks>
   private IEmbeddingGenerator<string, Embedding<float>> embeddingService
      => Kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

   public IEmbeddingGenerator<string, Embedding<float>> EmbeddingService
   {
      get { return embeddingService; }
   }

   /// <summary>
   /// Initialize Memory Store.
   /// </summary>
   /// <param name="kernel"></param>
   public StoreInstance(KernelInstance kernel)
   {
      _kernelInstance = kernel;
   }

   /// <summary>
   /// Creates and returns a configured instance of <see cref="QdrantVectorStore"/> for use with 
   /// the specified <see cref="Kernel"/>.
   /// </summary>
   /// <remarks>The returned <see cref="QdrantVectorStore"/> is initialized with a client connected 
   /// to the host specified in the configuration. The embedding generator is resolved from the
   /// provided <paramref name="kernel"/>
   /// to enable automatic embedding functionality.</remarks>
   /// <param name="kernel">The <see cref="Kernel"/> instance used to resolve required services for 
   /// the vector store.</param>
   /// <returns>A new instance of <see cref="QdrantVectorStore"/> configured with the necessary 
   /// embedding generator and client options.</returns>
   public QdrantVectorStore PrepareVectorStore(KernelModelConfig config)
   {
      // Configure Qdrant Vector Store and let it auto-embed via the generator
      _vectorStore = new QdrantVectorStore(
          qdrantClient: new QdrantClient(config.StoreHost),
          ownsClient: false,
          options: new QdrantVectorStoreOptions { EmbeddingGenerator = embeddingService }
      );

      return _vectorStore;
   }


}
