namespace Core.Constants;

public static class DomainErrors
{
    public const string NameRequired = "domain.name.required";
    public const string EmailRequired = "domain.email.required";
    public const string SymbolRequired = "domain.symbol.required";
    public const string TimeframeRequired = "domain.timeframe.required";

    public const string RiskOutOfRange = "domain.risk.out_of_range";
    public const string DrawdownOutOfRange = "domain.drawdown.out_of_range";
    public const string IntervalOutOfRange = "domain.interval.out_of_range";
    public const string SeverityUnknown = "domain.severity.unknown";

    public const string AlertRuleDisabled = "domain.alert.rule_disabled";
    public const string MaxConcurrentOutOfRange = "domain.prop.max_concurrent_out_of_range";
    public const string ProposalNotPending = "domain.agent.proposal_not_pending";
    public const string McpKeyAlreadyRevoked = "domain.mcp.key_already_revoked";

    public const string InstanceTransitionInvalid = "domain.instance.transition_invalid";
    public const string NodeMaxInstancesInvalid = "domain.node.max_instances_invalid";
}
