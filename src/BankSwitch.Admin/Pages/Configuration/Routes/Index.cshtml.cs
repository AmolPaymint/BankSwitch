using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.Routes;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public IndexModel(ISwitchConfigurationService service) => _service = service;

    [BindProperty]
    public RouteRowForm Input { get; set; } = new();

    public IReadOnlyList<RouteDefinition> Routes { get; private set; } = Array.Empty<RouteDefinition>();
    public IReadOnlyList<SinkNode> SinkNodes { get; private set; } = Array.Empty<SinkNode>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var result = await _service.SaveRouteAsync(null, ToRouteInput(Input), User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            await LoadAsync(cancellationToken);
            return Page();
        }
        return RedirectToPage();
    }

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SaveRouteAsync(id, ToRouteInput(Input), User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            await LoadAsync(cancellationToken);
            return Page();
        }
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Routes = await _service.GetRoutesAsync(cancellationToken);
        SinkNodes = await _service.GetSinkNodesAsync(cancellationToken);
    }

    private static RouteInput ToRouteInput(RouteRowForm input) => new(
        input.BinPrefix,
        input.SinkNodeId,
        input.IsActive,
        input.FallbackSinkNodeId,
        input.Priority,
        input.CountryCodes,
        input.MerchantCategoryCodes,
        input.CurrencyCodes,
        input.DeviceCodes,
        input.InterchangeCodes,
        input.CardRangePrefixes,
        input.InstitutionCodes,
        input.ProductCodes,
        input.NetworkCodes,
        input.AccountRanges);

    public sealed class RouteRowForm
    {
        [Required, MaxLength(12)]
        public string BinPrefix { get; set; } = string.Empty;
        public Guid SinkNodeId { get; set; }
        public Guid? FallbackSinkNodeId { get; set; }
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; }
        public string CountryCodes { get; set; } = string.Empty;
        public string MerchantCategoryCodes { get; set; } = string.Empty;
        public string CurrencyCodes { get; set; } = string.Empty;
        public string DeviceCodes { get; set; } = string.Empty;
        public string InterchangeCodes { get; set; } = string.Empty;
        public string CardRangePrefixes { get; set; } = string.Empty;
        public string InstitutionCodes { get; set; } = string.Empty;
        public string ProductCodes { get; set; } = string.Empty;
        public string NetworkCodes { get; set; } = string.Empty;
        public string AccountRanges { get; set; } = string.Empty;
    }
}
