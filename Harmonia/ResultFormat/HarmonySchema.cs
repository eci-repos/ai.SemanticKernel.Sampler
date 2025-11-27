using Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;

/// <summary>
/// Validates Harmony envelopes & embedded harmony-script objects against Draft 2020-12 JSON Schemas.
/// </summary>
public static class HarmonySchemaValidator
{
   private static JsonSchema? _envelopeSchema;

   /// <summary>
   /// Initialize the validator by loading the envelope schema from disk.
   /// Call once during app startup.
   /// </summary>
   public static void Initialize(string schemaFolderPath)
   {
      var path = Path.Combine(schemaFolderPath, "harmony_envelope_schema.json");
      using var reader = File.OpenText(path);
      var schemaText = reader.ReadToEnd();
      _envelopeSchema = JsonSchema.FromText(schemaText);
   }

   /// <summary>
   /// Validates the raw JSON (string) against the HarmonyEnvelope schema.
   /// Throws InvalidOperationException on failure with a readable message.
   /// </summary>
   public static void ValidateEnvelope(string json)
   {
      if (_envelopeSchema is null)
         throw new InvalidOperationException(
            "HarmonySchemaValidator not initialized. Call Initialize().");

      using var doc = JsonDocument.Parse(json);
      var result = _envelopeSchema.Evaluate(doc.RootElement, new EvaluationOptions
      {
         OutputFormat = OutputFormat.Hierarchical,
         RequireFormatValidation = false
      });

      if (!result.IsValid)
      {
         var details = JsonSerializer.Serialize(
            result.Errors, new JsonSerializerOptions { WriteIndented = true });
         throw new InvalidOperationException($"Harmony envelope validation failed:\n{details}");
      }
   }

   /// <summary>
   /// Validates the extracted harmony-script JsonElement against the 'HarmonyScript' sub-schema.
   /// This is optional if you validated the full envelope; kept here for targeted checks.
   /// </summary>
   public static void ValidateScript(JsonElement scriptElement)
   {
      if (_envelopeSchema is null)
         throw new InvalidOperationException("HarmonySchemaValidator not initialized.");

      // Build a schema that references the 'HarmonyScript' definition.
      var scriptSchema = JsonSchema.FromText("""
         {
           "$schema": "https://json-schema.org/draft/2020-12/schema",
           "$ref": "https://example.org/harmony-envelope.schema.json#/$defs/HarmonyScript"
         }
         """);

      var result = scriptSchema.Evaluate(scriptElement, new EvaluationOptions
      {
         OutputFormat = OutputFormat.Hierarchical
      });

      if (!result.IsValid)
      {
         var details = JsonSerializer.Serialize(
            result.Errors, new JsonSerializerOptions { WriteIndented = true });
         throw new InvalidOperationException($"harmony-script validation failed:\n{details}");
      }
   }

   /// <summary>
   /// Validates a JSON envelope against the expected schema using the specified schema file path.
   /// </summary>
   /// <remarks>This method initializes the schema validator and reads the JSON envelope from disk 
   /// before performing validation. If the schema file does not exist or the JSON is invalid, an 
   /// exception may be thrown. Make sure the schema file name is "harmony-envelope.schema.json"
   /// and the file is on the "Schemas/" folder, also make sure the given script name is on the
   /// "Scripts/" folder</remarks>
   /// <param name="jsonScriptFileName">The relative path to the schema file within the 'Scripts' 
   /// directory. Cannot be null or empty.</param>
   public static void Validate(string jsonScriptFileName)
   {
      // inside MainAsync, before reading the JSON
      Initialize("Schemas");

      // Read the envelope JSON from disk
      string json = File.ReadAllText("Scripts/" + jsonScriptFileName);

      // Validate the full envelope before parsing
      ValidateEnvelope(json);
   }

}

