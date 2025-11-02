using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public class EmbeddingConfig
{

   // Embeddings common settings (if needed)
   public ModelProvider EmbeddingProvider { get; set; } = ModelProvider.Ollama;
   public string? EmbeddingModel { get; set; } = "mxbai-embed-large";
   public string Endpoint { get; set; } = "http://localhost:11434";
   public int VectorSize { get; set; } = 1024; // embedding size

}
