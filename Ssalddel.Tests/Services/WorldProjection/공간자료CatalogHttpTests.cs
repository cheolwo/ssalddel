using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Controllers.Admin;
using Ssalddel.Services.WorldProjection.SpatialCatalog;

namespace Ssalddel.Tests.Services.WorldProjection;

// 실제 HTTP/권한 미들웨어 + 입력 Fixture. 실제 관리자 계정 로그인이나 Mongo 증거를 대신하지 않는다.
public sealed class 공간자료CatalogHttpTests
{
    [Theory]
    [InlineData("snapshots",null,401)] [InlineData("snapshots","member",403)] [InlineData("snapshots","admin",200)]
    [InlineData("documents",null,401)] [InlineData("documents","member",403)] [InlineData("documents","admin",200)]
    [InlineData("detail",null,401)] [InlineData("detail","member",403)] [InlineData("detail","admin",200)]
    [InlineData("elements",null,401)] [InlineData("elements","member",403)] [InlineData("elements","admin",200)]
    [InlineData("relations",null,401)] [InlineData("relations","member",403)] [InlineData("relations","admin",200)]
    [InlineData("presentation",null,401)] [InlineData("presentation","member",403)] [InlineData("presentation","admin",200)]
    public async Task 모든조회경로는관리자전용이며읽기로저장상태를바꾸지않는다(string action,string? actor,int expected)
    {
        await using var host=await Host.StartAsync();var before=host.Store.Total;
        var query="?bundleId="+Uri.EscapeDataString(host.Bundle)+"&documentId="+Uri.EscapeDataString(host.Document);
        var path=action=="detail" ? "documents/"+Uri.EscapeDataString(host.Document)+query+"&includeSensitive=true" : action+query;
        using var response=await host.SendAsync(path,actor);
        Assert.Equal(expected,(int)response.StatusCode);Assert.Equal(before,host.Store.Total);
        if(expected==200)
        {
            Assert.True(response.Headers.CacheControl?.NoStore);
            var body=await response.Content.ReadAsStringAsync();
            Assert.Contains(host.Bundle,body);
            Assert.Equal(action=="detail",body.Contains("roadAddress",StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task 잘못된페이지와없는묶음_손상된DB를서로다르게반환하고쓰기경로는없다()
    {
        await using var host=await Host.StartAsync();var before=host.Store.Total;
        using var invalid=await host.SendAsync("elements?take=501","admin");Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var missing=await host.SendAsync("documents?bundleId=unknown","admin");Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);
        using var post=await host.SendAsync("documents","admin",HttpMethod.Post);Assert.Equal(HttpStatusCode.MethodNotAllowed,post.StatusCode);
        host.Store.Data[공간자료Collections.Documents].Values.First()["payload"]["secretFixture"]="DO_NOT_EXPOSE";
        using var damaged=await host.SendAsync("documents","admin");Assert.Equal(HttpStatusCode.ServiceUnavailable,damaged.StatusCode);
        var body=await damaged.Content.ReadAsStringAsync();Assert.Contains("SpatialCatalogUnavailable",body);
        Assert.DoesNotContain("DO_NOT_EXPOSE",body);Assert.DoesNotContain("SpatialStored",body);Assert.Equal(before,host.Store.Total);
    }

    [Fact]
    public async Task 취소요청은실패JSON이나예시자료로숨기지않는다()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var controller=new 공간자료CatalogController(new 공간자료CatalogService(new 공간자료CatalogTests.MemoryStore()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>controller.문서목록(new(),cancellation.Token));
    }

    private sealed class Host(WebApplication app,HttpClient client,공간자료CatalogTests.MemoryStore store,string bundle,string document):IAsyncDisposable
    {
        public 공간자료CatalogTests.MemoryStore Store {get;}=store;
        public string Bundle {get;}=bundle;
        public string Document {get;}=document;
        public async Task<HttpResponseMessage> SendAsync(string path,string? actor,HttpMethod? method=null)
        {
            using var request=new HttpRequestMessage(method??HttpMethod.Get,공간자료CatalogCodes.Route+"/"+path);
            if(actor is not null)request.Headers.Add("X-Fixture-Actor",actor);
            return await client.SendAsync(request);
        }
        public static async Task<Host> StartAsync()
        {
            var store=new 공간자료CatalogTests.MemoryStore();var service=new 공간자료CatalogService(store);
            var graph=공간자료CatalogTests.Source();
            var geometry=new 공간자료Source("unity:fixture.json","fixture","Geometry",Encoding.UTF8.GetBytes("""
                {"revision":"r1","coordinateMethod":"WGS84-ECEF-ENU-at-zero-altitude","originLatitude":37,"originLongitude":127,
                 "offsetX":0,"offsetZ":0,"halfExtent":500,"roads":[],
                 "buildings":[{"id":"building:1","name":"private-name","roadAddress":"private-address","points":[{"x":0,"z":0}]}]}
                """));
            var batch=공간자료SnapshotBuilder.Build([graph,geometry]);await service.ImportAsync(batch,default);
            var builder=WebApplication.CreateEmptyBuilder(new WebApplicationOptions{EnvironmentName="Testing"});
            builder.WebHost.UseKestrel(o=>o.Listen(IPAddress.Loopback,0));
            builder.Services.AddLogging();builder.Services.AddRouting();builder.Services.AddSingleton(service);
            builder.Services.AddControllers().ConfigureApplicationPartManager(m=>
            {m.ApplicationParts.Clear();m.FeatureProviders.Clear();m.FeatureProviders.Add(new OnlyController());});
            builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions,FixtureAuthentication>("Fixture",_=>{});
            builder.Services.AddAuthorization(o=>o.AddPolicy(공간자료CatalogCodes.Policy,p=>p.RequireRole(살뜰.Data.역할명.서버관리자)));
            var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));await app.StartAsync(timeout.Token);
            var url=Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);
            var client=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(20)};
            return new(app,client,store,batch.Snapshot["_id"].AsString,batch.Documents.Single(x=>x["kind"]=="Geometry")["_id"].AsString);
        }
        public async ValueTask DisposeAsync(){client.Dispose();await app.StopAsync(CancellationToken.None);await app.DisposeAsync();}
    }
    private sealed class OnlyController:IApplicationFeatureProvider<ControllerFeature>
    {public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature)=>feature.Controllers.Add(typeof(공간자료CatalogController).GetTypeInfo());}
    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder)
        :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor=Request.Headers["X-Fixture-Actor"].ToString();if(string.IsNullOrEmpty(actor))return Task.FromResult(AuthenticateResult.NoResult());
            var identity=new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor),
                new Claim(ClaimTypes.Role,actor=="admin"?살뜰.Data.역할명.서버관리자:"User")],Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new(identity),Scheme.Name)));
        }
    }
}
