using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

/// <summary>
/// Provides methods for splitting text into chunks and generating embeddings for those chunks.
/// </summary>
/// <remarks>The <see cref="TextChunker"/> class includes utilities for processing text by dividing 
/// it into manageable chunks based on token count, paragraph boundaries, or character limits. 
/// It also provides functionality for generating embeddings for text chunks using a specified 
/// embedding generator. This class is designed to facilitate text preprocessing and embedding 
/// generation for natural language processing (NLP) tasks.</remarks>
public class TextChunker
{

   /// <summary>
   /// Smart Chunking by adding overlap to preserve context accross boundaries by targeting chuncks
   /// by token count, not character.
   /// </summary>
   /// <remarks>
   /// Where you build the corpus, swap ChunkByParagraph(...) with TokenAwareChunks(...). Later 
   /// you can plug in a real token counter (e.g., tiktoken/SharpToken) for exactness.
   /// </remarks>
   /// <param name="text">The input text to be split into chunks. Paragraphs are separated by two 
   /// consecutive newline characters.</param>
   /// <param name="maxTokens">number of tokens (default: 320)</param>
   /// <param name="overlapTokens">overlap tokens (default: 40)</param>
   /// <param name="countTokens">provide your tokens counter</param>
   /// <returns>text chuck is returned</returns>
   public static IEnumerable<string> TokenAwareChunks(
      string text, int maxTokens = 320, int overlapTokens = 40,
      Func<string, int> countTokens = null!)
   {
      countTokens ??= s =>
         Math.Max(1, s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length); // stub
      var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      int i = 0;

      while (i < words.Length)
      {
         int take = 0, tokens = 0;
         while (i + take < words.Length && tokens + countTokens(words[i + take]) <= maxTokens)
         {
            tokens += countTokens(words[i + take]);
            take++;
         }
         yield return string.Join(' ', words.Skip(i).Take(take));

         // step forward but keep an overlap
         int back = 0; int kept = 0;
         while (kept < overlapTokens && back < take)
         {
            kept += countTokens(words[i + take - 1 - back]);
            back++;
         }
         i += Math.Max(1, take - back);
      }
   }

   /// <summary>
   /// Splits the input text into chunks of paragraphs, ensuring that each chunk does not exceed the 
   /// specified maximum character limit.
   /// </summary>
   /// <remarks>This method processes the input text by splitting it into paragraphs based on double
   /// newline delimiters.  It then groups paragraphs into chunks, ensuring that the total character 
   /// count of each chunk does not exceed <paramref name="maxChars"/>.  If a paragraph is too long 
   /// to fit within the limit, it is split into smaller chunks.</remarks>
   /// <param name="text">The input text to be split into chunks. Paragraphs are separated by two 
   /// consecutive newline characters.</param>
   /// <param name="maxChars">The maximum number of characters allowed in each chunk. Defaults 
   /// to 800. If a single paragraph exceeds this limit, it will be further split into smaller 
   /// chunks.</param>
   /// <returns>An enumerable collection of strings, where each string represents a chunk of text 
   /// containing one or more paragraphs.</returns>
   public static IEnumerable<string> ChunkByParagraph(string text, int maxChars = 800)
   {
      var parts = text.Split(
         "\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      var buf = new StringBuilder();
      foreach (var p in parts)
      {
         if (buf.Length + p.Length + 2 <= maxChars)
         {
            if (buf.Length > 0) buf.AppendLine();
            buf.AppendLine(p);
         }
         else
         {
            if (buf.Length > 0) { yield return buf.ToString().Trim(); buf.Clear(); }
            if (p.Length <= maxChars) yield return p.Trim();
            else
            {
               // very long paragraph fallback
               for (int i = 0; i < p.Length; i += maxChars)
               {
                  yield return p.Substring(i, Math.Min(maxChars, p.Length - i));
               }
            }
         }
      }
      if (buf.Length > 0) yield return buf.ToString().Trim();
   }

   /// <summary>
   /// Generates embeddings for a collection of text chunks and assigns the resulting embeddings
   /// back to the corresponding chunks.
   /// </summary>
   /// <remarks>This method processes the text content of each chunk, generates embeddings using 
   /// the specified <paramref name="generator"/>, and assigns the resulting embeddings back to the 
   /// chunks. Chunks with empty or whitespace-only text are ignored during embedding generation.
   /// </remarks>
   /// <typeparam name="T">The type of the chunk data. Must implement <see cref="IChunkData"/>.
   /// </typeparam>
   /// <param name="generator">The embedding generator used to create embeddings for the text 
   /// chunks.</param>
   /// <param name="chunks">A list of chunk data objects. Each chunk must contain a non-empty 
   /// text value in its <see cref="IChunkData.Text"/>
   /// property. The generated embeddings will be assigned to the <see cref="IChunkData.Embedding"/>
   /// property of the corresponding chunk.</param>
   /// <param name="modelId">The identifier of the model to use for generating embeddings. 
   /// This parameter is optional and can be <see langword="null"/> to use the default model.
   /// </param>
   /// <returns></returns>
   public static async Task GetEmbeddings<T>(
      IEmbeddingGenerator<string, Embedding<float>> generator, 
      List<T> chunks, string? modelId) where T : IChunkData
   {
      //var texts = chunks.Select(c => c.Text).ToList();
      var textChunks = chunks.Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t));

