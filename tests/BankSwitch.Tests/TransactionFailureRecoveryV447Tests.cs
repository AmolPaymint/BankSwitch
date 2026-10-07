using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Xunit;

namespace BankSwitch.Tests;

public sealed class TransactionFailureRecoveryV447Tests
{
    [Fact]
    public async Task State_repository_returns_only_latest_state_older_than_cutoff()
    {
        var repo = new InMemoryTransactionStateRepository();
        var old = DateTimeOffset.UtcNow.AddMinutes(-10);
        var recent = DateTimeOffset.UtcNow.AddMinutes(-1);

        await repo.RecordTransitionAsync(new TransactionStateRecord { CorrelationId="A", Stan="1", SourceNodeId="SRC", PreviousState=TransactionLifecycleState.Validated, NewState=TransactionLifecycleState.ForwardedToSink, OccurredAt=old });
        await repo.RecordTransitionAsync(new TransactionStateRecord { CorrelationId="A", Stan="1", SourceNodeId="SRC", PreviousState=TransactionLifecycleState.ForwardedToSink, NewState=TransactionLifecycleState.Responded, OccurredAt=recent });
        await repo.RecordTransitionAsync(new TransactionStateRecord { CorrelationId="B", Stan="2", SourceNodeId="SRC", PreviousState=TransactionLifecycleState.Validated, NewState=TransactionLifecycleState.ForwardedToSink, OccurredAt=old });

        var stuck = await repo.GetTransactionsInStateAsync(TransactionLifecycleState.ForwardedToSink, DateTimeOffset.UtcNow.AddMinutes(-5), 100);

        Assert.Single(stuck);
        Assert.Equal("B", stuck[0].CorrelationId);
    }

    [Fact]
    public async Task Resolved_recovery_snapshot_is_not_due()
    {
        var repo = new InMemoryTransactionRecoverySnapshotRepository();
        var snapshot = BuildSnapshot(DateTimeOffset.UtcNow.AddMinutes(-10));
        await repo.UpsertPendingAsync(snapshot);
        await repo.MarkResolvedAsync(snapshot.CorrelationId, "00");

        var due = await repo.GetDueAsync(DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow, 5, 100);
        Assert.Empty(due);
    }

    [Fact]
    public async Task Recovery_service_sends_durable_0420_to_original_sink_and_marks_reversed()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var snapshotRepo = new InMemoryTransactionRecoverySnapshotRepository();
        var stateRepo = new InMemoryTransactionStateRepository();
        var audit = new NoopAudit();
        var stateMachine = new TransactionStateMachine(stateRepo, clock, audit);
        var sink = new SinkNode { NodeId="SINK-01", Name="Issuer", Host="127.0.0.1", Port=5001, IsActive=true };
        var nodes = new FakeNodes(sink);
        var sinkClient = new FakeSinkClient("00");
        var protector = new DevelopmentSensitiveDataProtector();
        var formatter = new Application.Iso8583AsciiBitmapFormatter();
      //  var formatter = new Iso8583AsciiBitmapFormatter();
        var snapshot = BuildProtectedSnapshot(clock.UtcNow.AddMinutes(-10), sink, protector, formatter);
        await snapshotRepo.UpsertPendingAsync(snapshot);

        var service = new TransactionRecoveryService(snapshotRepo, stateMachine, nodes, sinkClient, protector, formatter, audit, clock,
            new StandInOptions { StuckTransactionThresholdSeconds = 120 }, new ReversalOptions { MaxAttempts = 3 });

        var result = await service.RecoverStuckTransactionsAsync();
        var stored = await snapshotRepo.GetAsync(snapshot.CorrelationId);
        var state = await stateMachine.GetCurrentStateAsync(snapshot.CorrelationId);

