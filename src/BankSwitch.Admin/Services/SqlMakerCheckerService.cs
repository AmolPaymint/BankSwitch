using BankSwitch.Application;
using BankSwitch.Infrastructure;
// using Microsoft.Data.SqlClient;
using Npgsql;

namespace BankSwitch.Admin.Services;

public sealed class SqlMakerCheckerService : IMakerCheckerService
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    private readonly IAuditLogger _audit;

    public SqlMakerCheckerService(SecurePostgresConnectionFactory connectionFactory, IAuditLogger audit)
    {
        _connectionFactory = connectionFactory;
        _audit = audit;
    }
    public IReadOnlyCollection<ConfigChangeRequest> GetAll()
    {
        using var connection = _connectionFactory.Create();
        connection.Open();
        using var command = new NpgsqlCommand("""
        SELECT id, correlationid, area, oldvalue, newvalue, maker, checker,approvedat, effectiveat, reason, ticketreference, createdat, state
        FROM configchangerequests
        ORDER BY createdat DESC
        """, connection);
        using var reader = command.ExecuteReader();
        var results = new List<ConfigChangeRequest>();
        while (reader.Read())
        {
            results.Add(Read(reader));
        }
        return results;
    }

    public ConfigChangeRequest Draft(string area, string oldValue, string newValue, string maker, string reason, string ticketReference, DateTimeOffset? effectiveAt)
    {
        var request = new ConfigChangeRequest
        {
            Area = area,
            OldValue = oldValue,
            NewValue = newValue,
            Maker = maker,
            Reason = reason,
            TicketReference = ticketReference,
            EffectiveAt = effectiveAt,
            State = MakerCheckerState.Draft
        };
        using var connection = _connectionFactory.Create();
        connection.Open();
        using var command = new NpgsqlCommand("""
        INSERT INTO configchangerequests
        (
            id,
            correlationid,
            area,
            oldvalue,
            newvalue,
            maker,
            checker,
            approvedat,
            effectiveat,
            reason,
            ticketreference,
            createdat,
            state
        )
        VALUES
        (
            @Id,
            @CorrelationId,
            @Area,
            @OldValue,
            @NewValue,
            @Maker,
            '',
            NULL,
            @EffectiveAt,
            @Reason,
            @TicketReference,
            @CreatedAt,
            @State
        )
        """, connection);

        Bind(command, request);

        command.ExecuteNonQuery();

        _audit.LogAdminAudit(
            request.CorrelationId,
            maker,
            "DraftConfigChange",
            oldValue,
            newValue,
            reason,
            ticketReference);

        return request;
    }
    public ConfigChangeRequest Submit(Guid id, string actor) => Mutate(id, actor, MakerCheckerState.Draft, MakerCheckerState.Submitted, "SubmitConfigChange");

    public ConfigChangeRequest Approve(Guid id, string checker)
    {
        var item = Get(id);
        if (item.State != MakerCheckerState.Submitted) throw new InvalidOperationException("Only submitted changes can be approved.");
        if (string.Equals(item.Maker, checker, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Maker cannot approve their own change.");
        var next = item.EffectiveAt.HasValue && item.EffectiveAt.Value > DateTimeOffset.UtcNow ? MakerCheckerState.Scheduled : MakerCheckerState.Approved;
        var updated = item with { Checker = checker, ApprovedAt = DateTimeOffset.UtcNow, State = next };
        SaveState(updated);
        _audit.LogAdminAudit(updated.CorrelationId, checker, "ApproveConfigChange", updated.OldValue, updated.NewValue, updated.Reason, updated.TicketReference);
        return updated;
    }

    public ConfigChangeRequest Activate(Guid id, string actor) => Mutate(id, actor, MakerCheckerState.Approved, MakerCheckerState.Active, "ActivateConfigChange", allowFromScheduled: true);

    public ConfigChangeRequest Archive(Guid id, string actor)
    {
        var item = Get(id);
        var updated = item with { State = MakerCheckerState.Archived };
        SaveState(updated);
        _audit.LogAdminAudit(updated.CorrelationId, actor, "ArchiveConfigChange", updated.OldValue, updated.NewValue, updated.Reason, updated.TicketReference);
        return updated;
    }

    private ConfigChangeRequest Mutate(Guid id, string actor, MakerCheckerState expected, MakerCheckerState nextState, string action, bool allowFromScheduled = false)
    {
        var item = Get(id);
        if (item.State != expected && !(allowFromScheduled && item.State == MakerCheckerState.Scheduled))
        {
            throw new InvalidOperationException($"Change is {item.State}; expected {expected}.");
        }
        var updated = item with { State = nextState };
        SaveState(updated);
        _audit.LogAdminAudit(updated.CorrelationId, actor, action, updated.OldValue, updated.NewValue, updated.Reason, updated.TicketReference);
        return updated;
    }
    private ConfigChangeRequest Get(Guid id)
    {
        using var connection = _connectionFactory.Create();
        connection.Open();
        using var command = new NpgsqlCommand("""
        SELECT id, correlationid, area, oldvalue, newvalue, maker, checker,approvedat, effectiveat, reason, ticketreference, createdat, state
        FROM dbo.configchangerequests
        WHERE id = @Id
        LIMIT 1
        """, connection);
        command.Parameters.AddWithValue("@Id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new KeyNotFoundException("Config change was not found.");
        return Read(reader);
    }

    private void SaveState(ConfigChangeRequest request)
    {
        using var connection = _connectionFactory.Create();
        connection.Open();
        using var command = new NpgsqlCommand("""
        UPDATE dbo.configchangerequests
        SET checker = @Checker,
            approvedat = @ApprovedAt,
            effectiveat = @EffectiveAt,
            state = @State,
            updatedat = CLOCK_TIMESTAMP()
        WHERE id = @Id
        """, connection);
        command.Parameters.AddWithValue("@Id", request.Id);
        command.Parameters.AddWithValue("@Checker", request.Checker ?? string.Empty);
        command.Parameters.AddWithValue("@ApprovedAt", (object?)request.ApprovedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@EffectiveAt", (object?)request.EffectiveAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@State", request.State.ToString());
        command.ExecuteNonQuery();
    }
    private static void Bind(NpgsqlCommand command, ConfigChangeRequest request)
    {
        command.Parameters.AddWithValue("@Id", request.Id);
        command.Parameters.AddWithValue("@CorrelationId", request.CorrelationId);
        command.Parameters.AddWithValue("@Area", request.Area);
        command.Parameters.AddWithValue("@OldValue", request.OldValue);
        command.Parameters.AddWithValue("@NewValue", request.NewValue);
        command.Parameters.AddWithValue("@Maker", request.Maker);
        command.Parameters.AddWithValue("@EffectiveAt", (object?)request.EffectiveAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@Reason", request.Reason);
        command.Parameters.AddWithValue("@TicketReference", request.TicketReference);
        command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
        command.Parameters.AddWithValue("@State", request.State.ToString());
    }

    private static ConfigChangeRequest Read(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CorrelationId = reader.GetString(1),
        Area = reader.GetString(2),
        OldValue = reader.GetString(3),
        NewValue = reader.GetString(4),
        Maker = reader.GetString(5),
        Checker = reader.GetString(6),
        ApprovedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
        EffectiveAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
        Reason = reader.GetString(9),
        TicketReference = reader.GetString(10),
        CreatedAt = reader.GetDateTime(11),
        State = Enum.TryParse<MakerCheckerState>(reader.GetString(12), out var state) ? state : MakerCheckerState.Draft
    };
}
