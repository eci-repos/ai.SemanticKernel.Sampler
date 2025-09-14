using ai.SemanticKernel.Dapr.Library.Plugins;
using ai.SemanticKernel.Library;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Dapr.Library.Services;

public class ChatService
{
   private readonly Kernel _kernel;
   private readonly KernelFunction _chatFunction;
   private readonly KernelFunction _clearHistoryFunction;
   private readonly KernelFunction _getHistoryFunction;

   /// <summary>
   /// ChatService constructor initializes the Semantic Kernel and ChatPlugin.
   /// </summary>
   /// <param name="config">kernel and model configuration information</param>
   public ChatService(KernelModelConfig config)
   {
      KernelInstance kernelInstance = new KernelInstance(config);
      _kernel = kernelInstance.Instance;

      // Register the ChatPlugin
      var chatPlugin = ChatPlugin.RegisterPlugin(_kernel, "statestore");

      // Get the plugin functions
      _chatFunction = chatPlugin["Chat"];
      _clearHistoryFunction = chatPlugin["ClearHistory"];
      _getHistoryFunction = chatPlugin["GetHistory"];
   }

   /// <summary>
   /// Send a message to the chat plugin and get a response.
   /// </summary>
   /// <param name="userId">user id</param>
   /// <param name="message">message</param>
   /// <returns>return the chat response</returns>
   public async Task<string> SendMessageAsync(string userId, string message)
   {
      var result = await _kernel.InvokeAsync(
          _chatFunction,
          new() {
                { "userId", userId },
                { "userMessage", message }
          }
      );

      return result.GetValue<string>() ?? "No response received";
   }

   /// <summary>
   /// Get conversation history for a user.
   /// </summary>
   /// <param name="userId">user id</param>
   /// <returns>history is returned</returns>
   public async Task<string> GetHistoryAsync(string userId)
   {
      var result = await _kernel.InvokeAsync(
          _getHistoryFunction,
          new() { { "userId", userId } }
      );

      return result.GetValue<string>() ?? "Failed to retrieve history";
   }

   /// <summary>
   /// Clear conversation history for a user.
   /// </summary>
   /// <param name="userId">user id</param>
   /// <returns>history is cleared</returns>
   public async Task<string> ClearHistoryAsync(string userId)
   {
      var result = await _kernel.InvokeAsync(
          _clearHistoryFunction,
          new() { { "userId", userId } }
      );

      return result.GetValue<string>() ?? "Failed to clear history";
   }

}
