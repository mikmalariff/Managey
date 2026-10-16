# Managey

HR and approvals software for small and medium businesses. It runs on your own Windows Server, and staff open it in a browser on the office network.

## Features

- **Attendance**: clock in and out, late marking, team and company reports, CSV export
- **Leave**: leave types and yearly entitlement, per-person overrides, balances, half days, overlap and balance checks; weekends and public holidays are not counted
- **Requests and approvals**: leave, claims, purchases and general requests, each with attachments and a configurable multi-step approval chain (direct manager, HR, admin or a specific person)
- **People**: employee directory, departments, reporting lines, roles (Admin, HR, Manager, Staff)
- **Documents**: company library for policies, forms and handbooks
- **Email notifications**: approvers are emailed when a request needs them, and requesters when it is decided (any SMTP server, e.g. Microsoft 365)
- **Audit log**: every change is recorded with who did it and when

## Tech

| Part | Stack |
| --- | --- |
| Server | ASP.NET Core 8, EF Core, runs as a Windows Service |
| Database | SQLite (default, zero setup) or SQL Server |
| Client | React + TypeScript (Vite), served by the server |

Uploaded files, the SQLite database and the encryption keys live in `data\` next to the executable. Back that folder up. The database schema is upgraded automatically on start (EF Core migrations).

## Install on Windows Server

1. On a build machine with Node.js 20+ and the .NET 8 SDK, run `scripts\publish.ps1`.
2. Copy the `publish` folder to the server, for example to `C:\Managey`.
3. In an elevated PowerShell on the server, run `.\install-service.ps1 -InstallDir C:\Managey`. The script is in `scripts\`.
4. Open `http://<server-name>:5080` and sign in as `admin` / `ChangeMe123!`. **Change this password straight away** from *Account*.

To use SQL Server instead of SQLite, edit `appsettings.json` before the first start:

```json
"Database": {
  "Provider": "SqlServer",
  "ConnectionString": "Server=.\\SQLEXPRESS;Database=Managey;Trusted_Connection=True;TrustServerCertificate=True"
}
```

## Development

```bash
# terminal 1: API on http://localhost:5080
cd server/Managey.Api && dotnet run

# terminal 2: client with hot reload on http://localhost:5173 (proxies /api)
cd client && npm install && npm run dev
```

`npm run build` in `client/` writes the production bundle into `server/Managey.Api/wwwroot`.

## First-time setup

1. **Setup → Company**: company name and working hours, which decide late marking.
2. **Setup → Departments**, **Leave types** and **Public holidays**.
3. **People → Add employee**: set *Reports to*, because that person approves the employee's requests first.
4. **Setup → Approval flows**: adjust who approves each request type.
5. **Setup → Email**: enter your mail server and send a test email.

## Changing the database model

After changing entities, add a migration for **both** providers:

```bash
dotnet tool install -g dotnet-ef --version 8.0.11
cd server/Managey.Api
dotnet ef migrations add <Name> --context SqliteAppDbContext --output-dir Migrations/Sqlite
dotnet ef migrations add <Name> --context SqlServerAppDbContext --output-dir Migrations/SqlServer
```
