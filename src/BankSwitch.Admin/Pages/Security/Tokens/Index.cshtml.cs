using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Security.Tokens;

public sealed class IndexModel : PageModel
{
    private readonly ITokenizationService _service;

    public IndexModel(ITokenizationService service) => _service = service;

    public IReadOnlyList<CardToken> Tokens { get; private set; } = Array.Empty<CardToken>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken) => Tokens = await _service.GetTokensAsync(cancellationToken);

    public async Task<IActionResult> OnPostSuspendAsync(string token, CancellationToken cancellationToken)
        => await ApplyAsync(() => _service.SuspendTokenAsync(token, User.Identity?.Name ?? "unknown", cancellationToken), cancellationToken);

    public async Task<IActionResult> OnPostResumeAsync(string token, CancellationToken cancellationToken)
        => await ApplyAsync(() => _service.ResumeTokenAsync(token, User.Identity?.Name ?? "unknown", cancellationToken), cancellationToken);

    public async Task<IActionResult> OnPostDeleteAsync(string token, CancellationToken cancellationToken)
        => await ApplyAsync(() => _service.DeleteTokenAsync(token, User.Identity?.Name ?? "unknown", cancellationToken), cancellationToken);

    private async Task<IActionResult> ApplyAsync(Func<Task<CmsOperationResult<CardToken>>> action, CancellationToken cancellationToken)
    {
        var result = await action().ConfigureAwait(false);
        if (!result.IsSuccess) ErrorMessage = result.Message;
        Tokens = await _service.GetTokensAsync(cancellationToken).ConfigureAwait(false);
        return Page();
    }
}
