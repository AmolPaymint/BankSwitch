using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration.SourceNodes;

public sealed class EditModel : PageModel
{
    private readonly ISwitchConfigurationService _service;

    public EditModel(ISwitchConfigurationService service) => _service = service;

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public SourceNodeForm Input { get; set; } = new();

    public IReadOnlyList<Institution> Institutions { get; private set; } = Array.Empty<Institution>();

    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Institutions = await _service.GetInstitutionsAsync(cancellationToken);
        if (Id is null) return Page();
        var node = await _service.GetSourceNodeAsync(Id.Value, cancellationToken);
        if (node is null) return NotFound();

        Input = new SourceNodeForm
        {
            NodeId = node.NodeId,
            Name = node.Name,
            IsActive = node.IsActive,
            RequireMtls = node.Security.RequireMtls,
            RequirePrivateNetwork = node.Security.RequirePrivateNetwork,
            AllowedCidrs = string.Join("; ", node.Security.AllowedCidrs),
            CertificateThumbprint = node.Security.CertificateThumbprint,
            TpsLimit = node.Limits.TpsLimit,
            DailyAmountLimit = node.Limits.DailyAmountLimit,
            MaxMessageBytes = node.Limits.MaxMessageBytes,
            IdleTimeoutSeconds = (int)node.Limits.IdleTimeout.TotalSeconds,
            PermittedMtis = string.Join("; ", node.PermittedMtis),
            PermittedChannels = string.Join("; ", node.PermittedChannels),
            AllowedBinRanges = string.Join("; ", node.AllowedBinRanges),
            KeyProfile = node.KeyProfile,
            SettlementProfile = node.SettlementProfile,
            InstitutionCode = node.InstitutionCode
        };
        return Page();
    }

    [Authorize(Policy = "ConfigChecker")]
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Institutions = await _service.GetInstitutionsAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();

        var input = new SourceNodeInput
        {
            NodeId = Input.NodeId,
            Name = Input.Name,
            IsActive = Input.IsActive,
            RequireMtls = Input.RequireMtls,
            RequirePrivateNetwork = Input.RequirePrivateNetwork,
            AllowedCidrs = Input.AllowedCidrs,
            CertificateThumbprint = Input.CertificateThumbprint,
            TpsLimit = Input.TpsLimit,
            DailyAmountLimit = Input.DailyAmountLimit,
            MaxMessageBytes = Input.MaxMessageBytes,
            IdleTimeoutSeconds = Input.IdleTimeoutSeconds,
            PermittedMtis = Input.PermittedMtis,
            PermittedChannels = Input.PermittedChannels,
            AllowedBinRanges = Input.AllowedBinRanges,
            KeyProfile = Input.KeyProfile,
            SettlementProfile = Input.SettlementProfile,
            InstitutionCode = Input.InstitutionCode
        };

        var result = await _service.SaveSourceNodeAsync(Id, input, User.Identity?.Name ?? "unknown", cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        TempData["StatusMessage"] = $"Source node '{result.Value!.NodeId}' saved.";
        return RedirectToPage("/Configuration/SourceNodes/Index");
    }

    public sealed class SourceNodeForm
    {
        [Required, MaxLength(64)]
        public string NodeId { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool RequireMtls { get; set; } = true;
        public bool RequirePrivateNetwork { get; set; } = true;
        public string AllowedCidrs { get; set; } = string.Empty;
        [MaxLength(128)]
        public string CertificateThumbprint { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int TpsLimit { get; set; } = 50;
        [Range(0, double.MaxValue)]
        public decimal DailyAmountLimit { get; set; }
        [Range(1, int.MaxValue)]
        public int MaxMessageBytes { get; set; } = 4096;
        [Range(1, int.MaxValue)]
        public int IdleTimeoutSeconds { get; set; } = 65;
        public string PermittedMtis { get; set; } = string.Empty;
        public string PermittedChannels { get; set; } = string.Empty;
        public string AllowedBinRanges { get; set; } = string.Empty;
        [MaxLength(128)]
        public string KeyProfile { get; set; } = string.Empty;
        [MaxLength(128)]
        public string SettlementProfile { get; set; } = string.Empty;
        [MaxLength(32)]
        public string InstitutionCode { get; set; } = string.Empty;
    }
}
