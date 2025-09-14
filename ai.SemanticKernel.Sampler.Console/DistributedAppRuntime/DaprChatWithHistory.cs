using ai.SemanticKernel.Dapr.Library.Plugins;
using ai.SemanticKernel.Dapr.Library.Services;
using ai.SemanticKernel.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Sampler.Console.DistributedAppRuntime;

public class DaprChatWithHistory
{

   public static async Task RunAsync()
   {
      System.Console.WriteLine("Dapr Chat with History sample.");
      ChatService chatService = new ChatService(new KernelModelConfig());
      var result = await chatService.SendMessageAsync(
         "user1", "What is the main purpose of AI Semantic Search in a database?");
   }

}
