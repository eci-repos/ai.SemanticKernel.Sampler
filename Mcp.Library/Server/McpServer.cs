using Mcp.Library.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Mcp.Library.Server;

/// <summary>
/// Represents a server that processes JSON-RPC 2.0 messages over standard input and output.
/// </summary>
/// <remarks>The <see cref="McpServer"/> class is designed to handle JSON-RPC 2.0 communication 
/// using a framing protocol over standard input and output streams. It supports asynchronous 
/// processing of incoming requests and provides responses based on the registered tools and server
/// capabilities. <para> Typical usage involves creating an instance of <see cref="McpServer"/> 
/// with the required JSON serialization options and tool registry, and then calling 
/// <see cref="RunAsync"/> to start processing messages. </para>
/// <para> This class is thread-safe for concurrent use, as each incoming request is processed in 
/// a separate task. </para>
/// </remarks>
public sealed class McpServer
{
   private readonly JsonSerializerOptions _jsonSerializerOptions;
   private readonly ToolRegistry _registry;

   /// <summary>
   /// Initializes a new instance of the <see cref="McpServer"/> class with the specified JSON 
   /// serializer options and tool registry.
   /// </summary>
   /// <remarks>Use this constructor to create an instance of <see cref="McpServer"/> with custom 
   /// serialization settings and a specific tool registry.</remarks>
   /// <param name="json">The <see cref="JsonSerializerOptions"/> used to configure JSON 
   /// serialization and deserialization.</param>
   /// <param name="registry">The <see cref="ToolRegistry"/> that provides access to registered 
   /// tools.</param>
   public McpServer(JsonSerializerOptions json, ToolRegistry registry)
   { 
      _jsonSerializerOptions = json; 
      _registry = registry; 
   }

   /// <summary>
   /// Processes JSON-RPC 2.0 messages over standard input and output asynchronously.
   /// </summary>
   /// <remarks>This method reads JSON-RPC frames from the standard input stream, processes each 
   /// request, and writes the corresponding response to the standard output stream. It uses a 
   /// framing protocol where each message is prefixed with a "Content-Length" header. The method 
   /// runs continuously until the provided <see cref="CancellationToken"/> signals cancellation 
   /// or the input stream reaches the end of file (EOF).  Each request is handled in a separate 
   /// task to ensure concurrent processing. If an error occurs while processing a request, an
   /// appropriate JSON-RPC error response is generated and sent back to the client.</remarks>
   /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.
   /// The default value is <see cref="CancellationToken.None"/>.</param>
   /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task completes
   /// when the method stops processing due to cancellation or EOF.</returns>
   public async Task RunAsync(CancellationToken ct = default)
   {
      // Read JSON-RPC frames from stdin, write to stdout.
      // MCP typically uses JSON-RPC 2.0 over stdio with header framing:
      // Content-Length\r\n\r\n<body>
      var reader = new StreamReader(
         Console.OpenStandardInput(), Console.InputEncoding, false, 8192, true);
      var writer = new StreamWriter(
         Console.OpenStandardOutput(), Console.OutputEncoding, 8192, true) { AutoFlush = true };

      while (!ct.IsCancellationRequested)
      {
         var (ok, body) = await ReadFramedAsync(reader, ct);
         if (!ok) break; // EOF

         _ = Task.Run(async () =>
         {
            McpRpcResponse? resp = null;
            try
            {
               var req = JsonSerializer.Deserialize<McpRpcRequest>(body, _jsonSerializerOptions)!;
               resp = await HandleAsync(req, ct);
            }
            catch (Exception ex)
            {
               // Attempt to extract id for error correlation
               string? id = null;
               try { id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetString(); }
               catch { }

               resp = McpRpcResponse.RpcError(id, -32603, $"Internal error: {ex.Message}");
            }

            if (resp != null)
            {
               var payload = JsonSerializer.Serialize(resp, _jsonSerializerOptions);
               await WriteFramedAsync(writer, payload, ct);
            }
         }, ct);
      }
   }

