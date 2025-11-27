using Json.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.Console.StructuredOutputs;

/// <summary>
/// Demonstrates benefits and usage of the MenuSummary schema for structured outputs.
/// This class is optional and not tied to HarmonyExecutor.
/// </summary>
public static class MenuSummarySchemaHelper
{
   private static JsonSchema? _menuSchema;

   /// <summary>
   /// Loads the menu summary schema from disk.
   /// </summary>
   public static void Initialize(string schemaFolderPath)
   {
      var path = Path.Combine(schemaFolderPath, "menu-summary.schema.json");
      using var reader = File.OpenText(path);
      var schemaText = reader.ReadToEnd();
      _menuSchema = JsonSchema.FromText(schemaText);
   }

   /// <summary>
   /// Validates a JSON string against the menu summary schema.
   /// </summary>
   public static void ValidateMenuSummaryOrThrow(string json)
   {
      if (_menuSchema is null)
         throw new InvalidOperationException("MenuSummarySchemaHelper not initialized.");

      using var doc = JsonDocument.Parse(json);
      var result = _menuSchema.Evaluate(doc.RootElement, new EvaluationOptions
      {
         OutputFormat = OutputFormat.Hierarchical
      });

      if (!result.IsValid)
      {
         var details = JsonSerializer.Serialize(
            result.Errors, new JsonSerializerOptions { WriteIndented = true });
         throw new InvalidOperationException($"Menu summary validation failed:\n{details}");
      }
   }

   /// <summary>
   /// Explains why structured outputs are beneficial.
   /// </summary>
   public static string ExplainBenefits()
   {
      return @"
Structured outputs ensure:
- Predictable JSON shape for downstream rendering.
- Strict validation (additionalProperties=false) for safety.
- Detectable refusals when the model cannot comply.
- Easier integration with UI components and APIs.
";
   }

   /// <summary>
   /// Example of how to request structured output from an LLM using OpenAI response_format.
   /// </summary>
   public static string GetOpenAIRequestExample()
   {
      return @"
{
  ""model"": ""gpt-4o"",
  ""messages"": [
    { ""role"": ""system"", ""content"": ""Summarize the menu in JSON using the schema provided."" },
    { ""role"": ""user"", ""content"": ""Plan an easy Japanese dinner for 4 people."" }
  ],
  ""response_format"": {
    ""type"": ""json_schema"",
    ""json_schema"": {
      ""name"": ""menu_summary"",
      ""schema"": { ... contents of menu-summary.schema.json ... },
      ""strict"": true
    }
  }
}
";
   }

}

