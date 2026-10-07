using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration;

public sealed class IndexModel : PageModel
{
    private readonly ISwitchConfigurationService _service;
    private readonly ICardFeeService _cardFeeService;

    public IndexModel(ISwitchConfigurationService service, ICardFeeService cardFeeService)
    {
        _service = service;
        _cardFeeService = cardFeeService;
    }

    public int SourceNodeCount { get; private set; }
    public int SinkNodeCount { get; private set; }
    public int RouteCount { get; private set; }
    public int FeeCount { get; private set; }
    public int SchemeCount { get; private set; }
    public int InstitutionCount { get; private set; }
    public int CardFeeRuleCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        SourceNodeCount = (await _service.GetSourceNodesAsync(cancellationToken)).Count;
        SinkNodeCount = (await _service.GetSinkNodesAsync(cancellationToken)).Count;
        RouteCount = (await _service.GetRoutesAsync(cancellationToken)).Count;
        FeeCount = (await _service.GetFeesAsync(cancellationToken)).Count;
        SchemeCount = (await _service.GetSchemesAsync(cancellationToken)).Count;
        InstitutionCount = (await _service.GetInstitutionsAsync(cancellationToken)).Count;
        CardFeeRuleCount = (await _cardFeeService.GetCardFeeRulesAsync(cancellationToken)).Count;
    }
}
