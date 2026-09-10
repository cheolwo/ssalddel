using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Simulation.Infrastructure
{
    /// <summary>
    /// Remote Simulation Adapter가 공유하는 JSON 전송기다.
    /// 인증, 재시도 정책과 HttpClient 수명은 조립자가 소유한다.
    /// </summary>
    internal sealed class RemoteSimulationHttpJsonClient
    {
        private const string SessionRoutePrefix =
            "api/simulation/v1/sessions/";
        private readonly HttpClient client;
        private readonly JsonSerializerOptions jsonOptions;

        public RemoteSimulationHttpJsonClient(
            HttpClient client,
            JsonSerializerOptions? jsonOptions = null)
        {
            this.client = client
                ?? throw new ArgumentNullException(nameof(client));
            this.jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };
        }

        public async ValueTask<TResponse> PostAsync<TRequest, TResponse>(
            string sessionStableId,
            string operationPath,
            TRequest request,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException(
                    "Simulation Session 식별자가 필요합니다.",
                    nameof(sessionStableId));
            if (string.IsNullOrWhiteSpace(operationPath))
                throw new ArgumentException(
                    "Simulation 작업 경로가 필요합니다.",
                    nameof(operationPath));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var route = SessionRoutePrefix
                + Uri.EscapeDataString(sessionStableId.Trim()) + "/"
                + operationPath.TrimStart('/');
            var json = JsonSerializer.Serialize(request, jsonOptions);
            using var message = new HttpRequestMessage(HttpMethod.Post, route)
            {
                Content = new StringContent(
                    json, Encoding.UTF8, "application/json"),
            };
            using var response = await client.SendAsync(message, token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
                throw new InvalidOperationException(
                    "RemoteSimulationResponseEmpty");
            return JsonSerializer.Deserialize<TResponse>(
                    responseJson, jsonOptions)
                ?? throw new InvalidOperationException(
                    "RemoteSimulationResponseInvalid");
        }
    }
}
