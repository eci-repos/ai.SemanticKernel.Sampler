using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Mcp.Library.Models;

public record EmbeddingsArgs
{ 
   public string? text { get; init; } 
   public string[]? texts { get; init; } 
}

