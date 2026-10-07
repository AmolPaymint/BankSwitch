using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.Schemes;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public IndexModel(ISwitchConfigurationService service) => _service = service;

    public IReadOnlyList<Scheme> Schemes { get; private set; } = Array.Empty<Scheme>();
    public IReadOnlyDictionary<Guid, string> SourceNodeNames { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, string> RouteBinPrefixes { get; private set; } = new Dictionary<Guid, string>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Schemes = await _service.GetSchemesAsync(cancellationToken);
        SourceNodeNames = (await _service.GetSourceNodesAsync(cancellationToken)).ToDictionary(x => x.Id, x => x.NodeId);
        RouteBinPrefixes = (await _service.GetRoutesAsync(cancellationToken)).ToDictionary(x => x.Id, x => x.BinPrefix);
    }
}
