using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Controllers.Admin;
using Ssalddel.Extensions;

// 기존 앱 컨테이너와 별개인 수동 로컬 검증 전용 host. 실제 Controller/Store를 쓰되
// 다른 업무 API·background job·DB 초기화·로그인 계정 등록은 실행하지 않는다.
internal static class 공간자료Catalog검증Host
{
    public static async Task RunAsync(string root,bool serveAdmin=true)
    {
        var mongo=await 공간자료CatalogImport.ConnectAsync(root);
        var signingKey=new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var issuer="spatial-catalog-local-validation";
        var token=Token("서버관리자"); var memberToken=Token("User");
        var builder=WebApplication.CreateEmptyBuilder(new WebApplicationOptions{EnvironmentName="Development",ContentRootPath=root});
        builder.WebHost.UseKestrel(o=>o.Listen(IPAddress.Loopback,5298));
        builder.Services.AddLogging(o=>o.AddSimpleConsole());builder.Services.AddRouting();
        builder.Services.AddSingleton<IMongoClient>(mongo.Client);
        builder.Services.Configure<살뜰.Services.Options.MongoDbOptions>(o=>o.Database=mongo.Options.Database);
        builder.Services.AddSpatialCatalog();
        builder.Services.AddControllers().ConfigureApplicationPartManager(m=>
        {m.ApplicationParts.Clear();m.FeatureProviders.Clear();m.FeatureProviders.Add(new OnlyCatalogController());});
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>
        {
            o.MapInboundClaims=false;
            o.TokenValidationParameters=new TokenValidationParameters
            {
                ValidateIssuer=true,ValidIssuer=issuer,ValidateAudience=true,ValidAudience=issuer,
                ValidateLifetime=true,ClockSkew=TimeSpan.FromSeconds(5),ValidateIssuerSigningKey=true,IssuerSigningKey=signingKey,
                RoleClaimType="role",NameClaimType="sub"
            };
        });
        builder.Services.AddAuthorization(o=>o.AddPolicy(공간자료CatalogCodes.Policy,p=>p.RequireRole("서버관리자")));
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(25));
        await app.StartAsync(deadline.Token);
        Process? admin=null;
        try
        {
            var evidence=await VerifyHttpAsync(token,memberToken,deadline.Token);
            var folder=Path.Combine(root,공간자료CatalogImport.ArtifactRelative);
            공간자료CatalogImport.SaveExact(Path.Combine(folder,"http-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffff")+".json"),JsonSerializer.SerializeToUtf8Bytes(evidence,new JsonSerializerOptions{WriteIndented=true}));
            if(!serveAdmin)
            {
                Console.WriteLine(JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
                return;
            }
            // 기존 appsettings.Local.json/다른 개발 로그인 값을 읽지 않는 독립 content root.
            // 제품 설정 우선순위를 바꾸거나 토큰을 명령행·파일에 기록하지 않는다.
            var start=new ProcessStartInfo("dotnet") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder};
            start.ArgumentList.Add(Path.Combine(root,"SsalddelAdmin/bin/Debug/net10.0/SsalddelAdmin.dll"));
            start.ArgumentList.Add("--urls");start.ArgumentList.Add("http://127.0.0.1:5299");
            start.ArgumentList.Add("--contentRoot");start.ArgumentList.Add(folder);
            start.ArgumentList.Add("--webroot");start.ArgumentList.Add(Path.Combine(root,"SsalddelAdmin/wwwroot"));
            start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
            start.Environment["AdminApi__BaseUrl"]="http://127.0.0.1:5298/";
            start.Environment["AdminAuth__DevelopmentBootstrap__Enabled"]="true";
            start.Environment["AdminAuth__DevelopmentBootstrap__AccessToken"]=token;
            start.Environment["AdminAuth__DevelopmentBootstrap__UserId"]="spatial-catalog-validation";
            start.Environment["AdminAuth__DevelopmentBootstrap__UserName"]="공간자료 로컬 검증";
            start.Environment["AdminAuth__DevelopmentBootstrap__Roles__0"]="서버관리자";
            admin=Process.Start(start)??throw new InvalidOperationException("SpatialAdminStartFailed");
            Console.WriteLine("SPATIAL_CATALOG_READY API=http://127.0.0.1:5298 WEB=http://127.0.0.1:5299/spatial-catalog; ephemeral test identity; no account created.");
            // 수동 종료 신호는 이 검증 실행만 소유한다. 값/키는 파일로 저장하지 않는다.
            var stop=Path.Combine(folder,"stop-local-host");
            while(!deadline.IsCancellationRequested && !File.Exists(stop) && !admin.HasExited) await Task.Delay(1000,deadline.Token);
        }
        catch(OperationCanceledException) when(deadline.IsCancellationRequested) { }
        finally
        {
            await app.StopAsync(CancellationToken.None);
            if(admin is {HasExited:false}) {admin.Kill(entireProcessTree:true);await admin.WaitForExitAsync();}
            admin?.Dispose();
            Console.WriteLine("SPATIAL_CATALOG_LOCAL_HOST_STOPPED");
        }

        string Token(string role)=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer,issuer,
            [new Claim("sub","spatial-catalog-validation"),new Claim("role",role)],DateTime.UtcNow.AddSeconds(-5),DateTime.UtcNow.AddMinutes(26),
            new SigningCredentials(signingKey,SecurityAlgorithms.HmacSha256)));
    }

    private static async Task<object> VerifyHttpAsync(string token,string memberToken,CancellationToken ct)
    {
        using var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false};
        using var client=new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1:5298/"),Timeout=TimeSpan.FromSeconds(30)};
        var route=공간자료CatalogCodes.Route;
        using var anon=await client.GetAsync(route+"/documents",ct);Require(anon.StatusCode==HttpStatusCode.Unauthorized,"SpatialAnonymousNotDenied");
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",memberToken);
        using var member=await client.GetAsync(route+"/documents",ct);Require(member.StatusCode==HttpStatusCode.Forbidden,"SpatialMemberNotDenied");
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        var page=await client.GetFromJsonAsync<공간자료Page>(route+"/documents?take=100",ct)??throw new InvalidDataException("SpatialHttpEmpty");
        Require(page.Total==32 && page.Items.Count==32,"SpatialHttpDocumentCount");
        var bundle=Uri.EscapeDataString(page.BundleId);
        var myeonmokArea=Uri.EscapeDataString(동별공간PackageCatalog.MyeonmokArea);
        var junghwaArea=Uri.EscapeDataString(동별공간PackageCatalog.JunghwaArea);
        var myeonmokDocuments=await client.GetFromJsonAsync<공간자료Page>(route+"/documents?bundleId="+bundle+"&areaStableId="+myeonmokArea+"&take=100",ct);
        var junghwaDocuments=await client.GetFromJsonAsync<공간자료Page>(route+"/documents?bundleId="+bundle+"&areaStableId="+junghwaArea+"&take=100",ct);
        var myeonmokDocumentPage=myeonmokDocuments??throw new InvalidDataException("SpatialHttpMyeonmokDocumentsMissing");
        var junghwaDocumentPage=junghwaDocuments??throw new InvalidDataException("SpatialHttpJunghwaDocumentsMissing");
        Require(myeonmokDocumentPage.Total==11 && junghwaDocumentPage.Total==2,"SpatialHttpAreaDocumentFilter");
        var geometry=page.Items.Single(x=>x.GetProperty("kind").GetString()=="Geometry").GetProperty("_id").GetString()!;
        var index=page.Items.Single(x=>x.GetProperty("kind").GetString()=="SpatialIndex").GetProperty("_id").GetString()!;
        var query=$"?bundleId={bundle}&documentId={Uri.EscapeDataString(geometry)}&layer=buildings&take=500";
        var first=await client.GetFromJsonAsync<공간자료Page>(route+"/elements"+query,ct);
        var second=await client.GetFromJsonAsync<공간자료Page>(route+"/elements"+query+"&skip=500",ct);
        Require(first?.Total==602 && first.Items.Count==500 && second?.Items.Count==102 && second.BundleId==first.BundleId,"SpatialHttpPaginationMismatch");
        Require(first!.Items.Concat(second!.Items).Select(x=>x.GetProperty("_id").GetString()).Distinct().Count()==602,"SpatialHttpDuplicatePage");
        var detail=await client.GetFromJsonAsync<공간자료Detail>(route+"/documents/"+Uri.EscapeDataString(index)+"?bundleId="+bundle,ct);
        var json=detail!.Document.GetRawText();Require(!json.Contains("roadAddress") && !json.Contains("buildingManagementNumber") && !json.Contains("\"name\""),"SpatialHttpSensitiveLeak");
        var sensitive=await client.GetFromJsonAsync<공간자료Detail>(route+"/documents/"+Uri.EscapeDataString(index)+"?bundleId="+bundle+"&includeSensitive=true",ct);
        Require(sensitive!.SensitiveIncluded && sensitive.Document.GetProperty("payload").GetProperty("activityObservations")[0].TryGetProperty("roadAddress",out _),"SpatialAdminSensitiveDetailMissing");
        using var invalid=await client.GetAsync(route+"/elements?take=501",ct);Require((int)invalid.StatusCode==400,"SpatialHttpPageGuard");
        using var missing=await client.GetAsync(route+"/documents?bundleId=not-existing",ct);Require((int)missing.StatusCode==404,"SpatialHttpMissingBundleGuard");
        var presentation=await client.GetFromJsonAsync<공간자료Detail>(route+"/presentation"+query,ct);
        Require(presentation is {GameStateConnected:false,SensitiveIncluded:false} && presentation.BundleId==page.BundleId,"SpatialPresentationBoundary");
        var relationKey=first.Items[0].GetProperty("stableId").GetString();
        var reverse=await client.GetFromJsonAsync<공간자료Page>(route+"/relations?bundleId="+bundle+"&relationKey="+Uri.EscapeDataString(relationKey!)+"&direction=incoming",ct);
        Require(reverse is not null,"SpatialReverseLookupMissing");
        var observations=await client.GetFromJsonAsync<공간자료Page>(route+"/elements?bundleId="+bundle+"&areaStableId="+junghwaArea+"&layer=observations&take=1",ct);
        Require(observations?.Total==1686 && observations.Items.Count==1,"SpatialHttpJunghwaObservationFilter");
        var registration=await client.GetFromJsonAsync<공간자료Page>(route+"/elements?bundleId="+bundle+
            "&areaStableId="+junghwaArea+"&kind=NeighborhoodPackageRegistration&take=10",ct);
        Require(registration?.Total==1 && registration.Items.Single().GetProperty("stableId").GetString()==
            "neighborhood-spatial-package:region:kr:bjd:1126010300.v1","SpatialHttpNestedAreaFilter");
        var junghwaInventoryId=junghwaDocumentPage.Items.Single(x=>x.GetProperty("kind").GetString()=="ObservationInventory")
            .GetProperty("_id").GetString()!;
        var junghwaInventory=await client.GetFromJsonAsync<공간자료Detail>(route+"/documents/"+
            Uri.EscapeDataString(junghwaInventoryId)+"?bundleId="+bundle+"&includeSensitive=true",ct);
        var inventoryJson=junghwaInventory!.Document.GetRawText();
        Require(new[]{"roadAddress","lotAddress","shopName","상호명","도로명주소","지번주소","상가업소번호"}
            .All(field=>!inventoryJson.Contains("\""+field+"\"",StringComparison.Ordinal)),"SpatialHttpJunghwaPrivateFieldLeak");
        return new{bundleId=page.BundleId,documentCount=32,buildings=602,pages=new[]{500,102},
            myeonmokDocuments=myeonmokDocumentPage.Total,junghwaDocuments=junghwaDocumentPage.Total,
            junghwaObservations=observations!.Total,junghwaPackageRegistrations=registration!.Total,
            anonymousStatus=401,memberStatus=403,adminStatus=200,invalidPageStatus=400,missingBundleStatus=404,
            defaultSensitiveRedacted=true,explicitAdminDetail=true,junghwaInventoryMinimized=true,reverseRelations=reverse!.Total,presentationJson=true,
            authenticationEvidence="EphemeralSignedJwtFixture; real middleware; not production account login",
            mongoRead=true,mySqlWrite=false,gameStateConnected=false};
    }
    private static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    private sealed class OnlyCatalogController:IApplicationFeatureProvider<ControllerFeature>
    {public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature)=>feature.Controllers.Add(typeof(공간자료CatalogController).GetTypeInfo());}
}
