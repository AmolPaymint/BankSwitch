using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Xunit;

namespace BankSwitch.Tests;

public sealed class IsoValidationTests
{
    [Fact]
    public async Task Valid_purchase_passes_strict_validation()
    {
        var store = new InMemorySwitchStore();
        var validator = new StrictIso8583Validator(store, new SystemClock());
        var source = await store.GetSourceNodeAsync("SRC-DEV-001");
        var result = await validator.ValidateAsync(TestMessages.ValidPurchase(), source!);
        Assert.True(result.IsValid, result.Reason);
    }

    [Fact]
    public async Task Invalid_pan_fails_luhn()
    {
        var store = new InMemorySwitchStore();
        var validator = new StrictIso8583Validator(store, new SystemClock());
        var source = await store.GetSourceNodeAsync("SRC-DEV-001");
        var message = TestMessages.ValidPurchase().SetField(2, "5399830000000000");
        var result = await validator.ValidateAsync(message, source!);
        Assert.False(result.IsValid);
        Assert.Equal("14", result.ResponseCode);
    }

    [Fact]
    public async Task Invalid_amount_fails()
    {
        var store = new InMemorySwitchStore();
        var validator = new StrictIso8583Validator(store, new SystemClock());
        var source = await store.GetSourceNodeAsync("SRC-DEV-001");
        var message = TestMessages.ValidPurchase().SetField(4, "ABC");
        var result = await validator.ValidateAsync(message, source!);
        Assert.False(result.IsValid);
        Assert.Equal("13", result.ResponseCode);
    }

    [Fact]
    public async Task Unauthorized_channel_fails()
    {
        var store = new InMemorySwitchStore();
        var validator = new StrictIso8583Validator(store, new SystemClock());
        var source = await store.GetSourceNodeAsync("SRC-DEV-001");
        var message = TestMessages.ValidPurchase().SetField(123, "000000000000099");
        var result = await validator.ValidateAsync(message, source!);
        Assert.False(result.IsValid);
        Assert.Equal("58", result.ResponseCode);
    }
}
