using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

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

}
