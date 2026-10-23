# Budget Tracker

Selbst gehostete Web-App für private Finanzen: mehrere Konten, Einnahmen und Ausgaben, Daueraufträge, geplante Zahlungen, Spartöpfe und gemeinsame Ausgaben in Gruppen. Läuft als Blazor-Server-Anwendung (.NET 8) mit einer SQLite-Datei im Docker-Container auf eigenem Server oder NAS und wird im Browser bedient – gedacht für eine Person oder einen kleinen Haushalt bzw. Freundeskreis.

## Funktionen

### Konten und Buchungen
- **Mehrere Konten** mit Kontotyp, Anfangssaldo, Währung, Farbe und Icon; Umschalten über die Seitenleiste oder Ansicht „Alle Konten“, ein Konto ist als Standard markiert.
- **Transaktionen** (Einnahme/Ausgabe) mit Kategorie, Datum und Notiz, inklusive Filter nach Zeitraum, Typ, Kategorie und Suchtext.
- **Fremdwährung**: Originalbetrag und Originalwährung werden gespeichert, umgerechnet wird nach EUR; der Kurs kommt live von der Frankfurter-API.
- **Kategorien** mit Icon, Farbe und Typ (Einnahme/Ausgabe/beides); beim ersten Start werden 19 Standardkategorien angelegt.

### Planung
- **Wiederkehrende Posten** mit Intervall wöchentlich/monatlich/quartalsweise/jährlich, Startdatum und optionalem Enddatum; Ratenzahlungs-Modus mit Gesamtzahl und bereits bezahlten Raten.
- **Geplante Zahlungen** mit Fälligkeitsdatum; „bezahlt“ setzen erzeugt eine echte Transaktion. Summen für „ausstehend“ und „nächste 30 Tage“.
- **Spartöpfe** mit Zielbetrag, manuell gepflegtem Stand und Fortschrittsbalken – nicht an Transaktionen gekoppelt.

### Auswertungen
- **Dashboard**: aktueller Saldo, 30-Tage-Prognose (inkl. geplanter und wiederkehrender Posten), Monats-Einnahmen/-Ausgaben, Balkendiagramm über 6 Monate, Kategorien-Donut, Kontostand-Verlauf der letzten 30 Tage, Karten je Konto.
- **Finanzübersicht** und **Statistik**: Kennzahlen für den gewählten Zeitraum (Einnahmen, Ausgaben, Bilanz, Ø pro Monat), Monatsverlauf, kumulierter Cashflow, Donuts für Ausgaben und Einnahmen nach Kategorie.
- **Berichte**: Monatsbericht mit Kategorien, größten Ausgaben und Vormonatsvergleich; „PDF exportieren“ öffnet den Browser-Druckdialog (`window.print`), es wird keine PDF-Datei erzeugt.
- **Kontoauszug-Analyse**: Jahres-Kennzahlen, erkannte wiederkehrende Ausgaben, Top-Händler, größte Einzelausgaben, Ausgaben nach Kategorie.

### Kontoauszug-Import (CSV)
- Import von Kontoauszug-CSV; die Bank wird an den Spaltennamen erkannt (Sparkasse, DKB, ING, Commerzbank, Volksbank, sonst generisch).
- Vorschau mit auswählbaren Zeilen und Zielkonto, automatischer Kategorievorschlag über Händler-Keywords (Lebensmittel, Abos, Tanken, Gesundheit, Shopping, Energie, Telekom, Miete u. a.) und Duplikaterkennung innerhalb der Datei und gegen die Datenbank.

### Gemeinsame Ausgaben
- **Gruppen** mit Name, Slug und Farbe, Mitglieder als Admin oder Member; Einladung per E-Mail an bereits registrierte Nutzer, Annehmen/Ablehnen über die Glocke im Layout. Offene Gruppenansichten aktualisieren sich live über einen Singleton-Notifier.
- **Gruppenausgaben** mit „bezahlt von“, Kategorie, Datum und Aufteilung gleichmäßig oder prozentual/manuell je Mitglied; optional wiederkehrend und als Einnahme markierbar.
- **Salden** je Mitglied („du bekommst zurück“ / „du schuldest“) inklusive Ausgleichszahlungen in Teilbeträgen und Ausgleichshistorie.
- **Gruppenbericht** je Monat sowie **CSV-Import** für Gruppenhistorie (`Datum;Beschreibung;Zahler;Betrag;Währung;Notiz`); importierte Zeilen bleiben von der Schuldenberechnung ausgenommen.

