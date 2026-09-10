using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Agriculture;
using 살뜰.Services.External.PublicData.Korea;

internal static class 먹거리가격표본Import
{
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify","FoodModeInvalid");
        var relative=먹거리가격표본Acquisition.Relative;
        var folder=Path.Combine(root,relative);
        var hashes=new Dictionary<string,string> {
            ["daily-01.json"]="27E0F8197541E2523EA6666A3C572A1CBAF45E46C07ACB51C6C57CE871E79851",
            ["daily-02.json"]="C8CEE2F0944ECF0056FF67B497FB85256794F0DDE8AE682950A72B7C54115D5A",
            ["acquisition-02.json"]="3B0CAF15D23567A7175F749F3732A6E550E1F1E17FEADF135D1D2C6D4ED01C2F"
        };
        foreach(var pair in hashes) Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,pair.Key))))==pair.Value,"FoodInputHashChanged");
        using var receipt=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"acquisition-02.json")));
        var batches=new List<Batch>();
        foreach(var cls in new[]{"01","02"})
        {
            var name="daily-"+cls+".json";
            var entry=receipt.RootElement.GetProperty("files").EnumerateArray().Single(x=>x.GetProperty("filename").GetString()==name);
            var collected=entry.GetProperty("collectedAtUtc").GetDateTimeOffset();
            var json=File.ReadAllText(Path.Combine(folder,name));
            using var raw=JsonDocument.Parse(json);
            var rows=먹거리가격표본Parser.Parse(json,cls,collected);
            Require(rows.Count==2 && raw.RootElement.GetProperty("data").GetProperty("item").GetArrayLength()==(cls=="01"?13:21),"FoodSelectionCountChanged");
            batches.Add(new(name,collected,cls=="01"?13:21,rows));
        }
        var all=batches.SelectMany(x=>x.Rows).ToList(); var keys=all.Select(x=>x.RecordKey).ToList();
        Require(keys.Distinct().Count()==4,"CrossClassCollision");
        if(mode=="self-test")
        {
            result["selfTestsPassed"]=먹거리가격표본Parser.SelfTest(File.ReadAllText(Path.Combine(folder,"daily-01.json")),batches[0].Collected)+1+먹거리가격표본Acquisition.SelfTest();
            result["selectedRows"]=4; return;
        }
        var options=await 로컬공공자료Db.OptionsAsync(root);
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:potato-20260907',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"FoodImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                var existing=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(existing.All(x=>all.Any(y=>Same(x,y))),"FoodExistingConflict");
                result["beforeCount"]=existing.Count; result["databaseWriteAttempted"]=true;
                var inserted=0; var unchanged=0;
                foreach(var batch in batches)
                {
                    var registered=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,batch.Name),
                        new 공공공간원본등록Request(FarmRealityDataSourceIds.Kamis,FarmRealityDataSourceIds.KamisPriceObservations,
                            먹거리가격표본Parser.Version,먹거리가격표본Parser.Revision,batch.Rows[0].EvidenceAsOfUtc,"application/json",relative+"/"+batch.Name));
                    foreach(var row in batch.Rows) row.RawSnapshotId=registered.RawSnapshotId;
                    var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch.Rows);
                    Require(saved.UpdatedCount==0,"FoodUnexpectedUpdate");
                    inserted+=saved.InsertedCount; unchanged+=saved.ExistingCount;
                    if(registered.Inserted)
                    {
                        var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registered.RawSnapshotId);
                        raw.CollectedAtUtc=batch.Collected;
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);
                        run.StatusCode=외부데이터수집StatusCodes.Partial; run.FetchedCount=batch.SourceRows;
                        run.NormalizedCount=2;run.InsertedCount=saved.InsertedCount;run.ErrorCode="PendingHumanReview";
                        run.ErrorSummary="Private potato subset; condition omitted for credential safety; unit review required; no numeric price/publication/runtime. Receipt: "+relative+"/acquisition-02.json";
                        await db.SaveChangesAsync();
                    }
                }
                await transaction.CommitAsync();
                result["committed"]=true;result["inserted"]=inserted;result["existing"]=unchanged;
            }
        }
        await using var readback=new PublicDataIngestionDbContext(options);
        var stored=await readback.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        if(mode!="preview") Require(stored.Count==4,"FoodReadbackCountMismatch");
        Require(stored.All(x=>all.Any(y=>Same(x,y)) && x.RawSnapshot!=null && batches.Any(b=>b.Rows.Any(y=>y.RecordKey==x.RecordKey)
            && x.RawSnapshot.ContentHashSha256==hashes[b.Name].ToLowerInvariant())),"FoodReadbackMismatch");
        result["verifiedRows"]=stored.Count;result["target"]="hongdal-mysql-1 / hongdal_dev";result["mode"]=mode;
        result["rows"]=stored.OrderBy(x=>x.Id).Select(x=>new{x.Id,x.RawSnapshotId,x.StableId,x.QualityCode,x.NumericValue,x.UnitCode,x.EvidenceAsOfUtc});
    }
    private static bool Same(외부데이터정규화Record x,외부데이터정규화Record y)=>
        x.RecordKey==y.RecordKey && x.StableId==y.StableId && x.SourceId==y.SourceId && x.DatasetId==y.DatasetId
        && x.TextValue==y.TextValue && x.NumericValue==null && x.UnitCode==y.UnitCode && x.QualityCode==y.QualityCode
        && x.LimitationCode==y.LimitationCode && x.SourceVersion==y.SourceVersion && x.DataRevision==y.DataRevision
        && x.DimensionKey==y.DimensionKey && x.RegionStableId==y.RegionStableId && x.MetricCode==y.MetricCode && x.EvidenceAsOfUtc==y.EvidenceAsOfUtc;
    private static void Require(bool condition,string code){if(!condition)throw new InvalidDataException(code);}
    private sealed record Batch(string Name,DateTimeOffset Collected,int SourceRows,List<외부데이터정규화Record> Rows);
}
