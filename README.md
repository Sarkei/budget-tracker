# Budget Tracker

Ein selbst gehosteter, persönlicher Budget-Tracker auf Basis von Blazor Server (.NET 8). Verwalte Einnahmen, Ausgaben, Daueraufträge, Sparzielen und geteilte Gruppenausgaben – lokal auf dem eigenen Server, ohne Cloud-Abhängigkeit.

## Features

### Transaktionen & Kategorien
- **Transaktionen** – Einnahmen und Ausgaben erfassen, Kategorien zuweisen, Notizen hinzufügen
- **Fremdwährung** – Betrag in einer anderen Währung eingeben; automatische Umrechnung mit einstellbarem Wechselkurs
- **Kategorien** – Eigene Einnahmen- und Ausgabenkategorien mit Farbe und Icon
- **Filter & Suche** – Transaktionen nach Zeitraum, Kategorie, Typ und Text filtern

### Wiederkehrende Posten
- **Daueraufträge (Recurring)** – Regelmäßige Einnahmen und Ausgaben (wöchentlich, monatlich, quartalsweise, jährlich) verwalten
- **Ratenzahlungen** – Installment-Modus: feste Anzahl Zahlungen mit automatischer Fortschrittsverfolgung
- **Geplante Zahlungen (Scheduled)** – Einmalige zukünftige Zahlungen eintragen und bei Zahlung abhaken

### Sparziele
- **Spardosen** – Sparziele mit Name, Zielbetrag, Icon und Farbe anlegen
- **Fortschrittsbalken** – Visueller Fortschritt in Echtzeit; manuell befüllbar

### Geteilte Ausgaben (Gruppen)
- **Gruppen** – Gemeinsame Ausgabengruppen erstellen (ähnlich Splitwise)
- **Gruppenausgaben** – Ausgaben innerhalb einer Gruppe erfassen; bezahlt von einem Mitglied, aufgeteilt auf alle
- **Abrechnung** – Wer schuldet wem wie viel? Automatische Schuldenberechnung je Gruppe
- **Gruppenberichte** – Auswertung aller Ausgaben einer Gruppe mit Statistik

### Berichte & Dashboard
- **Dashboard** – Kontostand, aktuelle Monatsbilanz, Ausgaben nach Kategorie (Donut-Diagramm), letzte Transaktionen
- **Monatsbericht** – Einnahmen vs. Ausgaben, Kategoriebreakdown, Trend über mehrere Monate
- **Charts** – ApexCharts-Diagramme für alle Auswertungen

### Einstellungen
- **Profil** – Anzeigename, E-Mail ändern
- **Währung** – Standard-Währung und Anzeigeformat konfigurieren
- **Kategorien** – Kategorien verwalten (erstellen, umbenennen, löschen)
- **Konto** – Passwort ändern

---

## Tech Stack

| Komponente | Version |
|---|---|
| .NET / ASP.NET Core | 8.0 |
| Blazor Server | (Interactive Server Render) |
| MudBlazor | 6.21 |
| Blazor-ApexCharts | 4.0 |
| Entity Framework Core | 8.0 |
| SQLite | – |
| BCrypt.Net-Next | 4.0 |
| ASP.NET DataProtection | (Key-Persistenz für Container-Restarts) |

---

## Deployment (Docker)

### Voraussetzungen
- Docker & Docker Compose

### Starten

```bash
docker compose up -d
```

Der Container läuft auf Port **11111**. Die SQLite-Datenbank und DataProtection-Keys liegen im Volume `./data`.

### docker-compose.yaml

```yaml
services:
  budget-tracker:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: budget-tracker
    restart: unless-stopped
    ports:
      - "11111:11111"
    volumes:
      - ./data:/app/data
    environment:
      - DATA_PATH=/app/data
      - ASPNETCORE_ENVIRONMENT=Production
```

### Umgebungsvariablen

| Variable | Pflicht | Beschreibung |
|---|---|---|
| `DATA_PATH` | Nein | Pfad zur SQLite-Datenbank im Container (Standard: `/app/data`) |
| `ASPNETCORE_URLS` | Nein | Bind-Adresse (Standard: `http://+:11111`) |

---

## Lokale Entwicklung

```bash
# Abhängigkeiten wiederherstellen
dotnet restore BudgetTracker.csproj

# App starten
dotnet run --project BudgetTracker.csproj
```

Die App ist dann unter `https://localhost:5001` oder `http://localhost:5000` erreichbar.  
Die SQLite-Datenbank (`budget.db`) wird automatisch im Ordner `./data` erstellt.

---

## Ersten Nutzer anlegen

Beim ersten Aufruf kann über `/register` ein Konto erstellt werden. Passwörter werden mit BCrypt gehasht gespeichert.

---

## Datenbankschema (Überblick)

| Tabelle | Beschreibung |
|---|---|
| `Users` | Nutzerkonten (Login, BCrypt-Hash, Rolle) |
| `UserPreferences` | Einstellungen je Nutzer (Währung, Anzeigeformat) |
| `Accounts` | Konten mit Basiswährung |
| `AccountMembers` | Nutzer ↔ Konto Zuordnung |
| `Categories` | Einnahmen-/Ausgabenkategorien je Konto |
| `Transactions` | Alle Transaktionen inkl. Fremdwährungsfelder |
| `RecurringItems` | Daueraufträge und Ratenzahlungen |
| `ScheduledPayments` | Geplante Einmalzahlungen |
| `SavingsPots` | Sparziele |
| `SharedGroups` | Geteilte Ausgabengruppen |
| `GroupMembers` | Mitglieder einer Gruppe |
| `GroupExpenses` | Ausgaben innerhalb einer Gruppe |

---

## Projektstruktur

```
budget_tracker/
├── Components/
│   ├── Layout/          # MainLayout, NavMenu
│   ├── Pages/           # Alle Seiten (.razor)
│   └── Dialogs/         # Modal-Dialoge
├── Data/
│   ├── AppDbContext.cs
│   └── DatabaseInitializer.cs
├── Models/              # EF Core Entitäten
├── Services/
│   ├── CurrencyService.cs
│   ├── UserPreferencesService.cs
│   ├── AppStateService.cs
│   └── UserService.cs
├── wwwroot/             # Statische Assets
├── Dockerfile
├── docker-compose.yaml
└── BudgetTracker.csproj
```

---

## Lizenz

Siehe [LICENSE](LICENSE).