using BankSwitch.Application;
using Xunit;

namespace BankSwitch.Tests;

public sealed class NdcLodManagementV448Tests
{
    private static string FixturePath()
    {
        var candidates = new[] {
            Path.Combine(AppContext.BaseDirectory,"fixtures","ndc","ANDC306EMVXT25G.lod"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","fixtures","ndc","ANDC306EMVXT25G.lod")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"tests","fixtures","ndc","ANDC306EMVXT25G.lod"))
        };
        return candidates.First(File.Exists);
    }

    [Fact]
    public void Golden_ANDC306EMVXT25G_Parses_Expected_Record_Classes()
    {
        var bytes=File.ReadAllBytes(FixturePath());
        var analysis=new NdcLodParser().Parse("ANDC306EMVXT25G.lod",bytes);
        Assert.Equal(18469,analysis.SizeBytes);
        Assert.Equal(36,analysis.EtXRecordCount);
        Assert.True(analysis.ScreenCount > 300);
        Assert.True(analysis.StateCount > 100);
        Assert.Equal(3,analysis.ConfigurationRecordCount);
        Assert.Equal(4,analysis.EmvRecordCount);
        Assert.Contains(analysis.Records,x=>x.Section==NdcLodSectionType.Screens&&x.SectionCode=="11");
        Assert.Contains(analysis.Records,x=>x.Section==NdcLodSectionType.States&&x.SectionCode=="12");
        Assert.Contains(analysis.Records,x=>x.Section==NdcLodSectionType.ExtendedConfiguration&&x.SectionCode=="1A");
    }

    [Theory]
    [InlineData("A0000000031010")]
    [InlineData("A0000000041010")]
    [InlineData("A0000000043060")]
    [InlineData("A0000000651010")]
    [InlineData("A0000005241010")]
    [InlineData("A0000001523010")]
    public void Golden_Lod_Contains_Expected_Emv_Aids(string aid)
    {
        var a=new NdcLodParser().Parse("ANDC306EMVXT25G.lod",File.ReadAllBytes(FixturePath()));
        Assert.Contains(a.EmvApplications,x=>x.Aid.Equals(aid,StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Golden_Lod_Contains_Supervisor_And_Cash_Replenishment_Text()
    {
        var a=new NdcLodParser().Parse("ANDC306EMVXT25G.lod",File.ReadAllBytes(FixturePath()));
        var text=string.Join(" ",a.Screens.Select(x=>x.PrintableText));
        Assert.Contains("SUPERVISOR MAIN MENU",text,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CASH REPLENISHMENT MENU",text,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ENTER PIN",text,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Management_Requires_Approval_Then_Generates_Block_Frames()
    {
        var repo=new InMemoryNdcLodRepository();
        var parser=new NdcLodParser();
        var ndcRepo=new InMemoryNdcProtocolRepository();
        var ndc=new NdcProtocolEngine(ndcRepo,new NdcProtocolCodec(new HmacNdcMacProvider()),new SystemClock());
        var svc=new NdcLodManagementService(repo,parser,ndc,new SystemClock());
        var bytes=File.ReadAllBytes(FixturePath());
        var up=await svc.UploadAsync(new UploadNdcLodRequest("Golden ATM Load","3.06-EMV-XT25G","ANDC306EMVXT25G.lod",Convert.ToBase64String(bytes),"NCR","APTRA/NDC"),"maker");
        Assert.True(up.IsSuccess);
        var id=up.Value!.Package.Id;
        Assert.True((await svc.SubmitForApprovalAsync(id,"maker")).IsSuccess);
        Assert.False((await svc.ApproveAsync(id,"maker")).IsSuccess);
        Assert.True((await svc.ApproveAsync(id,"checker")).IsSuccess);
        var scheduled=await svc.ScheduleAsync(new ScheduleNdcLodDeploymentRequest(id,"ATM00001",AtmProtocol.NdcPlus,1024,"lod-test"),"maker");
        Assert.True(scheduled.IsSuccess);
        var frames=await svc.GenerateDeploymentFramesAsync(scheduled.Value!.Id,"maker");
        Assert.True(frames.IsSuccess);
        Assert.True(frames.Value!.FramesBase64.Count>10);
    }
}
