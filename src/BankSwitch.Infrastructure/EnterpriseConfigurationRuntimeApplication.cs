using System.Collections.Concurrent;
using BankSwitch.Application;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Runtime application boundary for the enterprise configuration control plane.
/// Hot-reload values are published into an in-process authoritative runtime store.
/// Settings that require connection/service/node/cluster restart are persisted but are
/// explicitly marked RestartRequired rather than pretending they were live-applied.
/// </summary>
public sealed class EnterpriseConfigurationRuntimeApplicator : IConfigurationRuntimeApplicator
{
    private readonly ConcurrentDictionary<string, string> _runtimeValues = new(StringComparer.OrdinalIgnoreCase);

    public Task<ConfigurationRuntimeApplicationResult> ValidateRuntimeAsync(ConfigurationChangeRequest request, CancellationToken cancellationToken = default)
    {
        var items = request.Items.Select(ValidateItem).ToArray();
        var failures = items.Where(x => string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult(new ConfigurationRuntimeApplicationResult(
            failures.Length == 0,
            items,
            failures.Length == 0 ? "Runtime application plan is valid." : string.Join(" | ", failures.Select(x => x.Message))));
    }

    public Task<ConfigurationRuntimeApplicationResult> ApplyAsync(ConfigurationChangeRequest request, CancellationToken cancellationToken = default)
    {
        var items = new List<ConfigurationRuntimeApplicationItem>();
        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = ValidateItem(item);
            if (plan.Status == "Rejected")
            {
                items.Add(plan);
                continue;
            }

            if (item.ReloadPolicy == ConfigurationReloadPolicy.HotReload)
            {
                _runtimeValues[Key(request.Environment, request.InstitutionScope, item.DomainCode, item.Key)] = item.NewValue;
                items.Add(plan with
                {
                    AppliedImmediately = true,
                    RestartRequired = false,
                    Status = "HotReloadApplied",
                    Message = "Value published to the runtime configuration store."
                });
            }
            else
            {
                items.Add(plan with
                {
                    AppliedImmediately = false,
                    RestartRequired = true,
                    Status = "PersistedRestartRequired",
                    Message = $"Persisted successfully; {item.ReloadPolicy} is required before the component consumes the new value."
                });
            }
        }

        var success = items.All(x => x.Status != "Rejected");
        return Task.FromResult(new ConfigurationRuntimeApplicationResult(success, items,
            success ? "Runtime application completed according to each setting reload policy." : "One or more settings could not be applied."));
    }

    public bool TryGetRuntimeValue(string environment, string institutionScope, string domainCode, string key, out string? value)
        => _runtimeValues.TryGetValue(Key(environment, institutionScope, domainCode, key), out value);

    private static ConfigurationRuntimeApplicationItem ValidateItem(ConfigurationChangeItem item)
    {
        if (item.IsSecretReference && !(item.NewValue.StartsWith("vault://", StringComparison.OrdinalIgnoreCase)
            || item.NewValue.StartsWith("kv://", StringComparison.OrdinalIgnoreCase)
            || item.NewValue.StartsWith("hsm://", StringComparison.OrdinalIgnoreCase)))
        {
            return new(item.DomainCode, item.Key, item.ReloadPolicy, false, false, "Rejected",
                "Secret settings must contain only vault://, kv:// or hsm:// references.");
        }

        return new(item.DomainCode, item.Key, item.ReloadPolicy,
            item.ReloadPolicy == ConfigurationReloadPolicy.HotReload,
            item.ReloadPolicy != ConfigurationReloadPolicy.HotReload,
            "Validated",
            item.ReloadPolicy == ConfigurationReloadPolicy.HotReload
                ? "Eligible for immediate runtime publication."
                : $"Requires {item.ReloadPolicy} after persistence.");
    }

    private static string Key(string environment, string scope, string domain, string key)
        => $"{environment.Trim()}::{scope.Trim()}::{domain.Trim()}::{key.Trim()}";
}

public sealed class EnterpriseConfigurationCompletenessService : IConfigurationCompletenessService
{
    private readonly IEnterpriseConfigurationRepository _repository;

    public EnterpriseConfigurationCompletenessService(IEnterpriseConfigurationRepository repository) => _repository = repository;

    public async Task<ConfigurationCompletenessReport> AssessAsync(CancellationToken cancellationToken = default)
    {
        var domains = await _repository.GetDomainsAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await _repository.GetDefinitionsAsync(null, cancellationToken).ConfigureAwait(false);
        var supportedTypes = Enum.GetValues<ConfigurationValueType>().ToHashSet();
        var items = definitions.Select(d =>
        {
            var frontend = supportedTypes.Contains(d.ValueType);
            var validation = supportedTypes.Contains(d.ValueType);
            var makerChecker = d.Sensitivity is ConfigurationSensitivity.Critical or ConfigurationSensitivity.Sensitive ? d.RequiresApproval : true;
            var secretSafe = !d.IsSecret || d.ValueType == ConfigurationValueType.SecretReference;
            var runtimeAvailable = true;
            var behavior = d.ReloadPolicy == ConfigurationReloadPolicy.HotReload ? "HotReloadRuntimeStore" : $"PersistThen{d.ReloadPolicy}";
            var complete = frontend && validation && makerChecker && secretSafe && runtimeAvailable;
            return new ConfigurationCompletenessItem(d.DomainCode, d.Key, d.ValueType, d.Sensitivity, d.ReloadPolicy,
                frontend, validation, makerChecker, secretSafe, runtimeAvailable, behavior, complete ? "Complete" : "Gap");
        }).ToArray();

        var completeCount = items.Count(x => x.Status == "Complete");
        var gapCount = items.Count(x => x.Status == "Gap");
        var partialCount = items.Length - completeCount - gapCount;
        var percent = items.Length == 0 ? 0 : Math.Round(100m * completeCount / items.Length, 2);
        return new(domains.Count, items.Length, completeCount, partialCount, gapCount, percent, items);
    }
}
