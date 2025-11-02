using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public enum ModelProvider
{
   Unknown = 0,
   OpenAI,
   AzureOpenAI,
   HuggingFace,
   Cohere,
   Ollama,
   FoundryLocal,
   GoogleGemini,
   Anthropic
}
