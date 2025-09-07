
using System.Runtime.CompilerServices;
using Dapr.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Plugins;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel;

// -------------------------------------------------------------------------------------------------

namespace ai.SemanticKernel.Dapr.Library.MemoryStore;

/// <summary>
/// IMemoryStore implemented on top of Dapr's state store (SQL Server sidecar).
/// Keys are namespaced as: {collection}:{id}
/// A per-collection index is kept at: {collection}::index  (List<string> of IDs)
/// </summary>
#pragma warning disable SKEXP0001 
public sealed class DaprMemoryStore : IMemoryStore
{
   private readonly DaprClient _dapr;
   private readonly string _stateStoreName;

   // Optional: tweak this if you expect large collections and want to batch loads
   private const int BatchReadSize = 128;

   public DaprMemoryStore(DaprClient daprClient, string stateStoreName = "sqlstatestore")
   {
      _dapr = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
      _stateStoreName = string.IsNullOrWhiteSpace(stateStoreName) ? 
         "sqlstatestore" : stateStoreName;
   }

   #region -- IMemoryStore

   public async Task CreateCollectionAsync(
      string collection, CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      var indexKey = GetIndexKey(collection);
      var exists = await _dapr.GetStateAsync<List<string>>(
         _stateStoreName, indexKey, cancellationToken: cancellationToken);
      if (exists is null)
      {
         await _dapr.SaveStateAsync(_stateStoreName, indexKey, new List<string>(), 
            cancellationToken: cancellationToken);
      }
   }

   public async Task<bool> DoesCollectionExistAsync(
      string collection, CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      var indexKey = GetIndexKey(collection);
      var (state, _) = await _dapr.GetStateAndETagAsync<List<string>>(
         _stateStoreName, indexKey, cancellationToken: cancellationToken);
      return state is not null;
   }

   public async IAsyncEnumerable<string> GetCollectionsAsync(
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
   {
      // We keep a global list of collections at a well-known key.
      var all = await _dapr.GetStateAsync<HashSet<string>>(
         _stateStoreName, GlobalCollectionsKey, cancellationToken: cancellationToken)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var name in all.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
         yield return name;
   }

   public async Task DeleteCollectionAsync(
      string collection, CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));

      var ids = await GetIndexAsync(collection, cancellationToken);
      foreach (var id in ids)
      {
         await _dapr.DeleteStateAsync(
            _stateStoreName, GetDataKey(collection, id), cancellationToken: cancellationToken);
      }

      await _dapr.DeleteStateAsync(
         _stateStoreName, GetIndexKey(collection), cancellationToken: cancellationToken);

      // Update global collections set
      var (set, etag) = await _dapr.GetStateAndETagAsync<HashSet<string>>(
         _stateStoreName, GlobalCollectionsKey, cancellationToken: cancellationToken);
      set ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      set.Remove(collection);
      await _dapr.SaveStateAsync(
         _stateStoreName, GlobalCollectionsKey, set, cancellationToken: cancellationToken);
   }

