using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;

// Root of the harmony-script content
public sealed class HarmonyScript
{
   [JsonPropertyName("vars")]
   public Dictionary<string, JsonElement>? Vars { get; set; }

   [JsonPropertyName("steps")]
   public List<HarmonyStep> Steps { get; set; } = new();
}

