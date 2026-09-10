using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 중랑구의 공개 시설 위치를 기존 검토보류 저장소에 축적한다. Scene/업무 상태는 변경하지 않는다.
internal static class 중랑구공간자료
{
    public const string Relative = "artifacts/local/public-data/jungnang-spatial-20260908-r2";
    private const string Region = "region:kr:sig:11260";
    private const string Revision = "jungnang-spatial-20260908.r1";
    private const string ReceiptHash = "f058734a90b9202b08ed0a68d9288fc324122965e60a5b38e3af921d7474dd84";
    private const string Source = "data-go-kr-local-government-spatial";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented=true, Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions Compact = new() { Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    internal sealed record Layer(string Id, string Kind, string NameField, string IdentityField);
    private static readonly Layer[] Layers = [new("15012890","park","PARK_NM","MANAGE_NO"),new("15012896","parking","PRKPLCE_NM","PRKPLCE_NO"),new("15012892","toilet","TOILET_NM","")];
    internal sealed record FileEvidence(string File, string Url, string Hash, long Bytes, DateTimeOffset CollectedAtUtc, int Rows);
    internal sealed record Receipt(string Dataset, string Kind, string Status, int ExpectedRows, int ReceivedRows, string License, List<FileEvidence> Files, string? Error);
    internal sealed record PointObservation(string Kind, string Name, string RoadAddress, string LotAddress, string ProviderCode, string ProviderName,
        string ProviderIdentity, string IdentityMethod, string ReferenceDate, double? Latitude, double? Longitude, string CoordinateStatus,
        string RawRecordSha256, string? AreaSquareMetres, string? OpeningHours);
    private sealed record Parsed(외부데이터정규화Record Record, string File);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidDataException(code); }
    private static string Str(JsonElement row, string name) => row.TryGetProperty(name,out var p) && p.ValueKind==JsonValueKind.String ? p.GetString()!.Trim() : "";
    private static string Canonical(JsonElement row) => JsonSerializer.Serialize(row.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal).ToDictionary(x=>x.Name,x=>x.Value),Compact);
    private static async Task Save(string folder, string name, object value)
    {
        await using var file = new FileStream(Path.Combine(folder,name),FileMode.CreateNew,FileAccess.Write,FileShare.None);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);
        Require(bytes.Length<=8*1024*1024,"ResultBudgetExceeded"); await file.WriteAsync(bytes);
    }

    public static async Task RunAsync(string mode, string root, Dictionary<string,object?> result)
    {
        Require(mode is "acquire" or "inventory" or "self-test" or "preview" or "apply" or "verify" or "export","SpatialModeInvalid");
        var folder=Path.Combine(root,Relative);
        if(mode=="inventory")
        {
            await using var db=new PublicDataIngestionDbContext(await 로컬공공자료Db.OptionsAsync(root));
            result["target"]="hongdal-mysql-1 / hongdal_dev";
            result["rawSources"]=await db.RawSnapshots.AsNoTracking().GroupBy(x=>new{x.SourceId,x.DatasetId}).Select(g=>new{g.Key.SourceId,g.Key.DatasetId,Count=g.Count()}).ToListAsync();
            result["regionalRecords"]=await db.NormalizedRecords.AsNoTracking().Where(x=>x.RegionStableId==Region).GroupBy(x=>new{x.SourceId,x.DatasetId,x.MetricCode,x.QualityCode}).Select(g=>new{g.Key.SourceId,g.Key.DatasetId,g.Key.MetricCode,g.Key.QualityCode,Count=g.Count()}).ToListAsync();
            return;
        }
        if(mode=="acquire") { await Acquire(folder); result["acquisitionFolder"]=Relative; result["layers"]=JsonSerializer.Deserialize<List<Receipt>>(File.ReadAllText(Path.Combine(folder,"acquisition.json")),Json)!.Select(x=>new{x.Dataset,x.Status,x.ReceivedRows,x.Error}); return; }
        Require(Hash(File.ReadAllBytes(Path.Combine(folder,"acquisition.json")))==ReceiptHash,"AcquisitionReceiptChanged");
        var receipts=JsonSerializer.Deserialize<List<Receipt>>(File.ReadAllText(Path.Combine(folder,"acquisition.json")),Json)!;
        var all=new List<Parsed>(); var selected=new List<object>();
        foreach(var layer in Layers)
        {
            var receipt=receipts.Single(x=>x.Dataset==layer.Id);
            if(receipt.Status!="Acquired") { selected.Add(new{layer.Kind,status=receipt.Status,receipt.Error}); continue; }
            Require(receipt.License=="이용허락범위 제한 없음","LicenseNotVerified");
            var rows=new List<Parsed>(); var fetched=0;
            foreach(var file in receipt.Files)
            {
                Require(Path.GetFileName(file.File)==file.File,"InputPathInvalid");
                var bytes=File.ReadAllBytes(Path.Combine(folder,file.File));
                Require(bytes.LongLength==file.Bytes && Hash(bytes)==file.Hash,"SourceHashChanged");
                if(file.Rows<0) continue;
                using var doc=JsonDocument.Parse(bytes);
                Require(doc.RootElement.ValueKind==JsonValueKind.Array && doc.RootElement.GetArrayLength()==file.Rows,"RawCountChanged");
                fetched+=file.Rows;
                var index=0;
                foreach(var item in doc.RootElement.EnumerateArray())
                {
                    var parsed=Parse(layer,item,file,index++);
                    if(parsed!=null) rows.Add(new(parsed,file.File));
                }
            }
            Require(fetched==receipt.ExpectedRows && fetched==receipt.ReceivedRows,"IncompleteDownload");
            // 같은 기관/고유번호/기준일에 다른 내용이 있으면 조용히 덮어쓰지 않는다.
            foreach(var group in rows.GroupBy(x=>x.Record.RecordKey)) Require(group.Select(x=>x.Record.TextValue).Distinct().Count()==1,"ConflictingDuplicate");
            var unique=rows.DistinctBy(x=>x.Record.RecordKey).ToList();
            all.AddRange(unique);
            selected.Add(new{layer.Kind,fetched,selected=rows.Count,unique=unique.Count,duplicateRows=rows.Count-unique.Count,
                missingCoordinates=unique.Count(x=>x.Record.SpatialPrecisionCode=="source-coordinate-unavailable")});
        }
        result["layers"]=selected; result["selectedRows"]=all.Count;
        Require(all.Count>0,"NoDistrictRows");
        if(mode=="self-test") { result["selfTestsPassed"]=SelfTest(); return; }
        var options=await 로컬공공자료Db.OptionsAsync(root);
        var keys=all.Select(x=>x.Record.RecordKey).ToList();
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
            result["beforeCount"]=before.Count;
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:jungnang-spatial',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"ImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"]=true;
                // 잠금 후 재확인. 동시 변경이나 기존 다른 판정/내용을 수정하지 않는다.
                before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(before.All(x=>all.Any(y=>Equivalent(x,y.Record))),"ExistingRecordConflict");
                var inserted=0; var existing=0;
                foreach(var group in all.GroupBy(x=>x.File))
                {
                    var receipt=receipts.Single(r=>r.Files.Any(f=>f.File==group.Key));
                    var file=receipt.Files.Single(f=>f.File==group.Key);
                    var registration=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,group.Key),
                        new(Source,"data-go-kr-"+receipt.Dataset+"-standard","download-sha256:"+file.Hash,Revision,null,"application/json",Relative+"/"+group.Key));
                    var rows=group.Select(x=>x.Record).ToList();
                    foreach(var row in rows) row.RawSnapshotId=registration.RawSnapshotId;
                    var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);
                    Require(saved.UpdatedCount==0,"UnexpectedUpdate"); inserted+=saved.InsertedCount; existing+=saved.ExistingCount;
                    if(registration.Inserted)
                    {
                        var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registration.RawSnapshotId); raw.CollectedAtUtc=file.CollectedAtUtc;
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);
                        run.StatusCode=외부데이터수집StatusCodes.Partial; run.FetchedCount=file.Rows; run.NormalizedCount=rows.Count; run.InsertedCount=saved.InsertedCount;
                        run.ErrorCode="PendingHumanReview"; run.ErrorSummary="Jungnang address selection only. Source points are not footprint, entrance, navigation or operating status. License and pagination: acquisition.json.";
                        await db.SaveChangesAsync();
                    }
                }
                await transaction.CommitAsync(); result["committed"]=true; result["inserted"]=inserted; result["existing"]=existing;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        if(mode!="preview") Require(stored.Count==all.Count,"ReadbackCountMismatch");
        Require(stored.All(x=>all.Any(y=>Equivalent(x,y.Record)) && x.RawSnapshot!=null && receipts.SelectMany(r=>r.Files).Any(f=>f.Hash==x.RawSnapshot.ContentHashSha256)),"ReadbackMismatch");
        result["verifiedRows"]=stored.Count; result["target"]="hongdal-mysql-1 / hongdal_dev";
        if(mode=="export")
        {
            // DB 재조회에서만 생성. 좌표는 WGS84이며 높이/Prefab/출입구를 임의 생성하지 않는다.
            object Feature(외부데이터정규화Record x,PointObservation observation) => new{x.StableId,x.SourceId,x.DatasetId,x.SourceVersion,x.DataRevision,x.QualityCode,x.EvidenceAsOfUtc,x.RawSnapshotId,
                sourceHash=x.RawSnapshot!.ContentHashSha256,sourcePath=x.RawSnapshot.StorageObjectName,observation};
            var features=stored.OrderBy(x=>x.StableId,StringComparer.Ordinal).Select(x=>Feature(x,JsonSerializer.Deserialize<PointObservation>(x.TextValue)!)).ToList();
            var markets=await verify.NormalizedRecords.AsNoTracking().Where(x=>x.RegionStableId==Region && x.SourceId==전통시장표본Parser.SourceId && x.DatasetId==전통시장표본Parser.DatasetId).Include(x=>x.RawSnapshot).ToListAsync();
            var marketFile=Path.Combine(root,"artifacts/local/public-data/eight-life-domains/market-20260908-r1/markets.json");
            const string marketHash="13ffd04a946ec7eec28222c8c3762f2e77aab919cd250888cdaaf7f51cce303b";
            Require(Hash(File.ReadAllBytes(marketFile))==marketHash,"MarketSourceChanged");
            var expectedMarkets=전통시장표본Parser.Parse(File.ReadAllText(marketFile),DateTimeOffset.UtcNow);
            Require(markets.Count==12 && markets.All(x=>expectedMarkets.Any(y=>Equivalent(x,y)) && x.RawSnapshot!.ContentHashSha256==marketHash),"MarketReadbackMismatch");
            foreach(var market in markets.OrderBy(x=>x.StableId,StringComparer.Ordinal))
            {
                using var doc=JsonDocument.Parse(market.TextValue); var m=doc.RootElement;
                features.Add(Feature(market,new("market",Str(m,"MRKT_NM"),Str(m,"RDNMADR"),Str(m,"LNMADR"),Str(m,"INSTT_CODE"),Str(m,"INSTT_NM"),market.StableId,"DerivedNameAndAddress",
                    Str(m,"REFERENCE_DATE"),double.Parse(Str(m,"LATITUDE"),CultureInfo.InvariantCulture),double.Parse(Str(m,"LONGITUDE"),CultureInfo.InvariantCulture),"SourcePointUnverified",
                    Hash(Encoding.UTF8.GetBytes(Canonical(m))),null,null)));
            }
            var name="unity-location-review-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json";
            await Save(folder,name,new{schema="jungnang-location-review.r1",region=Region,coordinateSystem="EPSG:4326",heightMetres=(double?)null,
                publicationApproved=false,gameStateConnected=false,geometryKind="SourcePointOrNull",features});
            result["export"]=Relative+"/"+name; result["exportedRows"]=features.Count; result["reusedMarketRows"]=markets.Count;
        }
        await Save(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json",result);
    }

    private static bool Equivalent(외부데이터정규화Record x, 외부데이터정규화Record y) => x.RecordKey==y.RecordKey && x.TextValue==y.TextValue
        && x.StableId==y.StableId && x.SourceId==y.SourceId && x.DatasetId==y.DatasetId && x.SourceVersion==y.SourceVersion
        && x.DataRevision==y.DataRevision && x.QualityCode==y.QualityCode && x.LimitationCode==y.LimitationCode && x.NumericValue==null
        && x.RegionStableId==y.RegionStableId && x.MetricCode==y.MetricCode && x.EvidenceAsOfUtc==y.EvidenceAsOfUtc
        && x.SpatialPrecisionCode==y.SpatialPrecisionCode && x.UnitCode==y.UnitCode && x.TemporalPrecisionCode==y.TemporalPrecisionCode && x.DimensionKey==y.DimensionKey;

    internal static 외부데이터정규화Record? Parse(Layer layer, JsonElement item, FileEvidence file, int index)
    {
        Require(item.ValueKind==JsonValueKind.Object,"RowObjectRequired");
        var road=Str(item,"RDNMADR"); var lot=Str(item,"LNMADR");
        bool InDistrict(string s) => s.StartsWith("서울특별시 중랑구 ",StringComparison.Ordinal) || s.StartsWith("서울 중랑구 ",StringComparison.Ordinal);
        if(!InDistrict(road) && !InDistrict(lot)) return null;
        Require(!(road.StartsWith("서울",StringComparison.Ordinal) && !InDistrict(road)) && !(lot.StartsWith("서울",StringComparison.Ordinal) && !InDistrict(lot)),"AddressDistrictConflict");
        var name=Str(item,layer.NameField); var dateText=Str(item,"REFERENCE_DATE");
        Require(name.Length>0 && DateOnly.TryParseExact(dateText,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),"NameOrDateMissing");
        var date=DateOnly.ParseExact(dateText,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        var evidence=new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
        Require(evidence<=file.CollectedAtUtc,"FutureReferenceDate");
        var latText=Str(item,"LATITUDE"); var lonText=Str(item,"LONGITUDE");
        double? lat=null,lon=null; var status="MissingOrInvalid";
        if(double.TryParse(latText,NumberStyles.Float,CultureInfo.InvariantCulture,out var la) && double.IsFinite(la) && la is >=37.5 and <=37.7
            && double.TryParse(lonText,NumberStyles.Float,CultureInfo.InvariantCulture,out var lo) && double.IsFinite(lo) && lo is >=127.0 and <=127.2)
        { lat=la; lon=lo; status="SourcePointUnverified"; }
        // 넓은 좌표 sanity 범위일 뿐 중랑구 경계 내 판정이 아니다. 주소 선택과 별도로 표기한다.
        var provider=Str(item,"INSTT_CODE"); Require(provider.Length>0,"ProviderCodeMissing");
        var providerId=layer.IdentityField.Length==0 ? "" : Str(item,layer.IdentityField);
        var method=providerId.Length==0 ? "DerivedNameAndAddress" : "ProviderIdentity";
        if(providerId.Length==0) providerId=Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]{name,road,lot},Compact)));
        var identity=Hash(Encoding.UTF8.GetBytes(provider+"|"+providerId));
        var rawHash=Hash(Encoding.UTF8.GetBytes(Canonical(item)));
        // 행 위치는 계보 경로일 뿐 의미 데이터 동일성에 영향을 주지 않도록 원문 hash로 연결한다.
        var point=new PointObservation(layer.Kind,name,road,lot,provider,Str(item,"INSTT_NM"),providerId,method,dateText,lat,lon,status,rawHash,
            layer.Kind=="park" ? Str(item,"PARK_AR") : null,layer.Kind=="toilet" ? Str(item,"OPEN_TIME") : null);
        var text=JsonSerializer.Serialize(point,Compact); Require(text.Length<=2000,"NormalizedRecordTooLarge");
        var dim="provider-identity-sha256="+identity;
        return new 외부데이터정규화Record { SourceId=Source,DatasetId="data-go-kr-"+layer.Id+"-standard",StableId="spatial:"+layer.Id+":"+identity,
            RegionStableId=Region,MetricCode="public-facility-"+layer.Kind,RecordKey=외부데이터RecordKey.Create(Source,"data-go-kr-"+layer.Id+"-standard",Region,"public-facility-"+layer.Kind,evidence,dim),
            TextValue=text,NumericValue=null,UnitCode="source-point",EvidenceAsOfUtc=evidence,CollectedAtUtc=file.CollectedAtUtc,
            SpatialPrecisionCode=lat.HasValue ? "source-point-unverified" : "source-coordinate-unavailable",TemporalPrecisionCode="date-only",
            QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;AddressSelected;NoDistrictPolygonTest;NotEntranceOrFootprint;NoNavigationOrOperatingStatus",
            DimensionKey=dim,SourceVersion="source-record-sha256:"+rawHash,DataRevision=Revision,FirstSeenAtUtc=file.CollectedAtUtc,LastSeenAtUtc=file.CollectedAtUtc };
    }

    private static async Task Acquire(string folder)
    {
        Require(!Directory.Exists(folder),"AcquisitionAlreadyExists"); Directory.CreateDirectory(folder);
        using var handler=new HttpClientHandler { AllowAutoRedirect=false };
        using var client=new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(45) };
        var receipts=new List<Receipt>(); long totalBytes=0;
        async Task<(byte[],FileEvidence)> Fetch(string url,string filename,int rows=-1)
        {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            Require(response.IsSuccessStatusCode,"SourceHttpStatus:"+(int)response.StatusCode);
            await using var input=await response.Content.ReadAsStreamAsync(deadline.Token);
            using var memory=new MemoryStream(); var buffer=new byte[65536]; int read;
            while((read=await input.ReadAsync(buffer,deadline.Token))>0) { Require(memory.Length+read<=32*1024*1024 && totalBytes+read<=128*1024*1024,"AcquisitionBudgetExceeded"); memory.Write(buffer,0,read); totalBytes+=read; }
            var bytes=memory.ToArray(); Require(bytes.Length>0,"EmptyResponse");
            await using(var output=new FileStream(Path.Combine(folder,filename),FileMode.CreateNew)) await output.WriteAsync(bytes);
            return (bytes,new(filename,url,Hash(bytes),bytes.LongLength,DateTimeOffset.UtcNow,rows));
        }
        foreach(var layer in Layers)
        {
            var files=new List<FileEvidence>(); var expected=0; var received=0; var license=""; string? error=null;
            try
            {
                var (meta,metaFile)=await Fetch($"https://www.data.go.kr/catalog/{layer.Id}/standard.json",layer.Id+"-metadata.json"); files.Add(metaFile);
                using var metadata=JsonDocument.Parse(meta);
                Require(metadata.RootElement.GetProperty("url").GetString()==$"https://www.data.go.kr/data/{layer.Id}/standard.do","MetadataIdentityMismatch");
                var (rdf,rdfFile)=await Fetch($"https://www.data.go.kr/biz/dcat/metadata/{layer.Id}.do",layer.Id+"-metadata.rdf"); files.Add(rdfFile);
                var rights=XDocument.Parse(Encoding.UTF8.GetString(rdf)).Descendants(XName.Get("rights","http://purl.org/dc/terms/")).Select(x=>x.Value.Trim()).Distinct().ToList();
                Require(rights.Count==1,"LicenseEvidenceMissingOrAmbiguous"); license=rights[0];
                Require(license=="이용허락범위 제한 없음","LicenseReviewRequired");
                var (header,headerFile)=await Fetch($"https://www.data.go.kr/download/columList.json?pk={layer.Id}&ext=JSON",layer.Id+"-columns.json"); files.Add(headerFile);
                using var columns=JsonDocument.Parse(header); expected=columns.RootElement.GetProperty("totalCount").GetInt32();
                Require(expected>0 && expected<=40000,"DatasetSizeOutsideBound");
                var table=columns.RootElement.GetProperty("tableVO"); var colNames=table.GetProperty("colNmList").EnumerateArray().Select(x=>x.GetString()!).ToList();
                foreach(var field in new[]{layer.NameField,"RDNMADR","LNMADR","LATITUDE","LONGITUDE","REFERENCE_DATE"}) Require(colNames.Contains(field),"RequiredColumnMissing");
                var prefix=$"https://www.data.go.kr/download/standard.json?publicDataPk={layer.Id}&"+string.Join("&",colNames.Select(x=>"colNmList="+Uri.EscapeDataString(x)))+
                    "&totalCount="+expected+"&svcTableNm="+Uri.EscapeDataString(table.GetProperty("svcTableNm").GetString()!)+"&perPage=10000&page=";
                for(var page=1;page<=Math.Ceiling(expected/10000.0);page++)
                {
                    var (bytes,file)=await Fetch(prefix+page,layer.Id+"-page-"+page+".json");
                    using var doc=JsonDocument.Parse(bytes); Require(doc.RootElement.ValueKind==JsonValueKind.Array,"SourceArrayRequired");
                    var count=doc.RootElement.GetArrayLength(); files.Add(file with { Rows=count }); received+=count;
                    Require(count==Math.Min(10000,expected-(page-1)*10000),"PageCountMismatch");
                }
                // 수집 중 목록 개수/열 변경 감지. 동일 개수의 원천 교체 가능성은 원천 스냅샷 한계로 남긴다.
                var (last,lastFile)=await Fetch($"https://www.data.go.kr/download/columList.json?pk={layer.Id}&ext=JSON",layer.Id+"-columns-after.json"); files.Add(lastFile);
                using var after=JsonDocument.Parse(last); Require(after.RootElement.GetProperty("totalCount").GetInt32()==expected
                    && after.RootElement.GetProperty("tableVO").GetProperty("colNmList").GetRawText()==table.GetProperty("colNmList").GetRawText(),"SourceChangedDuringAcquisition");
            }
            catch(Exception ex) { error=ex is InvalidDataException ? ex.Message : ex.GetType().Name; }
            var receipt=new Receipt(layer.Id,layer.Kind,error==null ? "Acquired" : "Blocked",expected,received,license,files,error);
            receipts.Add(receipt); await Save(folder,layer.Id+"-receipt.json",receipt);
        }
        await Save(folder,"acquisition.json",receipts);
    }

    private static int SelfTest()
    {
        const string fixture="{\"PARK_NM\":\"시험공원\",\"MANAGE_NO\":\"11260-1\",\"RDNMADR\":\"서울특별시 중랑구 시험로 1\",\"LNMADR\":\"\",\"INSTT_CODE\":\"3060000\",\"INSTT_NM\":\"서울특별시 중랑구\",\"REFERENCE_DATE\":\"2026-09-01\",\"LATITUDE\":\"37.60\",\"LONGITUDE\":\"127.09\"}";
        var file=new FileEvidence("test.json","https://www.data.go.kr/","",0,new DateTimeOffset(2026,9,8,0,0,0,TimeSpan.Zero),1);
        외부데이터정규화Record? P(string s) { using var d=JsonDocument.Parse(s); return Parse(Layers[0],d.RootElement,file,0); }
        var baseline=P(fixture)!; var checks=0;
        void Check(bool ok) { Require(ok,"SpatialSelfTestFailed:"+checks); checks++; }
        Check(baseline.QualityCode=="PendingHumanReview" && baseline.NumericValue==null);
        Check(Equivalent(baseline,P(fixture)!));
        Check(P(fixture.Replace("중랑구 시험로","강남구 시험로"))==null);
        Check(P(fixture.Replace("37.60",""))!.SpatialPrecisionCode=="source-coordinate-unavailable");
        Check(P(fixture.Replace("37.60","NaN"))!.SpatialPrecisionCode=="source-coordinate-unavailable");
        Check(P(fixture.Replace("127.09","0"))!.SpatialPrecisionCode=="source-coordinate-unavailable");
        Check(P(fixture.Replace("11260-1","11260-2"))!.RecordKey!=baseline.RecordKey);
        Check(P(fixture.Replace("2026-09-01","2026-09-02"))!.RecordKey!=baseline.RecordKey);
        Check(P(fixture.Replace("37.60","37.61"))!.SourceVersion!=baseline.SourceVersion);
        foreach(var invalid in new[]{fixture.Replace("시험공원",""),fixture.Replace("2026-09-01","invalid"),fixture.Replace("2026-09-01","2026-10-01"),fixture.Replace("3060000",""),fixture.Replace("\"LNMADR\":\"\"","\"LNMADR\":\"서울특별시 강남구 시험동\"")})
        { var rejected=false; try { _=P(invalid); } catch(InvalidDataException) { rejected=true; } Check(rejected); }
        return checks;
    }
}
