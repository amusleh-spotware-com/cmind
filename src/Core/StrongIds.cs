using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core;

public interface IStronglyTypedId<TSelf> where TSelf : IStronglyTypedId<TSelf>
{
    Guid Value { get; }
    static abstract TSelf New();
    static abstract TSelf From(Guid value);
}

public sealed class StrongIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var g))
            return TId.From(g);
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("Value", out var v) && v.TryGetGuid(out var g2))
                return TId.From(g2);
        }
        throw new JsonException($"Invalid {typeof(TId).Name}");
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

public sealed class StrongIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsValueType) return false;
        foreach (var i in typeToConvert.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>))
                return true;
        return false;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(
            typeof(StrongIdJsonConverter<>).MakeGenericType(typeToConvert))!;
}

public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct CtidId(Guid Value) : IStronglyTypedId<CtidId>
{
    public static CtidId New() => new(Guid.NewGuid());
    public static CtidId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct TradingAccountId(Guid Value) : IStronglyTypedId<TradingAccountId>
{
    public static TradingAccountId New() => new(Guid.NewGuid());
    public static TradingAccountId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct CBotId(Guid Value) : IStronglyTypedId<CBotId>
{
    public static CBotId New() => new(Guid.NewGuid());
    public static CBotId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct CBotSourceProjectId(Guid Value) : IStronglyTypedId<CBotSourceProjectId>
{
    public static CBotSourceProjectId New() => new(Guid.NewGuid());
    public static CBotSourceProjectId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ParamSetId(Guid Value) : IStronglyTypedId<ParamSetId>
{
    public static ParamSetId New() => new(Guid.NewGuid());
    public static ParamSetId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct NodeId(Guid Value) : IStronglyTypedId<NodeId>
{
    public static NodeId New() => new(Guid.NewGuid());
    public static NodeId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct InstanceId(Guid Value) : IStronglyTypedId<InstanceId>
{
    public static InstanceId New() => new(Guid.NewGuid());
    public static InstanceId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct McpApiKeyId(Guid Value) : IStronglyTypedId<McpApiKeyId>
{
    public static McpApiKeyId New() => new(Guid.NewGuid());
    public static McpApiKeyId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct AgentMandateId(Guid Value) : IStronglyTypedId<AgentMandateId>
{
    public static AgentMandateId New() => new(Guid.NewGuid());
    public static AgentMandateId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct AgentProposalId(Guid Value) : IStronglyTypedId<AgentProposalId>
{
    public static AgentProposalId New() => new(Guid.NewGuid());
    public static AgentProposalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct AlertRuleId(Guid Value) : IStronglyTypedId<AlertRuleId>
{
    public static AlertRuleId New() => new(Guid.NewGuid());
    public static AlertRuleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct AlertEventId(Guid Value) : IStronglyTypedId<AlertEventId>
{
    public static AlertEventId New() => new(Guid.NewGuid());
    public static AlertEventId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct PropRuleId(Guid Value) : IStronglyTypedId<PropRuleId>
{
    public static PropRuleId New() => new(Guid.NewGuid());
    public static PropRuleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct Email
{
    public string Value { get; }
    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("Invalid email", nameof(value));
        Value = value.Trim();
    }
    public string Normalized => Value.ToUpperInvariant();
    public override string ToString() => Value;
}

public readonly record struct Symbol
{
    public string Value { get; }
    public Symbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Invalid symbol", nameof(value));
        Value = value.Trim().ToUpperInvariant();
    }
    public override string ToString() => Value;
}

public readonly record struct Timeframe
{
    public string Value { get; }
    public Timeframe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Invalid timeframe", nameof(value));
        Value = value.Trim().ToLowerInvariant();
    }
    public override string ToString() => Value;
}

public readonly record struct DockerImageTag
{
    public string Value { get; }
    public DockerImageTag(string value)
    {
        Value = string.IsNullOrWhiteSpace(value) ? Constants.DockerImages.DefaultTag : value.Trim();
    }
    public override string ToString() => Value;
    public static DockerImageTag Latest => new(Constants.DockerImages.DefaultTag);
}

public readonly record struct NodeEndpointUrl
{
    public string Value { get; }
    public NodeEndpointUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new Domain.DomainException(Constants.DomainErrors.NodeEndpointUrlInvalid);
        Value = value.TrimEnd('/');
    }
    public override string ToString() => Value;
}

public readonly record struct ClusterJoinToken
{
    public string Value { get; }
    public ClusterJoinToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < Constants.NodeAgentAuth.MinSecretLength)
            throw new Domain.DomainException(Constants.DomainErrors.JoinTokenTooShort);
        Value = value;
    }
    public override string ToString() => Value;
}
