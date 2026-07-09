namespace Core.Domain;

public sealed record OAuthStateResult(UserId UserId, OpenApiApplicationId ApplicationId, bool IsInvite);

public interface IOAuthStateService
{
    string CreateState(UserId userId, OpenApiApplicationId applicationId, TimeSpan ttl, bool isInvite);

    OAuthStateResult? Validate(string state);
}
