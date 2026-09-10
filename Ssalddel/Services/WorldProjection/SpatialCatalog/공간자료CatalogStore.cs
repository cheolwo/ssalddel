using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using 살뜰.Services.Options;

namespace Ssalddel.Services.WorldProjection.SpatialCatalog;

public static class 공간자료Collections
{
    public const string Sets = "spatial_snapshot_sets";
    public const string Documents = "spatial_documents";
    public const string Elements = "spatial_elements";
    public const string Relations = "spatial_relations";
    public static readonly string[] All = [Sets, Documents, Elements, Relations];
}

// 제한된 동등 비교만 노출한다. HTTP에서 Mongo filter/정렬식을 직접 받지 않는다.
public sealed record 공간자료Filter(
    IReadOnlyList<string>? Ids = null, IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyDictionary<string, string>? Equal = null, string? RelationKey = null,
    string Direction = "both", bool NewestFirst = false, IReadOnlyList<string>? StableIds = null)
{
    public bool Matches(BsonDocument record) =>
        (Ids is null || Ids.Contains(공간자료Json.Text(record, "_id"))) &&
        (DocumentIds is null || DocumentIds.Contains(공간자료Json.Text(record, "documentId"))) &&
        (StableIds is null || StableIds.Contains(공간자료Json.Text(record, "stableId"))) &&
        (Equal is null || Equal.All(x => 공간자료Json.Text(record, x.Key) == x.Value)) &&
        (RelationKey is null ||
            (Direction != "incoming" && 공간자료Json.Text(record, "fromKey") == RelationKey) ||
            (Direction != "outgoing" && 공간자료Json.Text(record, "toKey") == RelationKey));
}

public interface I공간자료CatalogStore
{
    Task<IReadOnlyList<BsonDocument>> FindAsync(string collection, 공간자료Filter filter,
        int skip, int take, CancellationToken ct);
    Task<long> CountAsync(string collection, 공간자료Filter filter, CancellationToken ct);
    Task<int> InsertImmutableAsync(string collection, IReadOnlyList<BsonDocument> records, CancellationToken ct);
    Task MarkReadyAsync(string bundleId, string manifestHash, CancellationToken ct);
}

public sealed class Mongo공간자료CatalogStore(IMongoClient client, IOptions<MongoDbOptions> options)
    : I공간자료CatalogStore
{
    private readonly IMongoDatabase _db = client.GetDatabase(
        string.IsNullOrWhiteSpace(options.Value.Database) ? throw new InvalidOperationException("SpatialDatabaseRequired") : options.Value.Database);
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexed;

    private IMongoCollection<BsonDocument> Collection(string name)
    {
        if (!공간자료Collections.All.Contains(name)) throw new ArgumentException("SpatialCollectionNotAllowed");
        return _db.GetCollection<BsonDocument>(name);
    }

    private static FilterDefinition<BsonDocument> Filter(공간자료Filter query)
    {
        var builder = Builders<BsonDocument>.Filter;
        var parts = new List<FilterDefinition<BsonDocument>>();
        if (query.Ids is not null) parts.Add(builder.In("_id", query.Ids));
        if (query.DocumentIds is not null) parts.Add(builder.In("documentId", query.DocumentIds));
        if (query.StableIds is not null) parts.Add(builder.In("stableId", query.StableIds));
        if (query.Equal is not null) parts.AddRange(query.Equal.Select(x => builder.Eq(x.Key, x.Value)));
        if (query.RelationKey is not null) parts.Add(query.Direction switch
        {
            "incoming" => builder.Eq("toKey", query.RelationKey),
            "outgoing" => builder.Eq("fromKey", query.RelationKey),
            _ => builder.Or(builder.Eq("fromKey", query.RelationKey), builder.Eq("toKey", query.RelationKey))
        });
        return parts.Count == 0 ? builder.Empty : builder.And(parts);
    }

    public async Task<IReadOnlyList<BsonDocument>> FindAsync(string collection, 공간자료Filter filter,
        int skip, int take, CancellationToken ct) => await Collection(collection).Find(Filter(filter))
        .Sort(filter.NewestFirst ? Builders<BsonDocument>.Sort.Descending("createdAtUtc").Ascending("_id") : Builders<BsonDocument>.Sort.Ascending("_id"))
        .Skip(skip).Limit(take).ToListAsync(ct);

    public Task<long> CountAsync(string collection, 공간자료Filter filter, CancellationToken ct)
        => Collection(collection).CountDocumentsAsync(Filter(filter), cancellationToken: ct);

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        await _indexGate.WaitAsync(ct);
        try
        {
            if (_indexed) return;
            foreach (var name in new[] { 공간자료Collections.Elements, 공간자료Collections.Relations })
                await Collection(name).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("documentId").Ascending("kind").Ascending("_id")), cancellationToken: ct);
            foreach (var name in new[] { 공간자료Collections.Documents, 공간자료Collections.Elements, 공간자료Collections.Relations })
                await Collection(name).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("areaStableId").Ascending("kind").Ascending("_id")), cancellationToken: ct);
            foreach (var name in new[] { "fromKey", "toKey" })
                await Collection(공간자료Collections.Relations).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(name).Ascending("documentId")), cancellationToken: ct);
            await Collection(공간자료Collections.Elements).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("documentId").Ascending("layer").Ascending("tile")), cancellationToken: ct);
            _indexed = true;
        }
        finally { _indexGate.Release(); }
    }

    public async Task<int> InsertImmutableAsync(string collection, IReadOnlyList<BsonDocument> records, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var inserted = 0;
        foreach (var chunk in records.Chunk(300))
        {
            if (chunk.Length == 0) continue;
            try { await Collection(collection).InsertManyAsync(chunk, new InsertManyOptions { IsOrdered = false }, ct); inserted += chunk.Length; }
            catch (MongoBulkWriteException<BsonDocument> ex) when (ex.WriteErrors.All(x => x.Category == ServerErrorCategory.DuplicateKey) && ex.WriteConcernError is null)
            { inserted += (int)ex.Result.InsertedCount; }
            var actual = await FindAsync(collection, new(Ids: chunk.Select(x => x["_id"].AsString).ToArray()), 0, chunk.Length, ct);
            if (actual.Count != chunk.Length) throw new InvalidDataException("SpatialInsertReadbackMissing");
            foreach (var record in actual)
            {
                var expected = chunk.Single(x => x["_id"] == record["_id"]);
                // 묶음의 Ready 표시는 검증 이후 단일 문서 원자 갱신한다.
                if (collection == 공간자료Collections.Sets)
                {
                    if (record["manifestHash"] != expected["manifestHash"]) throw new InvalidDataException("SpatialBundleConflict");
                }
                else if (공간자료Json.Digest(record) != expected["recordHash"].AsString || record["recordHash"] != expected["recordHash"])
                    throw new InvalidDataException("SpatialSourceRevisionConflict");
            }
        }
        return inserted;
    }

    public async Task MarkReadyAsync(string bundleId, string manifestHash, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", bundleId) & Builders<BsonDocument>.Filter.Eq("manifestHash", manifestHash);
        var result = await Collection(공간자료Collections.Sets).UpdateOneAsync(filter,
            Builders<BsonDocument>.Update.Set("status", "Ready"), cancellationToken: ct);
        if (result.MatchedCount != 1) throw new InvalidDataException("SpatialBundleCommitMissing");
    }
}
