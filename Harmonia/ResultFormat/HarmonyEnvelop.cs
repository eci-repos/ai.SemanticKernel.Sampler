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
/// Represents a container for a collection of Harmony messages, providing methods to parse, access, 
/// and extract structured or plain content from the message set.
/// </summary>
/// <remarks>A HarmonyEnvelope typically contains a sequence of messages exchanged between different
/// roles, such as 'system' and 'user'. It supports parsing from JSON and offers methods to retrieve
/// structured scripts and plain prompts for further processing. This type is immutable in structure
/// but allows modification of its Messages collection. Thread safety is not guaranteed; synchronize 
/// access if used concurrently.</remarks>
public sealed class HarmonyEnvelope
{
   public List<HarmonyMessage> Messages { get; set; } = new();

   public static HarmonyEnvelope Parse(string json)
   {
      var options = new JsonSerializerOptions
      {
         PropertyNameCaseInsensitive = true
      };
      options.Converters.Add(new JsonStringEnumConverter());
      return JsonSerializer.Deserialize<HarmonyEnvelope>(json, options)
             ?? throw new InvalidOperationException("Invalid Harmony JSON.");
   }

   public HarmonyScript GetScript()
   {
      foreach (var msg in Messages)
      {
         if (msg.Role == "system"
             && string.Equals(
                msg.ContentType, "harmony-script", StringComparison.OrdinalIgnoreCase)
             && msg.Content.ValueKind == JsonValueKind.Object)
         {
            var options = new JsonSerializerOptions
            {
               PropertyNameCaseInsensitive = true
            };
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

   public IEnumerable<(HarmonyChannel Channel, string Content)> GetPlainSystemPrompts()
   {
      foreach (var msg in Messages)
      {
         if (msg.Role == "system" &&
             !string.Equals(
                msg.ContentType, "harmony-script", StringComparison.OrdinalIgnoreCase) &&
             msg.Content.ValueKind == JsonValueKind.String)
         {
            yield return (msg.Channel, msg.Content.GetString() ?? string.Empty);
         }
      }
   }

   public (HarmonyChannel Channel, string Content)? GetUserMessage()
   {
      foreach (var msg in Messages)
      {
         if (msg.Role == "user" && msg.Content.ValueKind == JsonValueKind.String)
         {
            return (msg.Channel, msg.Content.GetString() ?? string.Empty);
         }
      }
      return null;
   }
}
