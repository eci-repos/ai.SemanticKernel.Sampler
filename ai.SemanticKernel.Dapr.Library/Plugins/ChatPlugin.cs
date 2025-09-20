using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using Dapr.Client;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Dapr.Library.Plugins;

/// <summary>
/// Chat plugin using Dapr state store to maintain conversation history.
/// </summary>
public class ChatPlugin
{
   private readonly DaprClient _daprClient;
   private readonly IChatCompletionService _chatCompletionService;
   private string _storeName;

   public ChatPlugin(DaprClient daprClient, IChatCompletionService chatCompletionService,
      string storeName = "statestore")
   {
      _storeName = storeName;
      _daprClient = daprClient;
      _chatCompletionService = chatCompletionService;
   }

   #region -- Plugin Registration

   /// <summary>
   /// Register the ChatPlugin with the given kernel.
   /// </summary>
   /// <param name="kernel">Provided kernel that already added a ChatCompletion service</param>
   /// <returns>Registered plugin instance</returns>
   public static KernelPlugin RegisterPlugin(Kernel kernel, string storeName = "statestore")
   {
      if (kernel == null)
         throw new ArgumentNullException(nameof(kernel));

      // Create Dapr client using GRPC endpoint
      var daprClient = new DaprClientBuilder()
         .UseGrpcEndpoint("http://localhost:50001")
         .Build();

      // Get chat completion service from kernel
      var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

      // Create plugin instance with dependencies
      var chatPlugin = new ChatPlugin(daprClient, chatCompletionService, storeName);

      // Import the plugin
      var plugin = kernel.ImportPluginFromObject(chatPlugin, "ChatPlugin");

      return plugin;
   }

   #endregion
   #region -- Chat with History

   /// <summary>
   /// 
   /// </summary>
   /// <param name="userMessage"></param>
   /// <param name="userId"></param>
   /// <param name="cancellationToken"></param>
   /// <returns></returns>
   [KernelFunction]
   [Description("Sends a message to the chat and gets a response. Maintains conversation history.")]
   public async Task<string> ChatAsync(
       [Description("The user's message")] string userMessage,
       [Description("Unique user identifier for maintaining conversation history")] string userId,
       CancellationToken cancellationToken = default)
   {
      // Load or create chat history
      var chatHistory = await LoadChatHistoryAsync(userId, cancellationToken);

      // Add user message to history
      chatHistory.AddUserMessage(userMessage);

      // Get AI response
      var response = await _chatCompletionService.GetChatMessageContentAsync(
          chatHistory,
          cancellationToken: cancellationToken
      );

      // Add AI response to history
      chatHistory.AddAssistantMessage(response.Content);

      // Save updated history
      await SaveChatHistoryAsync(userId, chatHistory, cancellationToken);

      return response.Content;
   }

   [KernelFunction]
   [Description("Clears the conversation history for a specific user.")]
   public async Task<string> ClearHistoryAsync(
       [Description("Unique user identifier")] string userId,
       CancellationToken cancellationToken = default)
   {
      await ClearChatHistoryAsync(userId, cancellationToken);
      return "Conversation history cleared successfully.";
   }

   [KernelFunction]
   [Description("Gets the current conversation history for a user.")]
   public async Task<string> GetHistoryAsync(
       [Description("Unique user identifier")] string userId,
       CancellationToken cancellationToken = default)
   {
      var chatHistory = await LoadChatHistoryAsync(userId, cancellationToken);

      if (chatHistory.Count == 0)
         return "No conversation history found.";

      var historyText = string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"));
      return $"Conversation History:\n{historyText}";
   }

   /// <summary>
   /// Load chat history for a specific user.
   /// </summary>
   /// <param name="userId">user id</param>
   /// <param name="cancellationToken">cancellation token</param>
   /// <returns>Chat history is returned</returns>
   private async Task<ChatHistory> LoadChatHistoryAsync(
      string userId, CancellationToken cancellationToken)
   {
      var stateKey = $"chat-history-{userId}";

      try
      {
         var savedHistory = await _daprClient.GetStateAsync<ChatHistory>(
            _storeName, stateKey, cancellationToken: cancellationToken);
         return savedHistory ?? new ChatHistory();
      }
      catch
      {
         return new ChatHistory();
      }
   }

   /// <summary>
   /// Save chat history for a specific user.
   /// </summary>
   /// <param name="userId"></param>
   /// <param name="chatHistory"></param>
   /// <param name="cancellationToken"></param>
   /// <returns></returns>
   private async Task SaveChatHistoryAsync(
      string userId, ChatHistory chatHistory, CancellationToken cancellationToken)
   {
      var stateKey = $"chat-history-{userId}";
      await _daprClient.SaveStateAsync(
         _storeName, stateKey, chatHistory, cancellationToken: cancellationToken);
   }

   /// <summary>
   /// Clear chat history for a specific user.
   /// </summary>
   /// <param name="userId"></param>
   /// <param name="cancellationToken"></param>
   /// <returns></returns>
   private async Task ClearChatHistoryAsync(string userId, CancellationToken cancellationToken)
   {
      var stateKey = $"chat-history-{userId}";
      await _daprClient.DeleteStateAsync(
         _storeName, stateKey, cancellationToken: cancellationToken);
   }

   #endregion

}