      // Generate embeddings
      var gen = await generator.GenerateAsync(
          textChunks, new EmbeddingGenerationOptions { ModelId = modelId }
      );

      // assign back
      for (int i = 0; i < chunks.Count; i++)
      {
         chunks[i].Embedding = gen[i].Vector; // <— ReadOnlyMemory<float>
      }
   }

   /// <summary>
   /// Generates embeddings for a collection of text chunks using the specified embedding generator.
   /// </summary>
   /// <remarks>This method uses the provided <paramref name="generator"/> to asynchronously 
   /// generate embeddings  for the specified <paramref name="chunks"/>. The embeddings are 
   /// returned as a list of float arrays, 
   /// with each array representing the embedding vector for the corresponding input chunk.</remarks>
   /// <param name="generator">The embedding generator to use for creating embeddings. 
   /// This must implement  <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> 
   /// where <c>TInput</c> is <see cref="string"/> and <c>TEmbedding</c> 
   /// is <see cref="Embedding{float}"/>.</param>
   /// <param name="chunks">A list of text chunks for which embeddings will be generated. 
   /// Each string in the list represents  a separate input to the embedding generator.</param>
   /// <param name="modelId">An optional identifier for the model to use when generating 
   /// embeddings. If <see langword="null"/>,  the default model configured in the embedding 
   /// generator will be used.</param>
   /// <returns>A task that represents the asynchronous operation. The task result is a list of
   /// embeddings, where  each embedding is represented as an array of floating-point values (
   /// <see cref="float"/>). The order  of the embeddings in the returned list corresponds to the 
   /// order of the input chunks.</returns>
   public static async Task<List<float[]>> GetEmbeddings(
      IEmbeddingGenerator<string, Embedding<float>> generator,
      List<string> chunks, string? modelId)
   {
      // Generate embeddings
      var gen = await generator.GenerateAsync(
          chunks, new EmbeddingGenerationOptions { ModelId = modelId }
      );

      // assign back
      var list = new List<float[]>();
      for (int i = 0; i < chunks.Count; i++)
      {
         list.Add(gen[i].Vector.ToArray());
      }
      return list;
   }

   /// <summary>
   /// Get embeddings for a single input string.
   /// </summary>
   /// <param name="generator">The embedding generator to use for creating embeddings. 
   /// This must implement  <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> 
   /// where <c>TInput</c> is <see cref="string"/> and <c>TEmbedding</c> 
   /// is <see cref="Embedding{float}"/>.</param>
   /// <param name="input">input to the embedding generator</param>
   /// <param name="modelId">An optional identifier for the model to use when generating 
   /// embeddings. If <see langword="null"/>,  the default model configured in the embedding 
   /// generator will be used.</param>
   /// <returns>A task that represents the asynchronous operation. The task result is a list of
   /// embeddings, where  the embedding is represented as an array of floating-point values (
   /// <see cref="float"/>)</returns>
   public static async Task<float[]> GetEmbeddings(
      IEmbeddingGenerator<string, Embedding<float>> generator, string input, string? modelId)
   {
      // Generate embeddings
      var gen = await generator.GenerateAsync(
          input, new EmbeddingGenerationOptions { ModelId = modelId }
      );

      return gen.Vector.ToArray();
   }

}
