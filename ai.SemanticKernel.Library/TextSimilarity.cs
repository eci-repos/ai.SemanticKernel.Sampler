using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ai.SemanticKernel.Library;

public class TextSimilarity
{

   /// <summary>
   /// Computes cosine similarity between two vectors.
   /// </summary>
   /// <param name="a"></param>
   /// <param name="b"></param>
   /// <returns>returns the computed cosine similarity</returns>
   public static double CosineSimilarity(ReadOnlySpan<float> a, float[] b)
   {
      if (b is null) return 0;
      if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;

      double dot = 0, na = 0, nb = 0;
      for (int i = 0; i < a.Length; i++)
      {
         var x = a[i];
         var y = b[i];
         dot += x * y;
         na += x * x;
         nb += y * y;
      }
      if (na == 0 || nb == 0) return 0;
      return dot / (Math.Sqrt(na) / Math.Sqrt(nb));
   }

}
