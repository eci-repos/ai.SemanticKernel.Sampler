

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

/// <summary>
/// Common Kernel and Model settings and some defaults targeting a local model and embedding 
/// services including a local Memory Store defaulted to Qdrant.
/// </summary>
public class KernelModelConfig
{

   // Model common settings
   public string ModelEndpoint { get; set; } = "http://localhost:11434";
   public string Model { get; set; } = "llama3";
   public string ApiKey { get; set; } = "ollama";
   public string DefaultCollection { get; set; } = "knowledge-base";

   /// <summary>
   /// True if Model are Needed (a EmbeddingModel is provided)
   /// </summary>
   public bool ModelNeeded
   {
      get
      {
         return !String.IsNullOrWhiteSpace(ModelEndpoint);
      }
   }

   // Embeddings common settings (if needed)
   public string EmbeddingModel { get; set; } = "mxbai-embed-large";
   public int VectorSize { get; set; } = 1024; // embedding size

   /// <summary>
   /// True if Embeddings are Needed (a EmbeddingModel is provided)
   /// </summary>
   public bool EmbeddingsNeeded
   {
      get
      {
         return !String.IsNullOrWhiteSpace(EmbeddingModel);
      }
   }

   // Qdrant default port is 6333, but we use 6334 for gRPC communication
   public string StoreEndpoint { get; set; } = "http://localhost:6334";
   public int StorePort { get; set; } = 6334;
   public string StoreHost { get; set; } = "localhost";

   /// <summary>
   /// True if Memory Store is Needed (a Store Endpoint is provided)
   /// </summary>
   public bool MemoryStoreNeeded
   {
      get
      {  
         return !String.IsNullOrWhiteSpace(StoreEndpoint); 
      }
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="needModel"></param>
   /// <param name="needEmbeddings"></param>
   /// <param name="needStore"></param>
   public KernelModelConfig(
      bool needModel = true, bool needEmbeddings = false, bool needStore = false)
   {
      if (!needModel)
      {
         ModelEndpoint = String.Empty;
         Model = String.Empty;
         ApiKey = String.Empty;
      }
      if (!needEmbeddings)
      {
         EmbeddingModel = String.Empty;
         VectorSize = 0;
      }
      if (!needStore)
      {
         StoreEndpoint = String.Empty;
         StorePort = 0;
         StoreHost = String.Empty;
      }
   }

}
