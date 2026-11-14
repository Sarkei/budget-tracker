using System.Text.RegularExpressions;
using BudgetTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BudgetTracker.Pages;

public class ResetModel : PageModel
{
    private readonly PasswordResetService _resets;
    private readonly ILogger<ResetModel> _log;

    public ResetModel(PasswordResetService resets, ILogger<ResetModel> log)
    {
        _resets = resets;
        _log = log;
    }

    public string Token { get; set; } = "";
    public bool TokenInvalid { get; private set; }
    public bool Done { get; private set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? token = null)
    {
        Token = token ?? "";
        TokenInvalid = string.IsNullOrWhiteSpace(Token) || !await _resets.IsValidAsync(Token);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? token = null, string? password = null, string? password2 = null)
    {
        Token = token ?? "";
        password ??= "";
        password2 ??= "";

        var probleme = PruefePasswort(password);
        if (probleme.Count > 0)
        {
            ErrorMessage = string.Join(" ", probleme);
            return Page();
        }
        if (password != password2)
        {
            ErrorMessage = "Die beiden Passwörter stimmen nicht überein.";
            return Page();
        }

        try
        {
            if (!await _resets.ResetAsync(Token, password))
            {
                TokenInvalid = true;
                return Page();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Passwort konnte nicht gesetzt werden");
            ErrorMessage = "Das Passwort konnte nicht gespeichert werden. Bitte neuen Link anfordern.";
            return Page();
        }

        Done = true;
        return Page();
    }

    private static List<string> PruefePasswort(string p)
    {
        var m = new List<string>();
        if (p.Length < 8) m.Add("Mindestens 8 Zeichen.");
        if (!Regex.IsMatch(p, "[A-Z]")) m.Add("Ein Großbuchstabe fehlt.");
        if (!Regex.IsMatch(p, "[a-z]")) m.Add("Ein Kleinbuchstabe fehlt.");
        if (!Regex.IsMatch(p, "[0-9]")) m.Add("Eine Zahl fehlt.");
        if (!Regex.IsMatch(p, "[^a-zA-Z0-9]")) m.Add("Ein Sonderzeichen fehlt.");
        return m;
    }
}
