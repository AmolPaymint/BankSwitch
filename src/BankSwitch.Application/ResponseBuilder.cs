using BankSwitch.Domain;

namespace BankSwitch.Application;

public static class ResponseBuilder
{
    public static IsoMessage Decline(IsoMessage request, string responseCode)
    {
        var responseMti = request.Mti switch
        {
            "0100" => "0110",
            "0200" => "0210",
            "0220" => "0230",
            "0400" => "0410",
            "0420" => "0430",
            "0421" => "0430",
            _ => request.Mti.Length == 4 ? request.Mti[..2] + "10" : "0210"
        };
        return request.CloneResponse(responseMti, responseCode);
    }

    /// <summary>
    /// Builds an approved ISO response (code 00) carrying a stand-in or local
    /// authorization code in field 38. Used when no sink was reachable and
    /// stand-in processing approved the transaction.
    /// </summary>
    public static IsoMessage Approve(IsoMessage request, string authorizationCode)
    {
        var response = Decline(request, "00");
        if (!string.IsNullOrWhiteSpace(authorizationCode))
            response.SetField(38, authorizationCode);
        return response;
    }
}
