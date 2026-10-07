using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryOperationalControlRepository : IOperationalControlRepository
{
    private readonly ConcurrentDictionary<Guid, AgencyProfile> _agencies = new();
    private readonly ConcurrentDictionary<string, Guid> _agencyByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<AgencyCreditLedgerEntry> _agencyCreditLedger = new();
    private readonly ConcurrentDictionary<Guid, CorporateProfile> _corporates = new();
    private readonly ConcurrentDictionary<string, Guid> _corporateByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, CorporateDepartment> _departments = new();
    private readonly ConcurrentDictionary<string, Guid> _departmentByCorporateCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, CorporateEmployee> _employees = new();
    private readonly ConcurrentDictionary<string, Guid> _employeeByCorporateNumber = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, CorporateBudget> _budgets = new();
    private readonly ConcurrentDictionary<Guid, CardStockBatch> _stockBatches = new();
    private readonly ConcurrentDictionary<string, Guid> _stockByReference = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, AdvancedLimitRule> _limitRules = new();
    private readonly ConcurrentDictionary<Guid, BankSwitch.Domain.RiskRule> _riskRules = new();
    private readonly ConcurrentDictionary<Guid, NotificationMessage> _notifications = new();
    private readonly ConcurrentDictionary<Guid, StatementDocument> _statements = new();
    private readonly ConcurrentDictionary<Guid, List<StatementLine>> _statementLines = new();

    public Task AddAgencyAsync(AgencyProfile agency, CancellationToken cancellationToken = default)
    {
        _agencies[agency.Id] = agency;
        _agencyByCode[agency.AgencyCode] = agency.Id;
        return Task.CompletedTask;
    }

    public Task UpdateAgencyAsync(AgencyProfile agency, CancellationToken cancellationToken = default)
    {
        _agencies[agency.Id] = agency;
        _agencyByCode[agency.AgencyCode] = agency.Id;
        return Task.CompletedTask;
    }

    public Task<AgencyProfile?> GetAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default)
    {
        _agencies.TryGetValue(agencyId, out var agency);
        return Task.FromResult(agency);
    }

    public Task<AgencyProfile?> GetAgencyByCodeAsync(string agencyCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_agencyByCode.TryGetValue(agencyCode ?? string.Empty, out var id) && _agencies.TryGetValue(id, out var agency) ? agency : null);
    }

    public Task AddAgencyCreditLedgerEntryAsync(AgencyCreditLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        _agencyCreditLedger.Add(entry);
        return Task.CompletedTask;
    }

    public Task AddCorporateAsync(CorporateProfile corporate, CancellationToken cancellationToken = default)
    {
        _corporates[corporate.Id] = corporate;
        _corporateByCode[corporate.CorporateCode] = corporate.Id;
        return Task.CompletedTask;
    }

    public Task UpdateCorporateAsync(CorporateProfile corporate, CancellationToken cancellationToken = default)
    {
        _corporates[corporate.Id] = corporate;
        _corporateByCode[corporate.CorporateCode] = corporate.Id;
        return Task.CompletedTask;
    }

    public Task<CorporateProfile?> GetCorporateAsync(Guid corporateId, CancellationToken cancellationToken = default)
    {
        _corporates.TryGetValue(corporateId, out var corporate);
        return Task.FromResult(corporate);
    }

    public Task<CorporateProfile?> GetCorporateByCodeAsync(string corporateCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_corporateByCode.TryGetValue(corporateCode ?? string.Empty, out var id) && _corporates.TryGetValue(id, out var corporate) ? corporate : null);
    }

    public Task AddDepartmentAsync(CorporateDepartment department, CancellationToken cancellationToken = default)
    {
        _departments[department.Id] = department;
        _departmentByCorporateCode[$"{department.CorporateId:N}:{department.DepartmentCode}"] = department.Id;
        return Task.CompletedTask;
    }

    public Task<CorporateDepartment?> GetDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        _departments.TryGetValue(departmentId, out var department);
        return Task.FromResult(department);
    }

    public Task<CorporateDepartment?> GetDepartmentByCodeAsync(Guid corporateId, string departmentCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_departmentByCorporateCode.TryGetValue($"{corporateId:N}:{departmentCode ?? string.Empty}", out var id) && _departments.TryGetValue(id, out var department) ? department : null);
    }

    public Task AddEmployeeAsync(CorporateEmployee employee, CancellationToken cancellationToken = default)
    {
        _employees[employee.Id] = employee;
        _employeeByCorporateNumber[$"{employee.CorporateId:N}:{employee.EmployeeNumber}"] = employee.Id;
        return Task.CompletedTask;
    }

    public Task<CorporateEmployee?> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        _employees.TryGetValue(employeeId, out var employee);
        return Task.FromResult(employee);
    }

    public Task<CorporateEmployee?> GetEmployeeByNumberAsync(Guid corporateId, string employeeNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_employeeByCorporateNumber.TryGetValue($"{corporateId:N}:{employeeNumber ?? string.Empty}", out var id) && _employees.TryGetValue(id, out var employee) ? employee : null);
    }

    public Task AddCorporateBudgetAsync(CorporateBudget budget, CancellationToken cancellationToken = default)
    {
        _budgets[budget.Id] = budget;
        return Task.CompletedTask;
    }

    public Task<CorporateBudget?> GetCorporateBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        _budgets.TryGetValue(budgetId, out var budget);
        return Task.FromResult(budget);
    }

    public Task<IReadOnlyList<CorporateBudget>> GetActiveBudgetsAsync(Guid corporateId, Guid? departmentId, Guid? employeeId, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var budgets = _budgets.Values
            .Where(x => x.CorporateId == corporateId && x.Status == OperationalStatus.Active)
            .Where(x => (!x.DepartmentId.HasValue || x.DepartmentId == departmentId) && (!x.EmployeeId.HasValue || x.EmployeeId == employeeId))
            .Where(x => x.PeriodStart <= businessDate && x.PeriodEnd >= businessDate)
            .OrderBy(x => x.EmployeeId.HasValue ? 0 : x.DepartmentId.HasValue ? 1 : 2)
            .ToList();
        return Task.FromResult<IReadOnlyList<CorporateBudget>>(budgets);
    }

    public Task AddCardStockBatchAsync(CardStockBatch batch, CancellationToken cancellationToken = default)
    {
        _stockBatches[batch.Id] = batch;
        _stockByReference[batch.BatchReference] = batch.Id;
        return Task.CompletedTask;
    }

    public Task UpdateCardStockBatchAsync(CardStockBatch batch, CancellationToken cancellationToken = default)
    {
        _stockBatches[batch.Id] = batch;
        _stockByReference[batch.BatchReference] = batch.Id;
        return Task.CompletedTask;
    }

    public Task<CardStockBatch?> GetCardStockBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_stockByReference.TryGetValue(batchReference ?? string.Empty, out var id) && _stockBatches.TryGetValue(id, out var batch) ? batch : null);
    }

    public Task<IReadOnlyList<CardStockBatch>> GetCardStockBatchesAsync(Guid productId, CardOwnerType? ownerType, Guid? ownerId, CancellationToken cancellationToken = default)
    {
        var batches = _stockBatches.Values
            .Where(x => x.ProductId == productId)
            .Where(x => !ownerType.HasValue || x.OwnerType == ownerType.Value)
            .Where(x => !ownerId.HasValue || x.OwnerId == ownerId.Value)
            .OrderBy(x => x.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<CardStockBatch>>(batches);
    }

    public Task AddAdvancedLimitRuleAsync(AdvancedLimitRule rule, CancellationToken cancellationToken = default)
    {
        _limitRules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AdvancedLimitRule>> GetActiveAdvancedLimitRulesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AdvancedLimitRule>>(_limitRules.Values.Where(x => x.IsActive).OrderBy(x => x.Priority).ToList());
    }

    public Task AddRiskRuleAsync(BankSwitch.Domain.RiskRule rule, CancellationToken cancellationToken = default)
    {
        _riskRules[rule.Id] =rule;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BankSwitch.Domain.RiskRule>> GetActiveRiskRulesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BankSwitch.Domain.RiskRule>>(_riskRules.Values.Where(x => x.Enabled).OrderBy(x => x.Priority).ToList());
    }

    public Task AddNotificationAsync(NotificationMessage notification, CancellationToken cancellationToken = default)
    {
        _notifications[notification.Id] = notification;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationMessage>> GetPendingNotificationsAsync(int take, CancellationToken cancellationToken = default)
    {
        var items = _notifications.Values.Where(x => x.Status == BankSwitch.Domain.NotificationStatus.Pending).OrderBy(x => x.CreatedAt).Take(take).ToList();
        return Task.FromResult<IReadOnlyList<NotificationMessage>>(items);
    }

    public Task UpdateNotificationAsync(NotificationMessage notification, CancellationToken cancellationToken = default)
    {
        _notifications[notification.Id] = notification;
        return Task.CompletedTask;
    }

    public Task AddStatementAsync(StatementDocument statement, IReadOnlyCollection<StatementLine> lines, CancellationToken cancellationToken = default)
    {
        _statements[statement.Id] = statement;
        _statementLines[statement.Id] = lines.ToList();
        return Task.CompletedTask;
    }

    public Task<StatementDocument?> GetStatementAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        _statements.TryGetValue(statementId, out var statement);
        return Task.FromResult(statement);
    }

    public Task<IReadOnlyList<StatementLine>> GetStatementLinesAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<StatementLine>>(_statementLines.TryGetValue(statementId, out var lines) ? lines : Array.Empty<StatementLine>());
    }
}
