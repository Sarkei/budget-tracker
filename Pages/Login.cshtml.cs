using BudgetTracker.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace BudgetTracker.Pages;

public class LoginModel : PageModel
{
    private readonly UserService _users;

    public LoginModel(UserService users) => _users = users;

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        // If no users exist at all, redirect to registration
        if (await _users.IsFirstRunAsync())
            return Redirect("/register");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, string? email = null, string? password = null)
    {
        email    ??= "";
        password ??= "";

        var (outcome, user) = await _users.ValidateLoginAsync(email, password);
        if (outcome != LoginOutcome.Success)
        {
            Email = email;
            ErrorMessage = outcome == LoginOutcome.AccountDisabled
                ? "Ihr Account wurde deaktiviert. Bitte wenden Sie sich an den Administrator."
                : "E-Mail oder Passwort falsch.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user!.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName ?? user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        var dest = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        return Redirect(dest);
    }
}
