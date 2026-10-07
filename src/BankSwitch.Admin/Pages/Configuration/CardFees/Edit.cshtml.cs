using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.CardFees;

public sealed class EditModel : PageModel
{
    private readonly ICardFeeService _service;
    private readonly ISwitchConfigurationService _configurationService;

    public EditModel(ICardFeeService service, ISwitchConfigurationService configurationService)
    {
        _service = service;
        _configurationService = configurationService;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public CardFeeRuleForm Input { get; set; } = new();

    public IReadOnlyList<Fee> Fees { get; private set; } = Array.Empty<Fee>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Fees = await _configurationService.GetFeesAsync(cancellationToken);
        if (Id is null) return Page();

        var rule = await _service.GetCardFeeRuleAsync(Id.Value, cancellationToken);
        if (rule is null) return NotFound();

        Input = new CardFeeRuleForm
        {
            FeeType = rule.FeeType,
            ScopeType = rule.ScopeType,
            ScopeValue = rule.ScopeValue,
            IsWaiver = rule.IsWaiver,
            FeeId = rule.FeeId,
            IsActive = rule.IsActive,
            Description = rule.Description
        };
        return Page();
    }

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Fees = await _configurationService.GetFeesAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();

        var input = new CardFeeRuleInput(
            Input.FeeType,
            Input.ScopeType,
            Input.ScopeValue,
            Input.IsWaiver,
            Input.IsWaiver ? null : Input.FeeId,
            Input.IsActive,
            Input.Description);

        var result = await _service.SaveCardFeeRuleAsync(Id, input, User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        return RedirectToPage("/Configuration/CardFees/Index");
    }

    public sealed class CardFeeRuleForm
    {
        public CardFeeType FeeType { get; set; } = CardFeeType.Issuance;
        public CardFeeScopeType ScopeType { get; set; } = CardFeeScopeType.Bin;
        [Required, MaxLength(64)]
        public string ScopeValue { get; set; } = string.Empty;
        public bool IsWaiver { get; set; }
        public Guid? FeeId { get; set; }
        public bool IsActive { get; set; } = true;
        [MaxLength(400)]
        public string Description { get; set; } = string.Empty;
    }
}
