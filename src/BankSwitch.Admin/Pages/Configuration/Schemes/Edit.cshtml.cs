using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.Schemes;

public sealed class EditModel : PageModel
{
    private const int BlankPermissionRows = 3;

    private readonly ISwitchConfigurationService _service;

    public EditModel(ISwitchConfigurationService service) => _service = service;

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public SchemeForm Input { get; set; } = new();

    public IReadOnlyList<SourceNode> SourceNodes { get; private set; } = Array.Empty<SourceNode>();
    public IReadOnlyList<RouteDefinition> Routes { get; private set; } = Array.Empty<RouteDefinition>();
    public IReadOnlyList<Fee> Fees { get; private set; } = Array.Empty<Fee>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadReferenceDataAsync(cancellationToken);

        if (Id is null)
        {
            for (var i = 0; i < BlankPermissionRows; i++) Input.Permissions.Add(new PermissionRow());
            return Page();
        }

        var scheme = await _service.GetSchemeAsync(Id.Value, cancellationToken);
        if (scheme is null) return NotFound();

        Input = new SchemeForm
        {
            Name = scheme.Name,
            SourceNodeId = scheme.SourceNodeId,
            RouteId = scheme.RouteId,
            IsActive = scheme.IsActive,
            Permissions = scheme.Permissions
                .Select(p => new PermissionRow { TransactionTypeCode = p.TransactionTypeCode, ChannelCode = p.ChannelCode, FeeId = p.FeeId })
                .ToList()
        };
        for (var i = 0; i < BlankPermissionRows; i++) Input.Permissions.Add(new PermissionRow());
        return Page();
    }

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadReferenceDataAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();

        var permissions = Input.Permissions
            .Select(p => new SchemePermissionInput(p.TransactionTypeCode ?? string.Empty, p.ChannelCode ?? string.Empty, p.FeeId))
            .ToArray();

        var input = new SchemeInput(Input.Name, Input.SourceNodeId, Input.RouteId, Input.IsActive, permissions);
        var result = await _service.SaveSchemeAsync(Id, input, User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        return RedirectToPage("/Configuration/Schemes/Index");
    }

    private async Task LoadReferenceDataAsync(CancellationToken cancellationToken)
    {
        SourceNodes = await _service.GetSourceNodesAsync(cancellationToken);
        Routes = await _service.GetRoutesAsync(cancellationToken);
        Fees = await _service.GetFeesAsync(cancellationToken);
    }

    public sealed class SchemeForm
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public Guid SourceNodeId { get; set; }
        public Guid RouteId { get; set; }
        public bool IsActive { get; set; } = true;
        public List<PermissionRow> Permissions { get; set; } = new();
    }

    public sealed class PermissionRow
    {
        [MaxLength(2)]
        public string? TransactionTypeCode { get; set; }
        [MaxLength(2)]
        public string? ChannelCode { get; set; }
        public Guid FeeId { get; set; }
    }
}
