using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public class ChatModelConfig
{
   public const string DEFAULT_MODEL = "llama3";
   public const string DEFAULT_COMPLETION_MODEL = DEFAULT_MODEL;
   public const string DEFAULT_CHAT_MODEL = DEFAULT_MODEL;

   // Model common settings
   public ModelProvider ModelProvider { get; set; } = ModelProvider.Ollama;
   public string Endpoint { get; set; }
   public string ApiKey { get; set; } = "ollama";

   /// <summary>
   /// ChatModel (same as CompletionModel) [default:llama3]
   /// </summary>
   public string? ChatModel { get; set; } = DEFAULT_MODEL;

}
