using CTraderOpenApi.Messages;

namespace CTraderOpenApi.Client;

public sealed record OpenApiAccountInfo(long CtidTraderAccountId, long TraderLogin, bool IsLive, string? Broker);

public sealed record OpenApiGrant(long CtidUserId, IReadOnlyList<OpenApiAccountInfo> Accounts);

public interface IOpenApiConnectionFactory
{
    OpenApiConnection Create(bool live, string clientId, string clientSecret);
}

public interface IOpenApiClient
{
    Task<OpenApiGrant> LoadGrantAsync(string clientId, string clientSecret, string accessToken, CancellationToken ct);
}

public sealed class OpenApiClient(IOpenApiConnectionFactory connectionFactory) : IOpenApiClient
{
    public async Task<OpenApiGrant> LoadGrantAsync(
        string clientId, string clientSecret, string accessToken, CancellationToken ct)
    {
        await using var connection = connectionFactory.Create(live: true, clientId, clientSecret);
        await connection.StartAsync(ct);

        var profileResponse = await connection.SendAsync(
            new ProtoOAGetCtidProfileByTokenReq { AccessToken = accessToken },
            (int)ProtoOAPayloadType.ProtoOaGetCtidProfileByTokenReq, ct);
        var ctidUserId = ProtoOAGetCtidProfileByTokenRes.Parser.ParseFrom(profileResponse.Payload).Profile.UserId;

        var accountsResponse = await connection.SendAsync(
            new ProtoOAGetAccountListByAccessTokenReq { AccessToken = accessToken },
            (int)ProtoOAPayloadType.ProtoOaGetAccountsByAccessTokenReq, ct);
        var accounts = ProtoOAGetAccountListByAccessTokenRes.Parser.ParseFrom(accountsResponse.Payload)
            .CtidTraderAccount
            .Select(a => new OpenApiAccountInfo((long)a.CtidTraderAccountId, a.TraderLogin, a.IsLive, a.BrokerTitleShort))
            .ToList();

        return new OpenApiGrant(ctidUserId, accounts);
    }
}
