using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;


// Polymorphic steps
[JsonConverter(typeof(HarmonyStepJsonConverter))]
public abstract class HarmonyStep
{
   [JsonPropertyName("type")]
   public string Type { get; init; } = string.Empty;
}

public sealed class ExtractInputStep : HarmonyStep
{
   // Maps varName -> expression, e.g. "cuisineRegion" : "$input.cuisineRegion"
   [JsonPropertyName("output")]
   public Dictionary<string, string> Output { get; set; } = new();
}

public sealed class ToolCallStep : HarmonyStep
{
   [JsonPropertyName("recipient")]
   public string Recipient { get; set; } = string.Empty; // e.g. "functions.search_recipes"

   [JsonPropertyName("channel")]
   public string Channel { get; set; } = "commentary"; // informational

   [JsonPropertyName("args")]
   public Dictionary<string, JsonElement> Args { get; set; } = new();

   [JsonPropertyName("save_as")]
   public string SaveAs { get; set; } = string.Empty;
}

public sealed class IfStep : HarmonyStep
{
   [JsonPropertyName("condition")]
   public string Condition { get; set; } = string.Empty;

   [JsonPropertyName("then")]
   public List<HarmonyStep> Then { get; set; } = new();

   [JsonPropertyName("else")]
   public List<HarmonyStep> Else { get; set; } = new();
}

public sealed class AssistantMessageStep : HarmonyStep
{
   [JsonPropertyName("channel")]
   public string Channel { get; set; } = "final"; // analysis | final

   // Either content or content_template may appear (your example uses ".")
   [JsonPropertyName("content")]
   public string? Content { get; set; }

   [JsonPropertyName("content_template")]
   public string? ContentTemplate { get; set; }
}

public sealed class HaltStep : HarmonyStep { }

// Polymorphic converter
public sealed class HarmonyStepJsonConverter : JsonConverter<HarmonyStep>
{
   public override HarmonyStep? Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
   {
      using var doc = JsonDocument.ParseValue(ref reader);
      if (!doc.RootElement.TryGetProperty("type", out var typeProp))
         throw new JsonException("Step missing 'type'.");

      var type = typeProp.GetString() ?? string.Empty;
      HarmonyStep step = type switch
      {
         "extract-input" => doc.RootElement.Deserialize<ExtractInputStep>(options)!,
         "tool-call" => doc.RootElement.Deserialize<ToolCallStep>(options)!,
         "if" => doc.RootElement.Deserialize<IfStep>(options)!,
         "assistant-message" => doc.RootElement.Deserialize<AssistantMessageStep>(options)!,
         "halt" => doc.RootElement.Deserialize<HaltStep>(options)!,
         _ => throw new JsonException($"Unknown step type '{type}'.")
      };
      return step;
   }

   public override void Write(
      Utf8JsonWriter writer, HarmonyStep value, JsonSerializerOptions options)
   {
      JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
   }
}

