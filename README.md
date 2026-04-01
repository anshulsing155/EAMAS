# Employee Activity Monitoring & Analytics System (EAMAS)

A Windows-first employee monitoring system with local analytics, installable desktop software, and no AI or external analytics APIs.

## Tech stack
- C# / .NET 8
- WPF desktop UI
- SQLite local database
- MVVM architecture for scalability

## Core architecture
- `EAMAS.Core`: business logic, domain models, services, reports
- `EAMAS.Desktop`: WPF UI, views, view models, startup
- `Data`: local SQLite storage and configuration

## MVP scope
- Screen time tracking (active vs idle)
- Application usage logging
- Screenshot capture workflow
- Daily/weekly/monthly reports
- Local analytics and productivity scores
- Role-based UI for Admin / Manager / Employee

## Why this stack
- Native Windows desktop experience
- No external API or AI dependency
- Scalable architecture for features like alerts, export, encryption

## Next steps
1. Open the solution in Visual Studio 2022/2023.
2. Implement native activity tracking using Windows APIs.
3. Add screenshot capture and local storage.
4. Build dashboard views and reporting logic.
5. Harden privacy, encryption, and user consent flows.

## Folder structure
- `src/EAMAS.Core/`
- `src/EAMAS.Desktop/`

## Build
- Install .NET 8 SDK
- Open `EAMAS.sln` or the `*.csproj` projects in Visual Studio
- Restore NuGet packages
- Build and run the desktop app
