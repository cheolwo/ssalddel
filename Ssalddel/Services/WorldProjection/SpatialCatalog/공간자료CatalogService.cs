using MongoDB.Bson;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.WorldProjection;

namespace Ssalddel.Services.WorldProjection.SpatialCatalog;

[SsalddelCodeMetadata(공간자료CatalogCodes.Feature,SsalddelCodeLayer.Application,
    "공간 문서의 불변 반입·재조회 검증과 같은 묶음의 JSON 조회를 조율한다.",StepKey="catalog",FlowOrder=30,
    ReadsFrom=SsalddelCodeDataScope.SharedPublicData|SsalddelCodeDataScope.DerivedWorld,WritesTo=SsalddelCodeDataScope.DerivedWorld,
    Boundary="Ready는 저장 검증 결과이며 사람 승인·현실 결속·Simulation 상태 권위가 아니다.")]
public sealed class 공간자료CatalogService(I공간자료CatalogStore store)
{
    public async Task<int> ImportAsync(공간자료ImportBatch batch, CancellationToken ct)
    {
        // 변경된 같은 원본 판본은 쓰기 전 차단한다. 독립 반입이 경합해도 각 _id의 insert-only 대조가 다시 차단한다.
        foreach(var group in Groups(batch))
        foreach(var chunk in group.Records.Chunk(300))
        {
            var existing=await store.FindAsync(group.Collection,new(Ids:chunk.Select(x=>x["_id"].AsString).ToArray()),0,chunk.Length,ct);
            foreach(var record in existing)
                if(공간자료Json.Digest(record)!=chunk.Single(x=>x["_id"]==record["_id"])["recordHash"].AsString)
                    throw new InvalidDataException("SpatialSourceRevisionConflict");
        }
        var inserted=await store.InsertImmutableAsync(공간자료Collections.Sets,[batch.Snapshot],ct);
        foreach(var group in Groups(batch)) inserted+=await store.InsertImmutableAsync(group.Collection,group.Records,ct);
        await VerifyAsync(batch, requireReady:false, ct);
        await store.MarkReadyAsync(batch.Snapshot["_id"].AsString,batch.Snapshot["manifestHash"].AsString,ct);
        await VerifyAsync(batch, requireReady:true, ct);
        return inserted;
    }

    public async Task VerifyAsync(공간자료ImportBatch batch, bool requireReady, CancellationToken ct)
    {
        var snapshot=(await store.FindAsync(공간자료Collections.Sets,new(Ids:[batch.Snapshot["_id"].AsString]),0,1,ct)).SingleOrDefault();
        if(snapshot is null || snapshot["manifestHash"]!=batch.Snapshot["manifestHash"] || !snapshot["manifest"].Equals(batch.Snapshot["manifest"]))
            throw new InvalidDataException("SpatialBundleManifestMismatch");
        if(requireReady && snapshot["status"]!="Ready") throw new InvalidDataException("SpatialBundleNotReady");
        var ids=batch.Documents.Select(x=>x["_id"].AsString).ToArray();
        foreach(var group in Groups(batch))
        {
            var filter=group.Collection==공간자료Collections.Documents ? new 공간자료Filter(Ids:ids) : new(DocumentIds:ids);
            if(await store.CountAsync(group.Collection,filter,ct)!=group.Records.Count) throw new InvalidDataException("SpatialReadbackCountMismatch");
            foreach(var chunk in group.Records.Chunk(300))
            {
                var actual=await store.FindAsync(group.Collection,new(Ids:chunk.Select(x=>x["_id"].AsString).ToArray()),0,chunk.Length,ct);
                if(actual.Count!=chunk.Length) throw new InvalidDataException("SpatialReadbackMissing");
                foreach(var record in actual)
                {
                    공간자료Json.Verify(record);
                    if(record["recordHash"]!=chunk.Single(x=>x["_id"]==record["_id"])["recordHash"])
                        throw new InvalidDataException("SpatialReadbackHashMismatch");
                }
            }
        }
    }

    private static IEnumerable<(string Collection,IReadOnlyList<BsonDocument> Records)> Groups(공간자료ImportBatch batch)
    {
        yield return (공간자료Collections.Documents,batch.Documents);
        yield return (공간자료Collections.Elements,batch.Elements);
        yield return (공간자료Collections.Relations,batch.Relations);
    }

