using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

// -------------------------------------------------------------------------------------------------
namespace HarmoniaFoodTools;

/// <summary>
/// Native Semantic Kernel plugin implementing the Harmony `functions.*` tools
/// used by the food preparation Harmony script.
/// 
/// Tool names and parameter names are chosen to exactly match the Harmony
/// JSON tool definitions:
///   - search_recipes
///   - check_pantry
///   - generate_shopping_list
///   - build_prep_schedule
///   - get_cultural_background
///
/// Each method returns a JSON string (Task&lt;string&gt;) so that the
/// SemanticKernelInterop can simply call ToString() on the result and write
/// it back into the Harmony conversation as tool output.
/// </summary>
public sealed class FoodTools
{

   /// <summary>
   /// Search for recipes that match the user constraints.
   /// </summary>
   [KernelFunction("search_recipes")]
   [Description("Search for recipes that match cuisine, dietary tags, time budget, dish count, and spice level.")]
   public Task<string> search_recipes(
      [Description("Primary cuisine region, e.g. 'Japanese', 'Thai', 'Chinese-Sichuan'.")]
      string? cuisine_region = null,

      [Description("Dietary tags, e.g. ['vegetarian', 'gluten-free'].")]
      string[]? dietary_tags = null,

      [Description("Maximum total time in minutes for the full cooking session.")]
      int? max_total_time_minutes = null,

      [Description("Maximum number of dishes to plan.")]
      int? max_dish_count = null,

      [Description("Desired spice level: mild, medium, hot, or mixed.")]
      string? spice_level = null)
   {
      // TODO: Replace this stub with real recipe search logic.
      // For now, return a simple JSON payload that the model can consume.
      var json = /* language=json */ """
      {
        "recipes": [
          {
            "id": "sample_teriyaki_tofu",
            "name": "Weeknight Teriyaki Tofu",
            "serves": 4,
            "total_time_minutes": 45,
            "active_time_minutes": 25,
            "difficulty": "easy",
            "spice_level": "mild"
          }
        ]
      }
      """;

      return Task.FromResult(json);
   }

   /// <summary>
   /// Normalize the user's free-text pantry description into a structured
   /// list of ingredients and approximate quantities.
   /// </summary>
   [KernelFunction("check_pantry")]
   [Description("Parse the user's free-text pantry description into normalized ingredients.")]
   public Task<string> check_pantry(
      [Description("User's free-text description of pantry/fridge contents.")]
      string free_text_inventory)
   {
      // TODO: Replace with real parsing / NER over ingredients.
      var json = /* language=json */ """
      {
        "ingredients": [
          { "name": "soy sauce", "approx_amount": "plenty" },
          { "name": "rice vinegar", "approx_amount": "half bottle" },
          { "name": "sesame oil", "approx_amount": "small bottle" },
          { "name": "garlic", "approx_amount": "a few cloves" },
          { "name": "ginger", "approx_amount": "small knob" }
        ]
      }
      """;

      return Task.FromResult(json);
   }

   /// <summary>
   /// Given selected recipes and the user's pantry contents, compute a
   /// consolidated shopping list and suggested substitutions.
   /// </summary>
   [KernelFunction("generate_shopping_list")]
   [Description("Compute a consolidated shopping list and suggested substitutions for the chosen recipes.")]
   public Task<string> generate_shopping_list(
      [Description("JSON-serialized recipes object returned from search_recipes.")]
      string recipes,

      [Description("JSON-serialized pantry object returned from check_pantry.")]
      string pantry,

      [Description("Whether to allow close ingredient substitutions when possible.")]
      bool allow_substitutions = true)
   {
      // NOTE:
      // - In Harmony, these parameters are typed as 'any'.
      // - Here we accept them as raw JSON strings, which the model can read,
      //   and which you can parse into structured C# types if desired.

      // TODO: Replace stub with real diffing / substitution logic.
      var json = /* language=json */ """
      {
        "shopping_list": [
          {
            "name": "firm tofu",
            "quantity": 800,
            "unit": "grams",
            "for_dishes": ["sample_teriyaki_tofu"],
            "is_in_pantry": false,
            "is_essential": true
          },
          {
            "name": "green onions",
            "quantity": 4,
            "unit": "stalks",
            "for_dishes": ["sample_teriyaki_tofu"],
            "is_in_pantry": false,
            "is_essential": false,
            "suggested_substitute": "chives or leeks (finely sliced)"
          }
        ]
      }
      """;

      return Task.FromResult(json);
   }

   /// <summary>
   /// Generate a detailed prep schedule (timeline) for the selected recipes,
   /// possibly aligned to a desired serving time and tuned to the user's
   /// experience level.
   /// </summary>
   [KernelFunction("build_prep_schedule")]
   [Description("Build a detailed preparation schedule for the selected recipes.")]
   public Task<string> build_prep_schedule(
      [Description("JSON-serialized recipes object returned from search_recipes.")]
      string recipes,

      [Description("Optional desired serving time as ISO timestamp, e.g. '2025-11-09T18:30'.")]
      string? desired_serving_time = null,

      [Description("User cooking experience: beginner, intermediate, or advanced.")]
      string? experience_level = null)
   {
      // TODO: Replace stub with real scheduling logic.
      var json = /* language=json */ """
      {
        "schedule": [
          {
            "relative_minute": 0,
            "label": "Press tofu and start rice",
            "instructions": "Rinse and start rice in rice cooker. Press tofu for 15 minutes.",
            "dish_ids": ["sample_teriyaki_tofu"],
            "can_overlap": true
          },
          {
            "relative_minute": 20,
            "label": "Stir-fry tofu",
            "instructions": "Cut tofu into cubes, sear in wok, then add teriyaki sauce.",
            "dish_ids": ["sample_teriyaki_tofu"],
            "can_overlap": false
          }
        ]
      }
      """;

      return Task.FromResult(json);
   }

   /// <summary>
   /// Return cultural and regional background notes for dishes or ingredients.
   /// </summary>
   [KernelFunction("get_cultural_background")]
   [Description("Provide cultural and regional background notes for given dishes or ingredients.")]
   public Task<string> get_cultural_background(
      [Description("Dish or ingredient names to explain.")]
      string[] items)
   {
      // TODO: Replace stub with lookups into a real knowledge base.
      var json = /* language=json */ """
      {
        "notes": [
          {
            "item": "teriyaki",
            "text": "Teriyaki refers to a Japanese cooking technique in which foods are broiled or grilled with a glaze of soy sauce, mirin, and sugar."
          }
        ]
      }
      """;

      return Task.FromResult(json);
   }

   /// <summary>
   /// Builds a Semantic Kernel instance with the FoodTools plugin imported.
   /// </summary>
   /// <returns>Kernel instance is returned</returns>
   public static Kernel BuildKernelWithTools()
   {
      var builder = Kernel.CreateBuilder();

      // builder.AddOpenAIChatCompletion(...);

      var kernel = builder.Build();

      // Import plugin into kernel.Plugins as "functions"
      kernel.ImportPluginFromObject(new FoodTools(), "functions");

      return kernel;
   }

}