### Zugang und Verwaltung
- Login und Registrierung über eine eigene Nutzertabelle mit BCrypt-Hashes und Cookie-Authentifizierung (30 Tage, gleitend).
- Der erste registrierte Nutzer wird Admin; danach ist Registrierung nur mit freigeschalteter E-Mail-Adresse möglich.
- Admin-Bereich: Nutzerliste, E-Mail ändern, deaktivieren/aktivieren, endgültig löschen, Liste der freigeschalteten Adressen pflegen.
- Einstellungen: Profil und Passwort, Feature-Schalter (blenden Wiederkehrend, Spartöpfe, geplante Zahlungen und Gruppen aus der Navigation aus), Konto-Karten im Dashboard, Formatierung, Budget-Limit.
- Durchgängig dunkles UI, deutsche Oberfläche, Beträge im de-DE-Format.

## Technik

- **.NET 8 / ASP.NET Core** – Zielplattform `net8.0`, Web-SDK, Nullable und Implicit Usings aktiv.
- **Blazor Server** – Razor Components mit Interactive Server Rendering; Login, Logout und Registrierung als Razor Pages.
- **Entity Framework Core 8.0.11 + SQLite** – `AppDbContext` über `IDbContextFactory`, Datenbank als Datei `budget.db`.
- **MudBlazor 6.21.0** – UI-Komponenten und dunkles Theme.
- **Blazor-ApexCharts 6.1.0** – Balken-, Donut- und Flächendiagramme.
- **BCrypt.Net-Next 4.0.3** – Passwort-Hashing.
- **Cookie-Authentication + DataProtection** – Cookie `BudgetTracker.Auth`, Schlüssel unter `<DATA_PATH>/dp-keys`.
- **Docker** – Multi-Stage-Build von `sdk:8.0` nach `aspnet:8.0`, Port 11111.
- **Frankfurter API** – Wechselkurse mit einer Stunde In-Memory-Cache, Fallback auf Kurs 1,0 bei Fehlern.
- Keine Testprojekte, keine EF-Migrations (Schema über `EnsureCreated` plus manuelle `ALTER`/`CREATE TABLE`-Anweisungen).

## Projektstruktur

```
budget-tracker/
├── Components/
│   ├── App.razor, Routes.razor      # Host-Seite und Router mit Auth-Guard
│   ├── Layout/                      # MainLayout (Navigation, Kontowechsel, Einladungen), NavMenu
│   ├── Pages/                       # Dashboard, Transaktionen, Wiederkehrend, Spartöpfe, Gruppen, Berichte …
│   ├── Dialogs/                     # Anlege-/Bearbeiten-Dialoge inkl. CAMT- und Gruppen-CSV-Import
│   └── Shared/                      # DashboardStatCard, PageToolbar, UiClasses
├── Data/
│   ├── AppDbContext.cs              # DbSets, Beziehungen, Indizes
│   ├── DatabaseInitializer.cs       # Schema-Upgrades, Seed von Kategorien und Standardkonto
│   ├── budget.db                    # eingecheckte SQLite-Datei (Altlast)
│   └── dp-keys/                     # eingecheckter DataProtection-Schlüssel (Altlast)
├── Models/                          # User, Account, Category, Transaction, RecurringItem, ScheduledPayment,
│                                    # SavingsPot, SharedGroup/GroupExpense/ExpenseSplit/Settlement, TrustedEmail
├── Pages/                           # Razor Pages für Login, Logout, Register
├── Services/                        # CurrencyService, CamtCsvImportService, GroupCsvImportService, UserService,
│                                    # UserPreferencesService, AppStateService, GroupNotificationService
├── wwwroot/                         # app.css, pages.css, auth.css, favicon
├── style/                           # Styleguide-Vorlage (Fitness-Tracker-Look)
├── out_linux/                       # eingechecktes Publish-Ergebnis, vom Build ausgeschlossen
├── Dockerfile, docker-compose.yaml  # Container-Build und Betrieb
├── .gitlab-ci.yml, .gitpod.yml      # CI- und Gitpod-Konfiguration
└── BudgetTracker.csproj, budget_tracker.sln
```

## Start

```bash
# Container (Docker + Docker Compose)
docker compose up -d --build
# → http://localhost:11111, Datenbank in ./data
```