    public async Task<공간자료Page> SnapshotsAsync(공간자료Query query,CancellationToken ct)
    {
        Validate(query, paginationRequiresBundle:false);
        var filter=new 공간자료Filter(Equal:new Dictionary<string,string>{{"status","Ready"}},NewestFirst:true);
        var count=(int)await store.CountAsync(공간자료Collections.Sets,filter,ct);
        var items=await store.FindAsync(공간자료Collections.Sets,filter,query.Skip,query.Take,ct);
        return Page("",count,query,items);
    }

    public async Task<공간자료Page> DocumentsAsync(공간자료Query query,CancellationToken ct)
    {
        Validate(query);
        var snapshot=await SnapshotAsync(query.BundleId,ct);
        var filter=new 공간자료Filter(Ids:DocumentIds(snapshot),Equal:DocumentConditions(query,includeKind:true));
        var total=(int)await store.CountAsync(공간자료Collections.Documents,filter,ct);
        var records=await store.FindAsync(공간자료Collections.Documents,filter,query.Skip,query.Take,ct);
        foreach(var record in records) VerifyDocument(snapshot,record);
        var summaries=records.Select(x=>{ var copy=(BsonDocument)x.DeepClone(); copy.Remove("payload");return copy; }).ToArray();
        return Page(snapshot["_id"].AsString,total,query,summaries);
    }

