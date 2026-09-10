using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ssalddel.Domain.PublicData.Korea;

// 원장의 사실은 바꾸지 않는 로컬 참고 연결. OSM 단일 주소 후보도 입주 확정이 아니다.
internal static class 면목동주소공간연결
{
    internal const string InputHash="9811BEE6CFD67B741CBD47830325C8408C3499390737AED63E960CC22645E536";
    internal const string MapHash="4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
    internal const string OutputRelative="artifacts/local/public-data/myeonmok-spatial-link-20260908-r1/connection.json";
    private const string Unique="UniqueAddressCandidateNotVerifiedOccupancy";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal sealed record Item(string id,string kind,string name,string roadAddress,string sourceId,string datasetId,string sourceHash,string revision,string evidenceAsOf,string status,string quality);
    internal sealed record Link(Item item,string addressKey,string result,string[] buildingIds);
    internal sealed record Group(string addressKey,string buildingId,string relation,Item[] records);
    internal sealed record Bundle(string schema,string inputHash,string mapHash,string mapRevision,string addressRule,string relation,
        bool privateReviewOnly,bool gameStateConnected,int inputCount,int connectedCount,int unlinkedCount,Group[] groups,Link[] links);
    private static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    private static string S(JsonElement e,string n)=>e.TryGetProperty(n,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()!:"";
    private static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
    private static void Safe(string path)
    {
        for(var p=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);p!=null;p=p.Parent)
            if(p.Exists)Require(!p.Attributes.HasFlag(FileAttributes.ReparsePoint),"LinkReparseAncestor");
        if(File.Exists(path))Require(!File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint),"LinkReparseFile");
    }
    private static byte[] Read(string path,string expected)
    {
        Safe(path);Require(new FileInfo(path).Length<=16*1024*1024,"LinkInputBudget");
        var bytes=File.ReadAllBytes(path);Require(Hash(bytes)==expected,"LinkInputHashMismatch");return bytes;
    }
    private static string Key(string address)
    {
        if(address.StartsWith("중랑구 ",StringComparison.Ordinal))address="서울특별시 "+address;
        if(address.StartsWith("서울 중랑구 ",StringComparison.Ordinal))address="서울특별시"+address[2..];
        return 공개사업장주소정규화Engine.NormalizeRoadAddress(address)??"";
    }
    internal static string Decide(string key,int count)=>key.Length==0?"AddressMissing":count==0?"NoExactBuildingInCurrentMap":count==1?Unique:"MultipleBuildingsForAddress";
    internal static Bundle Project(JsonElement data,JsonElement map)
    {
        Require(S(data,"schema")=="myeonmok-address-review.r1"&&!data.GetProperty("unityApplied").GetBoolean(),"LinkSourceSchema");
        Require(S(map,"revision")=="sagajeong-reference.r3"&&S(map,"rawSha256")=="3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3", "LinkMapRevision");
        var mapIds=new HashSet<string>(StringComparer.Ordinal);
        var buildings=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        foreach(var b in map.GetProperty("buildings").EnumerateArray())
        {
            var id=S(b,"id");Require(id.Length>0&&mapIds.Add(id),"LinkDuplicateBuilding");
            var street=S(b,"street");var number=S(b,"houseNumber");
            if(street.Length==0||!Regex.IsMatch(number,@"^\d+(?:-\d+)?$"))continue;
            var key=Key("서울특별시 중랑구 "+street+" "+number);
            if(!buildings.TryGetValue(key,out var ids))buildings.Add(key,ids=[]);
            ids.Add(id);
        }
        var items=new Dictionary<string,Item>(StringComparer.Ordinal);
        void Add(Item item){Require(item.id.Length>0&&items.TryAdd(item.id,item),"LinkDuplicateRecord");Require(Regex.IsMatch(item.sourceHash,@"^[a-fA-F0-9]{64}$"),"LinkProvenanceMissing");}
        foreach(var f in data.GetProperty("features").EnumerateArray())
        {
            var o=f.GetProperty("observation");Require(S(f,"QualityCode")=="PendingHumanReview","LinkQualityUnexpected");
            Add(new(S(f,"StableId"),S(o,"Kind"),S(o,"Name"),S(o,"RoadAddress"),S(f,"SourceId"),S(f,"DatasetId"),S(f,"sourceHash"),S(f,"SourceVersion"),S(f,"EvidenceAsOfUtc"),S(o,"OperatingStatus"),S(f,"QualityCode")));
        }
        foreach(var f in data.GetProperty("restaurants").EnumerateArray())
            Add(new(S(f,"Id"),"restaurant",S(f,"BusinessName"),S(f,"RoadAddress"),S(f,"SourceId"),S(f,"SourceDatasetId"),S(f,"SourceHashSha256"),S(f,"SourceRevision"),"2026-02-24",S(f,"BusinessStatusName"),S(f,"quality")));
        foreach(var f in data.GetProperty("priorFacilities").EnumerateArray())
        {
            var o=f.GetProperty("observation");Require(S(f,"QualityCode")=="PendingHumanReview","LinkQualityUnexpected");
            var market=S(f,"DatasetId")=="data-go-kr-15012894-standard";
            var kind=market?"traditional-market-source-observation":"public-facility-"+S(o,"Kind");
            Add(new(S(f,"StableId"),kind,market?S(o,"MRKT_NM"):S(o,"Name"),market?S(o,"RDNMADR"):S(o,"RoadAddress"),S(f,"SourceId"),S(f,"DatasetId"),S(f,"sourceHash"),market?S(o,"REFERENCE_DATE"):S(o,"RawRecordSha256"),S(f,"EvidenceAsOfUtc"),"",S(f,"QualityCode")));
        }
        // 수집 단계의 주소 그룹과 독립 재계산 결과가 같은지 대조. 유사 이름/거리로 보충하지 않는다.
        var memberIds=new HashSet<string>(StringComparer.Ordinal);var groupKeys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var g in data.GetProperty("addressGroups").EnumerateArray())
        {
            var key=S(g,"addressKey");Require(key.Length>0&&groupKeys.Add(key)&&S(g,"method")==공개사업장주소정규화Engine.RuleRevision,"LinkAddressRuleMismatch");
            foreach(var m in g.GetProperty("members").EnumerateArray())
            {
                var id=S(m,"Id");Require(items.TryGetValue(id,out var item)&&memberIds.Add(id),"LinkGroupMemberInvalid");
                Require(Key(item!.roadAddress)==key&&item.kind==S(m,"Kind")&&item.name==S(m,"Name"),"LinkGroupContentMismatch");
            }
        }
        Require(items.Values.Count(x=>Key(x.roadAddress).Length>0)==memberIds.Count,"LinkAddressMemberMissing");
        var links=items.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(item=>
        {
            var key=Key(item.roadAddress);var ids=buildings.TryGetValue(key,out var found)?found.Order(StringComparer.Ordinal).ToArray():[];
            return new Link(item,key,Decide(key,ids.Length),ids);
        }).ToArray();
        var groups=links.Where(x=>x.result==Unique).GroupBy(x=>x.addressKey,StringComparer.Ordinal).OrderBy(x=>x.Key,StringComparer.Ordinal)
            .Select(g=>new Group(g.Key,g.First().buildingIds.Single(),Unique,g.Select(x=>x.item).ToArray())).ToArray();
        var connected=groups.Sum(x=>x.records.Length);
        return new("myeonmok-building-reference.r1",InputHash,MapHash,S(map,"revision"),공개사업장주소정규화Engine.RuleRevision,Unique,true,false,items.Count,connected,items.Count-connected,groups,links);
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "build" or "test","LinkModeInvalid");
        var input=Path.Combine(root,"artifacts/local/public-data/myeonmok-address-20260908-r2/address-review-20260908T095925381.json");
        const string map="C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json";
        var sourceBytes=Read(input,InputHash);var mapBytes=Read(map,MapHash);
        using var data=JsonDocument.Parse(sourceBytes);using var geography=JsonDocument.Parse(mapBytes);
        var bundle=Project(data.RootElement,geography.RootElement);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(bundle,Json);
        result["inputCount"]=bundle.inputCount;result["connectedRecords"]=bundle.connectedCount;result["connectedBuildings"]=bundle.groups.Length;
        result["reasons"]=bundle.links.GroupBy(x=>x.result).ToDictionary(g=>g.Key,g=>g.Count());
        result["connectedKinds"]=bundle.groups.SelectMany(x=>x.records).GroupBy(x=>x.kind).ToDictionary(g=>g.Key,g=>g.Count());
        if(mode=="test")
        {
            var n=0;void Check(bool ok){Require(ok,"LinkSelfTestFailed:"+(n+1));n++;}
            Check(bundle.inputCount==2006);Check(bundle.links.Select(x=>x.item.id).Distinct().Count()==2006);
            Check(bundle.connectedCount+bundle.unlinkedCount==2006);Check(bundle.links.Count(x=>x.result=="AddressMissing")==45);
            Check(Key("중랑구 면목로 300 (면목동, 2층)")=="서울특별시 중랑구 면목로 300");
            Check(Key("서울 중랑구 면목로 300")==Key("서울특별시 중랑구 면목로 300"));
            Check(Key("서울특별시 중랑구 면목로 300-1")!=Key("서울특별시 중랑구 면목로 300"));
            Check(Decide("",0)=="AddressMissing");Check(Decide("x",0)=="NoExactBuildingInCurrentMap");
            Check(Decide("x",1)==Unique);Check(Decide("x",2)=="MultipleBuildingsForAddress");
            Check(bundle.groups.All(g=>g.records.Length>0&&g.relation==Unique));
            Check(bundle.links.Where(x=>x.result!=Unique).All(x=>!bundle.groups.SelectMany(g=>g.records).Any(i=>i.id==x.item.id)));
            Check(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(Project(data.RootElement,geography.RootElement),Json)));
            var changed=System.Text.Json.Nodes.JsonNode.Parse(sourceBytes)!;changed["features"]!.AsArray().Add(changed["features"]![0]!.DeepClone());
            bool rejected=false;using(var d=JsonDocument.Parse(changed.ToJsonString()))try{Project(d.RootElement,geography.RootElement);}catch(InvalidDataException){rejected=true;}Check(rejected);
            changed=System.Text.Json.Nodes.JsonNode.Parse(mapBytes)!;changed["revision"]="stale";rejected=false;
            using(var d=JsonDocument.Parse(changed.ToJsonString()))try{Project(data.RootElement,d.RootElement);}catch(InvalidDataException){rejected=true;}Check(rejected);
            Check(Read(input,InputHash).SequenceEqual(sourceBytes)&&Read(map,MapHash).SequenceEqual(mapBytes));
            result["testsPassed"]=n;result["outputHash"]=Hash(bytes);return;
        }
        Read(input,InputHash);Read(map,MapHash);
        var output=Path.Combine(root,OutputRelative);Safe(output);Require(!File.Exists(output),"LinkOutputExists");
        Require(bytes.Length<=16*1024*1024,"LinkOutputBudget");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await using(var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))await stream.WriteAsync(bytes);
        Require(File.ReadAllBytes(output).SequenceEqual(bytes),"LinkOutputReadbackMismatch");
        result["output"]=output;result["outputHash"]=Hash(bytes);result["databaseWriteAttempted"]=false;
    }
}
