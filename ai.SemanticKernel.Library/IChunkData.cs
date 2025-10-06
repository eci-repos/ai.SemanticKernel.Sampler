using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public interface IChunkData
{
   [VectorStoreKey]
   Guid Id { get; init; }

   [VectorStoreData(IsFullTextIndexed = true)]
   string Text { get; init; }

   // IMPORTANT: non-nullable vector; you must populate it before UpsertAsync.
   [VectorStoreVector(1024)]
   ReadOnlyMemory<float> Embedding { get; set; }
}
