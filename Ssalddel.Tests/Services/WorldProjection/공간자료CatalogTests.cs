using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Services.WorldProjection.SpatialCatalog;

namespace Ssalddel.Tests.Services.WorldProjection;

public sealed class 공간자료CatalogTests
{
    internal static 공간자료Source Source(string revision="r1",string extra="",string path="graph.json")=>new(path,"fixture","GraphMap",Encoding.UTF8.GetBytes("""
        {"schemaVersion":"simulation-world-graph-map.v1","graphStableId":"graph:fixture","revision":"REV",
         "nodes":[{"nodeStableId":"node:a","name":"비공개 상호","roadAddress":"비공개 상세주소","buildingManagementNumber":"private-building-id","stateCode":"Unknown"},
                  {"nodeStableId":"node:b","title":"같은 이름"},{"nodeStableId":"node:c","title":"같은 이름"}],
         "edges":[{"edgeStableId":"edge:ab","fromNodeRef":"node:a","toNodeRef":"node:b","relationCode":"SupportsCandidate"}],
         "unresolvedRef":"not-in-bundle" EXTRA}
        """.Replace("REV",revision).Replace(" EXTRA",extra)));

    [Fact]
    public void 원본판본과JSON객체_정수소수문자열을보존하고_입력을수정하지않는다()
    {
        var source=Source(extra:",\"values\":[9223372036854775806,0.1234567890123456789,\"001\",null],\"extended\":{\"$date\":\"not-a-date\"}");
        var before=source.Bytes.ToArray();var batch=공간자료SnapshotBuilder.Build([source]);
        var payload=batch.Documents[0]["payload"];
        Assert.True(payload.IsBsonDocument);Assert.True(payload["values"][0].IsInt64);Assert.True(payload["values"][1].IsDecimal128);
        Assert.Equal("001",payload["values"][2].AsString);Assert.Equal("not-a-date",payload["extended"]["$date"].AsString);
        Assert.Equal(before,source.Bytes);Assert.Equal(공간자료Json.Hash(before),batch.Documents[0]["rawSha256"]);
        using var original=JsonDocument.Parse(before);var roundTrip=공간자료Json.Element(payload,true);
        Assert.True(JsonElement.DeepEquals(original.RootElement,roundTrip));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"unknown\"}")]
    [InlineData("{\"revision\":\"r1\"}")]
    [InlineData("{\"schemaVersion\":\"simulation-world-graph-map.v1\",\"revision\":1,\"revision\":2}")]
    [InlineData("{\"schemaVersion\":\"simulation-world-graph-map.v1\",\"x\":1e999}")]
    public void 미지원형식과중복JSON필드_숫자정밀도실패를조용히보정하지않는다(string json)
        =>Assert.ThrowsAny<Exception>(()=>공간자료SnapshotBuilder.Build([new("a.json","fixture","GraphMap",Encoding.UTF8.GetBytes(json))]));

    [Fact]
    public void 같은입력은같은묶음이며_문자열숫자판본은구별한다()
    {
        var a=공간자료SnapshotBuilder.Build([Source()]);var b=공간자료SnapshotBuilder.Build([Source()]);
        Assert.Equal(a.Snapshot["_id"],b.Snapshot["_id"]);
        Assert.Equal("doc:"+공간자료Json.Hash("graph.json\nsimulation-world-graph-map.v1\n\"r1\"\n"+공간자료CatalogCodes.AdapterVersion+"\n"),
            a.Documents[0]["_id"]);
        var number=Source() with {Bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Source().Bytes).Replace("\"r1\"","1"))};
        var text=Source("1");Assert.NotEqual(공간자료SnapshotBuilder.Build([number]).Documents[0]["_id"],공간자료SnapshotBuilder.Build([text]).Documents[0]["_id"]);
        Assert.True(공간자료SnapshotBuilder.Build([number]).Documents[0]["revision"].IsInt64);
    }

    [Fact]
    public void 명시관계만추출하고_같은이름_외부ID를자동연결하지않는다()
    {
        var batch=공간자료SnapshotBuilder.Build([Source()]);
        var edge=Assert.Single(batch.Relations,x=>x["kind"].AsString.StartsWith("GraphEdge:"));
        Assert.Equal("node:a",edge["fromKey"]);Assert.Equal("node:b",edge["toKey"]);
        Assert.DoesNotContain(batch.Relations,x=>x["fromKey"]=="node:b" && x["toKey"]=="node:c");
        Assert.All(batch.Relations,x=>{Assert.False(x["authorityApproved"].AsBoolean);Assert.StartsWith("/",x["pointer"].AsString);});
    }

    [Fact]
    public async Task 저장후독립서비스재조회와멱등성_민감정보경계를확인한다()
    {
        var store=new MemoryStore();var batch=공간자료SnapshotBuilder.Build([Source()]);
        Assert.True(await new 공간자료CatalogService(store).ImportAsync(batch,default)>0);
        Assert.Equal(0,await new 공간자료CatalogService(store).ImportAsync(batch,default));
        var service=new 공간자료CatalogService(store);await service.VerifyAsync(batch,true,default);
        var bundle=batch.Snapshot["_id"].AsString;var id=batch.Documents[0]["_id"].AsString;
        var summary=await service.DocumentsAsync(new(){BundleId=bundle},default);
        Assert.Single(summary.Items);Assert.False(summary.Items[0].TryGetProperty("payload",out _));
        var ordinary=await service.DocumentAsync(bundle,id,false,default);
        Assert.DoesNotContain("비공개",공간자료Json.Parse(Encoding.UTF8.GetBytes(ordinary.Document.GetRawText())).ToString());
        Assert.DoesNotContain("roadAddress",ordinary.Document.GetRawText());Assert.DoesNotContain("private-building-id",ordinary.Document.GetRawText());
        var detail=await service.DocumentAsync(bundle,id,true,default);
        Assert.True(detail.SensitiveIncluded);Assert.False(detail.GameStateConnected);
        Assert.Equal("비공개 상호",detail.Document.GetProperty("payload").GetProperty("nodes")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task 같은원본판본내용변경은새쓰기전에거부하고_기존묶음을보존한다()
    {
        var store=new MemoryStore();var service=new 공간자료CatalogService(store);
        await service.ImportAsync(공간자료SnapshotBuilder.Build([Source()]),default);var before=store.Total;
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.ImportAsync(공간자료SnapshotBuilder.Build([Source(extra:",\"changed\":true")]),default));
        Assert.Equal(before,store.Total);Assert.Single((await service.SnapshotsAsync(new(),default)).Items);
    }

    [Fact]
    public async Task 부분저장은조회에서숨기고_동일입력재시도로만완료한다()
    {
        var store=new MemoryStore{FailCollection=공간자료Collections.Elements};var service=new 공간자료CatalogService(store);
        var batch=공간자료SnapshotBuilder.Build([Source()]);
        await Assert.ThrowsAsync<IOException>(()=>service.ImportAsync(batch,default));
        Assert.Empty((await service.SnapshotsAsync(new(),default)).Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.DocumentsAsync(new(),default));
        store.FailCollection=null;await service.ImportAsync(batch,default);
        Assert.Single((await service.SnapshotsAsync(new(),default)).Items);
    }

    [Fact]
    public async Task 이전묶음의페이지가새반입판본과섞이지않는다()
    {
        var service=new 공간자료CatalogService(new MemoryStore());
        var first=공간자료SnapshotBuilder.Build([Source()]);await service.ImportAsync(first,default);
        var page=await service.ElementsAsync(new(){Take=2},default);
        await service.ImportAsync(공간자료SnapshotBuilder.Build([Source("r2",extra:",\"anotherRef\":\"node:c\"")]),default);
        var next=await service.ElementsAsync(new(){BundleId=page.BundleId,Skip=2,Take=2},default);
        Assert.Equal(first.Snapshot["_id"].AsString,next.BundleId);Assert.Equal(4,page.Total);Assert.Equal(4,next.Total);
        Assert.Equal(4,page.Items.Concat(next.Items).Select(x=>x.GetProperty("_id").GetString()).Distinct().Count());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.ElementsAsync(new(){Skip=2},default));
    }

    [Theory]
    [InlineData(0,0)] [InlineData(-1,100)] [InlineData(0,501)]
    public async Task 페이지상한과잘못된입력을차단한다(int skip,int take)
        =>await Assert.ThrowsAsync<ArgumentException>(()=>new 공간자료CatalogService(new MemoryStore()).ElementsAsync(new(){Skip=skip,Take=take},default));

    [Fact]
    public async Task 역관계조회는원문근거와해소불가를구분한다()
    {
        var service=new 공간자료CatalogService(new MemoryStore());await service.ImportAsync(공간자료SnapshotBuilder.Build([Source()]),default);
        var incoming=await service.RelationsAsync(new(){RelationKey="node:b",Direction="incoming"},default);
        Assert.True(incoming.Total>0);Assert.All(incoming.Items,x=>Assert.Equal("node:b",x.GetProperty("toKey").GetString()));
        Assert.All(incoming.Items,x=>Assert.Equal("ExactReferenceInBundle",x.GetProperty("targetResolution").GetString()));
        var missing=await service.RelationsAsync(new(){RelationKey="not-in-bundle",Direction="incoming"},default);
        Assert.Equal("ExternalOrUnresolved",Assert.Single(missing.Items).GetProperty("targetResolution").GetString());
    }

    [Fact]
    public async Task 객체형참조의기대판본충돌을낮은근거로덮지않는다()
    {
        var a=Source(extra:",\"catalogRef\":{\"path\":\"second.json\",\"expectedRevision\":\"r0\",\"expectedSha256\":\"WRONG\"}");
        var batch=공간자료SnapshotBuilder.Build([a,Source(path:"second.json")]);
        var service=new 공간자료CatalogService(new MemoryStore());await service.ImportAsync(batch,default);
        var links=await service.RelationsAsync(new(){RelationKey="second.json",Direction="incoming"},default);
        Assert.Equal("SourceRevisionMismatch",Assert.Single(links.Items).GetProperty("targetResolution").GetString());
    }

    [Fact]
    public async Task 도형좌표계를보존하고_표현사본도권위상태가아니다()
    {
        var geometry=new 공간자료Source("unity:map.json","fixture","Geometry",Encoding.UTF8.GetBytes("""
            {"revision":"r1","coordinateMethod":"WGS84-ECEF-ENU-at-zero-altitude","originLatitude":37.5806971,"originLongitude":127.0884106,
             "offsetX":550,"offsetZ":8,"halfExtent":500,"roads":[{"id":"road:1","x1":550,"x2":560,"z1":8,"z2":20}],"buildings":[]}
            """),"region:kr:bjd:1126010100");
        var batch=공간자료SnapshotBuilder.Build([geometry]);var service=new 공간자료CatalogService(new MemoryStore());await service.ImportAsync(batch,default);
        var result=await service.PresentationAsync(new(){BundleId=batch.Snapshot["_id"].AsString,DocumentId=batch.Documents[0]["_id"].AsString,
            SemanticLayerStableId="spatial-layer:road-centerline.v1",Tile="tile:myeonmok:500m:x0:z0"},default);
        Assert.False(result.GameStateConnected);Assert.Single(result.Document.GetProperty("items").EnumerateArray());
        Assert.Equal(37.5806971,result.Document.GetProperty("coordinateFrame").GetProperty("originLatitude").GetDouble());
        Assert.Equal("spatial-layer:road-centerline.v1",result.Document.GetProperty("items").EnumerateArray().Single().GetProperty("semanticLayerStableId").GetString());
    }

    [Fact]
    public void 면목동객체대장과배치후보를_실행권위없는별도문서로추출한다()
    {
        var catalog = new 공간자료Source("object-catalog.json", "fixture", "ObjectCatalog", Encoding.UTF8.GetBytes("""
            {"schemaVersion":"myeonmok-game-object-catalog.v1","catalogStableId":"game-object-catalog:fixture","revision":"r1",
             "workflowBindings":[{"bindingStableId":"workflow-binding:order","moduleCode":"Order"}],
             "candidateGenerationProfiles":[{"profileStableId":"candidate-profile:building","objectArchetypeRef":"object:building"}],
             "archetypes":[{"objectArchetypeStableId":"object:building","isExecutionAuthority":false}],
             "interactionProfiles":[{"profileStableId":"profile:order","wiId":"WI-ORDER-01","isExecutionAuthority":false}],
             "roleActionBindings":[{"bindingStableId":"binding:orderer","profileStableId":"profile:order","objectArchetypeRef":"object:building"}]}
            """));
        var projection = new 공간자료Source("generated:candidates.json", "fixture", "ObjectCandidateProjection", Encoding.UTF8.GetBytes("""
            {"schemaVersion":"myeonmok-game-object-candidate-projection.v1","projectionStableId":"projection:fixture","revision":"r1",
             "sceneReady":false,"gameStateConnected":false,"isExecutionAuthority":false,
             "candidates":[{"candidateStableId":"candidate:building:1","objectArchetypeRef":"object:building","sourceElementRef":"osm:way:1","tileRef":"tile:myeonmok:500m:x0:z0","placementEligibility":"BackdropOnly","isExecutionAuthority":false}]}
            """));

        var batch = 공간자료SnapshotBuilder.Build([catalog, projection]);

        Assert.Equal("game-object-catalog:fixture", batch.Documents.Single(x => x["kind"] == "ObjectCatalog")["stableId"]);
        Assert.Equal("projection:fixture", batch.Documents.Single(x => x["kind"] == "ObjectCandidateProjection")["stableId"]);
        Assert.Contains(batch.Elements, x => x["kind"] == "ObjectArchetype" && x["stableId"] == "object:building");
        Assert.Contains(batch.Elements, x => x["kind"] == "InteractionProfile" && x["stableId"] == "profile:order");
        Assert.Contains(batch.Elements, x => x["kind"] == "RoleActionBinding" && x["stableId"] == "binding:orderer");
        var candidate = Assert.Single(batch.Elements, x => x["kind"] == "ObjectCandidateBinding");
        Assert.Equal("candidate:building:1", candidate["stableId"]);
        Assert.Equal("object-candidates", candidate["layer"]);
        Assert.Contains(batch.Relations, x => x["fromKey"] == "candidate:building:1" && x["toKey"] == "osm:way:1");
        Assert.False(candidate["payload"]["isExecutionAuthority"].AsBoolean);
    }

    [Fact]
    public async Task 법정동고유식별자로_문서와구성요소와관계를같은범위에서조회한다()
    {
        const string myeonmok="region:kr:bjd:1126010100";
        const string junghwa="region:kr:bjd:1126010300";
        var first=Source(path:"myeonmok.json") with {AreaStableId=myeonmok};
        var second=Source("r2",path:"junghwa.json") with {AreaStableId=junghwa};
        var batch=공간자료SnapshotBuilder.Build([first,second]);
        Assert.Equal([myeonmok,junghwa],batch.Snapshot["areaStableIds"].AsBsonArray.Select(x=>x.AsString));

        var service=new 공간자료CatalogService(new MemoryStore());
        await service.ImportAsync(batch,default);
        var documents=await service.DocumentsAsync(new(){BundleId=batch.Snapshot["_id"].AsString,AreaStableId=junghwa},default);
        var elements=await service.ElementsAsync(new(){BundleId=batch.Snapshot["_id"].AsString,AreaStableId=junghwa},default);
        var relations=await service.RelationsAsync(new(){BundleId=batch.Snapshot["_id"].AsString,AreaStableId=junghwa},default);
        Assert.Single(documents.Items);Assert.Equal(4,elements.Total);Assert.True(relations.Total>0);
        Assert.All(elements.Items,x=>Assert.Equal(junghwa,x.GetProperty("areaStableId").GetString()));
        Assert.All(relations.Items,x=>Assert.Equal(junghwa,x.GetProperty("areaStableId").GetString()));

        var conflict=Source(extra:",\"areaStableId\":\""+myeonmok+"\"") with {AreaStableId=junghwa};
        Assert.Throws<InvalidDataException>(()=>공간자료SnapshotBuilder.Build([conflict]));
    }

    [Fact]
    public void 공통오행객체와동별패키지_관측재고스키마를별도문서로보존한다()
    {
        var area="region:kr:bjd:1126010300";
        var sources=new 공간자료Source[]
        {
            new("generated:common.json","shared","ObjectCatalog",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"five-element-work-object-catalog.v1","catalogStableId":"catalog:five-elements","revision":"r1",
                 "archetypes":[{"objectArchetypeStableId":"object:customer","elementCode":"METAL","isExecutionAuthority":false}],
                 "compatibilityAliases":[{"aliasStableId":"alias:customer","legacyRef":"old:customer","canonicalRef":"object:customer"}]}
                """)),
            new("registry.json","registry","NeighborhoodRegistry",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"neighborhood-package-registry.v1","registryStableId":"registry:neighborhoods","revision":"r1",
                 "coordinateFrames":[{"coordinateFrameStableId":"frame:seoul-east"}],
                 "packages":[{"packageStableId":"package:junghwa","areaStableId":"region:kr:bjd:1126010300"}]}
                """)),
            new("generated:package.json","neighborhood","NeighborhoodPackage",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"neighborhood-spatial-package.v1","packageStableId":"package:junghwa","revision":"r1",
                 "areaStableId":"region:kr:bjd:1126010300","readinessCode":"InventoryReady","sceneReady":false}
                """),area),
            new("generated:inventory.json","neighborhood","ObservationInventory",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"neighborhood-observation-inventory.v1","inventoryStableId":"inventory:junghwa","revision":"r1",
                 "areaStableId":"region:kr:bjd:1126010300","observations":[{"observationStableId":"observation:1","placementEligibility":"CoordinateCandidateOnly","isExecutionAuthority":false}]}
                """),area)
        };

        var batch=공간자료SnapshotBuilder.Build(sources);
        Assert.Equal(4,batch.Documents.Count);
        Assert.Contains(batch.Elements,x=>x["kind"]=="ObjectArchetype" && x["stableId"]=="object:customer");
        Assert.Contains(batch.Elements,x=>x["kind"]=="NeighborhoodPackageRegistration" && x["stableId"]=="package:junghwa"
            && x["areaStableId"]==area);
        var observation=Assert.Single(batch.Elements,x=>x["kind"]=="ObservationCandidate");
        Assert.Equal(area,observation["areaStableId"]);Assert.Equal("observations",observation["layer"]);
        Assert.False(observation["payload"]["isExecutionAuthority"].AsBoolean);
    }

    [Fact]
    public async Task 의미레이어대장과v2동별패키지를_기존구조레이어와분리해조회한다()
    {
        const string area="region:kr:bjd:1126010100";
        var sources=new 공간자료Source[]
        {
            new("semantic-layers.json","shared","SemanticLayerCatalog",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"spatial-semantic-layer-catalog.v1","catalogStableId":"catalog:spatial-layers","revision":"r1",
                 "semanticLayers":[{"semanticLayerStableId":"spatial-layer:building-footprint.v1","layerCode":"BuildingFootprint"}]}
                """)),
            new("registry.json","registry","NeighborhoodRegistry",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"neighborhood-package-registry.v2","registryStableId":"registry:neighborhoods","revision":"r2",
                 "packages":[{"packageStableId":"package:myeonmok","areaStableId":"region:kr:bjd:1126010100"}]}
                """)),
            new("package.json","neighborhood","NeighborhoodPackage",Encoding.UTF8.GetBytes("""
                {"schemaVersion":"neighborhood-spatial-package.v2","packageStableId":"package:myeonmok","revision":"r2",
                 "areaStableId":"region:kr:bjd:1126010100","readinessCode":"PlacementReviewReady","sceneReady":false,
                 "layerBindings":[{"layerBindingStableId":"binding:myeonmok:building","semanticLayerRef":"spatial-layer:building-footprint.v1",
                 "sourceRef":"placement:myeonmok","relationshipCode":"ProjectsLayer","readinessCode":"PlacementReviewReady"}]}
                """),area)
        };
        var batch=공간자료SnapshotBuilder.Build(sources);
        var definition=Assert.Single(batch.Elements,x=>x["kind"]=="SemanticLayerDefinition");
        Assert.Equal("spatial-layer:building-footprint.v1",definition["semanticLayerStableId"]);
        var binding=Assert.Single(batch.Elements,x=>x["kind"]=="SemanticLayerBinding");
        Assert.Equal("layer-bindings",binding["layer"]);Assert.Equal(area,binding["areaStableId"]);

        var service=new 공간자료CatalogService(new MemoryStore());await service.ImportAsync(batch,default);
        var page=await service.ElementsAsync(new(){BundleId=batch.Snapshot["_id"].AsString,AreaStableId=area,
            SemanticLayerStableId="spatial-layer:building-footprint.v1"},default);
        Assert.Single(page.Items);Assert.Equal("binding:myeonmok:building",page.Items[0].GetProperty("stableId").GetString());
        var relations=await service.RelationsAsync(new(){BundleId=batch.Snapshot["_id"].AsString,AreaStableId=area,
            SemanticLayerStableId="spatial-layer:building-footprint.v1"},default);
        Assert.True(relations.Total>=2);
    }

    [Fact]
    public async Task 저장자료손상은503용실패이며원본으로자동보충하지않는다()
    {
        var store=new MemoryStore();var service=new 공간자료CatalogService(store);var batch=공간자료SnapshotBuilder.Build([Source()]);await service.ImportAsync(batch,default);
        store.Data[공간자료Collections.Documents].Values.First()["payload"]["revision"]="tampered";
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.DocumentAsync(batch.Snapshot["_id"].AsString,batch.Documents[0]["_id"].AsString,false,default));
    }

    [Fact]
    public async Task 누락문서나관계대상손상을빈조회나정확연결로숨기지않는다()
    {
        var store=new MemoryStore();var service=new 공간자료CatalogService(store);
        var batch=공간자료SnapshotBuilder.Build([Source()]);await service.ImportAsync(batch,default);
        var target=store.Data[공간자료Collections.Elements].Values.Single(x=>x["stableId"]=="node:b");
        target["payload"]["title"]="tampered";
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.RelationsAsync(new(){RelationKey="node:b"},default));
        store.Data[공간자료Collections.Documents].Clear();
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.ElementsAsync(new(){DocumentId=batch.Documents[0]["_id"].AsString},default));
    }

    [Fact]
    public void 현재지도20파일의스키마변형을빠짐없이읽고원문hash를보존한다()
    {
        var root=RepositoryRoot();var sources=new List<공간자료Source>();
        foreach(var item in new[]{("graph-maps","GraphMap"),("placement-map-profiles","PlacementMap")})
            foreach(var path in Directory.GetFiles(Path.Combine(root,"eng/world-seedbeds",item.Item1),"*.json"))
                sources.Add(new(Path.GetRelativePath(root,path).Replace('\\','/'),"repository",item.Item2,File.ReadAllBytes(path)));
        var batch=공간자료SnapshotBuilder.Build(sources);Assert.Equal(20,batch.Documents.Count);
        Assert.Equal(13,batch.Documents.Select(x=>x["schema"]).Distinct().Count());
        Assert.All(batch.Documents,x=>Assert.Equal(공간자료Json.Hash(sources.Single(s=>s.SourcePath==x["sourcePath"]).Bytes),x["rawSha256"]));
        Assert.Contains(batch.Elements,x=>x["kind"]=="Constraint");Assert.Contains(batch.Elements,x=>x["kind"]=="CodeBinding");
        Assert.Contains(batch.Elements,x=>x["kind"]=="PlacementIntent");Assert.Contains(batch.Elements,x=>x["kind"]=="NormalizedRelation");
    }

    private static string RepositoryRoot()
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
            if(Directory.Exists(Path.Combine(dir.FullName,"eng/world-seedbeds/graph-maps")))return dir.FullName;
        throw new DirectoryNotFoundException("TestRepositoryUnavailable");
    }

    internal sealed class MemoryStore:I공간자료CatalogStore
    {
        public readonly Dictionary<string,Dictionary<string,BsonDocument>> Data=공간자료Collections.All.ToDictionary(x=>x,_=>new Dictionary<string,BsonDocument>());
        public string? FailCollection {get;set;}
        public int Total=>Data.Values.Sum(x=>x.Count);
        public Task<IReadOnlyList<BsonDocument>> FindAsync(string collection,공간자료Filter filter,int skip,int take,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();var found=Data[collection].Values.Where(filter.Matches);
            found=filter.NewestFirst?found.OrderByDescending(x=>공간자료Json.Text(x,"createdAtUtc"),StringComparer.Ordinal).ThenBy(x=>x["_id"].AsString,StringComparer.Ordinal):found.OrderBy(x=>x["_id"].AsString,StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<BsonDocument>>(found.Skip(skip).Take(take).Select(x=>(BsonDocument)x.DeepClone()).ToArray());
        }
        public Task<long> CountAsync(string collection,공간자료Filter filter,CancellationToken ct)=>Task.FromResult((long)Data[collection].Values.Count(filter.Matches));
        public Task<int> InsertImmutableAsync(string collection,IReadOnlyList<BsonDocument> records,CancellationToken ct)
        {
            if(collection==FailCollection)throw new IOException("InjectedFixtureFailure");var inserted=0;
            foreach(var record in records)
            {
                var id=record["_id"].AsString;
                if(Data[collection].TryGetValue(id,out var current))
                {var field=collection==공간자료Collections.Sets?"manifestHash":"recordHash";if(current[field]!=record[field])throw new InvalidDataException("SpatialSourceRevisionConflict");}
                else {Data[collection].Add(id,(BsonDocument)record.DeepClone());inserted++;}
            }
            return Task.FromResult(inserted);
        }
        public Task MarkReadyAsync(string bundleId,string manifestHash,CancellationToken ct)
        {Assert.Equal(manifestHash,Data[공간자료Collections.Sets][bundleId]["manifestHash"]);Data[공간자료Collections.Sets][bundleId]["status"]="Ready";return Task.CompletedTask;}
    }
}