#pragma warning disable SKEXP0001 
   public async Task<string> UpsertAsync(
      string collection, MemoryRecord record, CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      _ = record ?? throw new ArgumentNullException(nameof(record));
      if (string.IsNullOrWhiteSpace(record.Metadata.Id))
         throw new ArgumentException("MemoryRecord.Metadata.Id must be set.", nameof(record));

      await EnsureCollectionCreated(collection, cancellationToken);

      // Persist record
      var stored = StoredRecord.From(record);
      await _dapr.SaveStateAsync(
         _stateStoreName, GetDataKey(collection, record.Metadata.Id), stored, 
         cancellationToken: cancellationToken);

      // Update per-collection index
      var (ids, etag) = await _dapr.GetStateAndETagAsync<List<string>>(
         _stateStoreName, GetIndexKey(collection), cancellationToken: cancellationToken);
      ids ??= new List<string>();
      if (!ids.Contains(record.Metadata.Id, StringComparer.Ordinal))
      {
         ids.Add(record.Metadata.Id);
         await _dapr.SaveStateAsync(_stateStoreName, GetIndexKey(collection), ids, 
            cancellationToken: cancellationToken);
      }

      // Update global collections
      var (set, etag2) = await _dapr.GetStateAndETagAsync<HashSet<string>>(
         _stateStoreName, GlobalCollectionsKey, cancellationToken: cancellationToken);
      set ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (set.Add(collection))
      {
         await _dapr.SaveStateAsync(_stateStoreName, GlobalCollectionsKey, set, 
            cancellationToken: cancellationToken);
      }

      return record.Metadata.Id;
   }

   public async IAsyncEnumerable<string> UpsertBatchAsync(
      string collection, IEnumerable<MemoryRecord> records, 
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
   {
      _ = records ?? throw new ArgumentNullException(nameof(records));
      foreach (var rec in records)
      {
         var id = await UpsertAsync(collection, rec, cancellationToken);
         yield return id;
      }
   }

   public async Task<MemoryRecord?> GetAsync(
      string collection, string key, bool withEmbedding = false, 
      CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      _ = key ?? throw new ArgumentNullException(nameof(key));

      var stored = await _dapr.GetStateAsync<StoredRecord>(
         _stateStoreName, GetDataKey(collection, key), cancellationToken: cancellationToken);
      return stored is null ? null : stored.ToMemoryRecord(withEmbedding);
   }

   public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(
      string collection, IEnumerable<string> keys, bool withEmbedding = false, 
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      _ = keys ?? throw new ArgumentNullException(nameof(keys));

      // Dapr .NET SDK has GetStateAsync for single keys; batch optimization can be added later via
      // State API transactions if desired.
      foreach (var key in keys)
      {
         var rec = await GetAsync(collection, key, withEmbedding, cancellationToken);
         if (rec is not null) yield return rec;
      }
   }

   public async Task RemoveAsync(
      string collection, string key, CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      _ = key ?? throw new ArgumentNullException(nameof(key));

      await _dapr.DeleteStateAsync(_stateStoreName, GetDataKey(collection, key), 
         cancellationToken: cancellationToken);

      var (ids, etag) = await _dapr.GetStateAndETagAsync<List<string>>(
         _stateStoreName, GetIndexKey(collection), cancellationToken: cancellationToken);
      if (ids is not null && ids.Remove(key))
      {
         await _dapr.SaveStateAsync(
            _stateStoreName, GetIndexKey(collection), ids, cancellationToken: cancellationToken);
      }
   }

   public async Task RemoveBatchAsync(
      string collection, IEnumerable<string> keys, CancellationToken cancellationToken = default)
   {
      _ = keys ?? throw new ArgumentNullException(nameof(keys));
      foreach (var k in keys)
      {
         await RemoveAsync(collection, k, cancellationToken);
      }
   }

   public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
       string collection,
       ReadOnlyMemory<float> embedding,
       int limit,
       double minRelevanceScore = 0.0,
       bool withEmbeddings = false,
       [EnumeratorCancellation] CancellationToken cancellationToken = default)
   {
      _ = collection ?? throw new ArgumentNullException(nameof(collection));
      if (limit <= 0) yield break;

      var ids = await GetIndexAsync(collection, cancellationToken);
      if (ids.Count == 0) yield break;

      // Scan in batches to avoid huge roundtrips
      var scored = new List<(MemoryRecord rec, double score)>(
         capacity: Math.Min(limit * 4, ids.Count));
      foreach (var batch in Batch(ids, BatchReadSize))
      {
         foreach (var id in batch)
         {
            var stored = await _dapr.GetStateAsync<StoredRecord>(
               _stateStoreName, GetDataKey(collection, id), cancellationToken: cancellationToken);
            if (stored is null || stored.Embedding is null) continue;

            var similarity = CosineSimilarity(embedding.Span, stored.Embedding);
            if (similarity >= minRelevanceScore)
            {
               var rec = stored.ToMemoryRecord(withEmbeddings);
               scored.Add((rec, similarity));
            }
         }
      }

      foreach (var item in scored.OrderByDescending(s => s.score).Take(limit))
         yield return item;
   }

   public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(
       string collection,
       ReadOnlyMemory<float> embedding,
       double minRelevanceScore = 0.0,
       bool withEmbedding = false,
       CancellationToken cancellationToken = default)
   {
      var top = await GetNearestMatchesAsync(
         collection, embedding, limit: 1, minRelevanceScore, withEmbedding, cancellationToken)
         .FirstOrDefaultAsync(cancellationToken);
      return top == default ? null : top;
   }

   #endregion
   #region -- Helpers & Storage Model

   private static string GetDataKey(string collection, string id) => $"{collection}:{id}";
   private static string GetIndexKey(string collection) => $"{collection}::index";
   private const string GlobalCollectionsKey = "__collections__";

   private async Task EnsureCollectionCreated(string collection, CancellationToken ct)
   {
      if (!await DoesCollectionExistAsync(collection, ct))
      {
         await CreateCollectionAsync(collection, ct);
      }
   }

   private async Task<List<string>> GetIndexAsync(string collection, CancellationToken ct)
   {
      var indexKey = GetIndexKey(collection);
      var ids = await _dapr.GetStateAsync<List<string>>(
         _stateStoreName, indexKey, cancellationToken: ct);
      return ids ?? new List<string>();
   }

   private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
   {
      var bucket = new List<T>(size);
      foreach (var item in source)
      {
         bucket.Add(item);
         if (bucket.Count == size)
         {
            yield return bucket;
            bucket = new List<T>(size);
         }
      }
      if (bucket.Count > 0) yield return bucket;
   }

   /// <summary>
   /// Computes cosine similarity between two vectors.
   /// </summary>
   /// <param name="a"></param>
   /// <param name="b"></param>
   /// <returns></returns>
   private static double CosineSimilarity(ReadOnlySpan<float> a, float[] b)
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

   private sealed class StoredRecord
   {
      public string Id { get; set; } = default!;
      public string? Text { get; set; }
      public string? Description { get; set; }
      public string? AdditionalMetadata { get; set; }
      public string? ExternalSourceName { get; set; }
      public bool IsReference { get; set; }
      public float[]? Embedding { get; set; }
      public DateTimeOffset? Timestamp { get; set; }

      public static StoredRecord From(MemoryRecord record)
      {
         return new StoredRecord
         {
            Id = record.Metadata.Id,
            Text = record.Metadata.Text,
            Description = record.Metadata.Description,
            AdditionalMetadata = record.Metadata.AdditionalMetadata,
            ExternalSourceName = record.Metadata.ExternalSourceName,
            IsReference = record.Metadata.IsReference,
            Embedding = record.Embedding.ToArray(), // store vector
            Timestamp = record.Timestamp
         };
      }

      public MemoryRecord ToMemoryRecord(bool includeEmbedding)
      {
         var meta = new MemoryRecordMetadata(
             isReference: IsReference,
             id: Id,
             text: Text,
             description: Description,
             externalSourceName: ExternalSourceName,
             additionalMetadata: AdditionalMetadata);

         var embedding = includeEmbedding && Embedding is not null
             ? new ReadOnlyMemory<float>(Embedding)
             : ReadOnlyMemory<float>.Empty;

         return new MemoryRecord(meta, embedding, key: Id, timestamp: Timestamp);
      }
   }

   #endregion

}

internal static class AsyncLinq
{
   public static async Task<T?> FirstOrDefaultAsync<T>(
      this IAsyncEnumerable<T> source, CancellationToken ct = default)
   {
      await foreach (var item in source.WithCancellation(ct))
         return item;
      return default;
   }
}
