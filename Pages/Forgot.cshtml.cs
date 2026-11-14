using BudgetTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BudgetTracker.Pages;

public class ForgotModel : PageModel
{
    private readonly PasswordResetService _resets;
    private readonly ILogger<ForgotModel> _log;

    public ForgotModel(PasswordResetService resets, ILogger<ForgotModel> log)
    {
        _resets = resets;
        _log = log;
    }

    public bool Sent { get; private set; }
    public string Email { get; set; } = "";
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? email = null)
    {
        Email = (email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Bitte gib deine E-Mail-Adresse ein.";
            return Page();
        }

        // Immer dieselbe Antwort - ob die Adresse existiert, verrät die Seite nicht.
        try
        {
            await _resets.CreateAsync(Email);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Reset-Mail konnte nicht angelegt werden");
            ErrorMessage = "Die Anfrage konnte nicht verarbeitet werden. Bitte später erneut versuchen.";
            return Page();
        }

        Sent = true;
        return Page();
    }
}
