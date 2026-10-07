using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.SinkNodes;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public IndexModel(ISwitchConfigurationService service) => _service = service;

    public IReadOnlyList<SinkNode> Nodes { get; private set; } = Array.Empty<SinkNode>();

    public async Task OnGetAsync(CancellationToken cancellationToken) => Nodes = await _service.GetSinkNodesAsync(cancellationToken);
}
