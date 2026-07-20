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

    public const string DashboardWidgetUnknown = "domain.dashboard.widget_unknown";

    public const string AlertRuleDisabled = "domain.alert.rule_disabled";
    public const string MaxConcurrentOutOfRange = "domain.prop.max_concurrent_out_of_range";
    public const string ProposalNotPending = "domain.agent.proposal_not_pending";
    public const string McpKeyAlreadyRevoked = "domain.mcp.key_already_revoked";

    public const string OpenApiClientIdRequired = "domain.openapi.client_id_required";
    public const string OpenApiSecretRequired = "domain.openapi.secret_required";
    public const string OpenApiRedirectUriInvalid = "domain.openapi.redirect_uri_invalid";
    public const string OpenApiTokenRequired = "domain.openapi.token_required";
    public const string OpenApiManagedByProvider = "domain.openapi.managed_by_provider";
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

    public const string WhiteLabelOptionUnknown = "domain.whitelabel.option_unknown";
    public const string WhiteLabelOptionNotEditable = "domain.whitelabel.option_not_editable";
    public const string WhiteLabelValueInvalid = "domain.whitelabel.value_invalid";

    public const string BrokerNameRequired = "domain.account.broker_name_required";
    public const string BrokerNotAllowed = "domain.account.broker_not_allowed";

    public const string MfaSecretRequired = "domain.mfa.secret_required";
    public const string MfaEnrollmentNotPending = "domain.mfa.enrollment_not_pending";
    public const string MfaAlreadyEnabled = "domain.mfa.already_enabled";
    public const string MfaNotEnabled = "domain.mfa.not_enabled";
    public const string MfaBackupCodesRequired = "domain.mfa.backup_codes_required";

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

    public const string ReturnSeriesTooShort = "domain.quant.return_series_too_short";
    public const string ReturnSeriesNotFinite = "domain.quant.return_series_not_finite";
    public const string TrialCountInvalid = "domain.quant.trial_count_invalid";
    public const string ProbabilityOutOfRange = "domain.quant.probability_out_of_range";
    public const string TrialSurfaceInvalid = "domain.quant.trial_surface_invalid";
    public const string ExecutionInputInvalid = "domain.execution.input_invalid";
    public const string JournalNoteTitleRequired = "domain.journal.note_title_required";
    public const string JournalNoteBodyTooLong = "domain.journal.note_body_too_long";
    public const string PositioningInvalid = "domain.signals.positioning_invalid";
    public const string SentimentScoreInvalid = "domain.signals.sentiment_score_invalid";
    public const string PointInTimeSignalInvalid = "domain.signals.point_in_time_invalid";

    public const string RiskEnvelopeInvalid = "domain.autonomy.risk_envelope_invalid";
    public const string DisclaimerConsentInvalid = "domain.autonomy.disclaimer_consent_invalid";
    public const string PerformanceTargetInvalid = "domain.autonomy.performance_target_invalid";

    public const string AgentNameRequired = "domain.agent.name_required";
    public const string AgentTransitionInvalid = "domain.agent.transition_invalid";
    public const string AgentEnvelopeRequired = "domain.agent.envelope_required";
    public const string AgentConsentRequired = "domain.agent.consent_required";
    public const string AgentNoManagedAccounts = "domain.agent.no_managed_accounts";
    public const string AgentTemperamentInvalid = "domain.agent.temperament_invalid";
    public const string AgentMemoryContentRequired = "domain.agent.memory_content_required";

    public const string VolatilityTargetInvalid = "domain.portfolio.vol_target_invalid";
    public const string KellyFractionInvalid = "domain.portfolio.kelly_fraction_invalid";
    public const string LeverageCapInvalid = "domain.portfolio.leverage_cap_invalid";
    public const string PortfolioSeriesMismatch = "domain.portfolio.series_length_mismatch";
    public const string PortfolioInsufficientStrategies = "domain.portfolio.insufficient_strategies";
    public const string PortfolioDegenerateStrategy = "domain.portfolio.degenerate_strategy";

    public const string RegistrationRoleNotAllowed = "domain.registration.role_not_allowed";
    public const string UserActivationTransitionInvalid = "domain.registration.activation_transition_invalid";
    public const string EmailVerificationTokenRequired = "domain.registration.email_verification_token_required";
    public const string ProfileCountryInvalid = "domain.profile.country_invalid";
    public const string ProfilePhoneInvalid = "domain.profile.phone_invalid";
    public const string ProfileLocaleInvalid = "domain.profile.locale_invalid";
    public const string ProfileTimeZoneInvalid = "domain.profile.timezone_invalid";
    public const string ProfileTextTooLong = "domain.profile.text_too_long";

    public const string CultureNotSupported = "domain.localization.culture_not_supported";
    public const string TimeZoneNotSupported = "domain.time.timezone_not_supported";

    public const string AiModelRequired = "domain.ai.model_required";
    public const string AiEndpointInvalid = "domain.ai.endpoint_invalid";
    public const string AiEndpointInsecure = "domain.ai.endpoint_insecure";
    public const string AiMaxTokensOutOfRange = "domain.ai.max_tokens_out_of_range";
    public const string AiProviderKindNotAllowed = "domain.ai.provider_kind_not_allowed";
    public const string AiLocalProviderNotAllowed = "domain.ai.local_provider_not_allowed";
    public const string AiBuiltInNotAllowed = "domain.ai.built_in_not_allowed";
    public const string AiRunAlreadyFinished = "domain.ai.run_already_finished";

    public const string CalendarSeriesCodeRequired = "domain.calendar.series_code_required";
    public const string CalendarCountryCodeInvalid = "domain.calendar.country_code_invalid";
    public const string CalendarCurrencyCodeInvalid = "domain.calendar.currency_code_invalid";
    public const string CalendarImpactScoreOutOfRange = "domain.calendar.impact_score_out_of_range";
    public const string CalendarRevisionOutOfOrder = "domain.calendar.revision_out_of_order";
    public const string CalendarActualBeforeRelease = "domain.calendar.actual_before_release";
    public const string CalendarEventTransitionInvalid = "domain.calendar.event_transition_invalid";
    public const string CalendarNewsWindowInvalid = "domain.calendar.news_window_invalid";
    public const string CalendarApiClientNameRequired = "domain.calendar.api_client_name_required";
    public const string CalendarApiScopeInvalid = "domain.calendar.api_scope_invalid";
    public const string CalendarWebhookUrlInvalid = "domain.calendar.webhook_url_invalid";

    public const string CurrencyNotInUniverse = "domain.currency.not_in_universe";
    public const string CurrencyUniverseEmpty = "domain.currency.universe_empty";
    public const string CurrencyUniverseDuplicate = "domain.currency.universe_duplicate";
    public const string CurrencyIndicatorOutOfRange = "domain.currency.indicator_out_of_range";
    public const string CurrencyTrajectoryOutOfRange = "domain.currency.trajectory_out_of_range";
    public const string CurrencyWeightsNotNormalized = "domain.currency.weights_not_normalized";
    public const string CurrencyPanelEmpty = "domain.currency.panel_empty";
    public const string CurrencyHorizonUnknown = "domain.currency.horizon_unknown";

    public const string CotContractCodeRequired = "domain.cot.contract_code_required";
    public const string CotMarketNameRequired = "domain.cot.market_name_required";
    public const string CotReportDateInvalid = "domain.cot.report_date_invalid";
    public const string CotOpenInterestNegative = "domain.cot.open_interest_negative";
    public const string CotPositionNegative = "domain.cot.position_negative";
    public const string CotCategoryDuplicate = "domain.cot.category_duplicate";
    public const string CotCategoryNotInReportKind = "domain.cot.category_not_in_report_kind";
    public const string CotHistoryInsufficient = "domain.cot.history_insufficient";
}
