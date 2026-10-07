using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Configuration;

[Authorize(Policy = "ConfigMakerOrChecker")]
public sealed class ControlPlaneModel : PageModel
{
    public void OnGet() { }
}
