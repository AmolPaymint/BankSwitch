using BankSwitch.Application;
using BankSwitch.Domain;
using Xunit;

namespace BankSwitch.Tests;

public sealed class FeeAndReversalTests
{
    [Fact]
    public void Flat_fee_is_applied_before_percentage()
    {
        var calculator = new FeeCalculator();
        var fee = new Fee { Name = "flat", FlatAmount = 15m, PercentageOfTransaction = 10m };
        Assert.Equal(15m, calculator.Calculate(fee, 1000m));
    }

    [Fact]
    public void Percentage_fee_honors_min_and_max()
    {
        var calculator = new FeeCalculator();
        var fee = new Fee { Name = "pct", PercentageOfTransaction = 10m, Minimum = 5m, Maximum = 20m };
        Assert.Equal(20m, calculator.Calculate(fee, 1000m));
        Assert.Equal(5m, calculator.Calculate(fee, 1m));
    }

    [Fact]
    public void Reversal_backoff_matches_required_schedule()
    {
        var options = new ReversalOptions();
        Assert.Equal(TimeSpan.FromMinutes(1), options.GetBackoff(1));
        Assert.Equal(TimeSpan.FromMinutes(5), options.GetBackoff(2));
        Assert.Equal(TimeSpan.FromMinutes(15), options.GetBackoff(3));
        Assert.Equal(TimeSpan.FromHours(1), options.GetBackoff(4));
    }
}
