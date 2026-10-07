using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class FeeCalculator
{
    public decimal Calculate(Fee fee, decimal transactionAmount)
    {
        if (fee is null) throw new ArgumentNullException(nameof(fee));
        if (transactionAmount < 0m) throw new ArgumentOutOfRangeException(nameof(transactionAmount));

        if (fee.FlatAmount > 0m) return decimal.Round(fee.FlatAmount, 2, MidpointRounding.AwayFromZero);

        if (fee.Minimum > 0m && fee.Maximum > 0m && fee.Minimum > fee.Maximum)
            throw new InvalidOperationException(
                $"Fee '{fee.Id}' is misconfigured: Minimum ({fee.Minimum}) is greater than Maximum ({fee.Maximum}).");

        var charge = transactionAmount * fee.PercentageOfTransaction / 100m;
        charge = decimal.Round(charge, 2, MidpointRounding.AwayFromZero);

        if (fee.Minimum > 0m && charge < fee.Minimum) charge = fee.Minimum;
        if (fee.Maximum > 0m && charge > fee.Maximum) charge = fee.Maximum;

        return charge;
    }
}