using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class InterchangeFeeRuleEngine : IInterchangeFeeRuleEngine
{
    private readonly IInterchangeFeeRuleRepository _rules;

    public InterchangeFeeRuleEngine(IInterchangeFeeRuleRepository rules)
    {
        _rules = rules;
    }

    public async Task<CmsOperationResult<InterchangeFeeCalculation>> CalculateAsync(
        SettlementNetwork network,
        ClearingRecord record,
        InterchangeFeeContext context,
        CancellationToken cancellationToken = default)
    {
        var activeRules = await _rules.GetActiveRulesAsync(network, context.BusinessDate, cancellationToken).ConfigureAwait(false);
        var rule = activeRules
            .Where(r => Matches(r.ProductCode, context.ProductCode)
                && Matches(r.ChannelCode, context.ChannelCode)
                && Matches(r.MerchantCategoryCode, context.MerchantCategoryCode)
                && Matches(r.CountryCode, context.CountryCode)
                && Matches(r.CurrencyCode, context.CurrencyCode)
                && Matches(r.TransactionTypeCode, context.TransactionTypeCode))
            .OrderByDescending(Specificity)
            .ThenByDescending(r => r.Priority)
            .FirstOrDefault();

        if (rule is null)
        {
            return CmsOperationResult<InterchangeFeeCalculation>.Success(new InterchangeFeeCalculation
            {
                Network = network,
                RuleCode = "NO-RULE",
                InterchangeFeeAmount = 0m,
                SchemeFeeAmount = 0m,
                Direction = InterchangeDirection.Waived,
                Explanation = "No active interchange fee rule matched; fee waived."
            });
        }

        var fee = rule.FlatFee + (record.TransactionAmount * rule.PercentFee / 100m);
        if (rule.MinimumFee > 0m && fee < rule.MinimumFee) fee = rule.MinimumFee;
        if (rule.MaximumFee > 0m && fee > rule.MaximumFee) fee = rule.MaximumFee;
        fee = Math.Round(fee, 2, MidpointRounding.AwayFromZero);

        return CmsOperationResult<InterchangeFeeCalculation>.Success(new InterchangeFeeCalculation
        {
            Network = network,
            RuleCode = rule.RuleCode,
            InterchangeFeeAmount = fee,
            SchemeFeeAmount = 0m,
            Direction = rule.Direction,
            Explanation = $"{rule.RuleCode}: flat={rule.FlatFee}, percent={rule.PercentFee}, min={rule.MinimumFee}, max={rule.MaximumFee}, direction={rule.Direction}"
        });
    }

    private static bool Matches(string ruleValue, string actual) =>
        string.IsNullOrWhiteSpace(ruleValue) || ruleValue == "*" || string.Equals(ruleValue.Trim(), (actual ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static int Specificity(InterchangeFeeRule r) =>
        new[] { r.ProductCode, r.ChannelCode, r.MerchantCategoryCode, r.CountryCode, r.CurrencyCode, r.TransactionTypeCode }
            .Count(v => !string.IsNullOrWhiteSpace(v) && v != "*");
}
