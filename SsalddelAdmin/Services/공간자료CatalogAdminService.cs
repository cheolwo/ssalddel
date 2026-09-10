using System.Net.Http.Headers;
using Ssalddel.Contracts.Common.WorldProjection;

namespace SsalddelAdmin.Services;

public sealed class 공간자료CatalogAdminService(HttpClient httpClient,관리자인증세션Service session)
{
    public Task<공간자료Page> PageAsync(string action,공간자료Query query,CancellationToken ct=default)
        => GetAsync<공간자료Page>(action+Query(query),ct);
    public Task<공간자료Detail> DetailAsync(string bundleId,string documentId,bool includeSensitive=false,CancellationToken ct=default)
        => GetAsync<공간자료Detail>("documents/"+Uri.EscapeDataString(documentId)+"?bundleId="+Uri.EscapeDataString(bundleId)+"&includeSensitive="+includeSensitive.ToString().ToLowerInvariant(),ct);
    public Task<공간자료Detail> PresentationAsync(공간자료Query query,CancellationToken ct=default)
        => GetAsync<공간자료Detail>("presentation"+Query(query),ct);

    private async Task<T> GetAsync<T>(string path,CancellationToken ct)
    {
        if(!session.서버관리자인가 || string.IsNullOrWhiteSpace(session.AccessToken))throw new InvalidOperationException("서버관리자 로그인이 필요합니다.");
        using var request=new HttpRequestMessage(HttpMethod.Get,공간자료CatalogCodes.Route+"/"+path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",session.AccessToken);
        using var response=await httpClient.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode)throw new HttpRequestException($"공간자료 조회 실패 ({(int)response.StatusCode}). 이전 값이나 예시로 대체하지 않았습니다.");
        return await response.Content.ReadFromJsonAsync<T>(ct)??throw new InvalidDataException("공간자료 응답이 비어 있습니다.");
    }
    private static string Query(공간자료Query query)
    {
        var values=new Dictionary<string,string?>
        {
            ["bundleId"]=query.BundleId,["dataset"]=query.Dataset,["areaStableId"]=query.AreaStableId,
            ["kind"]=query.Kind,["revision"]=query.Revision,
            ["reviewState"]=query.ReviewState,["documentId"]=query.DocumentId,["stableId"]=query.StableId,
            ["layer"]=query.Layer,["tile"]=query.Tile,["relationKey"]=query.RelationKey,["direction"]=query.Direction,
            ["skip"]=query.Skip.ToString(),["take"]=query.Take.ToString()
        };
        return "?"+string.Join("&",values.Where(x=>!string.IsNullOrEmpty(x.Value)).Select(x=>x.Key+"="+Uri.EscapeDataString(x.Value!)));
    }
}
