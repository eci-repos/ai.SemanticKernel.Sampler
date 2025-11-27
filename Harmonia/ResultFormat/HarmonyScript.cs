using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;

/// <summary>
/// Represents a harmony-script payload containing variables and a sequence of steps for execution 
/// within the Harmony framework.
/// </summary>
/// <remarks>A HarmonyScript defines the structure and data required to execute a scripted workflow,
/// including named variables and ordered steps. Instances of this class are typically deserialized 
/// from system messages with a content type of "harmony-script". The class is immutable after 
/// deserialization and is not thread-safe for modification.</remarks>
public sealed class HarmonyScript
{
   [JsonPropertyName("vars")]
   public Dictionary<string, JsonElement>? Vars { get; set; }

   [JsonPropertyName("steps")]
   public List<HarmonyStep> Steps { get; set; } = new();

   /// <summary>
   /// Extracts and deserializes the first HarmonyScript from a list of HarmonyMessage objects that
   /// contains a valid harmony-script payload.
   /// </summary>
   /// <remarks>Only messages with the role "system", a content type of "harmony-script", and an 
   /// object-valued content are considered. The method validates the script before deserialization.
   /// If multiple valid harmony-script messages exist, only the first is returned.</remarks>
   /// <param name="messages">The collection of messages to search for a system message containing 
   /// a harmony-script. Cannot be null.</param>
   /// <returns>A HarmonyScript object deserialized from the first matching message in the
   /// collection.</returns>
   /// <exception cref="InvalidOperationException">Thrown if no valid harmony-script message is 
   /// found in the collection, or if deserialization of the harmony-script fails.</exception>
   public static HarmonyScript GetScript(List<HarmonyMessage> messages)
   {
      foreach (var msg in messages)
      {
         if (msg.Role == "system"
             && string.Equals(msg.ContentType, "harmony-script", StringComparison.OrdinalIgnoreCase)
             && msg.Content.ValueKind == JsonValueKind.Object)
         {
            // NEW: validate the script element
            HarmonySchemaValidator.ValidateScript(msg.Content);

            // Existing deserialization
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            HarmonyScript script;
            try
            {
               script = msg.Content.Deserialize<HarmonyScript>(options);
            }
            catch (JsonException ex)
            {
               throw new InvalidOperationException("Failed to deserialize harmony-script.", ex);
            }
            return script;
         }
      }
      throw new InvalidOperationException("No harmony-script found in messages.");
   }

   /// <summary>
   /// Call the GetScript with the messages from the envelope.
   /// </summary>
   /// <param name="envelope">envelope</param>
   /// <returns>A HarmonyScript object deserialized from the first matching message in the
   /// collection.</returns>
   public static HarmonyScript GetScript(HarmonyEnvelope envelope)
   {
      return GetScript(envelope.Messages);
   }

}

