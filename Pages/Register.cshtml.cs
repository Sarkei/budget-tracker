using BudgetTracker.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace BudgetTracker.Pages;

public class RegisterModel : PageModel
{
    private readonly UserService _users;

    public RegisterModel(UserService users) => _users = users;

    public bool IsFirstUser { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string FirstName { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string LastName { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        IsFirstUser = await _users.IsFirstRunAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        string? password = null,
        string? password2 = null)
    {
        IsFirstUser = await _users.IsFirstRunAsync();

        firstName ??= "";
        lastName  ??= "";
        email     ??= "";
        password  ??= "";
        password2 ??= "";

        FirstName = firstName;
        LastName  = lastName;
        Email     = email;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            ErrorMessage = "Vor- und Nachname sind erforderlich.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            ErrorMessage = "Bitte eine gültige E-Mail-Adresse eingeben.";
            return Page();
        }

        var pwError = ValidatePassword(password);
        if (pwError != null)
        {
            ErrorMessage = pwError;
            return Page();
        }

        if (password != password2)
        {
            ErrorMessage = "Passwörter stimmen nicht überein.";
            return Page();
        }

        var (outcome, user) = await _users.RegisterAsync(email, password, firstName, lastName);
        if (outcome == RegisterOutcome.AlreadyExists)
        {
            ErrorMessage = "Ein Konto mit dieser E-Mail-Adresse existiert bereits. Bitte melden Sie sich an.";
            return Page();
        }
        if (outcome == RegisterOutcome.NotTrusted)
        {
            ErrorMessage = "Diese E-Mail-Adresse ist nicht zur Registrierung freigegeben. Bitte wenden Sie sich an den Administrator.";
            return Page();
        }

        // Auto-login after registration
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

        return Redirect("/");
    }

    // Minimum security: 8+ chars, 1 uppercase, 1 lowercase, 1 digit
    private static string? ValidatePassword(string pw)
    {
        if (pw.Length < 8)
            return "Passwort muss mindestens 8 Zeichen lang sein.";
        if (!Regex.IsMatch(pw, @"[A-Z]"))
            return "Passwort muss mindestens einen Großbuchstaben enthalten.";
        if (!Regex.IsMatch(pw, @"[a-z]"))
            return "Passwort muss mindestens einen Kleinbuchstaben enthalten.";
        if (!Regex.IsMatch(pw, @"[0-9]"))
            return "Passwort muss mindestens eine Zahl enthalten.";
        return null;
    }
}
