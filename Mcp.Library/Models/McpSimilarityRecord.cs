using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Mcp.Library.Models;

public record McpSimilarityRecord
{ 
   public string id { get; init; } 
   public string text { get; init; } 
   public Dictionary<string, object>? meta { get; init; }
}


