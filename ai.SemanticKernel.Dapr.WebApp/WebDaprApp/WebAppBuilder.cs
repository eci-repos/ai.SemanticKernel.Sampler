using Dapr.Client;

namespace ai.SemanticKernel.Dapr.WebApp.WebDaprApp;

// -------------------------------------------------------------------------------------------------

public class WebAppBuilder
{

   /// <summary>
   /// Create builder given app arguments.
   /// </summary>
   /// <param name="args">app arguments</param>
   /// <returns>builder instance is returned</returns>
   public static WebApplicationBuilder CreateBuilder(string[] args)
   {
      var builder = WebApplication.CreateBuilder(args);
      builder.WebHost.UseUrls("http://localhost:5190");

      // -- Add services to the container.
      builder.Services.AddDaprClient();
      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();

      //builder.Services.AddRazorPages();
      //builder.Services.AddServerSideBlazor();
      //builder.Services.AddControllers();

      // -- Add Dapr services to the container.
      //builder.Services.AddDaprClient();
      //builder.Services.AddDaprSidekick();

      // -- Add Semantic Kernel services to the container.
      //builder.Services.AddSemanticKernelServices(builder.Configuration);

      return builder;
   }

   /// <summary>
   /// Build the web application given a builder.
   /// </summary>
   /// <param name="builder">builder instance</param>
   /// <returns>WebApplication instance is returned</returns>
   public static WebApplication BuildWebApplication(WebApplicationBuilder builder)
   {
      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment())
      {
         app.UseSwagger();
         app.UseSwaggerUI();
         //app.MapOpenApi();
      }

      app.UseHttpsRedirection();

      //app.UseRouting();
      //app.UseAuthentication();
      //app.UseCloudEvents();
      //app.MapControllers();
      //app.MapSubscribeHandler();

      //app.MapRazorPages();
      //app.MapBlazorHub();
      //app.MapFallbackToPage("/_Host");



      return app;
   }

   /// <summary>
   /// Build the web application given app arguments.
   /// </summary>
   /// <param name="args">app arguments</param>
   /// <returns>app instance is returned</returns>
   public static WebApplication BuildWebApplication(string[] args)
   {
      var builder = CreateBuilder(args);

      var app = BuildWebApplication(builder);

      return app;
   }

}
