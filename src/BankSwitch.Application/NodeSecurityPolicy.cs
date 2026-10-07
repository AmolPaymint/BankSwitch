using System.Net;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public static class NodeSecurityPolicy
{
    public static PolicyDecision ValidateSourceConnection(SourceNode sourceNode, string remoteIp, string? certificateThumbprint, bool isPrivateNetwork)
    {
        if (!sourceNode.IsActive) return PolicyDecision.Blocked("91", "Source node inactive.");
        return ValidateCommon(sourceNode.NodeId, sourceNode.Security, remoteIp, certificateThumbprint, isPrivateNetwork);
    }

    public static PolicyDecision ValidateSinkConnection(SinkNode sinkNode, string remoteIp, string? certificateThumbprint, bool isPrivateNetwork)
    {
        if (!sinkNode.IsActive) return PolicyDecision.Blocked("91", "Sink node inactive.");
        return ValidateCommon(sinkNode.NodeId, sinkNode.Security, remoteIp, certificateThumbprint, isPrivateNetwork);
    }

    private static PolicyDecision ValidateCommon(string nodeId, NodeSecurityProfile profile, string remoteIp, string? certificateThumbprint, bool isPrivateNetwork)
    {
        if (profile.RequirePrivateNetwork && !isPrivateNetwork)
        {
            return PolicyDecision.Blocked("91", $"Node {nodeId} must connect through VPN/MPLS/private network.");
        }

        if (profile.RequireMtls)
        {
            if (string.IsNullOrWhiteSpace(certificateThumbprint)) return PolicyDecision.Blocked("91", "Client certificate missing.");
            if (!string.Equals(NormalizeThumbprint(profile.CertificateThumbprint), NormalizeThumbprint(certificateThumbprint), StringComparison.OrdinalIgnoreCase))
            {
                return PolicyDecision.Blocked("91", "Certificate thumbprint mismatch.");
            }
        }

        if (profile.AllowedCidrs.Count > 0 && !profile.AllowedCidrs.Any(cidr => IsIpInCidr(remoteIp, cidr)))
        {
            return PolicyDecision.Blocked("91", $"IP {remoteIp} is not whitelisted for node {nodeId}.");
        }

        return PolicyDecision.Allowed();
    }

    private static string NormalizeThumbprint(string? value) => (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();

    public static bool IsPrivateAddress(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || bytes[0] == 0xfd || bytes[0] == 0xfc;
        }
        return false;
    }

    private static bool IsIpInCidr(string remoteIp, string rule)
    {
        if (string.IsNullOrWhiteSpace(rule)) return false;
        if (!IPAddress.TryParse(remoteIp, out var ip)) return false;
        if (!rule.Contains('/')) return string.Equals(remoteIp, rule, StringComparison.OrdinalIgnoreCase);
        var parts = rule.Split('/', 2, StringSplitOptions.TrimEntries);
        if (!IPAddress.TryParse(parts[0], out var network)) return false;
        if (!int.TryParse(parts[1], out var prefixLength)) return false;
        var ipBytes = ip.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (ipBytes.Length != networkBytes.Length) return false;
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != networkBytes[i]) return false;
        }
        if (remainingBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remainingBits));
        return (ipBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}

public sealed record PolicyDecision(bool IsAllowed, string ResponseCode, string Reason)
{
    public static PolicyDecision Allowed() => new(true, "00", "Allowed");
    public static PolicyDecision Blocked(string responseCode, string reason) => new(false, responseCode, reason);
}
