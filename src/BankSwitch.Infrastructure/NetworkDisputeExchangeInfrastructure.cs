using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryNetworkDisputeExchangeRepository : INetworkDisputeExchangeRepository
{
    private readonly ConcurrentDictionary<Guid, NetworkDisputeExchangeFile> _files = new();
    private readonly ConcurrentDictionary<Guid, List<NetworkDisputeExchangeRecord>> _records = new();

    public Task AddFileAsync(NetworkDisputeExchangeFile file, IReadOnlyCollection<NetworkDisputeExchangeRecord> records, CancellationToken cancellationToken = default)
    {
        _files[file.Id] = file;
        _records[file.Id] = records.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateFileAsync(NetworkDisputeExchangeFile file, CancellationToken cancellationToken = default)
    {
        _files[file.Id] = file;
        return Task.CompletedTask;
    }

    public Task<NetworkDisputeExchangeFile?> GetFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _files.TryGetValue(id, out var file);
        return Task.FromResult(file);
    }

    public Task<IReadOnlyList<NetworkDisputeExchangeFile>> GetFilesAsync(DateOnly? businessDate, DisputeExchangeNetwork? network, DisputeExchangeStatus? status, CancellationToken cancellationToken = default)
    {
        IEnumerable<NetworkDisputeExchangeFile> q = _files.Values;
        if (businessDate.HasValue) q = q.Where(f => f.BusinessDate == businessDate.Value);
        if (network.HasValue) q = q.Where(f => f.Network == network.Value);
        if (status.HasValue) q = q.Where(f => f.Status == status.Value);
        return Task.FromResult<IReadOnlyList<NetworkDisputeExchangeFile>>(q.OrderByDescending(f => f.CreatedAt).ToList());
    }

    public Task<IReadOnlyList<NetworkDisputeExchangeRecord>> GetRecordsAsync(Guid fileId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NetworkDisputeExchangeRecord>>(_records.TryGetValue(fileId, out var list) ? list : new List<NetworkDisputeExchangeRecord>());
}

public abstract class DelimitedNetworkDisputeFormatAdapter : INetworkDisputeFormatAdapter
{
    public abstract DisputeExchangeNetwork Network { get; }
    protected abstract string SchemePrefix { get; }

    public NetworkDisputeExchangeFile BuildFile(
        DisputeExchangeFileType fileType,
        DateOnly businessDate,
        IReadOnlyList<ChargebackCase> chargebackCases,
        IReadOnlyList<CustomerDispute> disputes,
        string fileName,
        string actor)
    {
        var lines = new List<string>
        {
            $"HDR|{SchemePrefix}|{fileType}|{businessDate:yyyyMMdd}|{DateTimeOffset.UtcNow:yyyyMMddHHmmss}|{chargebackCases.Count + disputes.Count}"
        };

        foreach (var c in chargebackCases)
        {
            var action = fileType switch
            {
                DisputeExchangeFileType.Representment => "REPRESENT",
                DisputeExchangeFileType.PreArbitration => "PREARB",
                DisputeExchangeFileType.Arbitration => "ARB",
                DisputeExchangeFileType.EvidencePackage => "EVIDENCE",
                _ => "CHARGEBACK"
            };
            lines.Add(string.Join('|', "DTL", action, c.Id, Escape(c.CaseReference), Escape(c.NetworkCaseId), Escape(c.Rrn), Escape(c.Stan), Escape(c.MaskedPan), Escape(c.ReasonCode), c.ChargebackAmount.ToString("0.00"), Escape(c.CurrencyCode), Escape(c.IssuerBin), Escape(c.AcquirerBin), Escape(c.MerchantId), Escape(c.TerminalId)));
        }

        foreach (var d in disputes)
        {
            var action = fileType is DisputeExchangeFileType.OdrComplaint or DisputeExchangeFileType.UdirComplaint ? fileType.ToString().ToUpperInvariant() : "DISPUTE";
            lines.Add(string.Join('|', "DSP", action, d.Id, Escape(d.DisputeReference), string.Empty, Escape(d.Rrn), Escape(d.Stan), Escape(d.MaskedPan), Escape(d.DisputeType.ToString()), d.DisputedAmount.ToString("0.00"), Escape(d.CurrencyCode), Escape(d.CustomerNumber), Escape(d.MerchantName), Escape(d.Channel)));
        }

        lines.Add($"TRL|{chargebackCases.Count + disputes.Count}");
        var content = string.Join('\n', lines) + "\n";
        return new NetworkDisputeExchangeFile
        {
            Network = Network,
            Direction = DisputeExchangeDirection.Outbound,
            FileType = fileType,
            Status = DisputeExchangeStatus.Validated,
            BusinessDate = businessDate,
            FileName = string.IsNullOrWhiteSpace(fileName) ? DefaultFileName(fileType, businessDate) : fileName,
            Content = content,
            ContentSha256 = Sha256(content),
            RecordCount = chargebackCases.Count + disputes.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor
        };
    }

    public IReadOnlyList<NetworkDisputeExchangeRecord> ParseFile(Guid fileId, DisputeExchangeFileType fileType, string content)
    {
        var list = new List<NetworkDisputeExchangeRecord>();
        foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split('|');
            if (p.Length == 0 || p[0] is "HDR" or "TRL") continue;
            if (p[0] is not ("DTL" or "DSP"))
            {
                list.Add(new NetworkDisputeExchangeRecord { FileId = fileId, Network = Network, FileType = fileType, RawRecord = line, ValidationStatus = "Invalid", ValidationError = "Unknown record type" });
                continue;
            }
            list.Add(new NetworkDisputeExchangeRecord
            {
                FileId = fileId,
                Network = Network,
                FileType = fileType,
                LocalCaseReference = p.ElementAtOrDefault(3) ?? string.Empty,
                NetworkCaseId = p.ElementAtOrDefault(4) ?? string.Empty,
                Rrn = p.ElementAtOrDefault(5) ?? string.Empty,
                Stan = p.ElementAtOrDefault(6) ?? string.Empty,
                MaskedPan = p.ElementAtOrDefault(7) ?? string.Empty,
                ReasonCode = p.ElementAtOrDefault(8) ?? string.Empty,
                Amount = decimal.TryParse(p.ElementAtOrDefault(9), out var amt) ? amt : 0m,
                CurrencyCode = p.ElementAtOrDefault(10) ?? string.Empty,
                ActionCode = p.ElementAtOrDefault(1) ?? string.Empty,
                RawRecord = line,
                ValidationStatus = string.IsNullOrWhiteSpace(p.ElementAtOrDefault(5)) ? "Invalid" : "Valid",
                ValidationError = string.IsNullOrWhiteSpace(p.ElementAtOrDefault(5)) ? "RRN missing" : string.Empty
            });
        }
        return list;
    }

    public NetworkDisputeValidationResult ValidateFile(NetworkDisputeExchangeFile file, IReadOnlyList<NetworkDisputeExchangeRecord> records)
    {
        var errors = new List<string>();
        if (file.Network != Network) errors.Add($"File network {file.Network} does not match adapter {Network}.");
        if (string.IsNullOrWhiteSpace(file.FileName)) errors.Add("File name is required.");
        if (string.IsNullOrWhiteSpace(file.Content)) errors.Add("File content is empty.");
        if (records.Any(r => r.ValidationStatus == "Invalid")) errors.AddRange(records.Where(r => r.ValidationStatus == "Invalid").Select(r => r.ValidationError));
        if (file.RecordCount != records.Count && file.Direction == DisputeExchangeDirection.Inbound) errors.Add($"Record count mismatch. Header/body={file.RecordCount}, parsed={records.Count}.");
        return errors.Count == 0 ? NetworkDisputeValidationResult.Ok() : new NetworkDisputeValidationResult(false, errors);
    }

    protected virtual string DefaultFileName(DisputeExchangeFileType fileType, DateOnly businessDate) => $"{SchemePrefix}_{fileType}_{businessDate:yyyyMMdd}_{DateTimeOffset.UtcNow:HHmmss}.txt";

    protected static string Escape(string value) => (value ?? string.Empty).Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
    protected static string Sha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class VisaDisputeFormatAdapter : DelimitedNetworkDisputeFormatAdapter
{
    public override DisputeExchangeNetwork Network => DisputeExchangeNetwork.Visa;
    protected override string SchemePrefix => "VISA_VROL";
}

public sealed class MastercardDisputeFormatAdapter : DelimitedNetworkDisputeFormatAdapter
{
    public override DisputeExchangeNetwork Network => DisputeExchangeNetwork.Mastercard;
    protected override string SchemePrefix => "MC_MCOM";
}

public sealed class NpciRupayDisputeFormatAdapter : DelimitedNetworkDisputeFormatAdapter
{
    public override DisputeExchangeNetwork Network => DisputeExchangeNetwork.NpciRupay;
    protected override string SchemePrefix => "NPCI_RUPAY_UDIR";
}

public sealed class NpciNfsDisputeFormatAdapter : DelimitedNetworkDisputeFormatAdapter
{
    public override DisputeExchangeNetwork Network => DisputeExchangeNetwork.NpciNfs;
    protected override string SchemePrefix => "NPCI_NFS_UDIR";
}

public sealed class RbiOdrDisputeFormatAdapter : DelimitedNetworkDisputeFormatAdapter
{
    public override DisputeExchangeNetwork Network => DisputeExchangeNetwork.RbiOdr;
    protected override string SchemePrefix => "RBI_ODR";
}

public sealed class NpciUdirDisputeFormatAdapter : DelimitedNetworkDisputeFormatAdapter
{
    public override DisputeExchangeNetwork Network => DisputeExchangeNetwork.NpciUdir;
    protected override string SchemePrefix => "NPCI_UDIR";
}

public sealed class SimulatedNetworkDisputeTransportGateway : INetworkDisputeTransportGateway
{
    public DisputeExchangeNetwork Network { get; }
    private readonly string _transport;

    public SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork network, string transport = "SFTP/API-SIM")
    {
        Network = network;
        _transport = transport;
    }

    public Task<NetworkDisputeTransportAck> TransmitAsync(NetworkDisputeExchangeFile file, CancellationToken cancellationToken = default)
    {
        var batch = $"{Network}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{file.Id.ToString("N")[..8]}";
        var tx = $"{_transport}-{Guid.NewGuid():N}";
        return Task.FromResult(new NetworkDisputeTransportAck(batch, tx, "00", $"{Network} dispute exchange accepted {file.RecordCount} record(s).", DisputeExchangeStatus.Acknowledged));
    }

    public Task<NetworkDisputeTransportAck> QueryStatusAsync(string externalBatchReference, CancellationToken cancellationToken = default)
        => Task.FromResult(new NetworkDisputeTransportAck(externalBatchReference, $"QUERY-{Guid.NewGuid():N}", "00", "Acknowledged", DisputeExchangeStatus.Acknowledged));
}
