using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public class StoreConfig
{

   // Store default collection name
   public string DefaultCollection { get; set; } = "knowledge-base";

   // Default models and deployments
   public const string DEFAULT_EMBEDDING_MODEL = "mxbai-embed-large";

   // Qdrant default port is 6333, but we use 6334 for gRPC communication
   public string Endpoint { get; set; } // = "http://localhost:6334";
   public int StorePort { get; set; } = 6334;
   public string StoreHost { get; set; } = "localhost";

}
