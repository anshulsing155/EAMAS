# Employee Activity Monitoring & Analytics System (EAMAS)

EAMAS is a Windows-first desktop application for employee activity monitoring, screenshot capture, reporting, and local productivity analytics.

## Current stack
- C# / .NET 8
- WPF desktop UI
- MongoDB
- MVVM architecture

## Solution layout
- `src/EAMAS.Core/` - domain models, MongoDB data access, analytics, reporting, and business services
- `src/EAMAS.Desktop/` - WPF application, views, view models, Windows integration, and startup flow

## Features
- Active vs idle time tracking
- Foreground application and window-title logging
- Periodic and manual screenshot capture
- Daily, weekly, monthly, and custom reports
- Productivity categorization and alerting
- Multi-tenant organisation login model
- Role-based access for SuperAdmin, Admin, Manager, and Employee

## Prerequisites
- Windows 10 or later
- .NET 8 SDK for development
- .NET 8 Desktop Runtime for running built binaries
- A reachable MongoDB instance

## Configuration
The app resolves database settings in this order:

1. `%LocalAppData%\EAMAS\config.json`
2. `DATABASE_URL` environment variable
3. `mongodb://127.0.0.1:27017` with database name `eamas` as a local-development fallback

On first launch without a local config file or `DATABASE_URL`, the app opens a database setup window.

## First-run admin account
The app seeds a default SuperAdmin account on first run if one does not already exist.

- Organisation code: `SYSTEM`
- Username: `superadmin`
- Password: `Admin@123`

You can override the initial seeded password before first launch with:

- `EAMAS_SUPERADMIN_PASSWORD`

Change the seeded password immediately after first login.

## Build and run
```powershell
dotnet build src\EAMAS.Desktop\EAMAS.Desktop.csproj
dotnet run --project src\EAMAS.Desktop\EAMAS.Desktop.csproj
```

## Privacy note
This application captures activity metadata and screenshots. If you plan to use it outside local development, make sure your deployment, consent flow, retention policy, and legal/privacy review match the laws and policies that apply to your organisation.
