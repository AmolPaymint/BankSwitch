using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BankSwitch.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankSwitch.Admin.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly IAdminAuthService _authService;

    public LoginModel(IAdminAuthService authService) => _authService = authService;

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string ErrorMessage { get; private set; } = string.Empty;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var result = await _authService.ValidateAsync(Input.Username, Input.Password, Input.MfaCode, HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (!result.IsAuthenticated)
        {
            ErrorMessage = result.FailureReason;
            return Page();
        }

        var identity = new ClaimsIdentity(result.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);
        return RedirectToPage("/Index");
    }

    public sealed class LoginInput
    {
        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        [Required, MinLength(14)]
        public string Password { get; set; } = string.Empty;
        [Required, RegularExpression("^[0-9]{6}$")]
        public string MfaCode { get; set; } = string.Empty;
    }
}
