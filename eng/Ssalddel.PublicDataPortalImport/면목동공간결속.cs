using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Domain.PublicData.Korea;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 공식 식별 결속과 주소/필지 후보를 구분하는 일회성 검토. Assignment/Unity/승인 상태는 쓰지 않는다.
internal static class 면목동공간결속
{
    internal const string Relative="artifacts/local/public-data/myeonmok-land-20260908-r1";
    internal const string Revision="myeonmok-land-review.r1";
    internal const string Region="region:kr:bjd:1126010100";
    internal const string Dataset="myeonmok-building-parcel-road-sample";
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal static readonly JsonSerializerOptions Compact=new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    internal static string S(JsonNode? n,string key)=>n?[key]?.ToString()??"";
    internal static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
    internal static string HashFile(string path){면목동사업체수집.Safe(path);using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllBytes(path))!;
    internal static string Address(string value)
    {
        if(value.StartsWith("중랑구 ",StringComparison.Ordinal))value="서울특별시 "+value;
        if(value.StartsWith("서울 중랑구 ",StringComparison.Ordinal))value="서울특별시"+value[2..];
        return 공개사업장주소정규화Engine.NormalizeRoadAddress(value)??"";
    }
    internal static async Task Save(string folder,string name,object value)
    {
        Require(Path.GetFileName(name)==name,"LandOutputName");var p=Path.Combine(folder,name);면목동사업체수집.Safe(p);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);Require(bytes.Length<=16*1024*1024,"LandJsonBudget");
        await using var f=new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None);await f.WriteAsync(bytes);
    }
    internal sealed record Input(string Path,string Sha256,long Bytes);
    internal sealed record Sample(string Id,string Group,string Name,string RoadAddress,string LotAddress,string BuildingManagementNumber,
        string LotKey,string LotBasis,string Longitude,string Latitude,string PriorResult,string[] OsmBuildingIds,string SelectionReason,
        string SourceId,string SourceHash,string EvidenceAsOf);
    internal sealed record Selection(string Revision,string ReviewAtUtc,Input[] Inputs,Sample[] Samples);
    internal sealed record Edge(string Status,string Method,string[] CandidateIds,string Reason);
    internal sealed record Reviewed(Sample Sample,Edge OfficialBuilding,Edge AddressBuilding,Edge GisBuilding,Edge BuildingParcel,Edge ParcelRoad,
        string[] GisIds,string[] RegisterPks,string SpatialStatus,object[] RestaurantComparisons);
    internal static Edge Decide(string method,string[] candidates,string missing,bool conflict=false)
    {
        var ids=candidates.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(conflict)return new("연결불가",method,ids,"IdentityConflict;NoFallback");
        return new(ids.Length==0?"연결불가":ids.Length>1?"복수후보":method=="OfficialIdentity"?"공식식별연결":method=="GeometryContains"?"공간단일후보":"주소단일후보",method,ids,ids.Length==0?missing:"CandidateOnly;NotVerifiedOccupancy");
    }
    private static string Lot(string text)
    {
        var m=Regex.Match(text,@"^(?:서울특별시 중랑구 면목동 )?(산\s*)?(\d{1,4})(?:-(\d{1,4}))?$");
        return m.Success?"1126010100"+(m.Groups[1].Success?"2":"1")+m.Groups[2].Value.PadLeft(4,'0')+(m.Groups[3].Success?m.Groups[3].Value:"0").PadLeft(4,'0'):"";
    }
    private static readonly (string Path,string Hash)[] FixedInputs=
    [
        ("artifacts/local/public-data/myeonmok-business-20260908-r1/selected.json","A1F9D1BCF1F04BD20C1285F9D2F0CF21A93B4151B27AE92D86852F343A1DC053"),
        ("artifacts/local/public-data/myeonmok-business-20260908-r1/business-readback.json","EC5F0D22859A3661411896E0EDC52B14B123D8FF533481B514FEDBC8140D5CF1"),
        ("artifacts/local/public-data/myeonmok-business-20260908-r1/connection.json","D109A0AF26608E9627551600B37FE8B22F473893D5F6FEA542E83E5D1FD176E2"),
        ("artifacts/local/public-data/myeonmok-address-20260908-r2/address-review-20260908T095925381.json",면목동주소공간연결.InputHash),
        ("artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip","674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755"),
        ("artifacts/local/neighborhood-source-acquisition/국가중점데이터_컬럼정의서(26.01.02)_배포용.xlsx","46DD29C6AB681C1E34CF00D91F8F2FE68B7E1868A853315EAA292838238ECB0F"),
        ("C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json",면목동주소공간연결.MapHash),
        ("artifacts/local/public-data/eight-life-domains/food-20260908-r1/daily-01.json","27E0F8197541E2523EA6666A3C572A1CBAF45E46C07ACB51C6C57CE871E79851"),
        ("artifacts/local/public-data/eight-life-domains/food-20260908-r1/daily-02.json","C8CEE2F0944ECF0056FF67B497FB85256794F0DDE8AE682950A72B7C54115D5A")
    ];
    private static Input[] CheckInputs(string root)=>FixedInputs.Select(x=>
    {
        var p=Path.GetFullPath(Path.Combine(root,x.Path));Require(HashFile(p).Equals(x.Hash,StringComparison.OrdinalIgnoreCase),"LandInputDrift:"+Path.GetFileName(p));
        return new Input(p,x.Hash,new FileInfo(p).Length);
    }).ToArray();
    private static Sample[] Select(string root)
    {
        var source=Read(Path.Combine(root,FixedInputs[0].Path));var readback=Read(Path.Combine(root,FixedInputs[1].Path));
        var links=Read(Path.Combine(root,FixedInputs[2].Path))["links"]!.AsArray().ToDictionary(x=>S(x!["item"],"id"),x=>x!);
        var fields=source["Rows"]!.AsArray().Where(x=>S(x,"Kind")=="shop").ToDictionary(x=>S(x!["Fields"],"상가업소번호"),x=>x!["Fields"]!);
        var candidates=new List<Sample>();
        foreach(var row in readback["rows"]!.AsArray())
        {
            var o=row!["observation"]!;var group=S(o,"Kind")=="factory"?"제조업":S(o,"Category");
            if(!new[]{"음식","소매","수리·개인","교육","제조업"}.Contains(group))continue;
            var id=S(row,"StableId");var l=links[id];var lot=Lot(S(o,"LotAddress"));var lotBasis="ExactLotAddressParsed;CandidateOnly";
            if(fields.TryGetValue(S(o,"ProviderId"),out var f))
            {
                var raw=S(f,"지번코드");Require(Regex.IsMatch(raw,@"^1126010100[12]\d{8}$")&&raw==lot,"SourceLotConflict");
                lot=raw;lotBasis="SourceLotCodeAndLotAddressAgree;NotBuildingIdentity";
            }
            candidates.Add(new(id,group,S(o,"Name"),S(o,"RoadAddress"),S(o,"LotAddress"),S(o,"BuildingManagementNumber"),lot,lot.Length>0?lotBasis:"MissingLotAddress",
                S(o,"Longitude"),S(o,"Latitude"),S(l,"result"),l["buildingIds"]!.AsArray().Select(x=>x!.ToString()).ToArray(),"",S(row,"SourceId"),S(o,"RawRowHash"),S(row,"EvidenceAsOfUtc")));
        }
        var prior=Read(Path.Combine(root,FixedInputs[3].Path));
        foreach(var row in prior["features"]!.AsArray().Where(x=>S(x!["observation"],"Kind")=="housing"))
        {
            var o=row!["observation"]!;var id=S(row,"StableId");var l=links[id];var lotText=S(o["Attributes"],"lotNumber");
            candidates.Add(new(id,"공동주택",S(o,"Name"),S(o,"RoadAddress"),lotText,"",Lot(lotText),"SourceHousingLotNumber;MyeonmokSourceContext;CandidateOnly","","",S(l,"result"),l["buildingIds"]!.AsArray().Select(x=>x!.ToString()).ToArray(),"",S(row,"SourceId"),S(row,"sourceHash"),S(row,"EvidenceAsOfUtc")));
        }
        var chosen=new List<Sample>();
        foreach(var group in new[]{"음식","소매","수리·개인","교육","제조업","공동주택"})
        {
            var pool=candidates.Where(x=>x.Group==group).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();Require(pool.Length>=5,"SampleGroupTooSmall:"+group);
            var local=new List<Sample>();
            foreach(var (status,count) in new[]{("UniqueAddressCandidateNotVerifiedOccupancy",1),("MultipleBuildingsForAddress",1),("NoExactBuildingInCurrentMap",3)})
                foreach(var x in pool.Where(x=>x.PriorResult==status).Take(count))local.Add(x with {SelectionReason="FrozenQuota:"+status+";StableIdOrdinal"});
            foreach(var x in pool.Where(x=>local.All(y=>y.Id!=x.Id)).Take(5-local.Count))local.Add(x with{SelectionReason="UnavailableQuotaFallbackSameGroup;StableIdOrdinal"});
            chosen.AddRange(local);
        }
        Require(chosen.Count==30&&chosen.Select(x=>x.Id).Distinct().Count()==30,"SampleCountOrIdentity");return chosen.ToArray();
    }
    internal static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "prepare" or "inventory" or "acquire" or "acquire-resume" or "review" or "replay" or "apply" or "verify" or "self-test","LandMode");
        if(mode=="self-test"){result["selfTestsPassed"]=SelfTest();return;}
        var inputs=CheckInputs(root);var folder=Path.Combine(root,Relative);면목동사업체수집.Safe(Path.Combine(folder,"selection.json"));
        if(mode=="inventory")
        {
            await using var db=new PublicDataIngestionDbContext(await 로컬공공자료Db.OptionsAsync(root));
            result["buildingSources"]=await db.BuildingRegisterTitles.AsNoTracking().GroupBy(x=>new{x.SigunguCode,x.SourceRevision}).Select(x=>new{x.Key,count=x.Count()}).ToListAsync();return;
        }
        if(mode=="prepare")
        {
            Require(!Directory.Exists(folder),"LandOutputAlreadyExists");var samples=Select(root);
            await using var db=new PublicDataIngestionDbContext(await 로컬공공자료Db.OptionsAsync(root));
            var before=await Baseline(db);Directory.CreateDirectory(folder);
            await Save(folder,"database-before.json",before);
            var now=DateTimeOffset.UtcNow;now=new(now.UtcTicks-now.UtcTicks%10,TimeSpan.Zero);
            await Save(folder,"selection.json",new Selection(Revision,now.ToString("O"),inputs,samples));
            result["samples"]=samples.GroupBy(x=>x.Group).ToDictionary(g=>g.Key,g=>g.Count());result["selectionHash"]=HashFile(Path.Combine(folder,"selection.json"));return;
        }
        var selection=JsonSerializer.Deserialize<Selection>(File.ReadAllBytes(Path.Combine(folder,"selection.json")),Json)!;
        Require(selection.Revision==Revision&&JsonSerializer.Serialize(selection.Samples,Compact)==JsonSerializer.Serialize(Select(root),Compact),"FrozenSelectionChanged");
        if(mode=="replay")
        {
            var again=면목동공간원천.ReadGis(root,selection.Samples.Select(x=>x.LotKey).Where(x=>x.Length>0).ToHashSet());
            Require(JsonNode.DeepEquals(Read(Path.Combine(folder,"gis-sample.json")),JsonSerializer.SerializeToNode(again,Json)),"GisReplayMismatch");
            result["frozenSelectionReproduced"]=true;result["gisReplayMatched"]=again.Rows.Length;
            result["geometryTopologyVerified"]=false;return;
        }
        if(mode is "acquire" or "acquire-resume"){await 면목동공간원천.Acquire(root,folder,selection,result,mode=="acquire-resume");return;}
        if(mode=="review")
        {
            await Review(root,folder,selection,result);return;
        }
        await Persist(root,folder,selection,mode,result);
    }
    private static async Task<object> Baseline(PublicDataIngestionDbContext db)
    {
        // 기존 자료를 수정하지 않았다는 증거. 새 연구 dataset만 제외하고 ID 순으로 전체 직렬 비교한다.
        var rows=await db.NormalizedRecords.AsNoTracking().Where(x=>x.DatasetId!=Dataset).OrderBy(x=>x.Id).ToListAsync();
        var assignments=await db.공개사업장건축물Assignments.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();
        var buildings=await db.BuildingRegisterTitles.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();
        var licensed=await db.공개인허가사업장Records.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();
        return new{normalizedCount=rows.Count,normalizedHash=Hash(JsonSerializer.SerializeToUtf8Bytes(rows,Compact)),assignmentCount=assignments.Count,assignmentHash=Hash(JsonSerializer.SerializeToUtf8Bytes(assignments,Compact)),buildingCount=buildings.Count,buildingHash=Hash(JsonSerializer.SerializeToUtf8Bytes(buildings,Compact)),licensedCount=licensed.Count,licensedHash=Hash(JsonSerializer.SerializeToUtf8Bytes(licensed,Compact))};
    }
    private static async Task Review(string root,string folder,Selection selection,Dictionary<string,object?> result)
    {
        var gis=면목동공간원천.ReadGis(root,selection.Samples.Select(x=>x.LotKey).Where(x=>x.Length>0).ToHashSet());
        var prior=Read(Path.Combine(root,FixedInputs[3].Path));var acquire=Read(Path.Combine(folder,"acquisition.json"));
        var reviewed=new List<Reviewed>();
        foreach(var s in selection.Samples)
        {
            var candidates=gis.Rows.Where(x=>x.Fields["A2"]==s.LotKey).ToArray();
            var rowIds=candidates.Select(x=>x.Fields["A1"]).ToArray();
            var api=acquire["buildingQueries"]!.AsArray().FirstOrDefault(x=>S(x,"lotKey")==s.LotKey);
            var registers=new List<JsonNode>();
            if(api!=null&&S(api,"status")=="Acquired")
            {
                var p=Path.Combine(folder,S(api,"file"));Require(HashFile(p)==S(api,"sha256"),"ApiResponseDrift");
                registers.AddRange(Read(p)["items"]!.AsArray().Select(x=>x!));
            }
            var exact=registers.Where(x=>Address(S(x,"newPlatPlc"))==Address(s.RoadAddress)&&Address(s.RoadAddress)!="").ToArray();
            var comparisons=new List<object>();
            if(s.Group=="음식")foreach(var r in prior["restaurants"]!.AsArray().Where(r=>Address(S(r,"RoadAddress"))==Address(s.RoadAddress)))
                comparisons.Add(new{id=S(r,"Id"),name=S(r,"BusinessName"),sameName=Name(S(r,"BusinessName"))==Name(s.Name),status=S(r,"BusinessStatusName"),detail=S(r,"DetailedStatusName"),source=S(r,"SourceId"),revision=S(r,"SourceRevision"),sourceHash=S(r,"SourceHashSha256"),relation="AddressComparisonOnly;NoMerge;NotCurrentOperation"});
            var gisEdge=Decide("ExactLotAddressToGisA2",rowIds,"NoGisForSourceLotOrLotMissing");
            var parcelIds=candidates.Select(x=>x.Fields["A2"]).Distinct().ToArray();
            reviewed.Add(new(s,new("연결불가","BuildingManagementNumber","".Split(',',StringSplitOptions.RemoveEmptyEntries),"OfficialAddressBuildingBridgeNotAcquired;NotOsmId"),
                Decide("ExactRoadAddressToRegister",exact.Select(x=>S(x,"mgmBldrgstPk")).Where(x=>x.Length>0).ToArray(),api==null?"LotQueryUnavailable":S(api,"status")),gisEdge,
                parcelIds.Length>0?new("공식식별연결","GisA2SourceParcelReference",parcelIds,"SourceAttributeOnly;ParcelGeometryAndAllAttachedLotsNotAcquired"):new("연결불가","GisA2SourceParcelReference",[],"BuildingCandidateUnavailable"),
                new("연결불가","ParcelToRoad",[],"OfficialParcelAndRoadGeometryNotAcquired;NoNearestSnap"),rowIds,registers.Select(x=>S(x,"mgmBldrgstPk")).ToArray(),"NotTested:BusinessCoordinateCrsUnconfirmed;GisRightsAndEncodingPending",comparisons.ToArray()));
        }
        await Save(folder,"gis-sample.json",gis);
        var all=Read(Path.Combine(root,FixedInputs[1].Path))["rows"]!.AsArray();var links=Read(Path.Combine(root,FixedInputs[2].Path))["links"]!.AsArray().ToDictionary(x=>S(x!["item"],"id"),x=>x!);
        var unmatched=all.Where(x=>S(links[S(x,"StableId")],"result")=="NoExactBuildingInCurrentMap").ToArray();
        // 좌표계 미확인이므로 범위 밖을 확정하지 않고 원인별 진단 보류를 명시한다.
        var coverage=new{unmatched=unmatched.Length,confirmedOutsideCoverage=0,confirmedInsideMissingAddress=0,confirmedMissingGeometry=0,undetermined=unmatched.Length,
            reason="SourceCrsUnconfirmed;CurrentSquareIsNotMyeonmokBoundary;NoForcedSpatialClassification",mapSha256=면목동주소공간연결.MapHash,
            coordinatePresent=unmatched.Count(x=>S(x!["observation"],"Longitude")!=""&&S(x!["observation"],"Latitude")!="")};
        await Save(folder,"coverage-review.json",coverage);
        var prices=FixedInputs.Skip(7).Select(x=>new{path=x.Path,sha256=x.Hash,items=Read(Path.Combine(root,x.Path))["data"]!["item"]!.AsArray().Where(x=>S(x,"item_code")=="152").Select(x=>x!.DeepClone()).ToArray(),
            region="ExistingRequestAllRegions;NotSeoulSpecific",market="NoMarketFieldInTheseItems",status="UnitReviewRequired;NoNumericOrPriceApplication"}).ToArray();
        await Save(folder,"price-review.json",prices);
        await Save(folder,"review.json",new{schema=Revision,selectionHash=HashFile(Path.Combine(folder,"selection.json")),acquisitionHash=HashFile(Path.Combine(folder,"acquisition.json")),gisHash=HashFile(Path.Combine(folder,"gis-sample.json")),privateReviewOnly=true,gameStateConnected=false,rows=reviewed});
        await Save(folder,"review-manifest.json",Directory.GetFiles(folder).Order(StringComparer.Ordinal).Select(p=>new Input(Path.GetFileName(p),HashFile(p),new FileInfo(p).Length)).ToArray());
        result["samples"]=reviewed.Count;result["gisCandidateCounts"]=reviewed.GroupBy(x=>x.GisBuilding.Status).ToDictionary(x=>x.Key,x=>x.Count());result["reviewHash"]=HashFile(Path.Combine(folder,"review.json"));
    }
    private static string Name(string name)=>Regex.Replace(name.Normalize(NormalizationForm.FormKC),@"\s+","");
    private static async Task Persist(string root,string folder,Selection selection,string mode,Dictionary<string,object?> result)
    {
        Require(HashFile(Path.Combine(folder,"selection.json"))=="F225D525D6D779127426542AF3D820165BE14E1ED235EBDED8CBFD3B9813FD0C","SelectionFingerprintChanged");
        Require(HashFile(Path.Combine(folder,"review.json"))=="72A70F9614F932AEDD72E2B178E2217FAFBE7F21D6CDE3C4D2311E288DCBF543","ReviewFingerprintChanged");
        Require(HashFile(Path.Combine(folder,"review-manifest.json"))=="410274E981E6B6CA21394DF7E7D0AAB080072C7CFBBF680DD7809E987841F49C","ReviewManifestFingerprintChanged");
        using var heldInputs=new HeldInputs(FixedInputs.Select(x=>Path.GetFullPath(Path.Combine(root,x.Path)))
            .Concat(Directory.GetFiles(folder,"*.json")));
        foreach(var input in JsonSerializer.Deserialize<Input[]>(File.ReadAllBytes(Path.Combine(folder,"review-manifest.json")),Json)!)
        {Require(Path.GetFileName(input.Path)==input.Path,"ManifestPath");var p=Path.Combine(folder,input.Path);Require(HashFile(p)==input.Sha256&&new FileInfo(p).Length==input.Bytes,"LandReviewDrift");}
        var path=Path.Combine(folder,"review.json");var review=Read(path);var hash=HashFile(path).ToLowerInvariant();var time=DateTimeOffset.Parse(selection.ReviewAtUtc,CultureInfo.InvariantCulture);
        var rows=review["rows"]!.AsArray().Select(r=>
        {
            var sample=r!["Sample"]!;var id=S(sample,"Id");var detailHash=Hash(JsonSerializer.SerializeToUtf8Bytes(r,Compact));
            var text=JsonSerializer.Serialize(new{sampleId=id,group=S(sample,"Group"),name=S(sample,"Name"),address=S(sample,"RoadAddress"),officialBuilding=r["OfficialBuilding"]!["Status"]!.ToString(),addressBuilding=r["AddressBuilding"]!["Status"]!.ToString(),gisBuilding=r["GisBuilding"]!["Status"]!.ToString(),gisCount=r["GisIds"]!.AsArray().Count,parcel=r["BuildingParcel"]!["Status"]!.ToString(),parcelRoad=r["ParcelRoad"]!["Status"]!.ToString(),detailHash,detailPath=Relative+"/review.json",sourceHash=S(sample,"SourceHash"),sourceEvidenceDate=S(sample,"EvidenceAsOf"),spatial="NotTested",limitation="CandidateOnly;NoOccupancy;NoParcelGeometry;NoTraversal;NoRuntime"},Compact);Require(text.Length<=2000,"LandRecordBudget");
            var identity=Hash(Encoding.UTF8.GetBytes(id)).ToLowerInvariant();
            return new 외부데이터정규화Record{SourceId="myeonmok-spatial-review",DatasetId=Dataset,StableId="land-sample:"+identity,
                RecordKey=외부데이터RecordKey.Create("myeonmok-spatial-review",Dataset,Region,"link-review",time,"sample="+identity),RegionStableId=Region,MetricCode="link-review",TextValue=text,NumericValue=null,UnitCode="review-observation",EvidenceAsOfUtc=time,CollectedAtUtc=time,SourceVersion="sha256:"+hash,DataRevision=Revision,DimensionKey="sample="+identity,QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;DerivedEvidence;NoConfirmedAssignment;NoRuntime",SpatialPrecisionCode="unverified-candidate",TemporalPrecisionCode="review-instant;source-dates-in-evidence",FirstSeenAtUtc=time,LastSeenAtUtc=time};
        }).ToArray();Require(rows.Length==30,"LandStoredCount");var keys=rows.Select(x=>x.RecordKey).ToList();
        var options=await 로컬공공자료Db.OptionsAsync(root);
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            Require(JsonNode.DeepEquals(Read(Path.Combine(folder,"database-before.json")),JsonSerializer.SerializeToNode(await Baseline(db),Json)),"ExistingDatabaseDriftBefore");
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText="SELECT GET_LOCK('mirror:public-data:myeonmok-land',0)";Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"LandImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();var existing=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(existing.All(x=>rows.Any(y=>Same(x,y))),"LandExistingConflict");result["databaseWriteAttempted"]=true;
                var registration=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(path,new("myeonmok-spatial-review",Dataset,"sha256:"+hash,Revision,time,"application/json",Relative+"/review.json"));
                foreach(var row in rows)row.RawSnapshotId=registration.RawSnapshotId;
                var stored=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);Require(stored.UpdatedCount==0,"LandUnexpectedUpdate");
                if(registration.Inserted){var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registration.RawSnapshotId);var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);run.StatusCode=외부데이터수집StatusCodes.Partial;run.NormalizedCount=30;run.InsertedCount=stored.InsertedCount;run.ErrorCode="PendingHumanReview";run.ErrorSummary="Derived sample evidence; official bridge/parcel/road access incomplete; no automatic human review or Reality Context approval.";await db.SaveChangesAsync();}
                Require(JsonNode.DeepEquals(Read(Path.Combine(folder,"database-before.json")),JsonSerializer.SerializeToNode(await Baseline(db),Json)),"ExistingDatabaseDriftBeforeCommit");
                CheckInputs(root);
                await transaction.CommitAsync();result["committed"]=true;result["inserted"]=stored.InsertedCount;result["existing"]=stored.ExistingCount;
                command.CommandText="SELECT RELEASE_LOCK('mirror:public-data:myeonmok-land')";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"LandLockReleaseFailed");
            }
        }
        await using var verification=new PublicDataIngestionDbContext(options);var actual=await verification.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        Require(actual.Count==30&&actual.All(x=>rows.Any(y=>Same(x,y))&&x.RawSnapshot!.ContentHashSha256==hash),"LandReadbackMismatch");
        var after=await Baseline(verification);Require(JsonNode.DeepEquals(Read(Path.Combine(folder,"database-before.json")),JsonSerializer.SerializeToNode(after,Json)),"ExistingDatabaseDriftAfter");
        CheckInputs(root);result["verifiedRows"]=30;result["existingDatabaseUnchanged"]=true;result["target"]="hongdal-mysql-1 / hongdal_dev";
        await Save(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json",new{result,after,rows=actual.Select(x=>new{x.Id,x.StableId,x.RecordKey,x.QualityCode,x.RawSnapshotId,x.SourceVersion,x.TextValue})});
    }
    private sealed class HeldInputs:IDisposable
    {
        private readonly List<FileStream> files=[];
        internal HeldInputs(IEnumerable<string> paths)
        {
            try { foreach(var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))files.Add(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)); }
            catch { Dispose();throw; }
        }
        public void Dispose(){foreach(var file in files)file.Dispose();}
    }
    private static bool Same(외부데이터정규화Record a,외부데이터정규화Record b)=>a.RecordKey==b.RecordKey&&a.StableId==b.StableId&&a.TextValue==b.TextValue&&a.SourceId==b.SourceId&&a.DatasetId==b.DatasetId&&a.SourceVersion==b.SourceVersion&&a.DataRevision==b.DataRevision&&a.QualityCode==b.QualityCode&&a.NumericValue==null&&a.UnitCode==b.UnitCode&&a.RegionStableId==b.RegionStableId&&a.EvidenceAsOfUtc==b.EvidenceAsOfUtc&&a.CollectedAtUtc==b.CollectedAtUtc&&a.LimitationCode==b.LimitationCode&&a.SpatialPrecisionCode==b.SpatialPrecisionCode&&a.TemporalPrecisionCode==b.TemporalPrecisionCode&&a.MetricCode==b.MetricCode&&a.DimensionKey==b.DimensionKey&&a.FirstSeenAtUtc==b.FirstSeenAtUtc;
    private static int SelfTest()
    {
        int count=0;void Check(bool ok){Require(ok,"LandSelfTest:"+(count+1));count++;}
        Check(Lot("서울특별시 중랑구 면목동 68-2")=="1126010100100680002");Check(Lot("산 1-3")=="1126010100200010003");Check(Lot("상봉동 1")=="");Check(Lot("0-1 junk")=="");
        Check(Address("중랑구 면목로 1 (면목동)")==Address("서울특별시 중랑구 면목로 1"));
        Check(Decide("OfficialIdentity",["a"],"").Status=="공식식별연결");Check(Decide("OfficialIdentity",["a"],"",true).Status=="연결불가");
        Check(Decide("ExactAddress",["a","b"],"").Status=="복수후보");Check(Decide("ExactAddress",["a"],"").Status=="주소단일후보");Check(Decide("GeometryContains",["a"],"").Status=="공간단일후보");Check(Decide("ExactAddress",[],"Missing").Reason=="Missing");Check(Decide("ExactAddress",["b","a"],"").CandidateIds.SequenceEqual(new[]{"a","b"}));
        Check(JsonSerializer.Serialize(Decide("ExactAddress",["a"],""))==JsonSerializer.Serialize(Decide("ExactAddress",["a"],"")));
        var input=new[]{"b","a"};var before=string.Join(',',input);Decide("ExactAddress",input,"");Check(string.Join(',',input)==before);
        count+=면목동공간원천.SelfTest();return count;
    }
}
