# Fitness Tracker Style Guide (1:1)

Diese Datei ist die Kopiervorlage, um den `fitness_tracker` Look in andere Projekte zu bringen.

## Kern-Tokens (Dark, wie im Fitness Tracker)

- `--brand: #14b8a6`
- `--brand-muted: #99f6e4`
- `--background: #171717`
- `--foreground: #f5f5f5`
- `--card-bg: #262626`
- `--border-muted: #404040`
- `--text-muted: #a3a3a3`
- `--text-placeholder: #737373`

## Typografie

- Schrift: `Inter`
- Headings und Labels: häufig `uppercase`, `font-black`, `tracking-widest`
- Sehr kompakte Micro-Labels (`8px` bis `10px`) mit viel Letter-Spacing

## Komponentenmuster

- **Primär-Button:** Brand-Outline + Brand-Tint (`bg-brand/15`) + `rounded-xl`
- **Selected-State:** `border-brand`, `bg-brand/15`
- **Cards:** dunkler Card-Layer + Border (`#404040`) + große Radien (`rounded-2xl`)
- **Bottom Nav:** fixed unten, blur, klarer aktiver Zustand in Brand-Farbe
- **Sidebar:** dunkles Panel, aktive Links mit Brand-Tint
- **Modals/Overlays:** `bg-black/70..80` + `backdrop-blur`

## Globale CSS-Basis (Copy/Paste)

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap');

:root {
  --brand: #14b8a6;
  --brand-muted: #99f6e4;
  --background: #171717;
  --foreground: #f5f5f5;
  --text-primary: var(--foreground);
  --card-bg: #262626;
  --border-muted: #404040;
  --text-muted: #a3a3a3;
  --text-placeholder: #737373;
}

html, body {
  min-height: 100dvh;
  background: var(--background);
  color: var(--foreground);
  font-family: "Inter", sans-serif;
}

button, [role='button'] {
  box-shadow: none !important;
}

.ui-btn-brand {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: .5rem;
  border: 2px solid var(--brand);
  background: color-mix(in srgb, var(--brand) 15%, transparent);
  color: var(--brand);
  border-radius: .75rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: .08em;
}

.ui-mark-selected {
  border: 2px solid var(--brand) !important;
  background: color-mix(in srgb, var(--brand) 15%, transparent) !important;
  color: var(--text-primary) !important;
}
```

## Umsetzung in budget_tracker

1. **Tokens + globale Utilities:** `wwwroot/app.css` (MudBlazor-Overrides, `.bt-card`, `.ui-btn-brand`, …)
2. **Seiten & Layout:** `wwwroot/pages.css` (Mobile-Nav, Settings, Transaktionen, Reports, Import-Dialoge, …)
3. **Login/Register:** `wwwroot/auth.css` + gemeinsame Tokens aus `app.css`
4. **Seitenköpfe:** `Components/Shared/PageToolbar.razor` (`.bt-page-title`, `.bt-pill-group`)
5. **Layout:** `Components/Layout/MainLayout.razor` – keine inline `<style>`-Blöcke mehr
6. **MudTheme:** `PaletteDark` in `MainLayout.razor` an Fitness-Werte (`#14b8a6`, `#262626`, `#404040`, …)

Keine duplizierten `<style>`-Blöcke in `.razor`/`.cshtml` – alles zentral in CSS.

## Stand

- Vollständiger Style-Rollout gemäß diesem Guide (Mai 2026).

