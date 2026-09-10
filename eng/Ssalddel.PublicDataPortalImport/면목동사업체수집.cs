using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.VisualBasic.FileIO;

internal static class 면목동사업체수집
{
    internal const string Relative="artifacts/local/public-data/myeonmok-business-20260908-r1";
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    internal static void Require(bool value,string code){if(!value)throw new InvalidDataException(code);}
    internal static void Safe(string path)
    {
        for(var d=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);d!=null;d=d.Parent)
            if(d.Exists)Require(!d.Attributes.HasFlag(FileAttributes.ReparsePoint),"ReparseAncestor");
        if(File.Exists(path))Require(!File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint),"ReparseFile");
    }
    internal static async Task Save(string path,object value)
    {
        Safe(path);var b=JsonSerializer.SerializeToUtf8Bytes(value,Json);Require(b.Length<=32*1024*1024,"JsonBudget");
        await using var f=new FileStream(path,FileMode.CreateNew);await f.WriteAsync(b);
    }
    internal sealed record FileEvidence(string File,string Url,string Sha256,long Bytes,DateTimeOffset CollectedAtUtc);
    internal sealed record Acquisition(string Kind,string Status,string? Error,string License,List<FileEvidence> Files);
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        if(mode!="acquire"){await 면목동사업체반입.RunAsync(mode,root,result);return;}
        var folder=Path.Combine(root,Relative);Safe(Path.Combine(folder,"receipt.json"));Require(!Directory.Exists(folder),"AcquisitionExists");Directory.CreateDirectory(folder);
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mirror-PublicDataReview/1.0");
        long total=0;
        async Task<FileEvidence> Fetch(string url,string name,long cap,int seconds,Dictionary<string,string>? form=null)
        {
            using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            using var req=new HttpRequestMessage(form==null?HttpMethod.Get:HttpMethod.Post,url);if(form!=null)req.Content=new FormUrlEncodedContent(form);
            using var response=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            Require(response.IsSuccessStatusCode,"HttpStatus:"+(int)response.StatusCode);
            Require(response.Content.Headers.ContentLength is not long length||length<=cap,"ContentLengthBudget");
            var path=Path.Combine(folder,name);Safe(path);await using var output=new FileStream(path,FileMode.CreateNew);await using var input=await response.Content.ReadAsStreamAsync(cancel.Token);
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];long size=0;int n;
            while((n=await input.ReadAsync(buffer,cancel.Token))>0){size+=n;total+=n;Require(size<=cap&&total<=405L*1024*1024,"DownloadBudget");hash.AppendData(buffer,0,n);await output.WriteAsync(buffer.AsMemory(0,n),cancel.Token);}
            Require(size>0,"EmptyDownload");return new(name,url,Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),size,DateTimeOffset.UtcNow);
        }
        var receipt=new List<Acquisition>();
        foreach(var kind in new[]{"factory","shop"})
        {
            var files=new List<FileEvidence>();var license="";string? error=null;
            try
            {
                var id=kind=="factory"?"15034963":"15083033";var date=kind=="factory"?"20260224":"20260630";
                files.Add(await Fetch($"https://www.data.go.kr/catalog/{id}/fileData.json",kind+"-metadata.json",2*1024*1024,30));
                using var meta=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,kind+"-metadata.json")));
                Require(meta.RootElement.GetProperty("license").GetString()=="이용허락범위 제한 없음"&&meta.RootElement.GetProperty("alternateName").GetString()!.EndsWith("_"+date,StringComparison.Ordinal),"SourceVersionOrLicenseChanged");license="이용허락범위 제한 없음";
                files.Add(await Fetch($"https://www.data.go.kr/data/{id}/fileData.do",kind+"-page.html",2*1024*1024,30));
                if(kind=="factory")
                {
                    const string url="https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003602535&fileDetailSn=1&insertDataPrcus=N";
                    Require(File.ReadAllText(Path.Combine(folder,"factory-page.html")).Contains(url,StringComparison.Ordinal),"FactoryLinkChanged");
                    files.Add(await Fetch(url,"factory.csv",2*1024*1024,30));
                }
                else
                {
                    var form=new Dictionary<string,string>{{"publicDataPk",id},{"publicDataDetailPk","uddi:b3094bc9-8756-4ecc-9141-9144b98a531e"},{"atchFileId",""},{"fileDetailSn","1"},{"publicDataTyCode","PR0051"}};
                    files.Add(await Fetch("https://www.data.go.kr/tcs/dss/selectFileDataDownload.do","shop-download-metadata.json",2*1024*1024,30,form));
                    using var download=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"shop-download-metadata.json")));
                    Require(download.RootElement.GetProperty("status").GetBoolean()&&download.RootElement.GetProperty("atchFileId").GetString()=="FILE_000000003695831"&&download.RootElement.GetProperty("fileDetailSn").GetString()=="1","ShopLinkChanged");
                    files.Add(await Fetch("https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003695831&fileDetailSn=1&insertDataPrcus=N","shops-national.zip",390L*1024*1024,240));
                }
            }
            catch(Exception ex){error=ex is InvalidDataException?ex.Message:ex.GetType().Name;}
            receipt.Add(new(kind,error==null?"Acquired":"Blocked",error,license,files));
        }
        await Save(Path.Combine(folder,"receipt.json"),receipt);result["acquisition"]=receipt;result["folder"]=Relative;
    }
    internal static IEnumerable<Dictionary<string,string>> Csv(TextReader reader)
    {
        using var csv=new TextFieldParser(reader);csv.SetDelimiters(",");csv.HasFieldsEnclosedInQuotes=true;csv.TrimWhiteSpace=false;
        var headers=csv.ReadFields()??throw new InvalidDataException("CsvHeaders");Require(headers.Length>1&&headers.Distinct(StringComparer.Ordinal).Count()==headers.Length,"CsvHeaders");
        while(!csv.EndOfData){var values=csv.ReadFields()!;Require(values.Length==headers.Length,"CsvColumns");yield return headers.Zip(values).ToDictionary(x=>x.First,x=>x.Second,StringComparer.Ordinal);}
    }
}
