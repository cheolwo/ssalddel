using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Services.WorldProjection.SpatialCatalog;
using 살뜰.Services.Options;

internal static class 공간자료CatalogImport
{
    internal const string ArtifactRelative = "artifacts/local/spatial-catalog/20260909-r5";
    internal const string UnityRoot = "C:/Users/user/ssalddel";
    private static readonly string[] GraphFiles =
    ["graph-map-overlays.v1.json", "hans-farm-fence-restoration.v1.json", "hans-farm-hex03-campaign.v1.json",
     "jungnang-myeonmok-reference.v1.json", "jungnang-myeonmok-reference.v2.json",
     "northern-life-hub-discovery.normalization.v1.json", "northern-life-hub-discovery.partitions.v1.json",
     "northern-life-hub-discovery.v1.json", "placement-rule-bindings.v1.json", "synthetic-neighborhood.v1.json", "unity-code-bindings.v1.json"];
    private static readonly string[] PlacementFiles =
    ["farm-cultivation-plot-seed-alignment.v1.json", "forest-edge-farm-hans-living-farm.v1.json",
     "forest-edge-living-farm-defense-region.h4-e4.v1.json", "hans-farm-first-fence-restoration-e5.v1.json",
     "hans-farm-hex03-development-handoff.v1.json", "jungnang-myeonmok-reference.v1.json",
     "jungnang-myeonmok-reference.v2.json", "neighborhood-market-extension.v1.json", "synthetic-neighborhood.v1.json"];
    private static readonly string[] ObjectCatalogFiles = ["jungnang-myeonmok-game-objects.v1.json"];

