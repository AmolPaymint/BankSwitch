using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.Institutions;

public sealed class EditModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public EditModel(ISwitchConfigurationService service) => _service = service;

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public InstitutionForm Input { get; set; } = new();

    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Id is null) return Page();
        var institution = await _service.GetInstitutionAsync(Id.Value, cancellationToken);
        if (institution is null) return NotFound();

        Input = new InstitutionForm
        {
            Code = institution.Code,
            Name = institution.Name,
            Type = institution.Type,
            CountryCode = institution.CountryCode,
            DefaultCurrencyCode = institution.DefaultCurrencyCode,
            IsActive = institution.IsActive
        };
        return Page();
    }

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();

        var input = new InstitutionInput(Input.Code, Input.Name, Input.Type, Input.CountryCode, Input.DefaultCurrencyCode, Input.IsActive);
        var result = await _service.SaveInstitutionAsync(Id, input, User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        return RedirectToPage("/Configuration/Institutions/Index");
    }

    public sealed class InstitutionForm
    {
        [Required, MaxLength(32)]
        public string Code { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public InstitutionType Type { get; set; } = InstitutionType.Both;
        [MaxLength(2)]
        public string CountryCode { get; set; } = string.Empty;
        [MaxLength(3)]
        public string DefaultCurrencyCode { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
