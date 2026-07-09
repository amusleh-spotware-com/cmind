using Core.Constants;

namespace Core.Domain;

public enum OpenApiScope
{
    View = 0,
    Trade = 1
}

public readonly record struct OpenApiClientId
{
    public string Value { get; }

    public OpenApiClientId(string value)
    {
        Value = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.OpenApiClientIdRequired);
    }

    public override string ToString() => Value;
}

public readonly record struct CtidTraderAccountId
{
    public long Value { get; }

    public CtidTraderAccountId(long value)
    {
        if (value <= 0) throw new DomainException(DomainErrors.CtidTraderAccountInvalid);
        Value = value;
    }

    public override string ToString() => Value.ToString();
}

public readonly record struct OpenApiRedirectUri
{
    public string Value { get; }

    public OpenApiRedirectUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException(DomainErrors.OpenApiRedirectUriInvalid);
        Value = value;
    }

    public override string ToString() => Value;
}