```bash
# Lokal, .NET-8-SDK erforderlich
dotnet restore BudgetTracker.csproj
dotnet run --project BudgetTracker.csproj
# → http://localhost:5000, Datenbank unter ./data/budget.db
```

Im Repo liegt kein `launchSettings.json`, daher gibt es keine vorkonfigurierte HTTPS-Adresse. Nach dem ersten Start erfolgt die Kontoerstellung unter `/register`; dieser erste Nutzer erhält die Admin-Rolle.

## Konfiguration

| Variable | Zweck |
|---|---|
| `DATA_PATH` | Verzeichnis für `budget.db` und `dp-keys`; ohne Angabe `./data` relativ zum Content-Root, im Image `/app/data` |
| `ASPNETCORE_ENVIRONMENT` | `Production` im Container; `Development` liefert ausführliche Fehlerseiten |
| `ASPNETCORE_URLS` | Bind-Adresse, im Image auf `http://+:11111` gesetzt |

`appsettings.json` enthält nur `Logging`, `AllowedHosts` und `DetailedErrors`. Eine `.env.example` gibt es nicht, Secrets werden keine benötigt. Die `docker-compose.yaml` bindet zusätzlich das externe Netzwerk `cloudflare` ein – es muss auf dem Host existieren (Cloudflare Tunnel) oder aus der Compose-Datei entfernt werden.

## Hinweise

- **Daueraufträge buchen nicht automatisch.** `RecurringItem`-Einträge fließen nur in die Prognose ein; Transaktionen entstehen ausschließlich manuell oder über „bezahlt“ bei geplanten Zahlungen; die Navigation `RecurringItem.GeneratedTransactions` bleibt ungenutzt.
- **Keine Migrations.** Schemaänderungen laufen über `EnsureCreated` plus `ALTER TABLE` in `try/catch`; bestehende Datenbanken werden nur notdürftig nachgezogen. Strukturelle Änderungen an vorhandenen Tabellen sind damit riskant.
- **Import nur als CSV.** Kontoauszüge (CAMT-CSV) und Gruppenhistorie sind die einzigen Übernahmewege. Ein Import aus Google Drive oder Google Sheets ist nicht implementiert; Bestandsdaten müssen vorher als CSV exportiert werden.
- **Einstellungen ohne Wirkung:** `CompactMode`, die Monatsbudget-Warnung sowie Standardwährung, Datumsformat und Sprache werden gespeichert, aber außerhalb der Einstellungsseite von keiner Seite gelesen – Anzeige und Formatierung sind fest auf de-DE/EUR.
- **Konten teilen fehlt in der UI.** `AccountMember` mit den Rollen Owner/Viewer existiert im Modell, wird aber von keiner Komponente verwendet; gemeinsame Nutzung läuft ausschließlich über Gruppen.
- **Fremdwährung rechnet immer nach EUR**, auch bei Konten mit anderer Kontowährung. Bei nicht erreichbarer Frankfurter-API liefert der Dienst Kurs 1,0, wodurch Beträge ungeprüft übernommen werden.
- **Laufzeit-Abhängigkeiten nach außen:** Google Fonts (fonts.googleapis.com) und die Frankfurter-API werden im Browser bzw. Server aufgerufen. Offline funktioniert die App, ist dann aber auf Schrift-Fallbacks und Kurs 1,0 angewiesen.
- **Repo-Hygiene:** `Data/budget.db` (zwei Nutzerdatensätze mit BCrypt-Hashes) und `Data/dp-keys/key-*.xml` liegen im Repo, obwohl `.gitignore` `*.db` ausschließt. Beides sollte entfernt und der DataProtection-Schlüssel neu erzeugt werden. `out_linux/` ist ein vollständiges Publish-Ergebnis (inkl. MudBlazor.dll) und wird nur über `<Compile Remove>` aus dem Build gehalten.
- **Lizenz und Contributing sind Vorlagen:** `LICENSE` ist MIT, nennt aber weiterhin „GitLab B.V.“ als Copyright; `CONTRIBUTING.md` ist unverändert der GitLab-DCO-Text.
- **CI passt nicht zum Hosting:** `.gitlab-ci.yml` baut und „deployed“ auf Branch `master` mit self-hosted Runner; im GitHub-Repo läuft diese Pipeline nicht.
- **Keine Tests**, keine Statikanalyse (Analyzers im csproj deaktiviert), keine Internationalisierung – die Oberfläche ist ausschließlich deutsch.
