using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.CardFees;

public sealed class IndexModel : PageModel
{
    private readonly ICardFeeService _service;
    private readonly ISwitchConfigurationService _configurationService;

    public IndexModel(ICardFeeService service, ISwitchConfigurationService configurationService)
    {
        _service = service;
        _configurationService = configurationService;
    }

    public IReadOnlyList<CardFeeRule> Rules { get; private set; } = Array.Empty<CardFeeRule>();
    public IReadOnlyDictionary<Guid, string> FeeNames { get; private set; } = new Dictionary<Guid, string>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Rules = await _service.GetCardFeeRulesAsync(cancellationToken);
        FeeNames = (await _configurationService.GetFeesAsync(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
    }
}
