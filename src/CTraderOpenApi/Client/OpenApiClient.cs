using CTraderOpenApi.Messages;

namespace CTraderOpenApi.Client;

public sealed record OpenApiAccountInfo(long CtidTraderAccountId, long TraderLogin, bool IsLive, string? Broker);

public interface IOpenApiConnectionFactory
{
    OpenApiConnection Create(bool live, string clientId, string clientSecret);
}

public interface IOpenApiClient
{
    Task<IReadOnlyList<OpenApiAccountInfo>> GetAccountListAsync(
        bool live, string clientId, string clientSecret, string accessToken, CancellationToken ct);
}

public sealed class OpenApiClient(IOpenApiConnectionFactory connectionFactory) : IOpenApiClient
{
    public async Task<IReadOnlyList<OpenApiAccountInfo>> GetAccountListAsync(
        bool live, string clientId, string clientSecret, string accessToken, CancellationToken ct)
    {
        await using var connection = connectionFactory.Create(live, clientId, clientSecret);
        await connection.StartAsync(ct);

        var request = new ProtoOAGetAccountListByAccessTokenReq { AccessToken = accessToken };
        var response = await connection.SendAsync(
            request, (int)ProtoOAPayloadType.ProtoOaGetAccountsByAccessTokenReq, ct);

        var list = ProtoOAGetAccountListByAccessTokenRes.Parser.ParseFrom(response.Payload);
        return list.CtidTraderAccount
            .Select(a => new OpenApiAccountInfo((long)a.CtidTraderAccountId, a.TraderLogin, a.IsLive, a.BrokerTitleShort))
            .ToList();
    }
}
