using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public class ProviderConfig
{

   // Provider information
   public string Name { get; set; }
   public string Description { get; set; } = String.Empty;
   public string ServiceId { get; set; } = "ollama-chat";

   public ChatModelConfig Model { get; set; } = new ChatModelConfig();
   public EmbeddingConfig Embedding { get; set; } = new EmbeddingConfig();
   public StoreConfig Store { get; set; } = new StoreConfig();

   /// <summary>
   /// True if Model are Needed (a EmbeddingModel is provided)
   /// </summary>
   public bool ModelNeeded
   {
      get
      {
         return !String.IsNullOrWhiteSpace(Model.Endpoint);
      }
   }

   /// <summary>
   /// True if Embeddings are Needed (a EmbeddingModel is provided)
   /// </summary>
   public bool EmbeddingsNeeded
   {
      get
      {
         return !String.IsNullOrWhiteSpace(Embedding.Endpoint);
      }
   }

   /// <summary>
   /// True if Memory Store is Needed (a Store Endpoint is provided)
   /// </summary>
   public bool MemoryStoreNeeded
   {
      get
      {
         return !String.IsNullOrWhiteSpace(Store.Endpoint);
      }
   }

   #region -- 1.50 - Constructures

   public ProviderConfig()
   {
   }

   public ProviderConfig(
      string endpoint,
      string apiKey,

      string? completionModel = null,
      string? chatModel = null,
      string? embeddingModel = null,
      string? completionDeployment = null,
      string? chatDeployment = null,
      string? embeddingDeployment = null,

      ModelProvider modelProvider = ModelProvider.Ollama,
      ModelProvider embeddingProvider = ModelProvider.Ollama)
   {
      this.Model.ModelProvider = modelProvider;
      this.Model.Endpoint = endpoint;
      this.Model.ApiKey = apiKey;
      this.Model.ChatModel = 
         String.IsNullOrWhiteSpace(chatModel) ? Model.ChatModel : chatModel;

      this.Embedding.EmbeddingProvider = embeddingProvider;
      this.Embedding.EmbeddingModel = 
         String.IsNullOrWhiteSpace(embeddingModel) ? 
         Embedding.EmbeddingModel : embeddingModel;
   }

   public ProviderConfig(
      bool needModel = true, bool needEmbeddings = false, bool needStore = false)
   {
      if (!needModel)
      {
         Model.Endpoint = String.Empty;
         Model.ChatModel = String.Empty;
         Model.ApiKey = String.Empty;
      }
      if (!needEmbeddings)
      {
         Embedding.EmbeddingModel = String.Empty;
         Embedding.VectorSize = 0;
      }
      if (!needStore)
      {
         Store.Endpoint = String.Empty;
         Store.StorePort = 0;
         Store.StoreHost = String.Empty;
      }
   }

   #endregion
   #region -- 4.00 - Providers for runtime



   #endregion

}
