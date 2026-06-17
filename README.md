# Endless Reincarnation Online

Endless Reincarnation Online is a third-person 3D action roguelite prototype built with Unity and an ASP.NET Core backend. The project combines local action combat, save progression, account-based cloud saves, friend social features, chat, and friend-assisted online dungeon sessions.

## Highlights

- Third-person melee/ranged combat with skills, buffs, equipment, enemy AI, boss flow, drops, shops, and checkpoint recovery.
- Unlimited save slots with local JSON storage and account-based remote save synchronization.
- Social platform backend for account registration, login, friend requests, friend list, chat messages, presence summaries, and active save context.
- Online dungeon backend for friend aid sessions, dungeon instance creation, participant validation, reward arbitration, and real-time state synchronization.
- UDP/KCP real-time channels for player state, enemy authority state, UI events, scene loading, damage events, and reward snapshots.

## Tech Stack

- Client: Unity, C#, Newtonsoft.Json, kcp2k
- Backend: ASP.NET Core 8, EF Core
- Database: MySQL 8.4 through Pomelo EF Core provider, with EF Core migrations
- Realtime: HTTP APIs for session lifecycle, UDP/KCP for gameplay synchronization

## Repository Layout

```text
Assets/                 Unity game client source and assets
Backend/SocialBackend/  Account, social, chat, and remote save backend
Backend/OnlineDungeonServer/ Online dungeon session and realtime backend
Docs/                   Project architecture and online multiplayer notes
Packages/               Unity package manifest
ProjectSettings/        Unity project settings
```

## Backend Services

### Social Backend

Default development endpoint:

```text
http://127.0.0.1:5076
```

Main APIs:

- `/api/auth/register`
- `/api/auth/login`
- `/api/friends`
- `/api/messages/thread`
- `/api/saves`
- `/api/active-save`
- `/api/aid/request`

### Online Dungeon Server

Default development endpoint:

```text
http://127.0.0.1:5086
```

Main responsibilities:

- Create and close dungeon instances.
- Validate players and join tokens.
- Keep in-memory runtime state for active dungeon sessions.
- Synchronize realtime gameplay messages through UDP/KCP.
- Arbitrate rewards, chest opening, kill rewards, and drop pickup results.

## Local Development

Start the social backend:

```powershell
.\start-social-backend.bat
```

Start the online dungeon backend:

```powershell
.\start-online-dungeon-server.bat
```

Then open the Unity project from this repository root.

Start the full backend stack with MySQL:

```powershell
dotnet publish Backend\SocialBackend\SocialBackend.csproj -c Release -o Backend\SocialBackend\publish --no-restore
dotnet publish Backend\OnlineDungeonServer\OnlineDungeonServer.csproj -c Release -o Backend\OnlineDungeonServer\publish --no-restore
docker compose up --build
```

Docker is required for the compose workflow. The Dockerfiles use the local `publish` output and the ASP.NET Core runtime image, so the .NET SDK does not need to be downloaded inside Docker. Without Docker, configure `ConnectionStrings:SocialDatabase` in `Backend/SocialBackend/appsettings.Development.json` to point to a local MySQL server.

Run backend checks:

```powershell
dotnet build Backend\SocialBackend\SocialBackend.csproj --no-restore
dotnet build Backend\OnlineDungeonServer\OnlineDungeonServer.csproj --no-restore
dotnet test Backend\SocialBackend.Tests\SocialBackend.Tests.csproj --no-restore
dotnet tool run dotnet-ef migrations script --project Backend\SocialBackend\SocialBackend.csproj --startup-project Backend\SocialBackend\SocialBackend.csproj --idempotent --no-build
```

## Current Engineering Roadmap

- Expand automated tests for aid sessions, dungeon instance lifecycle, and realtime message contracts.
- Add CI checks for backend build, migrations, and test execution.
- Add production deployment notes for reverse proxy, HTTPS, and secrets management.
