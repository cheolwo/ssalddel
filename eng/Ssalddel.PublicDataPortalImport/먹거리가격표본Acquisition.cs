using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

internal static class 먹거리가격표본Acquisition
{
    public const string Relative = "artifacts/local/public-data/eight-life-domains/food-20260908-r1";
    public const string Endpoint = "https://www.kamis.or.kr/service/price/xml.do";
    public const string Documentation = "https://www.kamis.or.kr/customer/reference/openapi_list.do?action=detail&boardno=1";
    public const string UnitRelative = "artifacts/local/public-data/eight-life-domains/food-unit-20260908-r1";
    public static async Task RunAsync(string root, bool unitComparison=false)
    {
        var relative=unitComparison ? UnitRelative : Relative;
        var folder = Path.Combine(root, relative);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()) throw new InvalidDataException("AcquisitionAlreadyExists");
        var secretPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft/UserSecrets/47766019-f542-4cfc-9d4a-14b2fbfeac0e/secrets.json");
        using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(secretPath));
        var key = secrets.RootElement.GetProperty("PublicData:Kamis:CertificationKey").GetString()!;
        var id = secrets.RootElement.GetProperty("PublicData:Kamis:RequesterId").GetString()!;
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("MissingCredential");
        Directory.CreateDirectory(folder);
        // 자격값은 승인된 공식 HTTPS 주소의 요청에만 사용. redirect/retry/상시 host 없음.
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var files = new List<object>();
        foreach (var cls in new[] { "01", "02" })
        foreach (var conversion in unitComparison ? new[]{"N","Y"} : new[]{"Y"})
        {
            var query = new Dictionary<string,string?> {
                ["action"]="dailyPriceByCategoryList", ["p_product_cls_code"]=cls,
                ["p_item_category_code"]="100", ["p_country_code"]="",
                ["p_regday"]="2026-09-07", ["p_convert_kg_yn"]=conversion, ["p_returntype"]="json"
            };
            var safeQuery = new Dictionary<string,string?>(query);
            query["p_cert_key"]=key; query["p_cert_id"]=id;
            using var response = await client.GetAsync(QueryHelpers.AddQueryString(Endpoint,query), HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) throw new InvalidDataException("KamisHttpStatus:"+(int)response.StatusCode);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count=await stream.ReadAsync(buffer,deadline.Token))>0) {
                if(memory.Length+count>2*1024*1024) throw new InvalidDataException("ResponseTooLarge");
                memory.Write(buffer,0,count);
            }
            var bytes=memory.ToArray(); var text=Encoding.UTF8.GetString(bytes);
            using var doc=JsonDocument.Parse(text);
            bytes=PreserveData(text,key,id);
            var suffix=cls+(unitComparison ? "-"+conversion : "");
            var filename="daily-"+suffix+".json";
            await using(var output=new FileStream(Path.Combine(folder,filename),FileMode.CreateNew)) await output.WriteAsync(bytes);
            var code=doc.RootElement.GetProperty("data").GetProperty("error_code").GetString();
            files.Add(new { filename, bytes=bytes.Length, sha256=Convert.ToHexString(SHA256.HashData(bytes)), collectedAtUtc=DateTimeOffset.UtcNow, query=safeQuery, responseCode=code,
                transformation="Original data subtree retained; top-level condition omitted to prevent credential persistence", completeHttpBodyStored=false });
            // 부분 성공도 파일은 보존하고, 최초 오류 뒤 후속 요청은 하지 않는다.
            await WriteReceiptAsync(folder,files,suffix);
            if(code!="000") throw new InvalidDataException("KamisResultCode:"+code);
        }
        Console.WriteLine(JsonSerializer.Serialize(new { acquired=files.Count, folder=relative, credentialPrinted=false, databaseWriteAttempted=false }));
    }
    internal static byte[] PreserveData(string text,string key,string id)
    {
        // condition은 자격값을 되돌려줄 수 있어 통째로 제외한다. data 원소는 원문 그대로 보존한다.
        using var doc=JsonDocument.Parse(text);
        var data=doc.RootElement.GetProperty("data");
        var decoded=JsonSerializer.Serialize(data,new JsonSerializerOptions { Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (new[]{key,id,Uri.EscapeDataString(key),Uri.EscapeDataString(id)}.Any(x=>decoded.Contains(x,StringComparison.Ordinal)))
            throw new InvalidDataException("CredentialEchoRejected");
        return Encoding.UTF8.GetBytes("{\"data\":"+data.GetRawText()+"}");
    }
    internal static int SelfTest()
    {
        const string key="fake-secret-value", id="fake-requester";
        var output=Encoding.UTF8.GetString(PreserveData("{\"condition\":{\"key\":\"fake-secret-value\"},\"data\":{\"price\":\"3,188\"}}",key,id));
        if(output!="{\"data\":{\"price\":\"3,188\"}}")throw new InvalidDataException("CredentialConditionTest");
        foreach(var value in new[]{key,id,"fake-\\u0073ecret-value"})
        {
            var rejected=false;
            try { PreserveData("{\"data\":{\"unexpected\":\""+value+"\"}}",key,id); }
            catch(InvalidDataException){rejected=true;}
            if(!rejected)throw new InvalidDataException("CredentialDataTest");
        }
        return 4;
    }
    private static async Task WriteReceiptAsync(string folder,List<object> files,string cls)
    {
        var receipt=new { endpoint=Endpoint, documentation=Documentation, provider="한국농수산식품유통공사 KAMIS", requestedDate="2026-09-07",
            use="PrivateReviewOnly", redistribution="NotVerified;NoPublication", automaticSchedule=false, files };
        await using var output=new FileStream(Path.Combine(folder,"acquisition-"+cls+".json"),FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(output,receipt,new JsonSerializerOptions { WriteIndented=true });
    }
}
