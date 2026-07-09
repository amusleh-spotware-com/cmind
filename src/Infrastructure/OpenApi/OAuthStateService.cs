using System.Text.Json;
using System.Text.Json.Serialization;
using Core;
using Core.Constants;
using Core.Domain;

namespace Infrastructure.OpenApi;

public sealed class OAuthStateService(ISecretProtector protector) : IOAuthStateService
{
    public string CreateState(UserId userId, OpenApiApplicationId applicationId, TimeSpan ttl, bool isInvite)
    {
        var payload = new StatePayload(
            userId.Value,
            applicationId.Value,
            isInvite,
            DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds(),
            Guid.NewGuid().ToString("N"));

        return protector.ProtectString(JsonSerializer.Serialize(payload), EncryptionPurposes.OpenApiOAuthState);
    }

    public OAuthStateResult? Validate(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;

        StatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StatePayload>(
                protector.UnprotectString(state, EncryptionPurposes.OpenApiOAuthState));
        }
        catch (Exception)
        {
            return null;
        }

        if (payload is null) return null;
        if (DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt) < DateTimeOffset.UtcNow) return null;

        return new OAuthStateResult(
            UserId.From(payload.UserId), OpenApiApplicationId.From(payload.ApplicationId), payload.IsInvite);
    }

    private sealed record StatePayload(
        [property: JsonPropertyName("u")] Guid UserId,
        [property: JsonPropertyName("a")] Guid ApplicationId,
        [property: JsonPropertyName("i")] bool IsInvite,
        [property: JsonPropertyName("e")] long ExpiresAt,
        [property: JsonPropertyName("n")] string Nonce);
}
