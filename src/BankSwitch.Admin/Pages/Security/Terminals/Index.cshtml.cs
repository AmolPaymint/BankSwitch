using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Security.Terminals;

public sealed class IndexModel : PageModel
{
    private readonly ITerminalKeyService _service;
    private readonly ISwitchConfigurationService _configurationService;

    public IndexModel(ITerminalKeyService service, ISwitchConfigurationService configurationService)
    {
        _service = service;
        _configurationService = configurationService;
    }

    [BindProperty]
    public RegisterTerminalForm Input { get; set; } = new();

    public IReadOnlyList<TerminalKeyProfile> Terminals { get; private set; } = Array.Empty<TerminalKeyProfile>();
    public IReadOnlyList<SourceNode> SourceNodes { get; private set; } = Array.Empty<SourceNode>();
    public string ErrorMessage { get; private set; } = string.Empty;
    public string StatusMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();

        var result = await _service.RegisterTerminalAsync(new RegisterTerminalRequest(Input.TerminalId, Input.SourceNodeId, Input.KeyProfile), User.Identity?.Name ?? "unknown", cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        Terminals = await _service.GetTerminalsAsync(cancellationToken).ConfigureAwait(false);
        StatusMessage = $"Terminal '{result.Value!.TerminalId}' registered.";
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateSessionKeyAsync(string terminalId, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        var result = await _service.GenerateSessionKeyAsync(terminalId, User.Identity?.Name ?? "unknown", cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        Terminals = await _service.GetTerminalsAsync(cancellationToken).ConfigureAwait(false);
        StatusMessage = $"New session key derived for '{result.Value!.TerminalId}': KSN {result.Value.KeySerialNumber}, KCV {result.Value.KeyCheckValue}.";
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Terminals = await _service.GetTerminalsAsync(cancellationToken).ConfigureAwait(false);
        SourceNodes = await _configurationService.GetSourceNodesAsync(cancellationToken).ConfigureAwait(false);
    }

    public sealed class RegisterTerminalForm
    {
        [Required, MaxLength(16), MinLength(4)]
        public string TerminalId { get; set; } = string.Empty;
        [Required]
        public string SourceNodeId { get; set; } = string.Empty;
        [Required, MaxLength(128)]
        public string KeyProfile { get; set; } = string.Empty;
    }
}
