using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class OperationalControlService : IOperationalControlService
{
    private readonly IOperationalControlRepository _ops;
    private readonly ICmsRepository _cms;
    private readonly ICorePrepaidCmsService _coreCms;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public OperationalControlService(
        IOperationalControlRepository ops,
        ICmsRepository cms,
        ICorePrepaidCmsService coreCms,
        IAuditLogger audit,
        IClock clock)
    {
        _ops = ops;
        _cms = cms;
        _coreCms = coreCms;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<AgencyProfile>> OnboardAgencyAsync(OnboardAgencyRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AgencyCode) || string.IsNullOrWhiteSpace(request.Name))
            return CmsOperationResult<AgencyProfile>.Fail("30", "Agency code and name are required.");
        if (request.CreditLimit < 0m) return CmsOperationResult<AgencyProfile>.Fail("30", "Agency credit limit cannot be negative.");
        if (await _ops.GetAgencyByCodeAsync(NormalizeCode(request.AgencyCode), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<AgencyProfile>.Fail("94", "Agency code already exists.");

        Guid? parentId = null;
        if (!string.IsNullOrWhiteSpace(request.ParentAgencyCode))
        {
            var parent = await _ops.GetAgencyByCodeAsync(NormalizeCode(request.ParentAgencyCode), cancellationToken).ConfigureAwait(false);
            if (parent is null || parent.Status != OperationalStatus.Active) return CmsOperationResult<AgencyProfile>.Fail("58", "Active parent agency was not found.");
            parentId = parent.Id;
        }

        var agency = new AgencyProfile
        {
            AgencyCode = NormalizeCode(request.AgencyCode),
            Name = request.Name.Trim(),
            ParentAgencyId = parentId,
            ContactName = request.ContactName?.Trim() ?? string.Empty,
            MobileNumber = request.MobileNumber?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            CountryCode = NormalizeCode(request.CountryCode),
            CreditMode = request.CreditMode,
            CreditLimit = request.CreditLimit,
            AvailableCredit = request.CreditLimit,
            UsedCredit = 0m,
            ReservedCredit = 0m,
            CommissionProfileCode = NormalizeCode(request.CommissionProfileCode),
            SettlementAccountNumber = request.SettlementAccountNumber?.Trim() ?? string.Empty,
            Status = OperationalStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddAgencyAsync(agency, cancellationToken).ConfigureAwait(false);
        await _ops.AddAgencyCreditLedgerEntryAsync(new AgencyCreditLedgerEntry
        {
            AgencyId = agency.Id,
            Direction = CreditLedgerDirection.Credit,
            Amount = request.CreditLimit,
            AvailableCreditAfter = agency.AvailableCredit,
            Reference = "OPENING-CREDIT",
            Narrative = "Opening agency credit position",
            CorrelationId = Guid.NewGuid().ToString("N"),
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "OnboardAgency", string.Empty, agency.AgencyCode, "Phase2 agency onboarding", "CMS-PHASE2");
        return CmsOperationResult<AgencyProfile>.Success(agency);
    }

    public async Task<CmsOperationResult<AgencyProfile>> AdjustAgencyCreditAsync(AdjustAgencyCreditRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m) return CmsOperationResult<AgencyProfile>.Fail("13", "Credit adjustment amount must be greater than zero.");
        var agency = await _ops.GetAgencyByCodeAsync(NormalizeCode(request.AgencyCode), cancellationToken).ConfigureAwait(false);
        if (agency is null || agency.Status != OperationalStatus.Active) return CmsOperationResult<AgencyProfile>.Fail("58", "Active agency was not found.");

        var updated = request.Direction switch
        {
            CreditLedgerDirection.Credit => agency with { CreditLimit = agency.CreditLimit + request.Amount, AvailableCredit = agency.AvailableCredit + request.Amount },
            CreditLedgerDirection.Debit when agency.AvailableCredit >= request.Amount => agency with { AvailableCredit = agency.AvailableCredit - request.Amount, UsedCredit = agency.UsedCredit + request.Amount },
            CreditLedgerDirection.Reserve when agency.AvailableCredit >= request.Amount => agency with { AvailableCredit = agency.AvailableCredit - request.Amount, ReservedCredit = agency.ReservedCredit + request.Amount },
            CreditLedgerDirection.Release when agency.ReservedCredit >= request.Amount => agency with { AvailableCredit = agency.AvailableCredit + request.Amount, ReservedCredit = agency.ReservedCredit - request.Amount },
            _ => null
        };
        if (updated is null) return CmsOperationResult<AgencyProfile>.Fail("51", "Insufficient agency credit for the requested adjustment.");

        await _ops.UpdateAgencyAsync(updated, cancellationToken).ConfigureAwait(false);
        await _ops.AddAgencyCreditLedgerEntryAsync(new AgencyCreditLedgerEntry
        {
            AgencyId = agency.Id,
            Direction = request.Direction,
            Amount = request.Amount,
            AvailableCreditAfter = updated.AvailableCredit,
            Reference = request.Reference?.Trim() ?? string.Empty,
            Narrative = request.Narrative?.Trim() ?? string.Empty,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "AdjustAgencyCredit", agency.AvailableCredit.ToString("0.00"), updated.AvailableCredit.ToString("0.00"), request.Narrative ?? string.Empty, request.Reference ?? string.Empty);
        return CmsOperationResult<AgencyProfile>.Success(updated);
    }

    public async Task<CmsOperationResult<CorporateProfile>> OnboardCorporateAsync(OnboardCorporateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CorporateCode) || string.IsNullOrWhiteSpace(request.Name))
            return CmsOperationResult<CorporateProfile>.Fail("30", "Corporate code and name are required.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<CorporateProfile>.Fail("30", "Currency code must be three numeric digits.");
        if (request.OpeningFundingBalance < 0m) return CmsOperationResult<CorporateProfile>.Fail("30", "Opening funding balance cannot be negative.");
        if (await _ops.GetCorporateByCodeAsync(NormalizeCode(request.CorporateCode), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CorporateProfile>.Fail("94", "Corporate code already exists.");

        var corporate = new CorporateProfile
        {
            CorporateCode = NormalizeCode(request.CorporateCode),
            Name = request.Name.Trim(),
            RegistrationNumber = request.RegistrationNumber?.Trim() ?? string.Empty,
            ContactName = request.ContactName?.Trim() ?? string.Empty,
            MobileNumber = request.MobileNumber?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            CurrencyCode = request.CurrencyCode.Trim(),
            RiskRating = string.IsNullOrWhiteSpace(request.RiskRating) ? "LOW" : NormalizeCode(request.RiskRating),
            FundingBalance = request.OpeningFundingBalance,
            AvailableFundingBalance = request.OpeningFundingBalance,
            Status = OperationalStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddCorporateAsync(corporate, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "OnboardCorporate", string.Empty, corporate.CorporateCode, "Phase2 corporate onboarding", "CMS-PHASE2");
        return CmsOperationResult<CorporateProfile>.Success(corporate);
    }

    public async Task<CmsOperationResult<CorporateDepartment>> CreateCorporateDepartmentAsync(CreateCorporateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var corporate = await _ops.GetCorporateByCodeAsync(NormalizeCode(request.CorporateCode), cancellationToken).ConfigureAwait(false);
        if (corporate is null || corporate.Status != OperationalStatus.Active) return CmsOperationResult<CorporateDepartment>.Fail("58", "Active corporate was not found.");
        if (string.IsNullOrWhiteSpace(request.DepartmentCode) || string.IsNullOrWhiteSpace(request.Name)) return CmsOperationResult<CorporateDepartment>.Fail("30", "Department code and name are required.");
        if (await _ops.GetDepartmentByCodeAsync(corporate.Id, NormalizeCode(request.DepartmentCode), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CorporateDepartment>.Fail("94", "Department code already exists for corporate.");
        var department = new CorporateDepartment
        {
            CorporateId = corporate.Id,
            DepartmentCode = NormalizeCode(request.DepartmentCode),
            Name = request.Name.Trim(),
            CostCenterCode = NormalizeCode(request.CostCenterCode),
            Status = OperationalStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddDepartmentAsync(department, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<CorporateDepartment>.Success(department);
    }

    public async Task<CmsOperationResult<CorporateEmployee>> CreateCorporateEmployeeAsync(CreateCorporateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var corporate = await _ops.GetCorporateByCodeAsync(NormalizeCode(request.CorporateCode), cancellationToken).ConfigureAwait(false);
        if (corporate is null || corporate.Status != OperationalStatus.Active) return CmsOperationResult<CorporateEmployee>.Fail("58", "Active corporate was not found.");
        if (string.IsNullOrWhiteSpace(request.EmployeeNumber) || string.IsNullOrWhiteSpace(request.FullName)) return CmsOperationResult<CorporateEmployee>.Fail("30", "Employee number and name are required.");
        if (await _ops.GetEmployeeByNumberAsync(corporate.Id, NormalizeCode(request.EmployeeNumber), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CorporateEmployee>.Fail("94", "Employee number already exists for corporate.");

        Guid? departmentId = null;
        if (!string.IsNullOrWhiteSpace(request.DepartmentCode))
        {
            var department = await _ops.GetDepartmentByCodeAsync(corporate.Id, NormalizeCode(request.DepartmentCode), cancellationToken).ConfigureAwait(false);
            if (department is null || department.Status != OperationalStatus.Active) return CmsOperationResult<CorporateEmployee>.Fail("58", "Active department was not found.");
            departmentId = department.Id;
        }

        var employee = new CorporateEmployee
        {
            CorporateId = corporate.Id,
            DepartmentId = departmentId,
            EmployeeNumber = NormalizeCode(request.EmployeeNumber),
            FullName = request.FullName.Trim(),
            MobileNumber = request.MobileNumber?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            Status = OperationalStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddEmployeeAsync(employee, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<CorporateEmployee>.Success(employee);
    }

    public async Task<CmsOperationResult<CorporateBudget>> CreateCorporateBudgetAsync(CreateCorporateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var corporate = await _ops.GetCorporateByCodeAsync(NormalizeCode(request.CorporateCode), cancellationToken).ConfigureAwait(false);
        if (corporate is null || corporate.Status != OperationalStatus.Active) return CmsOperationResult<CorporateBudget>.Fail("58", "Active corporate was not found.");
        if (request.BudgetAmount <= 0m) return CmsOperationResult<CorporateBudget>.Fail("13", "Budget amount must be greater than zero.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<CorporateBudget>.Fail("30", "Currency code must be three numeric digits.");
        if (request.PeriodEnd < request.PeriodStart) return CmsOperationResult<CorporateBudget>.Fail("30", "Budget period end must be after start.");

        Guid? departmentId = null;
        if (!string.IsNullOrWhiteSpace(request.DepartmentCode))
        {
            var department = await _ops.GetDepartmentByCodeAsync(corporate.Id, NormalizeCode(request.DepartmentCode), cancellationToken).ConfigureAwait(false);
            if (department is null) return CmsOperationResult<CorporateBudget>.Fail("58", "Department was not found.");
            departmentId = department.Id;
        }
        Guid? employeeId = null;
        if (!string.IsNullOrWhiteSpace(request.EmployeeNumber))
        {
            var employee = await _ops.GetEmployeeByNumberAsync(corporate.Id, NormalizeCode(request.EmployeeNumber), cancellationToken).ConfigureAwait(false);
            if (employee is null) return CmsOperationResult<CorporateBudget>.Fail("58", "Employee was not found.");
            employeeId = employee.Id;
        }

        var budget = new CorporateBudget
        {
            CorporateId = corporate.Id,
            DepartmentId = departmentId,
            EmployeeId = employeeId,
            BudgetCode = NormalizeCode(request.BudgetCode),
            CurrencyCode = request.CurrencyCode.Trim(),
            BudgetAmount = request.BudgetAmount,
            AvailableAmount = request.BudgetAmount,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = OperationalStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddCorporateBudgetAsync(budget, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<CorporateBudget>.Success(budget);
    }

    public async Task<CmsOperationResult<CardStockBatch>> AllocateCardStockAsync(AllocateCardStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0) return CmsOperationResult<CardStockBatch>.Fail("13", "Stock quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.BatchReference)) return CmsOperationResult<CardStockBatch>.Fail("30", "Batch reference is required.");
        if (await _ops.GetCardStockBatchByReferenceAsync(NormalizeCode(request.BatchReference), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CardStockBatch>.Fail("94", "Card stock batch reference already exists.");
        var product = await _cms.GetProductByCodeAsync(NormalizeCode(request.ProductCode), cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active) return CmsOperationResult<CardStockBatch>.Fail("58", "Active card product was not found.");

        var ownerId = await ResolveOwnerIdAsync(request.OwnerType, request.OwnerCode, cancellationToken).ConfigureAwait(false);
        if (ownerId.ResponseCode != "00") return CmsOperationResult<CardStockBatch>.Fail(ownerId.ResponseCode, ownerId.Message);

        var batch = new CardStockBatch
        {
            ProductId = product.Id,
            BatchReference = NormalizeCode(request.BatchReference),
            OwnerType = request.OwnerType,
            OwnerId = ownerId.Value,
            Quantity = request.Quantity,
            AvailableQuantity = request.Quantity,
            ReservedQuantity = 0,
            IssuedQuantity = 0,
            Status = CardStockStatus.Available,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddCardStockBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<CardStockBatch>.Success(batch);
    }

    public async Task<CmsOperationResult<BulkIssueCardsResult>> BulkIssueCardsAsync(BulkIssueCardsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Cards is null || request.Cards.Count == 0) return CmsOperationResult<BulkIssueCardsResult>.Fail("30", "At least one card issue item is required.");
        var product = await _cms.GetProductByCodeAsync(NormalizeCode(request.ProductCode), cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active) return CmsOperationResult<BulkIssueCardsResult>.Fail("58", "Active product was not found.");

        CardStockBatch? batch = null;
        if (!string.IsNullOrWhiteSpace(request.BatchReference))
        {
            batch = await _ops.GetCardStockBatchByReferenceAsync(NormalizeCode(request.BatchReference), cancellationToken).ConfigureAwait(false);
            if (batch is null || batch.Status != CardStockStatus.Available) return CmsOperationResult<BulkIssueCardsResult>.Fail("58", "Available card stock batch was not found.");
            if (batch.ProductId != product.Id) return CmsOperationResult<BulkIssueCardsResult>.Fail("58", "Card stock batch does not belong to product.");
            if (batch.AvailableQuantity < request.Cards.Count) return CmsOperationResult<BulkIssueCardsResult>.Fail("61", "Insufficient available card stock.");
        }

        var issued = new List<IssueCardResult>();
        var failures = new List<BulkIssueFailure>();
        foreach (var item in request.Cards)
        {
            var issueRequest = new IssueCardRequest(item.CustomerNumber, request.ProductCode, item.CardKind)
            {
                AgencyCode = request.AgencyCode ?? string.Empty,
                CorporateCode = request.CorporateCode ?? string.Empty,
                DepartmentCode = item.DepartmentCode ?? string.Empty,
                EmployeeNumber = item.EmployeeNumber ?? string.Empty,
                BatchReference = request.BatchReference ?? string.Empty
            };
            var result = await _coreCms.IssueCardAsync(issueRequest, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && result.Value is not null) issued.Add(result.Value);
            else failures.Add(new BulkIssueFailure(item.CustomerNumber, result.ResponseCode, result.Message));
        }

        var bulkResult = new BulkIssueCardsResult(request.Cards.Count, issued.Count, failures.Count, issued, failures);
        return CmsOperationResult<BulkIssueCardsResult>.Success(bulkResult);
    }

    public async Task<CmsOperationResult<AdvancedLimitRule>> CreateAdvancedLimitRuleAsync(CreateAdvancedLimitRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RuleCode) || string.IsNullOrWhiteSpace(request.Name)) return CmsOperationResult<AdvancedLimitRule>.Fail("30", "Rule code and name are required.");
        if (request.AmountLimit <= 0m && request.CountLimit <= 0) return CmsOperationResult<AdvancedLimitRule>.Fail("30", "Amount or count limit must be greater than zero.");
        var rule = new AdvancedLimitRule
        {
            RuleCode = NormalizeCode(request.RuleCode),
            Name = request.Name.Trim(),
            Scope = request.Scope,
            ScopeId = request.ScopeId,
            TransactionTypeCode = NormalizeCode(request.TransactionTypeCode),
            ChannelCode = NormalizeCode(request.ChannelCode),
            CurrencyCode = request.CurrencyCode?.Trim() ?? string.Empty,
            Period = request.Period,
            AmountLimit = request.AmountLimit,
            CountLimit = request.CountLimit,
            Priority = request.Priority <= 0 ? 100 : request.Priority,
            IsActive = true,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddAdvancedLimitRuleAsync(rule, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<AdvancedLimitRule>.Success(rule);
    }

    public async Task<CmsOperationResult<BankSwitch.Domain.RiskRule>> CreateRiskRuleAsync(CreateRiskRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RuleCode) || string.IsNullOrWhiteSpace(request.Name)) return CmsOperationResult<BankSwitch.Domain.RiskRule>.Fail("30", "Risk rule code and name are required.");
        var rule = new BankSwitch.Domain.RiskRule
        {
            RuleCode = NormalizeCode(request.RuleCode),
            Name = request.Name.Trim(),
            RuleType = (BankSwitch.Domain.RiskRuleType) request.RuleType,
            MatchValue = NormalizeCode(request.MatchValue),
            Action =(BankSwitch.Domain.RiskRuleAction) request.Action,
            ResponseCode = string.IsNullOrWhiteSpace(request.ResponseCode) ? "59" : request.ResponseCode.Trim(),
            AmountThreshold = request.AmountThreshold,
            Priority = request.Priority <= 0 ? 100 : request.Priority,
            Enabled = true,
            AlertTemplateCode = string.IsNullOrWhiteSpace(request.AlertTemplateCode) ? "RISK_ALERT" : NormalizeCode(request.AlertTemplateCode),
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddRiskRuleAsync(rule, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<BankSwitch.Domain.RiskRule>.Success(rule);
    }

    public async Task<CmsOperationResult<NotificationMessage>> QueueNotificationAsync(QueueNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient)) return CmsOperationResult<NotificationMessage>.Fail("30", "Notification recipient is required.");
        var notification = new NotificationMessage
        {
            Channel =(BankSwitch.Domain.NotificationChannel) request.Channel,
            Recipient = request.Recipient.Trim(),
            TemplateCode = NormalizeCode(request.TemplateCode),
            Subject = request.Subject?.Trim() ?? string.Empty,
            PayloadJson = request.PayloadJson?.Trim() ?? "{}",
            Reference = request.Reference?.Trim() ?? string.Empty,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            Status =  (BankSwitch.Domain.NotificationStatus)NotificationStatus.Pending,
            CreatedAt = _clock.UtcNow
        };
        await _ops.AddNotificationAsync(notification, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NotificationMessage>.Success(notification);
    }

    public async Task<CmsOperationResult<StatementResult>> GenerateStatementAsync(GenerateStatementRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PeriodEnd < request.PeriodStart) return CmsOperationResult<StatementResult>.Fail("30", "Statement period end must be after start.");
        var cards = await _cms.GetCardsForOwnerAsync(request.OwnerType, request.OwnerId, cancellationToken).ConfigureAwait(false);
        var allEntries = new List<LedgerEntry>();
        foreach (var card in cards)
        {
            var entries = await _cms.GetLedgerEntriesAsync(card.WalletAccountId, request.PeriodStart, request.PeriodEnd, cancellationToken).ConfigureAwait(false);
            allEntries.AddRange(entries.Where(x => string.IsNullOrWhiteSpace(request.CurrencyCode) || x.CurrencyCode == request.CurrencyCode));
        }
        var ordered = allEntries.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList();
        var opening = ordered.Count == 0 ? 0m : ordered[0].Direction == LedgerEntryDirection.Credit ? ordered[0].BalanceAfter - ordered[0].Amount : ordered[0].BalanceAfter + ordered[0].Amount;
        var closing = ordered.Count == 0 ? opening : ordered[^1].BalanceAfter;
        var debitTotal = ordered.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount);
        var creditTotal = ordered.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount);
        var statement = new StatementDocument
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            StatementNumber = $"STMT-{_clock.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            CurrencyCode = request.CurrencyCode?.Trim() ?? string.Empty,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            OpeningBalance = opening,
            ClosingBalance = closing,
            DebitTotal = debitTotal,
            CreditTotal = creditTotal,
            TransactionCount = ordered.Count,
            Status = StatementStatus.Generated,
            CreatedAt = _clock.UtcNow
        };
        var lines = ordered.Select(x => new StatementLine
        {
            StatementId = statement.Id,
            TransactionDate = x.CreatedAt,
            Reference = x.Reference,
            Narrative = x.Narrative,
            Direction = x.Direction,
            Amount = x.Amount,
            BalanceAfter = x.BalanceAfter
        }).ToList();
        await _ops.AddStatementAsync(statement, lines, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<StatementResult>.Success(new StatementResult(statement, lines));
    }

    public async Task<RiskELDecision> EvaluateRiskAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var rules = await _ops.GetActiveRiskRulesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in rules.OrderBy(x => x.Priority))
        {
            if (!MatchesRiskRule(rule, context)) continue;
            if (rule.Action is (BankSwitch.Domain.RiskRuleAction)RiskAction.Approve or (BankSwitch.Domain.RiskRuleAction)RiskAction.Alert) return RiskELDecision.Allow();
            var message = $"Risk rule {rule.RuleCode} matched: {rule.Name}.";
            return RiskELDecision.Block(rule.ResponseCode, message, (BankSwitch.Domain.RiskAction) rule.Action,(BankSwitch.Domain.RiskRule) rule);
        }
        return RiskELDecision.Allow();
    }

    public async Task<CmsOperationResult<bool>> ValidateAdvancedLimitsAsync(AdvancedLimitEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var rules = await _ops.GetActiveAdvancedLimitRulesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in rules.OrderBy(x => x.Priority))
        {
            if (!MatchesLimitRuleScope(rule, context)) continue;
            if (!string.IsNullOrWhiteSpace(rule.TransactionTypeCode) && !string.Equals(rule.TransactionTypeCode, context.AuthorizationRequest.TransactionTypeCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(rule.ChannelCode) && !string.Equals(rule.ChannelCode, context.AuthorizationRequest.ChannelCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(rule.CurrencyCode) && !string.Equals(rule.CurrencyCode, context.AuthorizationRequest.CurrencyCode, StringComparison.OrdinalIgnoreCase)) continue;

            var range = GetPeriodRange(rule.Period, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
            if (rule.Period == LimitPeriod.PerTransaction)
            {
                if (rule.AmountLimit > 0m && context.Amount > rule.AmountLimit) return CmsOperationResult<bool>.Fail("61", $"Advanced limit {rule.RuleCode} per-transaction amount exceeded.");
                continue;
            }
            var utilized = await _cms.GetUtilizedAmountAsync(context.Wallet.Id, new[] { LedgerEntryType.Purchase }, range.From, range.To, cancellationToken).ConfigureAwait(false);
            if (rule.AmountLimit > 0m && utilized + context.Amount > rule.AmountLimit) return CmsOperationResult<bool>.Fail("61", $"Advanced limit {rule.RuleCode} amount exceeded.");
            if (rule.CountLimit > 0)
            {
                var count = await _cms.GetTransactionCountAsync(context.Wallet.Id, new[] { LedgerEntryType.Purchase }, range.From, range.To, cancellationToken).ConfigureAwait(false);
                if (count + 1 > rule.CountLimit) return CmsOperationResult<bool>.Fail("65", $"Advanced limit {rule.RuleCode} count exceeded.");
            }
        }
        return CmsOperationResult<bool>.Success(true);
    }

    private async Task<CmsOperationResult<Guid?>> ResolveOwnerIdAsync(CardOwnerType ownerType, string? ownerCode, CancellationToken cancellationToken)
    {
        switch (ownerType)
        {
            case CardOwnerType.Issuer:
                return CmsOperationResult<Guid?>.Success(null);
            case CardOwnerType.Agency:
                var agency = await _ops.GetAgencyByCodeAsync(NormalizeCode(ownerCode ?? string.Empty), cancellationToken).ConfigureAwait(false);
                return agency is not null && agency.Status == OperationalStatus.Active ? CmsOperationResult<Guid?>.Success(agency.Id) : CmsOperationResult<Guid?>.Fail("58", "Active agency owner was not found.");
            case CardOwnerType.Corporate:
            case CardOwnerType.CorporateEmployee:
                var corporate = await _ops.GetCorporateByCodeAsync(NormalizeCode(ownerCode ?? string.Empty), cancellationToken).ConfigureAwait(false);
                return corporate is not null && corporate.Status == OperationalStatus.Active ? CmsOperationResult<Guid?>.Success(corporate.Id) : CmsOperationResult<Guid?>.Fail("58", "Active corporate owner was not found.");
            default:
                return CmsOperationResult<Guid?>.Fail("58", "Unsupported card-stock owner type.");
        }
    }

    private static bool MatchesRiskRule(BankSwitch.Domain.RiskRule rule, RiskEvaluationContext context)
    {
        var request = context.AuthorizationRequest;
        return rule.RuleType switch
        {
            BankSwitch.Domain.RiskRuleType.MerchantCategoryBlock => Match(rule.MatchValue, request.MerchantCategoryCode),
            BankSwitch.Domain.RiskRuleType.MerchantCountryBlock => Match(rule.MatchValue, request.MerchantCountryCode),
            BankSwitch.Domain.RiskRuleType.ChannelBlock => Match(rule.MatchValue, request.ChannelCode),
            BankSwitch.Domain.RiskRuleType.CurrencyBlock => Match(rule.MatchValue, request.CurrencyCode),
            BankSwitch.Domain.RiskRuleType.AmountThreshold => rule.AmountThreshold.HasValue && request.Amount >= rule.AmountThreshold.Value,
            BankSwitch.Domain.RiskRuleType.CustomerRiskRating => Match(rule.MatchValue, context.Customer.RiskRating),
            BankSwitch.Domain.RiskRuleType.AgencyStatus => context.Card.AgencyId.HasValue && Match(rule.MatchValue, OperationalStatus.Active.ToString()),
            BankSwitch.Domain.RiskRuleType.CorporateStatus => context.Card.CorporateId.HasValue && Match(rule.MatchValue, OperationalStatus.Active.ToString()),
            _ => false
        };
    }

    private static bool MatchesLimitRuleScope(AdvancedLimitRule rule, AdvancedLimitEvaluationContext context)
    {
        if (!rule.ScopeId.HasValue) return true;
        return rule.Scope switch
        {
            LimitScope.Program => context.Product.ProgramId == rule.ScopeId.Value,
            LimitScope.Product => context.Product.Id == rule.ScopeId.Value,
            LimitScope.Customer => context.Customer.Id == rule.ScopeId.Value,
            LimitScope.Card => context.Card.Id == rule.ScopeId.Value,
            LimitScope.Agency => context.Card.AgencyId == rule.ScopeId.Value,
            LimitScope.Corporate => context.Card.CorporateId == rule.ScopeId.Value,
            LimitScope.Department => context.Card.CorporateDepartmentId == rule.ScopeId.Value,
            LimitScope.Employee => context.Card.CorporateEmployeeId == rule.ScopeId.Value,
            _ => false
        };
    }

    private static (DateOnly From, DateOnly To) GetPeriodRange(LimitPeriod period, DateOnly businessDate)
    {
        return period switch
        {
            LimitPeriod.Daily => (businessDate, businessDate),
            LimitPeriod.Weekly => (businessDate.AddDays(-(int)businessDate.DayOfWeek), businessDate),
            LimitPeriod.Monthly => (new DateOnly(businessDate.Year, businessDate.Month, 1), businessDate),
            _ => (businessDate, businessDate)
        };
    }

    private static bool Match(string configured, string value)
    {
        if (string.IsNullOrWhiteSpace(configured)) return false;
        var values = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Any(x => string.Equals(x, value ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCurrency(string currencyCode) => !string.IsNullOrWhiteSpace(currencyCode) && currencyCode.Length == 3 && currencyCode.All(char.IsDigit);
    private static string NormalizeCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
