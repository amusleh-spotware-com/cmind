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

    public const string OpenApiClientIdRequired = "domain.openapi.client_id_required";
    public const string OpenApiSecretRequired = "domain.openapi.secret_required";
    public const string OpenApiRedirectUriInvalid = "domain.openapi.redirect_uri_invalid";
    public const string OpenApiTokenRequired = "domain.openapi.token_required";
    public const string CtidTraderAccountInvalid = "domain.openapi.ctid_trader_account_invalid";

    public const string CopyMultiplierInvalid = "domain.copy.multiplier_invalid";
    public const string CopyLotInvalid = "domain.copy.lot_invalid";
    public const string CopySlippageInvalid = "domain.copy.slippage_invalid";
    public const string CopyDelayInvalid = "domain.copy.delay_invalid";
    public const string CopyLeverageInvalid = "domain.copy.leverage_invalid";
    public const string CopyLotBoundsInvalid = "domain.copy.lot_bounds_invalid";
    public const string CopyLotSanityInvalid = "domain.copy.lot_sanity_invalid";
    public const string CopyTradingWindowInvalid = "domain.copy.trading_window_invalid";
    public const string CopyAccountProtectionInvalid = "domain.copy.account_protection_invalid";
    public const string CopyPropRuleInvalid = "domain.copy.prop_rule_invalid";
    public const string CopyDestinationConfigLocked = "domain.copy.destination_config_locked";
    public const string CopySymbolMapCsvInvalid = "domain.copy.symbol_map_csv_invalid";
    public const string CopyRiskParameterInvalid = "domain.copy.risk_parameter_invalid";
    public const string CopySourceEqualsDestination = "domain.copy.source_equals_destination";
    public const string CopyDestinationDuplicate = "domain.copy.destination_duplicate";
    public const string CopyProfileTransitionInvalid = "domain.copy.profile_transition_invalid";
    public const string CopyNodeIdentityInvalid = "domain.copy.node_identity_invalid";
    public const string CopyOrderTypesInvalid = "domain.copy.order_types_invalid";

    public const string InstanceTransitionInvalid = "domain.instance.transition_invalid";
    public const string NodeMaxInstancesInvalid = "domain.node.max_instances_invalid";
    public const string NodeEndpointUrlInvalid = "domain.node.endpoint_url_invalid";
    public const string JoinTokenTooShort = "domain.node.join_token_too_short";

    public const string FeatureFlagUnknown = "domain.feature.flag_unknown";

    public const string BrandingColorInvalid = "domain.branding.color_invalid";

    public const string PropFirmMoneyNegative = "domain.propfirm.money_negative";
    public const string PropFirmPercentOutOfRange = "domain.propfirm.percent_out_of_range";
    public const string PropFirmTradingDaysOutOfRange = "domain.propfirm.trading_days_out_of_range";
    public const string PropFirmStartingBalanceInvalid = "domain.propfirm.starting_balance_invalid";
    public const string PropFirmChallengeNotActive = "domain.propfirm.challenge_not_active";
    public const string PropFirmEquityOutOfOrder = "domain.propfirm.equity_out_of_order";
    public const string PropFirmDrawdownThresholdInvalid = "domain.propfirm.drawdown_threshold_invalid";
    public const string PropFirmChallengeTransitionInvalid = "domain.propfirm.challenge_transition_invalid";
    public const string PropFirmNodeIdentityInvalid = "domain.propfirm.node_identity_invalid";

    public const string LegalDocumentBodyRequired = "domain.legal.body_required";
    public const string LegalDocumentVersionInvalid = "domain.legal.version_invalid";
    public const string LegalDocumentAlreadyPublished = "domain.legal.already_published";
    public const string LegalDocumentNotPublished = "domain.legal.not_published";
    public const string ConsentVersionMismatch = "domain.legal.consent_version_mismatch";
}