    internal static List<공간자료Source> ReadInputs(string root)
    {
        var paths=new List<(string Path,string Kind)>();
        AddDirectory("eng/world-seedbeds/graph-maps",GraphFiles,"GraphMap");
        AddDirectory("eng/world-seedbeds/placement-map-profiles",PlacementFiles,"PlacementMap");
        AddDirectory("eng/world-seedbeds/object-catalogs",ObjectCatalogFiles,"ObjectCatalog");
        paths.AddRange(new[]
        {
            (동별공간PackageCatalog.RegistryRelative,"NeighborhoodRegistry"),
            ("eng/world-seedbeds/map-source-manifests/jungnang-myeonmok.v1.json","SourceManifest"),
            ("eng/world-seedbeds/map-source-manifests/jungnang-myeonmok.v2.json","SourceManifest"),
            ("eng/world-seedbeds/reality-context/farm-production.v1.json","RealityContextDefinition"),
            ("artifacts/local/public-data/myeonmok-diorama-view-r1/private-spatial-index.json","SpatialIndex"),
            ("artifacts/local/public-data/myeonmok-diorama-view-r1/presentation-projection.json","PresentationProjection"),
            ("unity:Assets/Ssalddel/Resources/SagajeongReference.json","Geometry")
        });
        return paths.OrderBy(x=>x.Path,StringComparer.Ordinal).Select(x=>new 공간자료Source(
            x.Path,Dataset(x.Path),x.Kind,File.ReadAllBytes(Physical(root,x.Path)),Area(x.Path,x.Kind))).ToList();

        void AddDirectory(string folder,string[] expected,string kind)
        {
            var actual=Directory.GetFiles(Path.Combine(root,folder),"*.json",SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
            if(!actual.SequenceEqual(expected.Order(StringComparer.Ordinal))) throw new InvalidDataException("SpatialApprovedFileSetChanged");
            paths.AddRange(expected.Select(x=>(folder+"/"+x,kind)));
        }
    }

    private static string Dataset(string path) => path==동별공간PackageCatalog.RegistryRelative
        ? "neighborhood-registry" : path.Contains("myeonmok",StringComparison.OrdinalIgnoreCase) || path.Contains("Sagajeong",StringComparison.Ordinal)
        ? "jungnang-myeonmok" : path.Contains("synthetic-neighborhood",StringComparison.Ordinal) || path.Contains("neighborhood-market",StringComparison.Ordinal)
        ? "synthetic-neighborhood" : path.Contains("hans",StringComparison.Ordinal) || path.Contains("forest-edge",StringComparison.Ordinal)
        ? "hans-farm" : path.Contains("farm-production",StringComparison.Ordinal) || path.Contains("farm-cultivation",StringComparison.Ordinal)
        ? "farm-production" : "northern-life-hub";

    private static string? Area(string path,string kind) => kind!="ObjectCatalog" &&
        (path.Contains("myeonmok",StringComparison.OrdinalIgnoreCase) || path.Contains("Sagajeong",StringComparison.Ordinal))
            ? 동별공간PackageCatalog.MyeonmokArea : null;

    private static string Physical(string root,string sourcePath) => sourcePath.StartsWith("unity:",StringComparison.Ordinal)
        ? Path.Combine(UnityRoot,sourcePath[6..]) : Path.Combine(root,sourcePath);

    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result,string? areaStableId=null)
    {
        if(mode is "serve" or "http-verify") { await 공간자료Catalog검증Host.RunAsync(root,mode=="serve");return; }
        if(mode is not ("preview" or "apply" or "verify")) throw new InvalidDataException("SpatialImportModeInvalid");
        if(areaStableId is not null && areaStableId is not (동별공간PackageCatalog.MyeonmokArea or 동별공간PackageCatalog.JunghwaArea))
            throw new InvalidDataException("SpatialAreaStableIdUnsupported");
        var physicalSources=ReadInputs(root);
        var legacyProjectionSource=면목동GameObjectCatalog.BuildProjection(root,physicalSources);
        var neighborhoods=동별공간PackageCatalog.Build(root,physicalSources,legacyProjectionSource);
        var sources=physicalSources.Where(x=>x.Kind!="ObjectCatalog").Concat(neighborhoods.Sources)
            .Where(x=>areaStableId is null || x.AreaStableId==areaStableId ||
                x.Kind=="NeighborhoodRegistry" || x.SourcePath==동별공간PackageCatalog.CommonCatalogSourcePath)
            .OrderBy(x=>x.SourcePath,StringComparer.Ordinal).ToList();
        var batch=공간자료SnapshotBuilder.Build(sources);
        var folder=Path.Combine(root,ArtifactRelative); EnsureNoReparse(folder); Directory.CreateDirectory(folder);
        var directFingerprints=physicalSources.Concat(neighborhoods.Sources).Select(x=>new { x.SourcePath,x.Dataset,x.Kind,x.AreaStableId,bytes=x.Bytes.LongLength,
            sha256=공간자료Json.Hash(x.Bytes),mtimeUtcTicks=x.SourcePath.StartsWith("generated:",StringComparison.Ordinal)
                ? 0L : File.GetLastWriteTimeUtc(Physical(root,x.SourcePath)).Ticks }).ToArray();
        var directPaths=directFingerprints.Select(x=>x.SourcePath).ToHashSet(StringComparer.Ordinal);
        var rawFingerprints=neighborhoods.RawInputFingerprints.Where(x=>!directPaths.Contains(x.SourcePath)).Select(x=>new
        {
            x.SourcePath, Dataset="neighborhood-source-input", Kind="RawSourceInput",
            AreaStableId=(string?)x.AreaStableId, bytes=x.Bytes, sha256=x.Sha256, mtimeUtcTicks=x.MtimeUtcTicks
        });
        var fingerprints=directFingerprints.Concat(rawFingerprints).OrderBy(x=>x.SourcePath,StringComparer.Ordinal).ToArray();
        var frozen=JsonSerializer.SerializeToUtf8Bytes(fingerprints,new JsonSerializerOptions{WriteIndented=true});
        var areaSuffix=areaStableId is null ? "" : "-"+areaStableId.Split(':')[^1];
        var frozenPath=Path.Combine(folder,"inputs"+areaSuffix+".json");
        if(mode=="preview") SaveExact(frozenPath,frozen);
        else if(!File.Exists(frozenPath) || !File.ReadAllBytes(frozenPath).SequenceEqual(frozen)) throw new InvalidDataException("SpatialFrozenInputsChanged");
        result["mode"]=mode;result["target"]="hongdal-mongo-1 / ssalddel_dev";
        result["bundleId"]=batch.Snapshot["_id"].AsString;result["documents"]=batch.Documents.Count;
        result["elements"]=batch.Elements.Count;result["relations"]=batch.Relations.Count;
        result["byKind"]=batch.Documents.GroupBy(x=>x["kind"].AsString).ToDictionary(x=>x.Key,x=>x.Count());
        result["byElementKind"]=batch.Elements.GroupBy(x=>x["kind"].AsString).ToDictionary(x=>x.Key,x=>x.Count());
        result["manifestHash"]=batch.Snapshot["manifestHash"].AsString;
        result["objectCandidates"]=batch.Elements.Count(x=>x["kind"]=="ObjectCandidateBinding");
        result["workflowObjectArchetypes"]=batch.Elements.Count(x=>x["kind"]=="ObjectArchetype");
        result["areaStableId"]=areaStableId;result["areaStableIds"]=batch.Snapshot["areaStableIds"].AsBsonArray.Select(x=>x.AsString).ToArray();
        result["neighborhoodPackageRegistrations"]=batch.Elements.Count(x=>x["kind"]=="NeighborhoodPackageRegistration");
        result["neighborhoodPackageDocuments"]=batch.Documents.Count(x=>x["kind"]=="NeighborhoodPackage");
        result["junghwaShopObservations"]=neighborhoods.JunghwaShopRows;
        result["junghwaFacilityObservations"]=neighborhoods.JunghwaFacilityRows;
        result["junghwaCoordinateObservations"]=neighborhoods.JunghwaCoordinateRows;
        if(mode!="preview")
        {
            var connection=await ConnectAsync(root);
            var baseline=await OtherCollectionsAsync(connection.Client);
            var store=new Mongo공간자료CatalogStore(connection.Client,Options.Create(connection.Options));
            if(mode=="apply")
            {
                result["databaseWriteAttempted"]=true;
                result["insertedRecords"]=await new 공간자료CatalogService(store).ImportAsync(batch,CancellationToken.None);
                result["committed"]=true;
            }
            // 별도 client/연결로 실제 저장값·native BSON·중복 수를 재조회한다.
            var independent=await ConnectAsync(root);
            var reader=new 공간자료CatalogService(new Mongo공간자료CatalogStore(independent.Client,Options.Create(independent.Options)));
            await reader.VerifyAsync(batch,true,CancellationToken.None);
            if(mode=="verify")
            {
                var resolutions=new SortedDictionary<string,int>(StringComparer.Ordinal);
                for(var skip=0;skip<batch.Relations.Count;skip+=500)
                {
                    var page=await reader.RelationsAsync(new(){BundleId=batch.Snapshot["_id"].AsString,Skip=skip,Take=500},CancellationToken.None);
                    foreach(var item in page.Items)
                    {var code=item.GetProperty("targetResolution").GetString()!;resolutions[code]=resolutions.GetValueOrDefault(code)+1;}
                }
                result["relationResolutionCounts"]=resolutions;
                result["resolutionIsApproval"]=false;
            }
            var after=await OtherCollectionsAsync(independent.Client);
            result["otherMongoCollectionCountsUnchanged"]=JsonSerializer.Serialize(baseline)==JsonSerializer.Serialize(after);
            result["otherMongoCollectionCounts"]=after;
            result["independentReadbackVerified"]=true;
            result["nativeBsonPayloads"]=batch.Documents.Count;
        }
        var unchanged=fingerprints.Where(x=>!x.SourcePath.StartsWith("generated:",StringComparison.Ordinal)).All(x=>
            File.GetLastWriteTimeUtc(Physical(root,x.SourcePath)).Ticks==x.mtimeUtcTicks &&
            공간자료Json.Hash(File.ReadAllBytes(Physical(root,x.SourcePath)))==x.sha256);
        if(!unchanged) throw new InvalidDataException("SpatialOriginalChangedDuringImport");
        동별공간PackageCatalog.VerifyRawInputs(root);
        result["sourceFilesHashMtimeUnchanged"]=true;result["mySqlWriteAttempted"]=false;
        result["gameStateConnected"]=false;result["distributionApproved"]=false;
        SaveExact(Path.Combine(folder,mode+areaSuffix+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffff")+".json"),JsonSerializer.SerializeToUtf8Bytes(result,new JsonSerializerOptions{WriteIndented=true}));
    }

    internal static async Task<(MongoClient Client,MongoDbOptions Options)> ConnectAsync(string root)
    {
        using var mongo=await InspectAsync("hongdal-mongo-1");using var app=await InspectAsync("hongdal-app-1");
        var container=mongo.RootElement[0]; var config=container.GetProperty("Config"); var labels=config.GetProperty("Labels");
        if(container.GetProperty("Name").GetString()!="/hongdal-mongo-1" || !container.GetProperty("State").GetProperty("Running").GetBoolean()
           || labels.GetProperty("com.docker.compose.project").GetString()!="hongdal"
           || labels.GetProperty("com.docker.compose.service").GetString()!="mongo"
           || !Path.GetFullPath(labels.GetProperty("com.docker.compose.project.working_dir").GetString()!).TrimEnd('\\','/').Equals(root,StringComparison.OrdinalIgnoreCase)
           || !container.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("27017/tcp").EnumerateArray().Any(x=>x.GetProperty("HostPort").GetString()=="27017"))
            throw new InvalidDataException("SpatialMongoTargetMismatch");
        static Dictionary<string,string> Env(JsonElement cfg)=>cfg.GetProperty("Env").EnumerateArray().Select(x=>x.GetString()!.Split('=',2)).ToDictionary(x=>x[0],x=>x[1]);
        var env=Env(config);var appEnv=Env(app.RootElement[0].GetProperty("Config"));
        if(appEnv.GetValueOrDefault("MongoDb__Database")!="ssalddel_dev") throw new InvalidDataException("SpatialMongoDatabaseMismatch");
        var settings=new MongoClientSettings
        {
            Server=new MongoServerAddress("127.0.0.1",27017),
            Credential=MongoCredential.CreateCredential("admin",env["MONGO_INITDB_ROOT_USERNAME"],env["MONGO_INITDB_ROOT_PASSWORD"]),
            ServerSelectionTimeout=TimeSpan.FromSeconds(10),ConnectTimeout=TimeSpan.FromSeconds(10),SocketTimeout=TimeSpan.FromSeconds(30)
        };
        var client=new MongoClient(settings);await client.GetDatabase("ssalddel_dev").RunCommandAsync<BsonDocument>(new BsonDocument("ping",1));
        return(client,new MongoDbOptions{Database="ssalddel_dev"});
    }

    private static async Task<JsonDocument> InspectAsync(string name)
    {
        var start=new ProcessStartInfo("docker"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        start.ArgumentList.Add("inspect");start.ArgumentList.Add(name);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));await process.WaitForExitAsync(deadline.Token);await error;
        if(process.ExitCode!=0)throw new InvalidDataException("SpatialDockerInspectionFailed");return JsonDocument.Parse(await output);
    }

    private static async Task<SortedDictionary<string,long>> OtherCollectionsAsync(MongoClient client)
    {
        var db=client.GetDatabase("ssalddel_dev");var names=await(await db.ListCollectionNamesAsync()).ToListAsync();
        var result=new SortedDictionary<string,long>(StringComparer.Ordinal);
        foreach(var name in names.Where(x=>!공간자료Collections.All.Contains(x) && !x.StartsWith("system.",StringComparison.Ordinal)))
            result[name]=await db.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        return result;
    }

    private static void EnsureNoReparse(string path)
    {
        for(var item=new DirectoryInfo(Path.GetFullPath(path));item is not null;item=item.Parent)
            if(item.Exists && item.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new InvalidDataException("SpatialArtifactReparseRejected");
    }
    internal static void SaveExact(string path,byte[] bytes)
    {
        if(File.Exists(path))
        {
            if(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) || !File.ReadAllBytes(path).SequenceEqual(bytes))throw new InvalidDataException("SpatialArtifactExistsDifferent");
            return;
        }
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);
    }
}
