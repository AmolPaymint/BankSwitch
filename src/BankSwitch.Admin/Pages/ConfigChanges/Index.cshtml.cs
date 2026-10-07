using System.ComponentModel.DataAnnotations;
using BankSwitch.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.ConfigChanges;

[Authorize(Policy = "ConfigMakerOrChecker")]
public sealed class IndexModel : PageModel
{
    private readonly IMakerCheckerService _service;

    public IndexModel(IMakerCheckerService service) => _service = service;

    public IReadOnlyCollection<ConfigChangeRequest> Items { get; private set; } = Array.Empty<ConfigChangeRequest>();

    [BindProperty]
    public CreateInput Input { get; set; } = new();

    public void OnGet() => Items = _service.GetAll();

    public IActionResult OnPostCreate()
    {
        if (!ModelState.IsValid)
        {
            Items = _service.GetAll();
            return Page();
        }
        _service.Draft(Input.Area, Input.OldValue, Input.NewValue, User.Identity?.Name ?? "unknown", Input.Reason, Input.TicketReference, Input.EffectiveAt);
        return RedirectToPage();
    }

    public IActionResult OnPostSubmit(Guid id) { _service.Submit(id, User.Identity?.Name ?? "unknown"); return RedirectToPage(); }

    [Authorize(Policy = "ConfigChecker")]
    public IActionResult OnPostApprove(Guid id) { _service.Approve(id, User.Identity?.Name ?? "unknown"); return RedirectToPage(); }

    [Authorize(Policy = "ConfigChecker")]
    public IActionResult OnPostActivate(Guid id) { _service.Activate(id, User.Identity?.Name ?? "unknown"); return RedirectToPage(); }

    [Authorize(Policy = "ConfigChecker")]
    public IActionResult OnPostArchive(Guid id) { _service.Archive(id, User.Identity?.Name ?? "unknown"); return RedirectToPage(); }

    public sealed class CreateInput
    {
        [Required, RegularExpression("^(Route|BIN|Fee|Scheme|SourceNode|SinkNode|KeyProfile|TransactionLimit|UserRole|Agency|AgencyCredit|Corporate|CorporateBudget|CardStock|BulkIssuance|AdvancedLimit|RiskRule|NotificationTemplate|StatementProfile|SettlementProfile|SettlementBatch|ReconciliationException|GLPosting|Refund|FinancialReversal|FinancialAdjustment|AgencySettlement|CorporateSettlement|CryptoKeyProfile|HsmKeyRotation|ThreeDS|AmlWatchlist|FraudPolicy|SiemConfig|DataWarehouseExport|ClusterNode|FailoverPolicy|DisasterRecoveryPlan|DisasterRecoveryDrill|RegulatoryReport)$")]
        public string Area { get; set; } = "Route";
        [MaxLength(8000)]
        public string OldValue { get; set; } = string.Empty;
        [Required, MaxLength(8000)]
        public string NewValue { get; set; } = string.Empty;
        [Required, MaxLength(512)]
        public string Reason { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string TicketReference { get; set; } = string.Empty;
        public DateTimeOffset? EffectiveAt { get; set; }
    }
}
