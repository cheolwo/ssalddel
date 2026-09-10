using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using static Ssalddel.Simulation.Application.로컬생활시작자료;

// 기존 로컬 수집 도구의 명시적 하위 명령. 서버 실행/운영 DB/업무 상태 변경 없음.
internal static class 로컬생활자료Import
{
    private static void Require(bool ok,string code)=>로컬생활시작자료.Require(ok,code);
    private static readonly string[] SourcePaths = {
        "eng/world-seedbeds/placement-map-profiles/synthetic-neighborhood.v1.json",
        "eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json",
        "docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/offline-life-bootstrap.r1.md",
        "eng/execution-ledgers/work-orders/neighborhood-life-assign.e7-work-order.json" };
    public static async Task RunAsync(string mode,string root,string[] args,Dictionary<string,object?> result)
    {
        Require(File.Exists(Path.Combine(root,"AGENTS.md")),"RepositoryRootRequired");
        var folder=Path.Combine(root,"artifacts/local/offline-life-r1");
        if(mode=="codec-test") {
            Require(args.Length==2,"BundlePathAndHashRequired");var b=Read(args[0],args[1]);var bytes=JsonBytes(b);
            var round=PayloadBytes(new BsonDocument("payload",BsonDocument.Parse(Encoding.UTF8.GetString(bytes))));
            result["beforeHash"]=CanonicalHash(bytes);result["afterHash"]=CanonicalHash(round);
            var codecFolder=Path.Combine(folder,"codec",CanonicalHash(round));
            WriteOnce(Path.Combine(codecFolder,"before-canonical.json"),CanonicalBytes(bytes),MaxBundleBytes);
            WriteOnce(Path.Combine(codecFolder,"after-canonical.json"),CanonicalBytes(round),MaxBundleBytes);
            WriteOnce(Path.Combine(codecFolder,"before.json"),bytes,MaxBundleBytes);
            WriteOnce(Path.Combine(codecFolder,"after.json"),round,MaxBundleBytes);return;
        }
        if(mode=="prepare") {
            Require(args.Length<=2,"PrepareAcceptsCourierCountAndOptionalIdPrefix");
            var courierCount=3;
            if(args.Length>0)Require(int.TryParse(args[0],System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,out courierCount) && courierCount>=1 && courierCount<=4,"OfflineLifeCourierCapacityInvalid");
            var idPrefix=args.Length==2?args[1]:"";
            var sources=SourcePaths.Select(p=>new 로컬생활원천 {Path=p,Sha256=Hash(ReadBounded(Path.Combine(root,p),MaxBundleBytes))}).ToArray();
            using var map=JsonDocument.Parse(ReadBounded(Path.Combine(root,SourcePaths[0]),MaxBundleBytes));
            var bundle=Seal(new 로컬생활시작Payload {Sources=sources,PlacementMap=map.RootElement.Clone(),
                InitialRequest=로컬생활구성표본.Create(Guid.Parse("b39956c7-6e4a-4677-b33a-90d047a331db"),courierCount,idPrefix)});
            var path=Path.Combine(folder,"prepared",bundle.ContentSha256,"bundle.json");
            WriteOnce(path,JsonBytes(bundle),MaxBundleBytes);result["bundlePath"]=path;result["bundleHash"]=bundle.ContentSha256;return;
        }
        if(mode=="run") {
            Require(args.Length==2,"BundlePathAndHashRequired");
            var bundle=Read(args[0],args[1]);var request=Copy(bundle.Payload.InitialRequest);request.ClientRequestId=Guid.NewGuid();
            var core=new 경영SimulationSessionAggregate(request);
            var run=Path.Combine(folder,"runs",request.ClientRequestId.ToString("N"));
            var store=new Ssalddel.Simulation.Infrastructure.FileSimulationLocalSaveSlotStore(run);
            for(int i=0;i<1800;i++) core.Advance(new 경영SimulationTick진행Request {CommandId="offline:tick:"+i,ExpectedRevision=core.Snapshot().Revision,TickCount=1});
            var package=core.CreateSavePackage(new SimulationSessionSaveRequest {SaveStableId="offline:final",ExpectedRevision=core.Snapshot().Revision});
            store.Write("offline-life",package);
            var reread=new Ssalddel.Simulation.Infrastructure.FileSimulationLocalSaveSlotStore(run).Read("offline-life").Package;
            var restored=SimulationSessionReplay.Restore(reread);
            Require(restored.Snapshot().CurrentTick==1800,"OfflineLifeReplayIncomplete");
            var report=로컬생활보고.Create(reread,bundle);var path=Path.Combine(run,"report.json");
            WriteOnce(path,JsonBytes(report),MaxReportBytes);로컬생활보고.Validate(JsonSerializer.Deserialize<로컬생활보고>(ReadBounded(path,MaxReportBytes))!);
            result["reportPath"]=path;result["foodReceived"]=report.FoodReceived;result["martReceived"]=report.MartReceived;
            result["replenishmentReceived"]=report.ReplenishmentReceived;result["tick"]=report.Tick;result["offlineCoreReplayVerified"]=true;
            result["unityExecuted"]=false;return;
        }
        Require(mode is "apply" or "export" or "report-apply","UnknownLocalLifeMode");
        var connection=await 공간자료CatalogImport.ConnectAsync(root);
        var db=connection.Client.GetDatabase(connection.Options.Database);
        if(mode=="apply") {
            Require(args.Length==2,"BundlePathAndHashRequired");var bundle=Read(args[0],args[1]);
            foreach(var source in bundle.Payload.Sources) Require(Hash(ReadBounded(Path.Combine(root,source.Path),MaxBundleBytes))==source.Sha256,"OfflineLifeSourceDrift");
            await InsertOnce(db,"simulation_bootstrap_bundles",bundle.ContentSha256,JsonBytes(bundle),result);
            result["bundleHash"]=bundle.ContentSha256;
        } else if(mode=="export") {
            Require(args.Length==1 && IsHash(args[0]),"BundleHashRequired");
            var doc=await db.GetCollection<BsonDocument>("simulation_bootstrap_bundles").Find(new BsonDocument("_id",args[0])).SingleOrDefaultAsync();
            Require(doc!=null,"OfflineLifeMongoBundleMissing");
            Require(doc!["visibility"]=="Private" && doc["reviewState"]=="PendingHumanReview"
                && doc["authority"]=="LocalSimulationOnly" && doc["payloadHash"].AsString==CanonicalHash(PayloadBytes(doc)),"OfflineLifeMongoExportMetadataMismatch");
            var bytes=PayloadBytes(doc!);var bundle=JsonSerializer.Deserialize<로컬생활시작묶음>(bytes)!;Validate(bundle);
            Require(bundle.ContentSha256==args[0],"OfflineLifeMongoIdentityMismatch");
            var path=Path.Combine(folder,"export",args[0],"bundle.json");WriteOnce(path,bytes,MaxBundleBytes);Read(path,args[0]);
            result["exportPath"]=path;result["bundleHash"]=args[0];
        } else {
            Require(args.Length==1,"ReportPathRequired");
            var bytes=ReadBounded(args[0],MaxReportBytes);CanonicalHash(bytes);var report=JsonSerializer.Deserialize<로컬생활보고>(bytes)!;로컬생활보고.Validate(report);
            Require(CanonicalHash(bytes)==CanonicalHash(JsonBytes(report)),"OfflineLifeReportUnmappedOrMissingJsonFields");
            Require(await db.GetCollection<BsonDocument>("simulation_bootstrap_bundles").CountDocumentsAsync(new BsonDocument("_id",report.BundleSha256))==1,"OfflineLifeReportBundleMissing");
            await InsertOnce(db,"simulation_run_reports",report.ContentSha256,bytes,result);
            result["reportHash"]=report.ContentSha256;
        }
        // 독립 클라이언트로 원문 해시 재확인. 기존 주소/주문/확정 연결 원장은 호출하지 않는다.
        if(result.TryGetValue("storedCollection",out var collection)) {
            var other=await 공간자료CatalogImport.ConnectAsync(root);
            var reread=await other.Client.GetDatabase(other.Options.Database).GetCollection<BsonDocument>((string)collection!)
                .Find(new BsonDocument("_id",(string)result["storedId"]!)).SingleAsync();
            Require(CanonicalHash(PayloadBytes(reread))==(string)result["storedPayloadHash"]!,"OfflineLifeMongoIndependentReadMismatch");
            result["independentReadVerified"]=true;
        }
    }
    private static byte[] PayloadBytes(BsonDocument doc)
    {
        using var stream=new MemoryStream();
        using(var writer=new Utf8JsonWriter(stream)) WriteBson(doc["payload"],writer);
        return stream.ToArray();
    }
    // BSON Double의 G17 표기를 JSON의 decimal로 다시 해석하지 않는다.
    // 원래 double 값의 round-trip 표기를 쓰며, 반입 전 전체 JSON 의미 해시 일치를 요구한다.
    private static void WriteBson(BsonValue value,Utf8JsonWriter writer)
    {
        switch(value.BsonType) {
            case BsonType.Document: writer.WriteStartObject();foreach(var p in value.AsBsonDocument){writer.WritePropertyName(p.Name);WriteBson(p.Value,writer);}writer.WriteEndObject();break;
            case BsonType.Array: writer.WriteStartArray();foreach(var p in value.AsBsonArray)WriteBson(p,writer);writer.WriteEndArray();break;
            case BsonType.String: writer.WriteStringValue(value.AsString);break;
            case BsonType.Boolean: writer.WriteBooleanValue(value.AsBoolean);break;
            case BsonType.Int32: writer.WriteNumberValue(value.AsInt32);break;
            case BsonType.Int64: writer.WriteNumberValue(value.AsInt64);break;
            case BsonType.Double: writer.WriteNumberValue(value.AsDouble);break;
            case BsonType.Null: writer.WriteNullValue();break;
            default: throw new InvalidDataException("OfflineLifeBsonTypeNotApproved");
        }
    }
    private static async Task InsertOnce(IMongoDatabase db,string name,string id,byte[] bytes,Dictionary<string,object?> result)
    {
        var payloadHash=CanonicalHash(bytes);var rows=db.GetCollection<BsonDocument>(name);
        var record=new BsonDocument { {"_id",id},{"visibility","Private"},{"reviewState","PendingHumanReview"},
            {"authority","LocalSimulationOnly"},{"payloadHash",payloadHash},{"payload",BsonDocument.Parse(Encoding.UTF8.GetString(bytes))} };
        Require(record.ToBson().Length<=MaxReportBytes,"OfflineLifeBsonTooLarge");
        Require(CanonicalHash(PayloadBytes(record))==payloadHash,"OfflineLifeBsonRoundTripMismatchBeforeWrite");
        var existing=await rows.Find(new BsonDocument("_id",id)).SingleOrDefaultAsync();
        if(existing==null) {
            result["databaseWriteAttempted"]=true;
            try {await rows.InsertOneAsync(record);result["committed"]=true;result["inserted"]=1;}
            catch(MongoWriteException e) when(e.WriteError.Category==ServerErrorCategory.DuplicateKey) { }
            existing=await rows.Find(new BsonDocument("_id",id)).SingleAsync();
        } else result["inserted"]=0;
        Require(existing["payloadHash"].AsString==payloadHash && CanonicalHash(PayloadBytes(existing))==payloadHash
            && existing["visibility"]=="Private" && existing["reviewState"]=="PendingHumanReview"
            && existing["authority"]=="LocalSimulationOnly","OfflineLifeMongoConflict");
        result["storedCollection"]=name;result["storedId"]=id;result["storedPayloadHash"]=payloadHash;
    }
}
