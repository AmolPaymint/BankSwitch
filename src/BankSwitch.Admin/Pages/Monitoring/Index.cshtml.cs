using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Monitoring;

public sealed class IndexModel : PageModel
{
    private readonly IMonitoringService _service;

    public IndexModel(IMonitoringService service) => _service = service;

    public ApplicationHealthSnapshot ApplicationHealth { get; private set; } = new(
        new ClusterHealthSnapshot(ClusterNodeHealthStatus.Offline, 0, 0, 0, 0, Array.Empty<ClusterNodeHeartbeat>()),
        TimeSpan.FromHours(1), 0, 0, 0, 0);

    public IReadOnlyList<DeviceHealth> Devices { get; private set; } = Array.Empty<DeviceHealth>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApplicationHealth = await _service.GetApplicationHealthAsync(cancellationToken);
        Devices = await _service.GetDeviceHealthAsync(cancellationToken);
    }
}
