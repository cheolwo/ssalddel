using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Ssalddel.Domain.PublicData;
using Ssalddel.Domain.PublicData.Korea;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 기존 서비스/로컬 검토 DB만 사용한다. 원본 주소를 지오코딩하거나 건물 입주로 확정하지 않는다.
internal static class 면목동주소자료
{
    public const string Relative="artifacts/local/public-data/myeonmok-address-20260908-r2";
    private const string Revision="myeonmok-address-20260908.r1";
    private const string Region="region:kr:bjd:1126010100";
    private const string ReceiptHash="c70b5fdedad5b8cb7d1d1e197df4fcb5b0d9aa8350abb4811159f2be98acc593";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    private static readonly JsonSerializerOptions Compact=new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal sealed record Evidence(string File,string Url,string Hash,long Bytes,DateTimeOffset CollectedAtUtc,string? RequestBody=null);
    internal sealed record Receipt(string Id,string Kind,string Status,string License,List<Evidence> Files,int ReceivedRows,string? Error);
    internal sealed record AddressObservation(string Kind,string Name,string RoadAddress,string LotAddress,string? AdministrativeDong,
        string SelectionBasis,string ProviderIdentity,string IdentityMethod,string? OperatingStatus,string? ClosureDate,
        string? SourceUpdatedAt,string? CoordinateX,string? CoordinateY,string? CoordinateSystem,
        Dictionary<string,string> Attributes,string RawRowHash);
    private sealed record Parsed(외부데이터정규화Record Record,string File);
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    // MySQL datetime(6)에서 관측한 100ns 자리 절삭을 입출력 경계에 명시한다. 원문 수집시각과 기존 RecordKey는 보존한다.
    private static DateTimeOffset DbTime(DateTimeOffset value)=>new(value.UtcTicks-value.UtcTicks%10,TimeSpan.Zero);
    private static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    private static string Str(JsonElement row,string field)=>row.TryGetProperty(field,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()!.Trim():"";
    private static string Canonical(JsonElement row)=>JsonSerializer.Serialize(row.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal).ToDictionary(x=>x.Name,x=>x.Value),Compact);
    private static bool District(string value)=>Regex.IsMatch(value,@"^(서울특별시|서울) 중랑구 ");
    internal static bool Myeonmok(string road,string lot)=>District(road)&&Regex.IsMatch(road,@"\(면목동(?:[,\s)]|$)")
        ||Regex.IsMatch(lot,@"^(서울특별시|서울) 중랑구 면목동(?:\s|$)");
    private static void SafePath(string path)
    {
        for(var p=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);p!=null;p=p.Parent)
            if(p.Exists)Require(!p.Attributes.HasFlag(FileAttributes.ReparsePoint),"ReparseAncestorRejected");
        if(File.Exists(path))Require(!File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint),"ReparseFileRejected");
    }
    private static async Task Save(string folder,string name,object value)
    {
        var path=Path.Combine(folder,name);SafePath(path);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);Require(bytes.Length<=16*1024*1024,"OutputBudgetExceeded");
        await using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);await file.WriteAsync(bytes);
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "inventory" or "acquire" or "self-test" or "preview" or "apply" or "verify" or "export" or "diagnose","MyeonmokModeInvalid");
        var folder=Path.Combine(root,Relative);SafePath(Path.Combine(folder,"receipt.json"));
        if(mode=="acquire"){await Acquire(folder);result["folder"]=Relative;return;}
        if(mode=="self-test"){result["selfTestsPassed"]=SelfTest();return;}
        var options=await 로컬공공자료Db.OptionsAsync(root);
        if(mode=="inventory")
        {
            await using var db=new PublicDataIngestionDbContext(options);
            result["businesses"]=await db.공개인허가사업장Records.AsNoTracking().GroupBy(x=>new{x.SourceId,x.SourceDatasetId,x.SourceRevision,x.SourceHashSha256}).Select(g=>new{g.Key,Count=g.Count()}).ToListAsync();
            result["buildings"]=await db.BuildingRegisterTitles.AsNoTracking().GroupBy(x=>x.SourceRevision).Select(g=>new{g.Key,Count=g.Count()}).ToListAsync();
            result["myeonmokNormalized"]=await db.NormalizedRecords.AsNoTracking().CountAsync(x=>x.RegionStableId==Region);
            return;
        }
        Require(Hash(File.ReadAllBytes(Path.Combine(folder,"receipt.json")))==ReceiptHash,"ReceiptChanged");
        var receipts=JsonSerializer.Deserialize<List<Receipt>>(File.ReadAllText(Path.Combine(folder,"receipt.json")),Json)!;
        var parsed=new List<Parsed>();
        foreach(var receipt in receipts)
        {
            foreach(var f in receipt.Files){Require(Path.GetFileName(f.File)==f.File,"InputNameInvalid");SafePath(Path.Combine(folder,f.File));var b=File.ReadAllBytes(Path.Combine(folder,f.File));Require(b.LongLength==f.Bytes&&Hash(b)==f.Hash,"InputChanged");}
            if(receipt.Status!="Acquired")continue;
            Require(receipt.License is "이용허락범위 제한 없음" or "공공누리 제1유형","LicenseMissing");
            if(receipt.Kind=="housing")
            {
                var f=receipt.Files.Single(x=>x.File.EndsWith(".csv",StringComparison.Ordinal));
                var rows=ReadCsv(File.ReadAllText(Path.Combine(folder,f.File),new UTF8Encoding(false,true)));
                Require(rows.Count==receipt.ReceivedRows,"HousingCountChanged");
                foreach(var row in rows){var record=Housing(row,f);if(record!=null)parsed.Add(new(record,f.File));}
            }
            else foreach(var f in receipt.Files.Where(x=>Regex.IsMatch(x.File,@"-page-\d+\.json$")))
            {
                using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,f.File)));
                foreach(var row in doc.RootElement.EnumerateArray()){var record=Medical(receipt,row,f);if(record!=null)parsed.Add(new(record,f.File));}
            }
        }
        Require(parsed.Count>0&&parsed.Select(x=>x.Record.RecordKey).Distinct().Count()==parsed.Count,"EmptyOrDuplicateRecords");
        result["selected"]=parsed.GroupBy(x=>x.Record.MetricCode).Select(g=>new{Kind=g.Key,Count=g.Count()});
        var keys=parsed.Select(x=>x.Record.RecordKey).ToList();
        bool Equal(외부데이터정규화Record a,외부데이터정규화Record b)=>a.RecordKey==b.RecordKey&&a.StableId==b.StableId&&a.SourceId==b.SourceId&&a.DatasetId==b.DatasetId
            &&a.TextValue==b.TextValue&&a.SourceVersion==b.SourceVersion&&a.QualityCode==b.QualityCode&&a.RegionStableId==b.RegionStableId&&a.DataRevision==b.DataRevision&&a.MetricCode==b.MetricCode&&a.NumericValue==null
            &&a.EvidenceAsOfUtc==b.EvidenceAsOfUtc&&a.CollectedAtUtc==b.CollectedAtUtc&&a.UnitCode==b.UnitCode&&a.TemporalPrecisionCode==b.TemporalPrecisionCode&&a.SpatialPrecisionCode==b.SpatialPrecisionCode&&a.DimensionKey==b.DimensionKey&&a.LimitationCode==b.LimitationCode;
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();result["beforeCount"]=before.Count;
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:myeonmok-address',0)";Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"ImportBusy");
                await using var tx=await db.Database.BeginTransactionAsync();
                before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(before.All(x=>parsed.Any(y=>Equal(x,y.Record))),"ExistingConflict");result["databaseWriteAttempted"]=true;
                var inserted=0;var existing=0;
                foreach(var group in parsed.GroupBy(x=>x.File))
                {
                    var r=receipts.Single(x=>x.Files.Any(f=>f.File==group.Key));var f=r.Files.Single(x=>x.File==group.Key);var rows=group.Select(x=>x.Record).ToList();
                    var registration=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,f.File),new(rows[0].SourceId,rows[0].DatasetId,"sha256:"+f.Hash,Revision,null,r.Kind=="housing"?"text/csv":"application/json",Relative+"/"+f.File));
                    foreach(var row in rows)row.RawSnapshotId=registration.RawSnapshotId;
                    var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);Require(saved.UpdatedCount==0,"UnexpectedUpdate");inserted+=saved.InsertedCount;existing+=saved.ExistingCount;
                    if(registration.Inserted)
                    {
                        var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registration.RawSnapshotId);raw.CollectedAtUtc=f.CollectedAtUtc;
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);run.StatusCode=외부데이터수집StatusCodes.Partial;run.NormalizedCount=rows.Count;run.InsertedCount=saved.InsertedCount;
                        run.ErrorCode="PendingHumanReview";run.ErrorSummary="Myeonmok source-address selection. Private review; historical closed records retained. No geocoding, occupancy, runtime or publication. receipt.json preserves license and source requests.";
                        await db.SaveChangesAsync();
                    }
                }
                await tx.CommitAsync();result["committed"]=true;result["inserted"]=inserted;result["existing"]=existing;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        if(mode=="diagnose")result["fieldDifferences"]=stored.Select(x=>new{stored=x,incoming=parsed.Single(y=>y.Record.RecordKey==x.RecordKey).Record}).SelectMany(pair=>typeof(외부데이터정규화Record).GetProperties().Where(p=>p.PropertyType==typeof(string)||p.PropertyType==typeof(DateTimeOffset)).Where(p=>!Equals(p.GetValue(pair.stored),p.GetValue(pair.incoming))).Select(p=>new{field=p.Name,stored=p.GetValue(pair.stored),incoming=p.GetValue(pair.incoming)})).Distinct().Take(25).ToArray();
        if(mode!="preview")Require(stored.Count==parsed.Count,"ReadbackCountMismatch");
        Require(stored.All(x=>parsed.Any(y=>Equal(x,y.Record))&&x.RawSnapshot!=null&&receipts.SelectMany(r=>r.Files).Any(f=>f.Hash==x.RawSnapshot.ContentHashSha256)),"ReadbackMismatch");
        result["verifiedRows"]=stored.Count;
        if(mode=="export")await Export(root,folder,verify,stored,result);
        await Save(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json",result);
    }
    private static 외부데이터정규화Record Record(string source,string dataset,string id,AddressObservation observation,Evidence file,DateTimeOffset? reference=null)
    {
        var evidence=reference??file.CollectedAtUtc;var identity=Hash(Encoding.UTF8.GetBytes(id));var text=JsonSerializer.Serialize(observation,Compact);Require(text.Length<=2000,"RowTooLarge");
        return new(){SourceId=source,DatasetId=dataset,StableId="address:"+dataset+":"+identity,RegionStableId=Region,MetricCode=observation.Kind,
            RecordKey=외부데이터RecordKey.Create(source,dataset,Region,observation.Kind,evidence,"identity="+identity),DimensionKey="identity="+identity,TextValue=text,NumericValue=null,
            UnitCode="source-address",EvidenceAsOfUtc=DbTime(evidence),CollectedAtUtc=DbTime(file.CollectedAtUtc),SpatialPrecisionCode="address-selected-no-geocoding",
            TemporalPrecisionCode=reference.HasValue?"date-only":"retrieval-snapshot-not-source-asof",QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;NotVerifiedOccupancyOrCurrentOperation;NoWgs84OrEntrance;NoRuntime",
            SourceVersion="source-row-sha256:"+observation.RawRowHash,DataRevision=Revision,FirstSeenAtUtc=DbTime(file.CollectedAtUtc),LastSeenAtUtc=DbTime(file.CollectedAtUtc)};
    }
    private static 외부데이터정규화Record? Medical(Receipt receipt,JsonElement row,Evidence file)
    {
        Require(Str(row,"OPNSFTEAMCODE")=="3060000","DistrictProviderMismatch");var road=Str(row,"RDNWHLADDR");var lot=Str(row,"SITEWHLADDR");
        if(!Myeonmok(road,lot))return null;
        Require((road.Length==0||District(road))&&(lot.Length==0||District(lot)),"AddressDistrictConflict");
        var name=Str(row,"BPLCNM");var id=Str(row,"MGTNO");Require(name.Length>0&&id.Length>0,"MedicalIdentityMissing");
        var attrs=new Dictionary<string,string>{{"type",Str(row,"METRORGASSRNM")},{"sourceDetailedStatus",Str(row,"DTLSTATENM")},{"licenseDate",Str(row,"APVPERMYMD")}};
        var o=new AddressObservation(receipt.Kind,name,road,lot,null,"SourceLegalDongAddress",id,"ProviderManagementNumber",Str(row,"TRDSTATENM"),Str(row,"DCBYMD"),Str(row,"LASTMODTS"),Str(row,"X"),Str(row,"Y"),"EPSG:5174 (source declaration; untransformed)",attrs,Hash(Encoding.UTF8.GetBytes(Canonical(row))));
        return Record("seoul-open-data",receipt.Id,id,o,file);
    }
    internal static List<Dictionary<string,string>> ReadCsv(string text)
    {
        using var parser=new TextFieldParser(new StringReader(text.TrimStart('\uFEFF'))){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=false};parser.SetDelimiters(",");
        var header=parser.ReadFields()??throw new InvalidDataException("CsvHeaderMissing");Require(header.Distinct().Count()==header.Length,"DuplicateHeader");
        var rows=new List<Dictionary<string,string>>();while(!parser.EndOfData){var cells=parser.ReadFields()!;Require(cells.Length==header.Length,"CsvWidthMismatch");rows.Add(header.Select((h,i)=>(h,cells[i].Trim())).ToDictionary(x=>x.h,x=>x.Item2));}return rows;
    }
    private static 외부데이터정규화Record? Housing(Dictionary<string,string> row,Evidence f)
    {
        foreach(var field in new[]{"연번","단지명","동명","번지수","소재지도로명주소","동수","최고층수","세대수","준공일"})Require(row.ContainsKey(field),"HousingColumnMissing");
        var dong=row["동명"];if(!new[]{"면목본동","면목2동","면목3.8동","면목3·8동","면목4동","면목5동","면목7동","면목동"}.Contains(dong))return null;
        var road=row["소재지도로명주소"];Require(road.StartsWith("중랑구 ",StringComparison.Ordinal)||District(road),"HousingDistrictMismatch");Require(row["단지명"].Length>0,"HousingNameMissing");
        var attrs=new Dictionary<string,string>();foreach(var field in new[]{"동수","최고층수","세대수","승강기","난방방식","준공일"})attrs[field]=row.GetValueOrDefault(field,"");
        attrs["lotNumber"]=row["번지수"];attrs["coverage"]="30세대 이상; 공공임대 여부 미확인";
        var rowHash=Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value),Compact)));
        var id=row["단지명"]+"|"+road;
        return Record("seoul-jungnang-open-data","jungnang-apartment-status",id,new("housing",row["단지명"],road,"",dong,"SourceAdministrativeDongToMyeonmokReview",id,"DerivedNameAndAddress",null,null,null,null,null,null,attrs,rowHash),f,new DateTimeOffset(2026,2,24,0,0,0,TimeSpan.Zero));
    }
    private static async Task Acquire(string folder)
    {
        Require(!Directory.Exists(folder),"AcquisitionExists");Directory.CreateDirectory(folder);
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mirror-PublicDataReview/1.0");
        var receipts=new List<Receipt>();long total=0;
        // r1의 병의원은 완료됐다. 원문/수집시각을 그대로 재사용하며 실패한 주택 다운로드만 명시 재검토한다.
        var previous=Path.Combine(Path.GetDirectoryName(folder)!,"myeonmok-address-20260908-r1");
        Require(Hash(File.ReadAllBytes(Path.Combine(previous,"receipt.json")))=="a643c6236560b2a2bd3beaa3fc9b3b76208a63552826573d916e8de675359a70","PriorReceiptChanged");
        var prior=JsonSerializer.Deserialize<List<Receipt>>(File.ReadAllText(Path.Combine(previous,"receipt.json")),Json)!;
        async Task<(byte[] Bytes,Evidence File)> Fetch(string url,string name,Dictionary<string,string>? form=null)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));using var request=new HttpRequestMessage(form==null?HttpMethod.Get:HttpMethod.Post,url);if(form!=null)request.Content=new FormUrlEncodedContent(form);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);Require(response.IsSuccessStatusCode,"HttpStatus:"+(int)response.StatusCode);
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var memory=new MemoryStream();var buffer=new byte[65536];int n;
            while((n=await stream.ReadAsync(buffer,timeout.Token))>0){total+=n;Require(memory.Length+n<=8*1024*1024&&total<=32*1024*1024,"DownloadBudgetExceeded");memory.Write(buffer,0,n);}
            var bytes=memory.ToArray();Require(bytes.Length>0,"EmptySource");var p=Path.Combine(folder,name);SafePath(p);await using(var output=new FileStream(p,FileMode.CreateNew))await output.WriteAsync(bytes);
            return(bytes,new(name,url,Hash(bytes),bytes.LongLength,DateTimeOffset.UtcNow,form==null?null:await new FormUrlEncodedContent(form).ReadAsStringAsync()));
        }
        foreach(var (id,kind,table) in new[]{("15006098","housing",""),("OA-16220","clinic","OPENMART3.TV_DATA_CLINICS"),("OA-16165","hospital","OPENMART3.TV_DATA_HOSPITALS")})
        {
            if(kind!="housing")
            {
                var old=prior.Single(x=>x.Id==id);Require(old.Status=="Acquired","PriorMedicalIncomplete");
                foreach(var file in old.Files)
                {
                    Require(Path.GetFileName(file.File)==file.File,"PriorPathInvalid");var input=Path.Combine(previous,file.File);SafePath(input);var bytes=File.ReadAllBytes(input);
                    Require(bytes.LongLength==file.Bytes&&Hash(bytes)==file.Hash,"PriorFileChanged");total+=bytes.LongLength;Require(total<=32*1024*1024,"ReuseBudgetExceeded");
                    await using var copy=new FileStream(Path.Combine(folder,file.File),FileMode.CreateNew);await copy.WriteAsync(bytes);
                }
                receipts.Add(old);continue;
            }
            var files=new List<Evidence>();string? error=null;var license="";var count=0;
            try
            {
                if(kind=="housing")
                {
                    var (meta,m)=await Fetch("https://www.data.go.kr/catalog/15006098/fileData.json",id+"-metadata.json");files.Add(m);using var doc=JsonDocument.Parse(meta);
                    Require(doc.RootElement.GetProperty("license").GetString()=="이용허락범위 제한 없음"&&doc.RootElement.GetProperty("alternateName").GetString()!.EndsWith("_20260224",StringComparison.Ordinal),"HousingVersionOrLicenseChanged");license="이용허락범위 제한 없음";
                    var (page,p)=await Fetch("https://www.data.go.kr/data/15006098/fileData.do",id+"-page.html");files.Add(p);
                    const string url="https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003604219&fileDetailSn=1&insertDataPrcus=N";
                    Require(Encoding.UTF8.GetString(page).Contains(url,StringComparison.Ordinal),"HousingDownloadLinkChanged");
                    var (csv,f)=await Fetch(url,id+".csv");files.Add(f);count=ReadCsv(new UTF8Encoding(false,true).GetString(csv)).Count;Require(count==203,"HousingExpectedCountChanged");
                }
                else
                {
                    var (page,p)=await Fetch("https://data.jungnang.go.kr/openinf/sheetview.jsp?infId="+id,id+"-metadata.html");files.Add(p);var html=Encoding.UTF8.GetString(page);
                    Require(html.Contains(table,StringComparison.Ordinal)&&html.Contains("EPSG:5174",StringComparison.Ordinal)&&html.Contains("상업적 이용 및 변경 가능",StringComparison.Ordinal),"MedicalMetadataChanged");license="공공누리 제1유형";
                    var keys=new HashSet<string>(StringComparer.Ordinal);string? firstHash=null;var ended=false;
                    for(var pageNo=1;pageNo<=20;pageNo++)
                    {
                        var form=new Dictionary<string,string>{{"onepagerow","100"},{"ibpage",pageNo.ToString(CultureInfo.InvariantCulture)},{"vinfId",id},{"dsId",table},{"strOrderby",""},{"strWhere"," AND OPNSFTEAMCODE = 3060000"},{"filterCol",""},{"txtFilter",""},{"sortCol",""},{"sortArrow",""},{"scrollPg","Y"}};
                        var (data,f)=await Fetch("https://data.jungnang.go.kr/openinf/sheetexec2.jsp",id+"-page-"+pageNo+".json",form);files.Add(f);using var doc=JsonDocument.Parse(data);Require(doc.RootElement.ValueKind==JsonValueKind.Array,"MedicalArrayMissing");
                        var received=doc.RootElement.GetArrayLength();Require(received<=100,"PageBoundExceeded");count+=received;
                        foreach(var row in doc.RootElement.EnumerateArray()){Require(Str(row,"OPNSFTEAMCODE")=="3060000"&&keys.Add(Str(row,"MGTNO"))&&Str(row,"MGTNO").Length>0,"MedicalDuplicateOrDistrictMismatch");}
                        if(pageNo==1)firstHash=f.Hash;
                        if(received<100)
                        {
                            ended=true;form["ibpage"]="1";var (_,after)=await Fetch("https://data.jungnang.go.kr/openinf/sheetexec2.jsp",id+"-first-after.json",form);files.Add(after);Require(after.Hash==firstHash,"FirstPageChanged");break;
                        }
                    }
                    Require(ended,"MedicalPageLimitReached");
                }
            }
            catch(Exception ex){error=ex is InvalidDataException?ex.Message:ex.GetType().Name;}
            receipts.Add(new(id,kind,error==null?"Acquired":"Blocked",license,files,count,error));
        }
        await Save(folder,"receipt.json",receipts);
    }
    private static async Task Export(string root,string folder,PublicDataIngestionDbContext db,List<외부데이터정규화Record> rows,Dictionary<string,object?> result)
    {
        var restaurants=await db.공개인허가사업장Records.AsNoTracking().Where(x=>x.SourceId=="seoul-jungnang-open-data"&&x.SourceDatasetId=="jungnang-restaurant-status").ToListAsync();
        var selected=restaurants.Where(x=>Myeonmok(x.RoadAddress??"",x.LotAddress??"")).OrderBy(x=>x.Id).ToList();
        var file=Path.Combine(root,"artifacts/local/public-spatial/source/20260814-myeonmok/서울특별시_중랑구_음식점현황_20260224.csv");var hash=Hash(File.ReadAllBytes(file));
        Require(hash=="49e7e434b168a589d7c788e88cee50cddb1e7829db61aaeab993f50ad3864d8a","RestaurantOriginalChanged");
        var source=ReadCsv(File.ReadAllText(file,new UTF8Encoding(false,true)));
        Require(restaurants.Count==source.Count&&restaurants.All(x=>x.SourceHashSha256==hash),"RestaurantSourceCountOrHashMismatch");
        foreach(var row in selected)Require(source.Count(x=>x["인허가번호"]==row.ManagementNumber&&x["업소명"]==row.BusinessName&&x["소재지(도로명)"]==row.RoadAddress)==1,"RestaurantReadbackMismatch");
        var features=new List<object>();
        var addressMembers=new List<(string Key,string Kind,string Id,string Name)>();
        void Address(string address,string kind,string id,string name)
        {
            if(address.StartsWith("중랑구 ",StringComparison.Ordinal))address="서울특별시 "+address;
            if(address.StartsWith("서울 중랑구 ",StringComparison.Ordinal))address="서울특별시 "+address[3..];
            var key=공개사업장주소정규화Engine.NormalizeRoadAddress(address);
            if(key!=null)addressMembers.Add((key,kind,id,name));
        }
        foreach(var row in rows){var o=JsonSerializer.Deserialize<AddressObservation>(row.TextValue)!;Address(o.RoadAddress,o.Kind,row.StableId,o.Name);}
        foreach(var row in selected)Address(row.RoadAddress??"","restaurant",row.Id.ToString(),row.BusinessName);
        features.AddRange(rows.OrderBy(x=>x.RecordKey,StringComparer.Ordinal).Select(x=>(object)new{x.StableId,x.SourceId,x.DatasetId,x.QualityCode,x.RawSnapshotId,x.SourceVersion,x.EvidenceAsOfUtc,sourceHash=x.RawSnapshot!.ContentHashSha256,observation=JsonSerializer.Deserialize<AddressObservation>(x.TextValue)}));
        var existing=await db.NormalizedRecords.AsNoTracking().Where(x=>x.RegionStableId=="region:kr:sig:11260"&&(x.MetricCode.StartsWith("public-facility-")||x.DatasetId==전통시장표본Parser.DatasetId)).Include(x=>x.RawSnapshot).ToListAsync();
        var prior=new List<object>();
        foreach(var row in existing)
        {
            using var doc=JsonDocument.Parse(row.TextValue);var e=doc.RootElement;var road=Str(e,"RoadAddress");var lot=Str(e,"LotAddress");if(road.Length==0)road=Str(e,"RDNMADR");if(lot.Length==0)lot=Str(e,"LNMADR");
            if(!Myeonmok(road,lot))continue;
            Require(row.RawSnapshot!=null,"PriorRawMissing");var rawPath=Path.GetFullPath(Path.Combine(root,row.RawSnapshot!.StorageObjectName));Require(rawPath.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"PriorRawOutsideRepository");SafePath(rawPath);
            Require(Hash(File.ReadAllBytes(rawPath))==row.RawSnapshot.ContentHashSha256,"PriorRawChanged");
            prior.Add(new{row.StableId,row.SourceId,row.DatasetId,row.EvidenceAsOfUtc,row.QualityCode,row.RawSnapshotId,sourceHash=row.RawSnapshot.ContentHashSha256,observation=e.Clone()});
            var name=Str(e,"Name");if(name.Length==0)name=Str(e,"MRKT_NM");Address(road,row.MetricCode,row.StableId,name);
        }
        var addressGroups=addressMembers.GroupBy(x=>x.Key,StringComparer.Ordinal).OrderBy(x=>x.Key,StringComparer.Ordinal).Select(g=>new{addressKey=g.Key,method=공개사업장주소정규화Engine.RuleRevision,relation="SharedAddressCandidateNotVerifiedOccupancy",members=g.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(x=>new{x.Kind,x.Id,x.Name}).ToArray()}).ToArray();
        var output="address-review-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json";
        await Save(folder,output,new{schema="myeonmok-address-review.r1",scope="면목동 우선. 사가정역·면목역 중심 후속 공간연결; 역세권 경계 판정 아님",quality="PendingHumanReview",wgs84Coordinates=(object?)null,unityApplied=false,
            limitations=new[]{"주소는 실제 입주·출입구·통행 증명이 아님","신규 병의원 좌표는 원천 EPSG:5174 문자열이며 미변환","공동주택은 원자료 행정동 이름에 따른 선택;30세대 미만 미포함","페이지 단위 조회이며 전체 동시 스냅샷은 미보장"},features,priorFacilities=prior,addressGroups,
            restaurants=selected.Select(x=>new{x.Id,x.SourceId,x.SourceDatasetId,x.SourceRevision,x.SourceHashSha256,x.ManagementNumber,x.BusinessName,x.RoadAddress,x.NormalizedRoadAddressKey,x.BusinessTypeName,x.BusinessStatusName,quality="ExistingSourceReview;CurrentOperationUnknown"})});
        result["restaurantSourceRows"]=restaurants.Count;result["myeonmokRestaurants"]=selected.Count;result["priorFacilities"]=prior.Count;result["exportFile"]=output;
        result["addressGroups"]=addressGroups.Length;result["addresslessRows"]=rows.Count+selected.Count+prior.Count-addressMembers.Count;
        result["medicalStatuses"]=rows.Select(x=>JsonSerializer.Deserialize<AddressObservation>(x.TextValue)!).Where(x=>x.Kind!="housing").GroupBy(x=>new{x.Kind,x.OperatingStatus}).Select(g=>new{g.Key,Count=g.Count()});
    }
    private static int SelfTest()
    {
        var checks=0;void Check(bool ok){Require(ok,"SelfTestFailed:"+checks);checks++;}
        Check(Myeonmok("서울특별시 중랑구 사가정로 1 (면목동)",""));Check(!Myeonmok("서울특별시 중랑구 면목로 1 (상봉동)",""));Check(!Myeonmok("서울특별시 다른구 길 1 (면목동)",""));Check(Myeonmok("","서울특별시 중랑구 면목동 1-2"));Check(!Myeonmok("","서울특별시 중랑구 면목동쪽 1"));
        Check(ReadCsv("a,b\r\n\"x,y\",\"a\"\"b\"\r\n").Single()["b"]=="a\"b");Check(ReadCsv("a,b\n1,\n").Single()["b"]=="");
        foreach(var input in new[]{"a,a\n1,2", "a,b\n1,2,3"}){var failed=false;try{ReadCsv(input);}catch(InvalidDataException){failed=true;}Check(failed);}
        var file=new Evidence("test","https://example.invalid/","",0,new DateTimeOffset(2026,9,8,0,0,0,TimeSpan.Zero));
        var housing=new Dictionary<string,string>{{"연번","1"},{"단지명","시험단지"},{"동명","면목본동"},{"번지수","1"},{"소재지도로명주소","중랑구 시험로 1"},{"동수","1"},{"최고층수","0"},{"세대수",""},{"준공일",""}};
        var house=Housing(housing,file)!;var observation=JsonSerializer.Deserialize<AddressObservation>(house.TextValue)!;
        Check(observation.Attributes["최고층수"]=="0"&&observation.Attributes["세대수"]=="");Check(house.QualityCode=="PendingHumanReview"&&house.NumericValue==null&&observation.CoordinateX==null);
        Check(house.RecordKey==Housing(housing,file)!.RecordKey);housing["동명"]="상봉1동";Check(Housing(housing,file)==null);
        const string fixture="{\"OPNSFTEAMCODE\":\"3060000\",\"MGTNO\":\"test\",\"BPLCNM\":\"시험의원\",\"RDNWHLADDR\":\"서울특별시 중랑구 시험로 1 (면목동)\",\"TRDSTATENM\":\"폐업\",\"DCBYMD\":\"2001-01-01\",\"X\":\"\",\"Y\":\"0\"}";
        using var medical=JsonDocument.Parse(fixture);var receipt=new Receipt("OA-16220","clinic","Acquired","공공누리 제1유형",[],1,null);var m=Medical(receipt,medical.RootElement,file)!;
        var mo=JsonSerializer.Deserialize<AddressObservation>(m.TextValue)!;Check(mo.OperatingStatus=="폐업"&&mo.ClosureDate=="2001-01-01");Check(mo.CoordinateX==""&&mo.CoordinateY=="0"&&m.SpatialPrecisionCode=="address-selected-no-geocoding");Check(m.TemporalPrecisionCode=="retrieval-snapshot-not-source-asof");
        using var invalid=JsonDocument.Parse(fixture.Replace("3060000","9999999"));var rejected=false;try{Medical(receipt,invalid.RootElement,file);}catch(InvalidDataException){rejected=true;}Check(rejected);
        var time=DateTimeOffset.Parse("2026-09-08T09:54:38.7749848Z",CultureInfo.InvariantCulture);Check(DbTime(time).ToString("O",CultureInfo.InvariantCulture)=="2026-09-08T09:54:38.7749840+00:00");Check(DbTime(DbTime(time))==DbTime(time));Check(DbTime(time.AddTicks(10))!=DbTime(time));
        return checks;
    }
}
