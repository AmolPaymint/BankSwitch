using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.Fees;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public IndexModel(ISwitchConfigurationService service) => _service = service;

    [BindProperty]
    public FeeRowForm Input { get; set; } = new();

    public IReadOnlyList<Fee> Fees { get; private set; } = Array.Empty<Fee>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken) => Fees = await _service.GetFeesAsync(cancellationToken);

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var result = await _service.SaveFeeAsync(null, ToInput(Input), User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            Fees = await _service.GetFeesAsync(cancellationToken);
            return Page();
        }
        return RedirectToPage();
    }

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SaveFeeAsync(id, ToInput(Input), User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            Fees = await _service.GetFeesAsync(cancellationToken);
            return Page();
        }
        return RedirectToPage();
    }

    private static FeeInput ToInput(FeeRowForm form) => new(form.Name, form.FlatAmount, form.PercentageOfTransaction, form.Minimum, form.Maximum, form.IsActive);

    public sealed class FeeRowForm
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [Range(0, double.MaxValue)]
        public decimal FlatAmount { get; set; }
        [Range(0, 100)]
        public decimal PercentageOfTransaction { get; set; }
        [Range(0, double.MaxValue)]
        public decimal Minimum { get; set; }
        [Range(0, double.MaxValue)]
        public decimal Maximum { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