        Assert.Equal(1, result.ReversedCount);
        Assert.Equal(TransactionRecoverySnapshotStatus.Reversed, stored!.Status);
        Assert.Equal(TransactionLifecycleState.Reversed, state);
        Assert.Equal(sink.Id, sinkClient.LastSinkId);
        Assert.Equal("0420", sinkClient.LastMessage!.Mti);
        Assert.Equal(snapshot.OriginalDataElement, sinkClient.LastMessage.GetRequiredField(90));
    }

    [Fact]
    public async Task Recovery_rejection_is_retried_not_falsely_marked_reversed()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var snapshotRepo = new InMemoryTransactionRecoverySnapshotRepository();
        var stateRepo = new InMemoryTransactionStateRepository();
        var audit = new NoopAudit();
        var stateMachine = new TransactionStateMachine(stateRepo, clock, audit);
        var sink = new SinkNode { NodeId="SINK-01", Name="Issuer", Host="127.0.0.1", Port=5001, IsActive=true };
        var protector = new DevelopmentSensitiveDataProtector();
        var formatter = new Application.Iso8583AsciiBitmapFormatter();
        //var formatter = new Iso8583AsciiBitmapFormatter();
        var snapshot = BuildProtectedSnapshot(clock.UtcNow.AddMinutes(-10), sink, protector, formatter);
        await snapshotRepo.UpsertPendingAsync(snapshot);

        var service = new TransactionRecoveryService(snapshotRepo, stateMachine, new FakeNodes(sink), new FakeSinkClient("96"), protector, formatter, audit, clock,
            new StandInOptions { StuckTransactionThresholdSeconds = 120 }, new ReversalOptions { MaxAttempts = 3 });

        var result = await service.RecoverStuckTransactionsAsync();
        var stored = await snapshotRepo.GetAsync(snapshot.CorrelationId);

        Assert.Equal(0, result.ReversedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(TransactionRecoverySnapshotStatus.RetryScheduled, stored!.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Null(stored.ResolvedAt);
    }

    [Fact]
    public async Task Recovery_payload_is_encrypted_at_rest_and_round_trips()
    {
        var protector = new DevelopmentSensitiveDataProtector();
        var formatter = new Application.Iso8583AsciiBitmapFormatter();
        //var formatter = new Iso8583AsciiBitmapFormatter();
        var sink = new SinkNode { NodeId="SINK", IsActive=true };
        var snapshot = BuildProtectedSnapshot(DateTimeOffset.UtcNow, sink, protector, formatter);

        Assert.DoesNotContain("5399838383838381", snapshot.ProtectedReversalPayload, StringComparison.Ordinal);
        var base64 = protector.Unprotect(snapshot.ProtectedReversalPayload, "TXN-RECOVERY-REVERSAL");
        var message = formatter.Parse(Convert.FromBase64String(base64));
        Assert.Equal("5399838383838381", message.GetRequiredField(2));
        Assert.Equal("0420", message.Mti);
    }

    private static TransactionRecoverySnapshot BuildSnapshot(DateTimeOffset forwardedAt) => new()
    {
        CorrelationId = Guid.NewGuid().ToString("N"), SourceNodeId="SRC", SinkNodeId=Guid.NewGuid(), Stan="123456", Rrn="123456789012", OriginalMti="0200",
        OriginalDataElement="020012345609241200000000000000000000000000", ProtectedReversalPayload="protected", ForwardedAt=forwardedAt, NextAttemptAt=forwardedAt
    };

   // private static TransactionRecoverySnapshot BuildProtectedSnapshot(DateTimeOffset forwardedAt, SinkNode sink, ISensitiveDataProtector protector, Iso8583AsciiBitmapFormatter formatter)
    private static TransactionRecoverySnapshot BuildProtectedSnapshot(DateTimeOffset forwardedAt, SinkNode sink, ISensitiveDataProtector protector, Application.Iso8583AsciiBitmapFormatter formatter)
    {
        var ode = "020012345609241200000000000000000000000000";
        var reversal = new IsoMessage("0420")
            .SetField(2,"5399838383838381").SetField(3,"000000").SetField(4,"000000001000")
            .SetField(7,"0924120000").SetField(11,"123456").SetField(37,"123456789012")
            .SetField(41,"TERM0001").SetField(49,"356").SetField(90,ode).SetField(123,"000000000000001");
        var protectedPayload = protector.Protect(Convert.ToBase64String(formatter.Format(reversal)), "TXN-RECOVERY-REVERSAL");
        return new TransactionRecoverySnapshot
        {
            CorrelationId=Guid.NewGuid().ToString("N"), SourceNodeId="SRC", SinkNodeId=sink.Id, Stan="123456", Rrn="123456789012", OriginalMti="0200",
            OriginalDataElement=ode, ProtectedReversalPayload=protectedPayload, ForwardedAt=forwardedAt, NextAttemptAt=forwardedAt
        };
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow=now;
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeNodes : INodeRepository
    {
        private readonly SinkNode _sink;
        public FakeNodes(SinkNode sink)=>_sink=sink;
        public Task<SourceNode?> GetSourceNodeAsync(string nodeId, CancellationToken cancellationToken=default)=>Task.FromResult<SourceNode?>(new SourceNode{NodeId=nodeId,Name=nodeId,IsActive=true});
        public Task<SinkNode?> GetSinkNodeAsync(Guid sinkNodeId, CancellationToken cancellationToken=default)=>Task.FromResult<SinkNode?>(sinkNodeId==_sink.Id?_sink:null);
    }

    private sealed class FakeSinkClient : ISinkClient
    {
        private readonly string _responseCode;
        public FakeSinkClient(string responseCode)=>_responseCode=responseCode;
        public Guid LastSinkId { get; private set; }
        public IsoMessage? LastMessage { get; private set; }
        public Task<IsoMessage> SendAsync(IsoMessage message, SinkNode sinkNode, CancellationToken cancellationToken=default)
        {
            LastSinkId=sinkNode.Id; LastMessage=message;
            return Task.FromResult(new IsoMessage("0430").SetField(39,_responseCode));
        }
    }

    private sealed class NoopAudit : IAuditLogger
    {
        public void LogTransaction(TransactionLog log) { }
        public void LogSecurity(string correlationId,string eventName,string message) { }
        public void LogAdminAudit(string correlationId,string actor,string action,string oldValue,string newValue,string reason,string ticketReference) { }
        public void LogSystem(string correlationId,string eventName,Exception? exception=null) { }
        public void LogReconciliation(string correlationId,string settlementProfile,string status,string details) { }
    }
}
