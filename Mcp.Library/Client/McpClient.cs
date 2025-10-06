using Mcp.Library.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Mcp.Library.Client;

// -------------------------------------------------------------------------------------------------
public sealed class McpClient : IDisposable
{
   private readonly JsonSerializerOptions _json;
   private Process _proc;
   private StreamReader _reader;
   private StreamWriter _writer;
   private int _idCounter = 1;

   private readonly 
      ConcurrentDictionary<string, TaskCompletionSource<McpRpcResponse>> _pending = new();
   private readonly CancellationTokenSource _cts = new();
   private readonly ConcurrentDictionary<string, Channel<JsonElement>> _streams = new();

   public McpClient(JsonSerializerOptions json) => _json = json;

   public async Task SpawnAsync(string command, string args, Dictionary<string, string> env)
   {
      var psi = new ProcessStartInfo(command, args)
      {
         RedirectStandardInput = true,
         RedirectStandardOutput = true,
         RedirectStandardError = true,
         UseShellExecute = false,
         CreateNoWindow = true
      };

      foreach (var kv in env)
         if (!string.IsNullOrEmpty(kv.Value)) psi.Environment[kv.Key] = kv.Value;

      _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
      _proc.Start();

      _reader = new StreamReader(_proc.StandardOutput.BaseStream, Encoding.UTF8, false, 8192, true);
      _writer = new StreamWriter(_proc.StandardInput.BaseStream, Encoding.UTF8, 8192, true) 
      { 
         AutoFlush = true
      };

      _proc.ErrorDataReceived += (_, e) => {
         if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine($"[server stderr] {e.Data}");
      };
      _proc.BeginErrorReadLine();

      _ = Task.Run(ReadLoopAsync);
   }

   public async Task<McpInitializeResult> InitializeAsync()
   {
      var req = new McpRpcRequest
      {
         Id = NextId(), Method = "initialize", Params = JsonDocument.Parse("{}").RootElement
      };
      var resp = await SendAsync(req);
      var result = resp.Result! as JsonElement?;
      return result!.Value.Deserialize<McpInitializeResult>(_json)!;
   }

   public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync()
   {
      var req = new McpRpcRequest 
      { 
         Id = NextId(), Method = "tools/list", Params = JsonDocument.Parse("{}").RootElement
      };
      var resp = await SendAsync(req);
      var result = resp.Result! as JsonElement?;
      return result!.Value.GetProperty("tools").Deserialize<List<McpToolDescriptor>>(_json)!;
   }

   public async Task<T> CallAsync<T>(string name, JsonElement args)
   {
      var payload = new McpCallToolParams { Name = name, Arguments = args };
      var req = new McpRpcRequest
      { 
         Id = NextId(), Method = "tools/call", 
         Params = JsonSerializer.SerializeToElement(payload, _json) 
      };
      var resp = await SendAsync(req);
      var rslt = resp.Result! as JsonElement?;
      var result = rslt!.Value.GetProperty("result");
      return result.Deserialize<T>(_json)!;
   }

   public async IAsyncEnumerable<string> CallStreamAsync(
      string name, JsonElement args, [EnumeratorCancellation] CancellationToken ct = default)
   {
      var payload = new McpCallToolParams { Name = name, Arguments = args };
      var req = new McpRpcRequest 
      { 
         Id = NextId(), Method = "tools/callStream", 
         Params = JsonSerializer.SerializeToElement(payload, _json) 
      };
      var resp = await SendAsync(req, ct);
      var rslt = resp.Result! as JsonElement?;
      var callId = rslt!.Value.GetProperty("callId").GetString();
      var channel = Channel.CreateUnbounded<JsonElement>();
      _streams[callId] = channel;
      try
      {
         await foreach (var el in channel.Reader.ReadAllAsync(ct))
         {
            var kind = el.GetProperty("kind").GetString();
            if (kind == "delta")
            {
               yield return el.GetProperty("text").GetString() ?? string.Empty;
            }
            else if (kind == "result")
            {
               yield return el.GetProperty("result").ToString();
            }
            else if (kind == "end")
            {
               yield break;
            }
            else if (kind == "error")
            {
               throw new Exception(el.GetProperty("error").GetString());
            }
         }
      }
      finally
      {
         _streams.TryRemove(callId, out _);
      }
   }

   private async Task<McpRpcResponse> SendAsync(McpRpcRequest req, CancellationToken ct = default)
   {
      var tcs = new TaskCompletionSource<
         McpRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
      _pending[req.Id] = tcs;

      var json = JsonSerializer.Serialize(req, _json);
      await WriteFramedAsync(_writer, json, ct);

      using (ct.Register(() => tcs.TrySetCanceled()))
      {
         return await tcs.Task.ConfigureAwait(false);
      }
   }

   private async Task ReadLoopAsync()
   {
      try
      {
         while (!_cts.IsCancellationRequested)
         {
            var (ok, body) = await ReadFramedAsync(_reader, _cts.Token);
            if (!ok) break;
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;


            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null)
            {
               var id = idEl.GetString();
               if (_pending.TryRemove(id, out var tcs))
               {
                  var resp = JsonSerializer.Deserialize<McpRpcResponse>(body, _json)!;
                  if (resp.Error != null)
                     tcs.TrySetException(
                        new Exception($"RPC error {resp.Error.Code}: {resp.Error.Message}"));
                  else
                     tcs.TrySetResult(resp);
               }
               continue;
            }


            if (root.TryGetProperty("method", out var methodEl))
            {
               var method = methodEl.GetString();
               if (string.Equals(method, "events/stream", StringComparison.OrdinalIgnoreCase))
               {
                  var p = root.GetProperty("params");
                  var callId = p.GetProperty("callId").GetString();
                  if (_streams.TryGetValue(callId, out var ch))
                  {
                     await ch.Writer.WriteAsync(p);
                     if (p.GetProperty("kind").GetString() == "end") ch.Writer.TryComplete();
                  }
               }
            }
         }
      }
      catch (Exception ex)
      {
         foreach (var kv in _pending) kv.Value.TrySetException(ex);
         foreach (var kv in _streams) kv.Value.Writer.TryComplete(ex);
      }
   }

   private string NextId() => Interlocked.Increment(ref _idCounter).ToString();

   public void Dispose()
   {
      try { _cts.Cancel(); } catch { }
      try { _writer?.Dispose(); } catch { }
      try { _reader?.Dispose(); } catch { }
      try { if (!_proc?.HasExited ?? false) _proc.Kill(true); } catch { }
   }

   private static async Task WriteFramedAsync(
      StreamWriter writer, string json, CancellationToken ct)
   {
      await writer.WriteAsync($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
   }

   private static async Task<(bool ok, string body)> ReadFramedAsync(
      StreamReader reader, CancellationToken ct)
   {
      string? line;
      int contentLength = -1;
      while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
      {
         if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
         {
            var val = line.Substring("Content-Length:".Length).Trim();
            if (int.TryParse(val, out var n)) contentLength = n;
         }
      }
      if (contentLength < 0) return (false, string.Empty);


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

}