   /// <summary>
   /// Handles an incoming RPC request asynchronously and returns the appropriate response.
   /// </summary>
   /// <remarks>This method processes various RPC methods, including:
   /// <list type="bullet">
   ///    <item>
   ///       <description><c>initialize</c>: Returns server information, protocol version, and 
   ///          capabilities.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>tools/list</c>: Returns a list of available tools.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>tools/call</c>: Invokes a tool with the specified arguments and 
   ///          returns the result or an error.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>ping</c>: Responds with a simple acknowledgment.</description>
   ///    </item>
   /// </list>
   /// If the method specified in the request is not recognized, an error response is returned 
   /// with the code <c>-32601</c>.
   /// </remarks>
   /// <param name="req">The RPC request to process, containing the method name and any associated
   /// parameters.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.
   /// </param>
   /// <returns>A task that represents the asynchronous operation. The task result is an
   /// <see cref="McpRpcResponse"/> containing the result of the requested method, or an error 
   /// response if the method is not found or fails.</returns>
   private async Task<McpRpcResponse> HandleAsync(McpRpcRequest req, CancellationToken ct)
   {
      switch (req.Method)
      {
         case "initialize":
            var init = new McpInitializeResult
            {
               ProtocolVersion = "2024-11-05",
               ServerInfo = new McpServerInfo { Name = "mcp-sk-server", Version = "0.2.0" },
               Capabilities = new McpCapabilities
               {
                  Tools = new McpToolingCapability { ListChanged = true }
               }
            };
            return McpRpcResponse.RpcResult(req.Id, init);

         case "tools/list":
            var tools = _registry.ListTools();
            return McpRpcResponse.RpcResult(req.Id, new { tools });

         case "tools/call":
            {
               JsonElement? v = req.Params!;
               var call = JsonSerializer.
                  Deserialize<McpCallToolParams>((JsonElement)v, _jsonSerializerOptions)!;
               var (ok, result, error) = 
                  await _registry.TryCallAsync(call.Name, call.Arguments, ct);
               if (ok) return McpRpcResponse.RpcResult(req.Id, new { result });
               return McpRpcResponse.RpcError(req.Id, -32001, error ?? "Tool error");
            }

         case "ping":
            return McpRpcResponse.RpcResult(req.Id, new { pong = true });
      }

      return McpRpcResponse.RpcError(req.Id, -32601, $"Method not found: {req.Method}");
   }

   /// <summary>
   /// Reads a framed message from the specified <see cref="StreamReader"/> asynchronously, 
   /// extracting the message body based on the "Content-Length" header.
   /// </summary>
   /// <remarks>This method expects the input stream to contain HTTP-like headers, including a 
   /// "Content-Length" header  specifying the size of the message body in bytes. The method reads 
   /// the headers, determines the content length,  and then reads the specified number of
   /// characters from the stream. If the "Content-Length" header is missing  or invalid, the 
   /// method returns <c>(false, string.Empty)</c>.</remarks>
   /// <param name="reader">The <see cref="StreamReader"/> to read the framed message from.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the 
   /// operation to complete.</param>
   /// <returns>A tuple containing a boolean and a string:
   /// <list type="bullet">
   ///    <item>
   ///       <description><c>ok</c>: <see langword="true"/> if a valid message body was
   ///          successfully read; otherwise, <see langword="false"/>.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>body</c>: The message body as a string, or an empty string if no
   ///          valid message was read.</description>
   ///    </item> 
   /// </list>
   /// </returns>
   private static async Task<(bool ok, string body)> ReadFramedAsync(
      StreamReader reader, CancellationToken ct)
   {
      // Expect headers like: Content-Length: N\r\n...\r\n\r\n
      string? line;
      int contentLength = -1;
      while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
      {
         if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
         {
            var val = line.Substring("Content-Length:".Length).Trim();
            if (int.TryParse(val, out var n)) contentLength = n;
         }
         // ignore other headers
      }

      if (contentLength < 0)
      {
         // no more input
         return (false, string.Empty);
      }

      char[] buffer = ArrayPool<char>.Shared.Rent(contentLength);
      try
      {
         int read = 0;
         while (read < contentLength)
         {
            int r = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), ct);
            if (r == 0) break;
            read += r;
         }
         var body = new string(buffer, 0, read);
         return (true, body);
      }
      finally
      {
         ArrayPool<char>.Shared.Return(buffer);
      }
   }

   /// <summary>
   /// Writes a JSON string to the specified <see cref="StreamWriter"/> with a content-length 
   /// header.
   /// </summary>
   /// <remarks>The method writes the JSON string prefixed by a "Content-Length" header, which 
   /// specifies the byte length of the JSON string when encoded in UTF-8. The header and JSON 
   /// content are separated by two CRLF sequences.</remarks>
   /// <param name="writer">The <see cref="StreamWriter"/> to which the framed JSON string will be
   /// written. Cannot be <c>null</c>.</param>
   /// <param name="json">The JSON string to write. Cannot be <c>null</c> or empty.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.
   /// </param>
   /// <returns>A task that represents the asynchronous write operation.</returns>
   private static async Task WriteFramedAsync(
      StreamWriter writer, string json, CancellationToken ct)
   {
      await writer.WriteAsync($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
   }

}

