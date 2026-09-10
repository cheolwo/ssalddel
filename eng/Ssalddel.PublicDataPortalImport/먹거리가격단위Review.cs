using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Agriculture;
using 살뜰.Services.External.PublicData.Korea;

internal static class 먹거리가격단위Review
{
    const string Revision="potato-unit-comparison-20260908.r1";
    static readonly Dictionary<string,string> Hashes=new() {
        ["daily-01-N.json"]="87FFF771347B83F20E236ECC7DAAE388077F7705802F27359DA395A3D5DBD7ED",
        ["daily-01-Y.json"]="27E0F8197541E2523EA6666A3C572A1CBAF45E46C07ACB51C6C57CE871E79851",
        ["daily-02-N.json"]="8275DD8FAC92C54DEDED84856A33A00946D305B96E34B077E5C1AD0331F38AF5",
        ["daily-02-Y.json"]="C8CEE2F0944ECF0056FF67B497FB85256794F0DDE8AE682950A72B7C54115D5A",
        ["acquisition-02-Y.json"]="B08747A4207E2E2F48DE11E87A741BDA3C65A507CFC32A055D5E1C992070BE66"
    };
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "self-test" or "apply" or "verify" or "review","UnitModeInvalid");
        var folder=Path.Combine(root,먹거리가격표본Acquisition.UnitRelative);
        foreach(var pair in Hashes) Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,pair.Key))))==pair.Value,"UnitInputHashChanged");
        using var receipt=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"acquisition-02-Y.json")));
        var collected=receipt.RootElement.GetProperty("files").EnumerateArray().Max(x=>x.GetProperty("collectedAtUtc").GetDateTimeOffset());
        var observations=new List<외부데이터정규화Record>(); var reviews=new List<object>();
        foreach(var cls in new[]{"01","02"})
        {
            var n=File.ReadAllText(Path.Combine(folder,$"daily-{cls}-N.json"));
            var y=File.ReadAllText(Path.Combine(folder,$"daily-{cls}-Y.json"));
            // 기존 Y 표본/원천 상품 계약과 정확 대조. 이전 원자료·DB 레코드는 수정하지 않는다.
            Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,먹거리가격표본Acquisition.Relative,$"daily-{cls}.json"))))==Hashes[$"daily-{cls}-Y.json"],"PriorYDrift");
            var prior=먹거리가격표본Parser.Parse(y,cls,collected);
            Require(prior.Count==2,"UnitSelectionCount");
            var pairs=Compare(n,y,cls);
            foreach(var pair in pairs)
            {
                var parent=prior.Single(x=>x.DimensionKey.Contains("rank="+pair.rank+";",StringComparison.Ordinal));
                var text=JsonSerializer.Serialize(new { parentRecordKey=parent.RecordKey, sourceN=Hashes[$"daily-{cls}-N.json"], sourceY=Hashes[$"daily-{cls}-Y.json"], pair });
                Require(text.Length<=2000,"UnitRecordTooLarge");
                var row=new 외부데이터정규화Record {
                    SourceId=FarmRealityDataSourceIds.Kamis,DatasetId=FarmRealityDataSourceIds.KamisPriceObservations,
                    StableId=parent.StableId.Replace("kg-request","unit-review"),RegionStableId=parent.RegionStableId,
                    MetricCode="food-price-unit-comparison",DimensionKey=parent.DimensionKey,TextValue=text,
                    NumericValue=null,UnitCode="comparison-evidence",EvidenceAsOfUtc=parent.EvidenceAsOfUtc,CollectedAtUtc=collected,
                    SpatialPrecisionCode=parent.SpatialPrecisionCode,TemporalPrecisionCode="date-only",
                    QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;NoPublication;NoRuntime;MixedComparisonUnitsObserved;NoPriceCorrection",
                    SourceVersion="2026-09-07;N-Y-comparison",DataRevision=Revision,FirstSeenAtUtc=collected,LastSeenAtUtc=collected
                };
                row.RecordKey=외부데이터RecordKey.Create(row.SourceId,row.DatasetId,row.RegionStableId,row.MetricCode,row.EvidenceAsOfUtc,row.DimensionKey);
                observations.Add(row);reviews.Add(pair);
            }
        }
        Require(observations.Count==4 && observations.Select(x=>x.RecordKey).Distinct().Count()==4,"UnitIdentityCollision");
        result["comparisons"]=reviews;result["parentRecordsModified"]=false;
        if(mode=="self-test") { result["selfTestsPassed"]=SelfTest(folder);return; }
        if(mode=="review")return;
        var options=await 로컬공공자료Db.OptionsAsync(root); var keys=observations.Select(x=>x.RecordKey).ToList();
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:potato-20260907',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"UnitImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                var existing=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(existing.All(x=>observations.Any(y=>Same(x,y))),"UnitExistingConflict");
                result["databaseWriteAttempted"]=true;
                var rawIds=new Dictionary<string,long>();
                foreach(var cls in new[]{"01","02"}) foreach(var conversion in new[]{"N","Y"})
                {
                    var name=$"daily-{cls}-{conversion}.json";
                    var source=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,name),new 공공공간원본등록Request(
                        FarmRealityDataSourceIds.Kamis,FarmRealityDataSourceIds.KamisPriceObservations,$"survey-date:2026-09-07;convert-kg:{conversion}",Revision,
                        observations[0].EvidenceAsOfUtc,"application/json",먹거리가격표본Acquisition.UnitRelative+"/"+name));
                    rawIds[name]=source.RawSnapshotId;
                    if(source.Inserted){
                        var snapshot=await db.RawSnapshots.SingleAsync(x=>x.Id==source.RawSnapshotId);
                        snapshot.CollectedAtUtc=receipt.RootElement.GetProperty("files").EnumerateArray().Single(x=>x.GetProperty("filename").GetString()==name).GetProperty("collectedAtUtc").GetDateTimeOffset();
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==snapshot.FirstCollectionRunId);
                        run.StatusCode=외부데이터수집StatusCodes.Partial;run.FetchedCount=cls=="01"?13:21;
                        run.NormalizedCount=2;run.InsertedCount=2;run.ErrorCode="PendingHumanReview";
                        run.ErrorSummary="N/Y comparison evidence only; no parent price update; mixed historical units. Receipt: "+먹거리가격표본Acquisition.UnitRelative+"/acquisition-02-Y.json";
                        await db.SaveChangesAsync();
                    }
                }
                foreach(var row in observations) row.RawSnapshotId=rawIds[row.DimensionKey.StartsWith("class=01;",StringComparison.Ordinal)?"daily-01-N.json":"daily-02-N.json"];
                var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(observations);
                Require(saved.UpdatedCount==0,"UnitUnexpectedUpdate");
                await transaction.CommitAsync();result["committed"]=true;result["inserted"]=saved.InsertedCount;result["existing"]=saved.ExistingCount;
            }
        }
        await using var readback=new PublicDataIngestionDbContext(options);
        var rows=await readback.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        Require(rows.Count==4 && rows.All(x=>observations.Any(y=>Same(x,y)) && x.RawSnapshot?.ContentHashSha256==Hashes[x.DimensionKey.StartsWith("class=01;",StringComparison.Ordinal)?"daily-01-N.json":"daily-02-N.json"].ToLowerInvariant()),"UnitReadbackMismatch");
        result["verifiedRows"]=rows.Count; result["ids"]=rows.Select(x=>new{x.Id,x.RawSnapshotId,x.QualityCode});
    }
    internal static List<Pair> Compare(string nJson,string yJson,string cls)
    {
        Require(cls is "01" or "02","UnitClassInvalid");
        using var nDoc=JsonDocument.Parse(nJson);using var yDoc=JsonDocument.Parse(yJson);
        static Dictionary<string,JsonElement> Select(JsonDocument d){
            Require(d.RootElement.GetProperty("data").GetProperty("error_code").GetString()=="000","UnitResponseFailed");
            var rows=d.RootElement.GetProperty("data").GetProperty("item").EnumerateArray().Where(x=>x.GetProperty("item_code").GetString()=="152").ToArray();
            Require(rows.Length==2 && rows.Select(x=>x.GetProperty("rank_code").GetString()).Distinct().Count()==2,"UnitDuplicateOrMissing");
            return rows.ToDictionary(x=>x.GetProperty("rank_code").GetString()!);
        }
        var ns=Select(nDoc);var ys=Select(yDoc); var result=new List<Pair>();
        foreach(var rank in new[]{"04","05"}){
            Require(ns.ContainsKey(rank)&&ys.ContainsKey(rank),"UnitGradeMissing");var n=ns[rank];var y=ys[rank];
            string V(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
            var unit=cls=="01"?"100g":"20kg";var factor=cls=="01"?10m:0.05m;
            foreach(var field in new[]{"item_name","item_code","kind_code","rank","rank_code","unit"})Require(V(n,field)==V(y,field),"UnitIdentityMismatch");
            Require(V(n,"item_name")=="감자" && V(n,"kind_code")=="01" && V(n,"unit")==unit
                && V(n,"kind_name")==$"수미(노지)({unit})" && V(y,"kind_name")=="수미(노지)(1kg)" && V(y,"day1")=="당일 (09/07)","UnitProductOrDateMismatch");
            var slots=new List<Slot>();
            for(var i=1;i<=7;i++){
                var label=V(n,"day"+i);Require(label==V(y,"day"+i),"UnitDayMismatch");
                var nr=V(n,"dpr"+i);var yr=V(y,"dpr"+i);var a=Number(nr);var b=Number(yr);
                string status;decimal? scaled=null,delta=null;
                if(a==null || b==null)status=a==null&&b==null?"MissingBoth":"MissingOne";
                else {
                    scaled=a*factor;delta=b-scaled;
                    // 양쪽 정수 표시가 각각 최대0.5 반올림됐다는 가설의 정합 범위. 가격 보정·품질 합격 기준이 아니다.
                    status=delta==0?"ExactScaled":a==b?"UnchangedNonKg":Math.Abs(delta.Value)<=(factor+1m)/2m?"RoundingCompatible":"Inconsistent";
                }
                slots.Add(new(i,label,nr,yr,scaled,delta,status));
            }
            result.Add(new(cls,rank,unit,factor,slots));
        }
        return result;
    }
    static decimal? Number(string raw){if(raw=="-")return null;Require(decimal.TryParse(raw,NumberStyles.AllowThousands|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)&&n>=0,"UnitNumericInvalid");return n;}
    static bool Same(외부데이터정규화Record a,외부데이터정규화Record b)=>a.RecordKey==b.RecordKey&&a.TextValue==b.TextValue&&a.NumericValue==null&&a.QualityCode==b.QualityCode&&a.DataRevision==b.DataRevision&&a.UnitCode==b.UnitCode&&a.LimitationCode==b.LimitationCode;
    static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    public sealed record Slot(int index,string label,string n,string y,decimal? scaled,decimal? delta,string status);
    public sealed record Pair(string cls,string rank,string sourceUnit,decimal factor,List<Slot> slots);
    static int SelfTest(string folder){
        var n=File.ReadAllText(Path.Combine(folder,"daily-01-N.json"));var y=File.ReadAllText(Path.Combine(folder,"daily-01-Y.json"));
        var pairs=Compare(n,y,"01");
        Require(pairs.Count==2&&pairs[0].slots[0].status=="RoundingCompatible","UnitPositiveTest");
        Require(pairs.SelectMany(x=>x.slots).Count(x=>x.status=="UnchangedNonKg")==6,"UnitHistoricalTest");
        Require(pairs.All(x=>x.slots[1].status=="MissingBoth"),"UnitMissingTest");
        Require(JsonSerializer.Serialize(pairs)==JsonSerializer.Serialize(Compare(n,y,"01")),"UnitDeterministicTest");
        foreach(var bad in new[]{n.Replace("09/07","09/08"),n.Replace("100g","20kg"),n.Replace("\"319\"","\"NaN\""),n.Replace("감자","다른품목")}){
            var rejected=false;try{Compare(bad,y,"01");}catch(InvalidDataException){rejected=true;}Require(rejected,"UnitNegativeTest");
        }
        var inconsistent=Compare(n.Replace("\"319\"","\"999\""),y,"01");
        Require(inconsistent[0].slots[0].status=="Inconsistent","UnitInconsistentTest");
        var missing=Compare(n.Replace("\"319\"","\"-\""),y,"01");
        Require(missing[0].slots[0].status=="MissingOne","UnitMissingOneTest");
        Require(pairs[1].slots[0].status=="ExactScaled","UnitExactTest");
        var wholesale=Compare(File.ReadAllText(Path.Combine(folder,"daily-02-N.json")),File.ReadAllText(Path.Combine(folder,"daily-02-Y.json")),"02");
        Require(wholesale.SelectMany(x=>x.slots).Count(x=>x.status=="ExactScaled")==6,"WholesaleFactorTest");
        return 12;
    }
}