    public async Task<공간자료Detail> DocumentAsync(string bundleId,string documentId,bool sensitive,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(bundleId)) throw new ArgumentException("SpatialBundleRequired");
        var snapshot=await SnapshotAsync(bundleId,ct);
        if(!DocumentIds(snapshot).Contains(documentId)) throw new KeyNotFoundException("SpatialDocumentNotInBundle");
        var doc=(await store.FindAsync(공간자료Collections.Documents,new(Ids:[documentId]),0,1,ct)).SingleOrDefault()
            ?? throw new InvalidDataException("SpatialStoredDocumentMissing");
        VerifyDocument(snapshot,doc);
        return new(bundleId,sensitive,false,공간자료Json.Element(doc,sensitive));
    }

    public async Task<공간자료Page> ElementsAsync(공간자료Query query,CancellationToken ct)
    {
        Validate(query);
        var snapshot=await SnapshotAsync(query.BundleId,ct);
        var docs=await EligibleDocumentsAsync(snapshot,query,ct);
        var conditions=new Dictionary<string,string>();
        Add(conditions,"areaStableId",query.AreaStableId);
        Add(conditions,"kind",query.Kind); Add(conditions,"layer",query.Layer);
        Add(conditions,"semanticLayerStableId",query.SemanticLayerStableId); Add(conditions,"tile",query.Tile);
        Add(conditions,"stableId",query.StableId); Add(conditions,"reviewState",query.ReviewState);
        var filter=new 공간자료Filter(DocumentIds:docs.Select(x=>x["_id"].AsString).ToArray(),Equal:conditions);
        var total=(int)await store.CountAsync(공간자료Collections.Elements,filter,ct);
        var records=await store.FindAsync(공간자료Collections.Elements,filter,query.Skip,query.Take,ct);
        foreach(var record in records) 공간자료Json.Verify(record);
        return Page(snapshot["_id"].AsString,total,query,records);
    }

    public async Task<공간자료Page> RelationsAsync(공간자료Query query,CancellationToken ct)
    {
        Validate(query);
        var snapshot=await SnapshotAsync(query.BundleId,ct);
        var documents=await EligibleDocumentsAsync(snapshot,query,ct);
        var conditions=new Dictionary<string,string>(); Add(conditions,"areaStableId",query.AreaStableId); Add(conditions,"kind",query.Kind);
        Add(conditions,"semanticLayerStableId",query.SemanticLayerStableId);
        var filter=new 공간자료Filter(DocumentIds:documents.Select(x=>x["_id"].AsString).ToArray(),Equal:conditions,RelationKey:query.RelationKey,Direction:query.Direction);
        var total=(int)await store.CountAsync(공간자료Collections.Relations,filter,ct);
        var relations=await store.FindAsync(공간자료Collections.Relations,filter,query.Skip,query.Take,ct);
        var allDocuments=await store.FindAsync(공간자료Collections.Documents,new(Ids:DocumentIds(snapshot)),0,100,ct);
        if(allDocuments.Count!=DocumentIds(snapshot).Length)throw new InvalidDataException("SpatialStoredDocumentMissing");
        foreach(var document in allDocuments)VerifyDocument(snapshot,document);
        var targets=relations.Select(x=>x["toKey"].AsString).Distinct().ToArray();
        var targetElements=await store.FindAsync(공간자료Collections.Elements,new(DocumentIds:DocumentIds(snapshot),StableIds:targets),0,100_000,ct);
        foreach(var target in targetElements)공간자료Json.Verify(target);
        var output=new List<BsonDocument>();
        foreach(var relation in relations)
        {
            공간자료Json.Verify(relation);
            var copy=(BsonDocument)relation.DeepClone(); var key=copy["toKey"].AsString;
            copy["sourcePath"]=allDocuments.Single(x=>x["_id"]==copy["documentId"])["sourcePath"];
            var matches=targetElements.Where(x=>x["stableId"]==key).ToArray();
            var local=matches.Where(x=>x["documentId"]==copy["documentId"]).ToArray();
            if(local.Length>0) matches=local;
            var docMatches=allDocuments.Where(x=>x["sourcePath"]==key || x["stableId"]==key).ToArray();
            var candidateIds=matches.Select(x=>x["_id"].AsString).Concat(docMatches.Select(x=>x["_id"].AsString)).Distinct().ToArray();
            var expected=copy["expectedSha256"].AsString;
            var mismatch=expected.Length>0 && docMatches.Length>0 && docMatches.All(x=>!string.Equals(x["rawSha256"].AsString,expected,StringComparison.OrdinalIgnoreCase));
            var expectedRevision=copy["expectedRevision"].AsString;
            mismatch |= expectedRevision.Length>0 && docMatches.Length>0 && docMatches.All(x=>공간자료Json.Text(x,"revision")!=expectedRevision);
            copy["targetResolution"]=mismatch ? "SourceRevisionMismatch" : candidateIds.Length switch { 0=>"ExternalOrUnresolved",1=>"ExactReferenceInBundle",_=>"MultipleRepresentationsOrRevisions" };
            copy["targetCandidateIds"]=new BsonArray(candidateIds); copy["resolutionIsApproval"]=false;
            output.Add(copy);
        }
        return Page(snapshot["_id"].AsString,total,query,output);
    }

    public async Task<공간자료Detail> PresentationAsync(공간자료Query query,CancellationToken ct)
    {
        Validate(query);
        if(string.IsNullOrWhiteSpace(query.BundleId) || string.IsNullOrWhiteSpace(query.DocumentId)) throw new ArgumentException("SpatialPresentationScopeRequired");
        var detail=await DocumentAsync(query.BundleId,query.DocumentId,false,ct);
        var document=공간자료Json.Parse(System.Text.Encoding.UTF8.GetBytes(detail.Document.GetRawText()));
        if(공간자료Json.Text(document,"kind") is not ("Geometry" or "PresentationProjection" or "PlacementMap"))
            throw new ArgumentException("SpatialPresentationDocumentKindInvalid");
        var elements=await ElementsAsync(query,ct);
        var result=new BsonDocument
        {
            ["schema"]="spatial-presentation-page.r1", ["sourceDocumentId"]=query.DocumentId,
            ["sourceRawSha256"]=document["rawSha256"], ["sourceRevision"]=document["revision"],
            ["coordinateFrame"]=document["coordinateFrame"], ["privateReviewOnly"]=true,
            ["gameStateConnected"]=false, ["distributionApproved"]=false,
            ["total"]=elements.Total, ["skip"]=elements.Skip, ["take"]=elements.Take,
            ["nextSkip"]=elements.NextSkip.HasValue ? (BsonValue)elements.NextSkip.Value : BsonNull.Value,
            ["items"]=new BsonArray(elements.Items.Select(x=>공간자료Json.Parse(System.Text.Encoding.UTF8.GetBytes(x.GetRawText()))))
        };
        return new(query.BundleId,false,false,공간자료Json.Element(result));
    }

    private async Task<BsonDocument> SnapshotAsync(string? id,CancellationToken ct)
    {
        var filter=new 공간자료Filter(Ids:id is null ? null : [id],Equal:new Dictionary<string,string>{{"status","Ready"}},NewestFirst:true);
        var snapshot=(await store.FindAsync(공간자료Collections.Sets,filter,0,1,ct)).SingleOrDefault()
            ?? throw new KeyNotFoundException("SpatialReadyBundleUnavailable");
        var digest=공간자료Json.Hash(공간자료Json.Element(snapshot["manifest"],true).GetRawText()+"\n"+snapshot["adapterVersion"].AsString);
        if(digest!=snapshot["manifestHash"].AsString || snapshot["_id"]!="bundle:"+digest)
            throw new InvalidDataException("SpatialStoredManifestCorrupt");
        return snapshot;
    }

    private async Task<IReadOnlyList<BsonDocument>> EligibleDocumentsAsync(BsonDocument snapshot,공간자료Query query,CancellationToken ct)
    {
        var ids=DocumentIds(snapshot);
        if(query.DocumentId is not null)
        {
            if(!ids.Contains(query.DocumentId)) throw new KeyNotFoundException("SpatialDocumentNotInBundle");
            ids=[query.DocumentId];
        }
        var documents=await store.FindAsync(공간자료Collections.Documents,new(Ids:ids),0,100,ct);
        if(documents.Count!=ids.Length)throw new InvalidDataException("SpatialStoredDocumentMissing");
        foreach(var document in documents)VerifyDocument(snapshot,document);
        var conditions=DocumentConditions(query,false,includeArea:false);
        return documents.Where(x=>conditions.All(c=>공간자료Json.Text(x,c.Key)==c.Value)).ToArray();
    }

    private static void VerifyDocument(BsonDocument snapshot,BsonDocument document)
    {
        공간자료Json.Verify(document);
        var expected=snapshot["manifest"].AsBsonArray.Single(x=>x["documentId"]==document["_id"]);
        if(expected["recordHash"]!=document["recordHash"])throw new InvalidDataException("SpatialDocumentManifestMismatch");
    }

    private static string[] DocumentIds(BsonDocument snapshot) => snapshot["manifest"].AsBsonArray.Select(x=>x["documentId"].AsString).ToArray();
    private static Dictionary<string,string> DocumentConditions(공간자료Query query,bool includeKind,bool includeArea=true)
    {
        var conditions=new Dictionary<string,string>(); Add(conditions,"dataset",query.Dataset);
        if(includeArea) Add(conditions,"areaStableId",query.AreaStableId);
        if(includeKind) { Add(conditions,"kind",query.Kind);Add(conditions,"stableId",query.StableId); }
        Add(conditions,"reviewState",query.ReviewState);
        if(query.Revision is not null) conditions["revisionKey"]=query.Revision;
        return conditions;
    }
    private static void Add(Dictionary<string,string> values,string key,string? value) { if(value is not null) values[key]=value; }
    private static 공간자료Page Page(string id,int total,공간자료Query query,IEnumerable<BsonDocument> items) =>
        new(id,total,query.Skip,query.Take,query.Skip+query.Take<total ? query.Skip+query.Take : null,true,items.Select(x=>공간자료Json.Element(x)).ToArray());

    private static void Validate(공간자료Query query,bool paginationRequiresBundle=true)
    {
        if(query.Skip<0 || query.Skip>1_000_000 || query.Take is <1 or >500) throw new ArgumentException("SpatialPageOutOfRange");
        if(paginationRequiresBundle && query.Skip>0 && string.IsNullOrWhiteSpace(query.BundleId)) throw new ArgumentException("SpatialBundleRequiredForPagination");
        if(query.Direction is not ("both" or "incoming" or "outgoing")) throw new ArgumentException("SpatialDirectionInvalid");
        if(new[]{query.BundleId,query.Dataset,query.AreaStableId,query.Kind,query.Revision,query.ReviewState,query.DocumentId,query.StableId,query.SemanticLayerStableId,query.Layer,query.Tile,query.RelationKey}.Any(x=>x is not null && (x.Length==0 || x.Length>512 || x.Any(char.IsControl))))
            throw new ArgumentException("SpatialQueryValueInvalid");
    }
}
