using ai.SemanticKernel.Library;
using Microsoft.SemanticKernel.ChatCompletion;
using Mcp.Library.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Mcp.Library.Client;

// -------------------------------------------------------------------------------------------------
namespace Mcp.Library.Server;

/// <summary>
/// Represents the entry point for the Managed Cognitive Processing (MCP) application.
/// </summary>
/// <remarks>This class initializes the necessary components for the MCP application, including the 
/// kernel host, tool registry, and server. It registers various AI-centric tools for tasks such as
/// generating embeddings, computing semantic similarity, chat completions, and running workflows. 
/// The application is designed to process input and output in UTF-8 encoding and operates as a 
/// server using standard input/output (stdio).</remarks>
public class McpServerMain
{

   public static async Task<int> Main(string[] args)
   {
      Console.OutputEncoding = Encoding.UTF8;
      Console.InputEncoding = Encoding.UTF8;

      var jsonOptions = McpJson.Options;
      var config = new KernelConfig();

      // Initialize SK
      var skHost = await KernelHost.CreateAsync(config);

      // Register AI-centric tools
      var registry = new ToolRegistry(jsonOptions);

      // Embeddings for a single string or a batch
      registry.AddTool(
          new McpTool(
              name: McpHelper.EmbeddingsToolName,
              description: 
                 "Return embeddings for one or more texts using the configured embedding model.",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""text"": { ""type"": ""string"" },
                    ""texts"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
                  },
                  ""oneOf"": [
                    { ""required"": [""text""] },
                    { ""required"": [""texts""] }
                  ]
                }"),
              handler: async (payload, ct) =>
              {
                 var embed = skHost.GetEmbeddingGenerator();

                 List<string> inputs = new();
                 var root = payload.RootElement;

                 if (root.TryGetProperty("text", out var one))
                    inputs.Add(one.GetString()!);
                 if (root.TryGetProperty("texts", out var many) && 
                     many.ValueKind == JsonValueKind.Array)
                    inputs.AddRange(many.EnumerateArray().
                       Select(e => e.GetString()!).Where(s => s is not null)!);

                 if (inputs.Count == 0)
                 {
                    return RequestResult.Fail("Provide either 'text' or 'texts'.");
                 }

                 var vectors = await TextChunker.GetEmbeddings(embed, inputs, config.ChatModel);

                 var data = new
                 {
                    count = vectors.Count,
                    dimensions = vectors[0].Length,
                    embeddings = vectors
                 };

                 return RequestResult.Okey(data);
              }
          )
      );

      // Semantic similarity over ad-hoc records (no index needed)
      registry.AddTool(
          new McpTool(
              name: McpHelper.SimilarityToolName,
              description:
                 "Given a prompt and array of records, compute cosine similarity and return " +
                 "top_k matches.",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""prompt"": { ""type"": ""string"" },
                    ""records"": {
                      ""type"": ""array"",
                      ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                          ""id"":   { ""type"": ""string"" },
                          ""text"": { ""type"": ""string"" },
                          ""meta"": { ""type"": ""object"" }
                        },
                        ""required"": [""id"", ""text""]
                      }
                    },
                    ""top_k"": { ""type"": ""integer"", ""default"": 5 },
                    ""includeEmbeddings"": { ""type"": ""boolean"", ""default"": false }
                  },
                  ""required"": [""prompt"", ""records""]
                }"),
              handler: async (payload, ct) =>
              {
                 var root = payload.RootElement;
                 var prompt = root.GetProperty("prompt").GetString()!;
                 var topK = root.TryGetProperty(
                    "top_k", out var kEl) ? Math.Max(1, kEl.GetInt32()) : 5;
                 var include = root.TryGetProperty(
                    "includeEmbeddings", out var incEl) && incEl.GetBoolean();

                 var records = root.GetProperty("records")
                       .EnumerateArray()
                       .Select(x => new
                      {
                         id = x.GetProperty("id").GetString()!,
                         text = x.GetProperty("text").GetString()!,
                         meta = x.TryGetProperty("meta", out var m) ? m : default(JsonElement?)
                      }).ToArray();

                 if (records.Length == 0)
                    return RequestResult.Fail("Provide at least one record.");

                 var embed = skHost.GetEmbeddingGenerator();

                 // Embed prompt
                 var promptVec = await TextChunker.GetEmbeddings(embed, prompt, config.ChatModel);

                 // Embed each record and score
                 var scored = new List<dynamic>(records.Length);
                 foreach (var r in records)
                 {
                    var recVec = await TextChunker.GetEmbeddings(embed, r.text, config.ChatModel);
                    var score = TextSimilarity.CosineSimilarity(promptVec, recVec);

                    scored.Add(new
                    {
                       id = r.id,
                       text = r.text,
                       score,
                       meta = r.meta.HasValue ? r.meta.Value : default(JsonElement?),
                       embedding = include ? recVec.ToArray() : null
                    });
                 }

                 var top = scored
                       .OrderByDescending(s => (double)s.score)
                       .Take(topK)
                       .ToArray();

                 return RequestResult.Okey(new
                 {
                    dimensions = promptVec.Length,
                    top_k = topK,
                    results = top
                 });
              }
          )
      );

      // Chat completion as a tool (kept from your original)
      registry.AddTool(
          new McpTool(
              name: McpHelper.ChatCompletionToolName,
              description: "Call the configured chat model with a prompt.",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""prompt"": { ""type"": ""string"" },
                    ""system"": { ""type"": ""string"" },
                    ""max_tokens"": { ""type"": ""integer"" }
                  },
                  ""required"": [""prompt""]
                }"),
              handler: async (payload, ct) =>
              {
                 var prompt = payload.RootElement.GetProperty("prompt").GetString()!;
                 var system = payload.RootElement.
                    TryGetProperty("system", out var sysEl) ? sysEl.GetString() : null;
                 var maxToks = payload.RootElement.
                    TryGetProperty("max_tokens", out var mtEl) ? mtEl.GetInt32() : (int?)null;

                 var chat = skHost.GetChatCompletionService();
                 var history = new ChatHistory();
                 if (!string.IsNullOrWhiteSpace(system)) history.AddSystemMessage(system);
                 history.AddUserMessage(prompt);

                 var result = await chat.GetChatMessageContentAsync(
                    history, new PromptExecutionSettings(), skHost.Instance, ct);

                 return RequestResult.Okey(new { text = result.Content ?? string.Empty });
              }
          )
      );

      // Example: run an SK workflow (kept)
      registry.AddTool(
          new McpTool(
              name: McpHelper.WorkflowRunToolName,
              description: "Run a named SK workflow with inputs (e.g., draft->summarize).",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""inputs"": { ""type"": ""object"" }
                  },
                  ""required"": [""name""]
                }"),
              handler: async (payload, ct) => await skHost.RunWorkflowAsync(payload, ct)
          )
      );

      // Start MCP server (stdio)
      var server = new McpServer(jsonOptions, registry);
      await server.RunAsync();

      return 0;
   }

   /// <summary>
   /// MCP server entry point for hosting via McpHostProcess.
   /// </summary>
   /// <param name="args">arguments</param>
   public static void McpServerRun(string[] args)
   {
      Task<int> task = McpServerMain.Main(args);

      try
      {
         task.Wait();
         if (task.Status == TaskStatus.RanToCompletion)
         {
            var result = task.Result;
            Environment.Exit(1);
         }
      }
      catch (AggregateException ae)
      {
         KernelIO.Error.WriteLine(ae.Flatten().Message);
         Environment.Exit(1);
      }
      catch (Exception ex)
      {
         KernelIO.Error.WriteLine(ex.Message);
         Environment.Exit(1);
      }
   }

}

