using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.Institutions;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public IndexModel(ISwitchConfigurationService service) => _service = service;

    public IReadOnlyList<Institution> Institutions { get; private set; } = Array.Empty<Institution>();

    public async Task OnGetAsync(CancellationToken cancellationToken) => Institutions = await _service.GetInstitutionsAsync(cancellationToken);
}
