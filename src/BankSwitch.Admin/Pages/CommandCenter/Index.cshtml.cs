using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.CommandCenter;

public sealed class IndexModel : PageModel
{
    private readonly IHostEnvironment _environment;
    public IndexModel(IHostEnvironment environment) => _environment = environment;
    public string EnvironmentName => _environment.EnvironmentName;
    public void OnGet() { }
}
