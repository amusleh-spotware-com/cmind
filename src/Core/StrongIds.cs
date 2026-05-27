using System.ComponentModel;
using System.Globalization;

namespace Core;

public interface IStronglyTypedId<TSelf> where TSelf : IStronglyTypedId<TSelf>
{
    Guid Value { get; }
    static abstract TSelf New();
    static abstract TSelf From(Guid value);
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
