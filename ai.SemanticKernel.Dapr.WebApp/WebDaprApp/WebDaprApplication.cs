using ai.SemanticKernel.Dapr.Library.Services;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ai.SemanticKernel.Library;

// -------------------------------------------------------------------------------------------------
// In PS go to this foloder:
//    cd C:\prjs\ai\semantic.kernel\ai.SemanticKernel\ai.SemanticKernel.Dapr.WebApp
//    dapr run --app-id sk-app --resources-path ./components -- dotnet run

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Dapr.WebApp.WebDaprApp;

public class WebDaprApplication
{
   private ChatService _chatService;

   private WebApplication _app;
   public WebApplication Application
   {
      get { return _app; }
   }

   public WebDaprApplication(WebApplication app)
   {
      _chatService = new ChatService(new KernelConfig());
      _app = app;
   }

   /// <summary>
   /// Build the web application given app arguments.
   /// </summary>
   /// <param name="args">app arguments</param>
   /// <returns>WebApplication instance is returned</returns>
   public static WebDaprApplication MapWebApplication(string[] args)
   {
      var builder = WebAppBuilder.CreateBuilder(args);
      var app = WebAppBuilder.BuildWebApplication(builder);
      var wapp = new WebDaprApplication(app);
      wapp.SetupWebAppRoutes();
      return wapp;
   }

   /// <summary>
   /// Setup the web application routes.
   /// </summary>
   public void SetupWebAppRoutes()
   {
      var app = this.Application;

      // Load your ChatPlugin and expose minimal endpoints
      app.MapPost("/chat", async (
          string userId,
          string message) =>
      {
         string result;
         try
         {
            result = await _chatService.SendMessageAsync(userId, message);
         }
         catch (Exception ex)
         {
            result = $"Error: {ex.Message}" + (ex.InnerException != null ?
               $";  Error: {ex.InnerException.Message}": String.Empty);
         }
         return Results.Ok(new { reply = result });
      });

      app.MapGet("/history", async (string userId) =>
      {
         var history = await _chatService.GetHistoryAsync(userId);
         return Results.Ok(new { history });
      });

      app.MapPost("/clear", async (string userId) =>
      {
         var msg = await _chatService.ClearHistoryAsync(userId);
         return Results.Ok(new { message = msg });
      });

      // quick health probe
      app.MapGet("/", () => "Dapr chat-service is running");
   }

}
