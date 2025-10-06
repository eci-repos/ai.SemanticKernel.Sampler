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

public class KernelConfig
{
   public const string OPENAI_API_KEY = "OPENAI_API_KEY";
   public const string AZURE_OPENAI_API_KEY = "AZURE_OPENAI_API_KEY";
   public const string AZURE_OPENAI_ENDPOINT = "AZURE_OPENAI_ENDPOINT";
   public const string AZURE_OPENAI_DEPLOYMENT = "AZURE_OPENAI_DEPLOYMENT";

   // Default models and deployments
   public const string DEFAULT_MODEL = "llama3";
   public const string DEFAULT_EMBEDDING_MODEL = "mxbai-embed-large";
   public const string DEFAULT_COMPLETION_MODEL = DEFAULT_MODEL;
   public const string DEFAULT_CHAT_MODEL = DEFAULT_MODEL;

   // For services that require deployment names (like Azure OpenAI)
   public const string DEFAULT_COMPLETION_DEPLOYMENT = DEFAULT_MODEL;
   public const string DEFAULT_CHAT_DEPLOYMENT = DEFAULT_MODEL;
   public const string DEFAULT_EMBEDDING_DEPLOYMENT = "text-embedding-3-small";

   public Dictionary<string, string?> _configDictionary = new Dictionary<string, string?>();

   // Model common settings
   public string Endpoint { get; set; } = "http://localhost:11434";
   public string ApiKey { get; set; } = "ollama";

   public string? CompletionModel { get; set; }
   public string? ChatModel { get; set; } = DEFAULT_MODEL;

   // Embeddings common settings (if needed)
   public string? EmbeddingModel { get; set; } = "mxbai-embed-large";
   public int VectorSize { get; set; } = 1024; // embedding size

   /// <summary>
   /// True if Model are Needed (a EmbeddingModel is provided)
   /// </summary>
   public bool ModelNeeded
   {
      get
      {
         return !String.IsNullOrWhiteSpace(Endpoint);
      }
   }

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

   // Deployment names for Azure OpenAI or other services that require them
   public string? CompletionDeployment { get; set; }
   public string? ChatDeployment { get; set; }
   public string? EmbeddingDeployment { get; set; }

   // Qdrant default port is 6333, but we use 6334 for gRPC communication
   public string StoreEndpoint { get; set; } = "http://localhost:6334";
   public int StorePort { get; set; } = 6334;
   public string StoreHost { get; set; } = "localhost";

   public string DefaultCollection { get; set; } = "knowledge-base";

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

   public KernelConfig(
      string endpoint,
      string apiKey,
      string? completionModel = null,
      string? chatModel = null,
      string? embeddingModel = null,
      string? completionDeployment = null,
      string? chatDeployment = null,
      string? embeddingDeployment = null)
   {
      this.Endpoint = endpoint ?? DEFAULT_EMBEDDING_MODEL;
      this.ApiKey = apiKey ?? ApiKey;

      this.CompletionModel = completionModel ?? DEFAULT_COMPLETION_MODEL;
      this.ChatModel = chatModel ?? DEFAULT_CHAT_MODEL;
      this.EmbeddingModel = embeddingModel ?? DEFAULT_EMBEDDING_MODEL;

      this.CompletionDeployment = completionDeployment ?? DEFAULT_COMPLETION_DEPLOYMENT;
      this.ChatDeployment = chatDeployment ?? DEFAULT_CHAT_DEPLOYMENT;
      this.EmbeddingDeployment = embeddingDeployment ?? DEFAULT_EMBEDDING_DEPLOYMENT;

      SetupDictionary();
   }

   public KernelConfig(
      bool needModel = true, bool needEmbeddings = false, bool needStore = false)
   {
      if (!needModel)
      {
         Endpoint = String.Empty;
         ChatModel = String.Empty;
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

      SetupDictionary();
   }

   private void SetupDictionary()
   {
      // Load configuration from appsettings.json
      var config = new ConfigurationBuilder()
         .SetBasePath(Directory.GetCurrentDirectory())
         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
         .Build();

      string? openAiKey = config["OpenAI:ApiKey"];
      string? azureKey = config["AzureOpenAI:ApiKey"];
      string? azureEndpoint = config["AzureOpenAI:Endpoint"];
      string? azureDeployment = config["AzureOpenAI:Deployment"];
      _configDictionary = new Dictionary<string, string?> {
         { "OPENAI_API_KEY", openAiKey },
         { "AZURE_OPENAI_API_KEY", azureKey },
         { "AZURE_OPENAI_ENDPOINT", azureEndpoint },
         { "AZURE_OPENAI_DEPLOYMENT", azureDeployment },
      };
   }

}
