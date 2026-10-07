using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.SourceNodes;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public IndexModel(ISwitchConfigurationService service) => _service = service;

    public IReadOnlyList<SourceNode> Nodes { get; private set; } = Array.Empty<SourceNode>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => Nodes = await _service.GetSourceNodesAsync(cancellationToken);
}
