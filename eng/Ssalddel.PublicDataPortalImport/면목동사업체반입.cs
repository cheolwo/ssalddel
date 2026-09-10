using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Domain.PublicData.Korea;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;
using static 면목동사업체수집;

// 공급원 관측은 기존 비공개 원장에만 반입. 같은 주소/상호는 대응 후보이며 자동 병합하지 않는다.
internal static class 면목동사업체반입
{
    private static void Require(bool condition,string code)=>면목동사업체수집.Require(condition,code);
    private const string ReceiptHash="a15b270ab8ac12018f11471bf5f1c39b0e56d989639d91c6b1adc9958ee373c8";
    private const string PreparedHash="a1f9d1bcf1f04bd20c1285f9d2f0cf21a93b4151b27ae92d86852f343a1dc053";
    private const string Revision="myeonmok-business-20260908.r1";
    private const string Region="region:kr:bjd:1126010100";
    private const string SeoulEntry="소상공인시장진흥공단_상가(상권)정보_서울_202606.csv";
    private static readonly JsonSerializerOptions Compact=new(){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal sealed record Selected(string Kind,string SourceFile,Dictionary<string,string> Fields);
    internal sealed record Prepared(string Revision,int SeoulRows,int FactoryRows,string ZipEntry,List<Selected> Rows);
    internal sealed record Business(string Kind,string Name,string Branch,string RoadAddress,string LotAddress,string Industry,
        string Category,string Product,string ProviderId,string IdentityMethod,string StatusAsOfSource,string? Longitude,string? Latitude,
        string? BuildingManagementNumber,string? Floor,string? Room,string RawRowHash);
    private static DateTimeOffset DbTime(DateTimeOffset t)=>new(t.UtcTicks-t.UtcTicks%10,TimeSpan.Zero);
    private static string Canonical(Dictionary<string,string> row)=>JsonSerializer.Serialize(row.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value),Compact);
    private static string FileHash(string path){Safe(path);using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();}
    private static string Key(string address)
    {
        if(address.StartsWith("서울 중랑구 ",StringComparison.Ordinal))address="서울특별시"+address[2..];
        return 공개사업장주소정규화Engine.NormalizeRoadAddress(address)??"";
    }
    private static string NameKey(string name)=>Regex.Replace(name.Normalize(NormalizationForm.FormKC),@"\s+","");
    private static string Required(Dictionary<string,string> row,string field){Require(row.TryGetValue(field,out var s),"ColumnMissing:"+field);return s!;}
    private static bool SelectShop(Dictionary<string,string> row)
    {
        if(Required(row,"시군구코드")!="11260"||Required(row,"법정동코드")!="1126010100")return false;
        Require(row["시군구명"]=="중랑구"&&row["법정동명"]=="면목동"&&row["시도코드"]=="11","ShopRegionConflict");
        Require(면목동주소자료.Myeonmok(row["도로명주소"],row["지번주소"]),"ShopAddressRegionConflict");return true;
    }
    private static string? Coordinate(string value,double min,double max)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        Require(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)&&n>=min&&n<=max,"CoordinateInvalid");return value;
    }
    private static Business Project(Selected selected)
    {
        var r=selected.Fields;var rawHash=Hash(Encoding.UTF8.GetBytes(Canonical(r)));
        if(selected.Kind=="shop")
        {
            Require(SelectShop(r),"ShopOutOfScope");
            foreach(var f in new[]{"상가업소번호","상호명","지점명","상권업종소분류명","상권업종대분류명","경도","위도","건물관리번호","층정보","호정보"})Required(r,f);
            Require(r["상가업소번호"].Length>0&&r["상호명"].Length>0,"ShopIdentityMissing");
            return new("shop",r["상호명"],r["지점명"],r["도로명주소"],r["지번주소"],r["상권업종소분류명"],r["상권업종대분류명"],"",r["상가업소번호"],"ProviderShopIdNotBusinessRegistrationId","ProviderOperatingListingAt20260630;CurrentOperationUnverified",
                Coordinate(r["경도"],124,132),Coordinate(r["위도"],33,39),r["건물관리번호"],r["층정보"],r["호정보"],rawHash);
        }
        Require(selected.Kind=="factory","KindInvalid");
        foreach(var f in new[]{"순번","회사명","공장대표주소(도로명)","업종명","생산품"})Required(r,f);
        Require(면목동주소자료.Myeonmok(r["공장대표주소(도로명)"],""),"FactoryOutOfScope");Require(r["회사명"].Length>0,"FactoryNameMissing");
        var id=Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]{r["회사명"],r["공장대표주소(도로명)"],r["업종명"],r["생산품"]},Compact)));
        return new("factory",r["회사명"],"",r["공장대표주소(도로명)"],"",r["업종명"],"제조업",r["생산품"],id,"DerivedNameAddressIndustryProduct;NotPersistentRegistryId","RegisteredFactoryAt20260224;CurrentOperationUnverified",null,null,null,null,null,rawHash);
    }
    private static 외부데이터정규화Record Record(Selected selected,FileEvidence source)
    {
        var b=Project(selected);var isShop=b.Kind=="shop";var sourceId=isShop?"semas-commercial-listing":"seoul-jungnang-open-data";
        var dataset=isShop?"data-go-kr-15083033":"jungnang-registered-factory-15034963";
        var date=new DateTimeOffset(2026,isShop?6:2,isShop?30:24,0,0,0,TimeSpan.Zero);var identity=Hash(Encoding.UTF8.GetBytes(b.ProviderId));
        var text=JsonSerializer.Serialize(b,Compact);Require(text.Length<=2000,"BusinessTextBudget");var collected=DbTime(source.CollectedAtUtc);
        return new(){SourceId=sourceId,DatasetId=dataset,StableId="business:"+dataset+":"+identity,
            RecordKey=외부데이터RecordKey.Create(sourceId,dataset,Region,b.Kind,date,"identity="+identity),RegionStableId=Region,
            MetricCode="business-"+b.Kind,TextValue=text,NumericValue=null,UnitCode="source-business-observation",EvidenceAsOfUtc=date,CollectedAtUtc=collected,
            SourceVersion="source-row-sha256:"+b.RawRowHash,DataRevision=Revision,DimensionKey="identity="+identity,
            QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;NotAllBusinesses;NoVerifiedOccupancyOrCurrentOperation;NoRuntime",
            SpatialPrecisionCode=isShop?"source-address-and-lonlat-crs-not-confirmed":"source-road-address",TemporalPrecisionCode="source-date-only",
            FirstSeenAtUtc=collected,LastSeenAtUtc=collected};
    }
    private static bool Equal(외부데이터정규화Record a,외부데이터정규화Record b)=>
        a.RecordKey==b.RecordKey&&a.SourceId==b.SourceId&&a.DatasetId==b.DatasetId&&a.StableId==b.StableId&&a.RegionStableId==b.RegionStableId&&a.MetricCode==b.MetricCode&&a.TextValue==b.TextValue&&a.NumericValue==null&&a.UnitCode==b.UnitCode
        &&a.EvidenceAsOfUtc==b.EvidenceAsOfUtc&&a.CollectedAtUtc==b.CollectedAtUtc&&a.SourceVersion==b.SourceVersion&&a.DataRevision==b.DataRevision&&a.DimensionKey==b.DimensionKey&&a.QualityCode==b.QualityCode&&a.LimitationCode==b.LimitationCode&&a.SpatialPrecisionCode==b.SpatialPrecisionCode&&a.TemporalPrecisionCode==b.TemporalPrecisionCode;
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "prepare" or "source-check" or "self-test" or "inventory" or "preview" or "apply" or "verify" or "export","BusinessModeInvalid");
        if(mode=="self-test"){result["selfTestsPassed"]=SelfTest();return;}
        var folder=Path.Combine(root,Relative);
        if(mode=="inventory")
        {
            await using var db=new PublicDataIngestionDbContext(await 로컬공공자료Db.OptionsAsync(root));
            result["target"]="hongdal-mysql-1 / hongdal_dev";
            result["normalized"]=await db.NormalizedRecords.AsNoTracking().Where(x=>x.RegionStableId==Region).GroupBy(x=>new{x.SourceId,x.DatasetId,x.MetricCode}).Select(g=>new{g.Key,Count=g.Count()}).ToListAsync();
            result["licensed"]=await db.공개인허가사업장Records.AsNoTracking().GroupBy(x=>new{x.SourceId,x.SourceDatasetId}).Select(g=>new{g.Key,Count=g.Count()}).ToListAsync();
            await Save(Path.Combine(folder,"inventory-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json"),result);return;
        }
        Require(FileHash(Path.Combine(folder,"receipt.json"))==ReceiptHash,"ReceiptChanged");
        var receipts=JsonSerializer.Deserialize<List<Acquisition>>(File.ReadAllBytes(Path.Combine(folder,"receipt.json")),Json)!;
        foreach(var a in receipts)
        {
            Require(a.Status=="Acquired"&&a.License=="이용허락범위 제한 없음","SourceNotAcquiredOrLicenseMissing");
            foreach(var f in a.Files){Require(Path.GetFileName(f.File)==f.File,"SourcePathInvalid");var p=Path.Combine(folder,f.File);Require(new FileInfo(p).Length==f.Bytes&&FileHash(p)==f.Sha256,"SourceChanged");}
        }
        if(mode is "prepare" or "source-check")
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);using var fs=File.OpenRead(Path.Combine(folder,"shops-national.zip"));
            using var zip=new ZipArchive(fs,ZipArchiveMode.Read,false,Encoding.GetEncoding(949));
            var entry=zip.Entries.Single(x=>x.FullName==SeoulEntry);Require(entry.Length==301889640,"SeoulEntryChanged");
            var rows=new List<Selected>();int seoulRows=0,factoryRows=0;
            using(var reader=new StreamReader(entry.Open(),new UTF8Encoding(false,true)))
                foreach(var row in Csv(reader)){Require(++seoulRows<=700000,"SeoulRowBudget");if(SelectShop(row))rows.Add(new("shop","shops-national.zip",row));}
            using(var reader=new StreamReader(Path.Combine(folder,"factory.csv"),new UTF8Encoding(false,true)))
                foreach(var row in Csv(reader)){factoryRows++;if(면목동주소자료.Myeonmok(Required(row,"공장대표주소(도로명)"),""))rows.Add(new("factory","factory.csv",row));}
            Require(factoryRows==384&&rows.Count>0&&rows.Count<=20000,"SelectedCountInvalid");
            foreach(var row in rows)Project(row);
            Require(rows.Select(x=>x.Kind+":"+Project(x).ProviderId).Distinct().Count()==rows.Count,"DuplicateProviderIdentity");
            var projection=new Prepared(Revision,seoulRows,factoryRows,SeoulEntry,rows);
            if(mode=="prepare")await Save(Path.Combine(folder,"selected.json"),projection);
            else Require(Hash(JsonSerializer.SerializeToUtf8Bytes(projection,Json))==PreparedHash&&FileHash(Path.Combine(folder,"selected.json"))==PreparedHash,"SourceReplayMismatch");
            result["seoulRowsRead"]=seoulRows;result["factoryRowsRead"]=factoryRows;result["selected"]=rows.GroupBy(x=>x.Kind).ToDictionary(g=>g.Key,g=>g.Count());result["selectedHash"]=FileHash(Path.Combine(folder,"selected.json"));return;
        }
        Require(FileHash(Path.Combine(folder,"selected.json"))==PreparedHash,"PreparedInputNotFrozen");
        var prepared=JsonSerializer.Deserialize<Prepared>(File.ReadAllBytes(Path.Combine(folder,"selected.json")),Json)!;Require(prepared.Revision==Revision&&prepared.ZipEntry==SeoulEntry,"PreparedRevision");
        var files=receipts.SelectMany(x=>x.Files).ToDictionary(x=>x.File);
        var parsed=prepared.Rows.Select(x=>(Input:x,Record:Record(x,files[x.SourceFile]))).ToArray();
        var keys=parsed.Select(x=>x.Record.RecordKey).ToList();Require(keys.Distinct().Count()==keys.Count,"DuplicateRecordKeys");
        var options=await 로컬공공자료Db.OptionsAsync(root);result["target"]="hongdal-mysql-1 / hongdal_dev";
        result["selected"]=parsed.GroupBy(x=>x.Input.Kind).ToDictionary(g=>g.Key,g=>g.Count());
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();result["beforeCount"]=before.Count;
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText="SELECT GET_LOCK('mirror:public-data:myeonmok-business',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"ImportBusy");await using var tx=await db.Database.BeginTransactionAsync();
                before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();var wanted=parsed.ToDictionary(x=>x.Record.RecordKey,x=>x.Record);
                Require(before.All(x=>Equal(x,wanted[x.RecordKey])),"ExistingRecordConflict");result["databaseWriteAttempted"]=true;int inserted=0,existing=0;
                foreach(var group in parsed.GroupBy(x=>x.Input.SourceFile))
                {
                    var source=files[group.Key];var rows=group.Select(x=>x.Record).ToArray();
                    var registration=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,source.File),new(rows[0].SourceId,rows[0].DatasetId,"sha256:"+source.Sha256,Revision,rows[0].EvidenceAsOfUtc,source.File.EndsWith(".zip",StringComparison.Ordinal)?"application/zip":"text/csv",Relative+"/"+source.File));
                    foreach(var row in rows)row.RawSnapshotId=registration.RawSnapshotId;
                    // 작은 배치마다 기존 EF 저장소 경계를 사용하고 전체 반입은 같은 트랜잭션으로 원자화한다.
                    for(var i=0;i<rows.Length;i+=250){var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows.Skip(i).Take(250).ToArray());Require(saved.UpdatedCount==0,"UnexpectedUpdate");inserted+=saved.InsertedCount;existing+=saved.ExistingCount;}
                    if(registration.Inserted)
                    {
                        var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registration.RawSnapshotId);raw.CollectedAtUtc=DbTime(source.CollectedAtUtc);
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);run.StatusCode=외부데이터수집StatusCodes.Partial;run.FetchedCount=group.First().Input.Kind=="shop"?prepared.SeoulRows:prepared.FactoryRows;
                        run.NormalizedCount=rows.Length;run.InsertedCount=rows.Length;run.ErrorCode="PendingHumanReview";run.ErrorSummary="Myeonmok only; private review. National ZIP/Seoul entry is source envelope, not nationwide DB ingestion. Source/license/selection lineage in receipt.json and selected.json. No current occupancy, publication or Unity approval.";await db.SaveChangesAsync();
                    }
                }
                await tx.CommitAsync();result["committed"]=true;result["inserted"]=inserted;result["existing"]=existing;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);var stored=await verify.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        if(mode!="preview")Require(stored.Count==keys.Count,"ReadbackCountMismatch");var expected=parsed.ToDictionary(x=>x.Record.RecordKey);
        Require(stored.All(x=>Equal(x,expected[x.RecordKey].Record)&&x.RawSnapshot!=null&&x.RawSnapshot.ContentHashSha256==files[expected[x.RecordKey].Input.SourceFile].Sha256),"ReadbackMismatch");
        result["verifiedRows"]=stored.Count;
        if(mode=="export")await Export(root,folder,verify,stored,result);
        await Save(Path.Combine(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json"),result);
    }
    private static async Task Export(string root,string folder,PublicDataIngestionDbContext db,List<외부데이터정규화Record> rows,Dictionary<string,object?> result)
    {
        var priorPath=Path.Combine(root,"artifacts/local/public-data/myeonmok-address-20260908-r2/address-review-20260908T095925381.json");
        Require(FileHash(priorPath).Equals(면목동주소공간연결.InputHash,StringComparison.OrdinalIgnoreCase),"PriorReadbackChanged");
        var data=JsonNode.Parse(File.ReadAllBytes(priorPath))!;var features=data["features"]!.AsArray();var groups=data["addressGroups"]!.AsArray();
        var prior=new List<(string Id,string Name,string Address,string Kind)>();
        foreach(var x in features)prior.Add((x!["StableId"]!.GetValue<string>(),x["observation"]!["Name"]!.GetValue<string>(),x["observation"]!["RoadAddress"]!.GetValue<string>(),x["observation"]!["Kind"]!.GetValue<string>()));
        foreach(var x in data["restaurants"]!.AsArray())prior.Add((x!["Id"]!.GetValue<string>(),x["BusinessName"]!.GetValue<string>(),x["RoadAddress"]?.GetValue<string>()??"","restaurant"));
        // 현행 DB의 기존 음식점과 의료 기록이 동결 사본에서 달라졌으면 결합을 중단한다.
        var restaurantIds=data["restaurants"]!.AsArray().Select(x=>x!["Id"]!.GetValue<string>()).ToHashSet();
        var restaurants=await db.공개인허가사업장Records.AsNoTracking().Where(x=>x.SourceId=="seoul-jungnang-open-data"&&x.SourceDatasetId=="jungnang-restaurant-status").ToListAsync();
        foreach(var x in restaurants.Where(x=>restaurantIds.Contains(x.Id.ToString())))Require(prior.Any(p=>p.Id==x.Id.ToString()&&p.Name==x.BusinessName&&p.Address==(x.RoadAddress??"")),"PriorRestaurantChanged");
        Require(restaurants.Count(x=>restaurantIds.Contains(x.Id.ToString()))==restaurantIds.Count,"PriorRestaurantMissing");
        var priorIds=features.Select(x=>x!["StableId"]!.GetValue<string>()).ToList();var priorStored=await db.NormalizedRecords.AsNoTracking().Where(x=>priorIds.Contains(x.StableId)).ToListAsync();Require(priorStored.Count==priorIds.Count,"PriorFeatureMissing");
        foreach(var x in priorStored){var f=features.Single(n=>n!["StableId"]!.GetValue<string>()==x.StableId)!;Require(x.SourceVersion==f["SourceVersion"]!.GetValue<string>()&&JsonNode.DeepEquals(JsonNode.Parse(x.TextValue),f["observation"]),"PriorFeatureChanged");}
        var duplicates=new List<object>();var newItems=new List<(string Id,string Name,string Address,string Kind)>();var coordinates=0;
        foreach(var row in rows.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var b=JsonSerializer.Deserialize<Business>(row.TextValue)!;var key=Key(b.RoadAddress);if(b.Longitude!=null&&b.Latitude!=null)coordinates++;
            var matches=key.Length==0?[]:prior.Where(p=>Key(p.Address)==key&&NameKey(p.Name)==NameKey(b.Name)).Select(p=>p.Id).ToArray();
            if(matches.Length>0)duplicates.Add(new{newId=row.StableId,priorIds=matches,relation="ExactNormalizedNameAndRoadAddressCandidate;NotMerged"});
            newItems.Add((row.StableId,b.Name,b.RoadAddress,b.Kind));
            var o=new 면목동주소자료.AddressObservation(b.Kind,b.Name,b.RoadAddress,b.LotAddress,null,"LegalDongCodeOrExplicitFactoryAddress",b.ProviderId,b.IdentityMethod,b.StatusAsOfSource,null,null,b.Longitude,b.Latitude,b.Longitude==null?null:"LongitudeLatitude;CRSNotExplicitlyConfirmed",
                new(){{"branch",b.Branch},{"category",b.Category},{"industry",b.Industry},{"product",b.Product},{"buildingManagementNumber",b.BuildingManagementNumber??""},{"floor",b.Floor??""},{"room",b.Room??""}},b.RawRowHash);
            features.Add(JsonSerializer.SerializeToNode(new{row.StableId,row.SourceId,row.DatasetId,row.QualityCode,row.RawSnapshotId,row.SourceVersion,row.EvidenceAsOfUtc,sourceHash=row.RawSnapshot!.ContentHashSha256,observation=o},Json));
            if(key.Length==0)continue;
            var g=groups.FirstOrDefault(x=>x!["addressKey"]!.GetValue<string>()==key);
            if(g==null){g=new JsonObject{{"addressKey",key},{"method",공개사업장주소정규화Engine.RuleRevision},{"relation","SharedAddressCandidateNotVerifiedOccupancy"},{"members",new JsonArray()}};groups.Add(g);}
            g["members"]!.AsArray().Add(new JsonObject{{"Kind",b.Kind},{"Id",row.StableId},{"Name",b.Name}});
        }
        var within=newItems.Where(x=>Key(x.Address).Length>0).GroupBy(x=>Key(x.Address)+"|"+NameKey(x.Name)).Where(g=>g.Count()>1).Select(g=>new{ids=g.Select(x=>x.Id).ToArray(),relation="SameNameAddressNewSources;NotMerged"}).ToArray();
        await Save(Path.Combine(folder,"address-review.json"),data);
        const string mapPath="C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json";
        Require(FileHash(mapPath).Equals(면목동주소공간연결.MapHash,StringComparison.OrdinalIgnoreCase),"MapChanged");using var map=JsonDocument.Parse(File.ReadAllBytes(mapPath));using var input=JsonDocument.Parse(data.ToJsonString());
        var bundle=면목동주소공간연결.Project(input.RootElement,map.RootElement) with{inputHash=FileHash(Path.Combine(folder,"address-review.json"))};
        await Save(Path.Combine(folder,"connection.json"),bundle);
        await Save(Path.Combine(folder,"business-readback.json"),new{sourceReceiptHash=ReceiptHash,selectedHash=PreparedHash,quality="PendingHumanReview",rows=rows.Select(x=>new{x.Id,x.RecordKey,x.StableId,x.RawSnapshotId,x.SourceId,x.DatasetId,x.EvidenceAsOfUtc,x.SourceVersion,x.QualityCode,observation=JsonSerializer.Deserialize<Business>(x.TextValue)}),duplicateCandidates=duplicates,withinNewCandidates=within});
        var newIds=rows.Select(x=>x.StableId).ToHashSet();
        result["duplicateCandidateRowsAgainstPrior"]=duplicates.Count;result["withinNewDuplicateGroups"]=within.Length;result["sourceCoordinateRows"]=coordinates;
        result["categories"]=rows.Select(x=>JsonSerializer.Deserialize<Business>(x.TextValue)!).GroupBy(x=>x.Category).ToDictionary(g=>g.Key,g=>g.Count());
        result["combinedRecords"]=bundle.inputCount;result["combinedConnectedRecords"]=bundle.connectedCount;result["combinedBuildings"]=bundle.groups.Length;
        result["newLinkResults"]=bundle.links.Where(x=>newIds.Contains(x.item.id)).GroupBy(x=>x.result).ToDictionary(g=>g.Key,g=>g.Count());result["unityApplied"]=false;
    }
    private static int SelfTest()
    {
        int n=0;void Check(bool ok){Require(ok,"BusinessSelfTest:"+(n+1));n++;}
        void Reject(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected);}
        var f=new Dictionary<string,string>{{"순번","1"},{"회사명","시험제조"},{"공장대표주소(도로명)","서울특별시 중랑구 사가정로 1 (면목동)"},{"업종명","제조"},{"생산품","의류"}};
        var selected=new Selected("factory","factory.csv",f);var time=new DateTimeOffset(2026,9,8,0,0,0,TimeSpan.Zero).AddTicks(17);var file=new FileEvidence("factory.csv","https://example.invalid/","test",1,time);
        var record=Record(selected,file);Check(record.NumericValue==null&&record.QualityCode=="PendingHumanReview");Check(record.CollectedAtUtc.Ticks%10==0);Check(record.EvidenceAsOfUtc.Month==2);
        Check(record.RecordKey==Record(selected,file).RecordKey);Check(Project(selected).Longitude==null);Check(Project(selected).IdentityMethod.StartsWith("Derived"));
        var before=Canonical(f);Project(selected);Check(Canonical(f)==before);var swapped=f.Reverse().ToDictionary(x=>x.Key,x=>x.Value);Check(Project(selected).RawRowHash==Project(selected with{Fields=swapped}).RawRowHash);
        var wrong=new Dictionary<string,string>(f){["공장대표주소(도로명)"]="서울특별시 중랑구 면목로 1 (상봉동)"};Reject(()=>Project(selected with{Fields=wrong}));
        Check(Coordinate("",124,132)==null);Check(Coordinate("127.1",124,132)=="127.1");Reject(()=>Coordinate("NaN",124,132));Reject(()=>Coordinate("0",124,132));Reject(()=>Coordinate("Infinity",33,39));
        Check(Key("서울 중랑구 사가정로 1 (면목동, 2층)")==Key("서울특별시 중랑구 사가정로 1"));Check(Key("서울특별시 중랑구 사가정로 1-1")!=Key("서울특별시 중랑구 사가정로 1"));Check(NameKey("시험 가게")==NameKey("시험가게"));Check(NameKey("가게1")!=NameKey("가게2"));
        Check(Csv(new StringReader("a,b\n\"x,y\",\"a\"\"b\"\n")).Single()["b"]=="a\"b");Reject(()=>Csv(new StringReader("a,a\n1,2")).ToArray());Reject(()=>Csv(new StringReader("a,b\n1,2,3")).ToArray());
        var shop=new Dictionary<string,string>{{"시군구코드","11260"},{"법정동코드","1126010100"},{"시군구명","중랑구"},{"법정동명","면목동"},{"시도코드","11"},{"도로명주소","서울특별시 중랑구 사가정로 1"},{"지번주소","서울특별시 중랑구 면목동 1"}};
        Check(SelectShop(shop));shop["법정동코드"]="1126010200";Check(!SelectShop(shop));shop["법정동코드"]="1126010100";shop["법정동명"]="상봉동";Reject(()=>SelectShop(shop));
        var copy=Record(selected,file);Check(Equal(copy,record));copy.TextValue="changed";Check(!Equal(copy,record));
        shop["법정동명"]="면목동";
        foreach(var pair in new Dictionary<string,string>{{"상가업소번호","fixture-1"},{"상호명","시험가게"},{"지점명","시험점"},{"상권업종소분류명","시험업"},{"상권업종대분류명","소매"},{"경도","127.09"},{"위도","37.58"},{"건물관리번호","1126010100123456789012345"},{"층정보","2"},{"호정보",""}})shop[pair.Key]=pair.Value;
        var item=new Selected("shop","shops-national.zip",shop);var projected=Project(item);Check(projected.Floor=="2"&&projected.Room=="");Check(projected.BuildingManagementNumber==shop["건물관리번호"]);Check(projected.ProviderId=="fixture-1");
        var shopRecord=Record(item,file);Check(shopRecord.EvidenceAsOfUtc.Month==6&&shopRecord.MetricCode=="business-shop");Check(JsonSerializer.Deserialize<Business>(shopRecord.TextValue)==projected);
        var changedShop=new Dictionary<string,string>(shop){["상가업소번호"]="fixture-2"};Check(Record(item with{Fields=changedShop},file).RecordKey!=shopRecord.RecordKey);
        changedShop["상가업소번호"]="fixture-1";changedShop["상호명"]="다른상호";Check(Record(item with{Fields=changedShop},file).RecordKey==shopRecord.RecordKey);Check(!Equal(Record(item with{Fields=changedShop},file),shopRecord));
        changedShop["상가업소번호"]="";Reject(()=>Project(item with{Fields=changedShop}));Check(!shopRecord.LimitationCode.Contains("Approved",StringComparison.Ordinal));return n;
    }
}
