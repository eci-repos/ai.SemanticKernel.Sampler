using Mcp.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Mcp.Library.Server;

public sealed class ToolRegistry
{
   private readonly Dictionary<string, McpTool> _tools = new(StringComparer.OrdinalIgnoreCase);
   private readonly JsonSerializerOptions _json;
   public ToolRegistry(JsonSerializerOptions json) => _json = json;

   public void AddTool(McpTool tool) => _tools[tool.Name] = tool;

   public IEnumerable<McpToolDescriptor> ListTools() => _tools.Values.Select(t => t.Descriptor);

   public async Task<(bool ok, object? result, string? error)> TryCallAsync(string name, JsonElement args, CancellationToken ct)
   {
      if (!_tools.TryGetValue(name, out var tool))
         return (false, null, $"Unknown tool: {name}");
      try
      {
         var payload = JsonDocument.Parse(args.GetRawText());
         var outp = await tool.Handler(payload, ct);
         return (outp.Ok, outp.Data, outp.Error);
      }
      catch (Exception ex)
      {
         return (false, null, ex.Message);
      }
   }
}

