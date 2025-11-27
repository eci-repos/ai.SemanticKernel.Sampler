using ai.SemanticKernel.Library;
using Harmonia.ResultFormat;
using HarmoniaFoodDemo;
using HarmoniaFoodTools;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.Console;

public class HarmoniaFoodExecutor
{

   /// <summary>
   /// Example entry point: builds a Kernel, imports AsianFoodTools as 'functions',
   /// then runs the AsianFoodPrep scenario using the given JSON file path.
   /// </summary>
   public static async Task MainAsync(string[] args)
   {
      string jsonScriptFileName = args.Length > 0
         ? args[0] : "foodPlannerSample.1.json";

      // Validate the Harmony JSON file against the given schema
      HarmonySchemaValidator.Validate(jsonScriptFileName);

      // prepare the kernel with tools...
      Kernel kernel = BuildKernelWithTools();

      // Parse the Harmony message
      string json = File.ReadAllText("Scripts/" + jsonScriptFileName);
      var envelope = HarmonyEnvelope.Parse(json);

      // Provide user input for extract-input step
      var input = new Dictionary<string, object?>
      {
         ["cuisineRegion"] = "Mexican",
         ["serves"] = 4,
         ["totalTimeMinutes"] = 45,
         ["experienceLevel"] = "beginner",
         ["spicePreference"] = "mild",
         ["pantryText"] = "beans, tortillas, onion",
         ["servingTime"] = "2025-11-16T18:30:00-05:00",
         ["dietaryTags"] = "vegetarian,gluten-optional",
         ["maxDishCount"] = 3
      };

      // 5) Execute
      var executor = new HarmonyExecutor(kernel);
      var result = await executor.ExecuteAsync(envelope, input);

      KernelIO.Console.WriteLine("==== FINAL OUTPUT ====");
      KernelIO.Console.WriteLine(result.FinalText);

      KernelIO.Console.WriteLine("\n==== VARS SNAPSHOT ====");
      foreach (var kv in result.Vars)
      {
         KernelIO.Console.WriteLine($"{kv.Key}: {kv.Value}");
      }
   }

   /// <summary>
   /// Builds a Semantic Kernel instance that:
   ///  - Has a chat completion service configured.
   ///  - Imports the AsianFoodTools plugin as 'functions', so that
   ///    Harmony tool calls like 'functions.search_recipes' resolve.
   /// </summary>
   private static Kernel BuildKernelWithTools()
   {
      var host = KernelHost.PrepareKernelHost(null);

      // Import the AsianFoodTools plugin under the 'functions' namespace.
      // This ensures kernel.Plugins.TryGetFunction("functions", "search_recipes", ...)
      // will succeed inside SemanticKernelInterop.ExecuteToolCallsAsync.
      host.Instance.ImportPluginFromObject(new FoodTools(), "functions");

      return host.Instance;
   }

}
