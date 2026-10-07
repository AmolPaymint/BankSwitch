using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Reports;

public sealed class TransactionsModel : PageModel
{
    private const int PageSize = 50;

    private readonly IMonitoringService _monitoringService;
    private readonly ISwitchConfigurationService _configurationService;

    public TransactionsModel(IMonitoringService monitoringService, ISwitchConfigurationService configurationService)
    {
        _monitoringService = monitoringService;
        _configurationService = configurationService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SourceNodeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SinkNodeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Mti { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ResponseCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public TransactionReportPage Report { get; private set; } = new(Array.Empty<TransactionLog>(), 0, 0, 0, 0m, 0);
    public IReadOnlyList<SourceNode> SourceNodes { get; private set; } = Array.Empty<SourceNode>();
    public IReadOnlyList<SinkNode> SinkNodes { get; private set; } = Array.Empty<SinkNode>();
    public int TotalPages => Report.TotalCount == 0 ? 1 : (int)Math.Ceiling(Report.TotalCount / (double)PageSize);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        SourceNodes = await _configurationService.GetSourceNodesAsync(cancellationToken);
        SinkNodes = await _configurationService.GetSinkNodesAsync(cancellationToken);

        var page = Math.Max(1, Page);
        var filter = new TransactionReportFilter(
            From: From.HasValue ? new DateTimeOffset(From.Value, TimeSpan.Zero) : null,
            To: To.HasValue ? new DateTimeOffset(To.Value, TimeSpan.Zero) : null,
            SourceNodeId: string.IsNullOrWhiteSpace(SourceNodeId) ? null : SourceNodeId,
            SinkNodeId: string.IsNullOrWhiteSpace(SinkNodeId) ? null : SinkNodeId,
            Mti: string.IsNullOrWhiteSpace(Mti) ? null : Mti,
            ResponseCode: string.IsNullOrWhiteSpace(ResponseCode) ? null : ResponseCode,
            Page: page,
            PageSize: PageSize);

        Report = await _monitoringService.GetTransactionReportAsync(filter, cancellationToken);
    }
}
