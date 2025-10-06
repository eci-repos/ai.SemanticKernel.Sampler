using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Mcp.Library.Models;

public class McpSimilarityResult
{
   public string id { get; init; }
   public string text { get; init; }
   public double score { get; init; }
   public JsonElement? meta { get; init; }
   public double[]? embedding { get; init; }
}
