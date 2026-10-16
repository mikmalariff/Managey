# Managey

HR and approvals software for small and medium businesses. It runs on your own Windows Server, and staff open it in a browser on the office network.

## Features

- **Attendance**: clock in and out, late marking, team and company reports, CSV export
- **Leave**: leave types and yearly entitlement, per-person overrides, balances, half days, overlap and balance checks; weekends and public holidays are not counted
- **Requests and approvals**: leave, claims, purchases and general requests, each with attachments and a configurable multi-step approval chain (direct manager, HR, admin or a specific person)
- **People**: employee directory, departments, reporting lines, roles (Admin, HR, Manager, Staff), bulk import and update from Excel or CSV
- **Payroll**: monthly payroll for any country. Basic salary, fixed allowances and deductions, approved claims and unpaid leave, plus contribution schemes you define (pension, social security, tax...). Printable payslips for staff; Excel summary, bank file and per-scheme reports for HR. A Malaysia preset (EPF, SOCSO, EIS, SKBBK, PCB) is built in.
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

1. **Setup → Company**: company name, currency, working days, and working hours (which decide late marking).
2. **Setup → Departments**, **Leave types** and **Public holidays**.
3. **People → Add employee**: set *Reports to*, because that person approves the employee's requests first.
4. **Setup → Approval flows**: adjust who approves each request type.
5. **Setup → Email**: enter your mail server and send a test email.
6. **Setup → Payroll**: set up your contribution schemes, or apply a country preset.

To bring in existing staff, go to **People → Import from Excel**, download the template, fill it in and upload it. You see a preview first, and nothing is saved until every row is valid. New employees get a temporary password; download the list when the import finishes.

## Payroll

Payroll does not assume any country. Statutory and company deductions are **contribution schemes** that you define under **Setup → Payroll**. Each scheme has a name and a short code, and one of these methods:

| Method | Use it for | Example |
| --- | --- | --- |
| Percentage of wages | Flat employee and employer rates, with an optional wage ceiling | Pension 5% / 7.5% up to 5,000 |
| Percentage by wage band | Rates that change with salary; a band can round wages up to a step first | Malaysia EPF |
| Fixed amount by wage band | Official contribution tables | Malaysia SOCSO and EIS |
| Fixed amount | The same amount for everyone | Union fee 10 a month |
| Amount per employee | Amounts the tax office sets per person | Monthly income tax (PCB, PAYE) |

Schemes can start from a given month, add new employees automatically, and hold the employer's reference number. Each employee is enrolled in schemes individually under **People → (employee) → Pay details**, with their member number. Bands can be pasted straight from a spreadsheet.

**Country presets** create ready-made schemes, which you can then edit. The **Malaysia** preset includes:
- EPF (standard, 60+ and foreign worker rates, Third Schedule wage bands)
- SOCSO categories 1 and 2, EIS and LINDUNG 24 Jam (SKBBK), using PERKESO's official 65-band tables
- PCB as an amount per employee

Monthly flow:
1. **Payroll → Run payroll** creates draft payslips for every active employee with a basic salary. Approved claims are added as reimbursements (once only), and unpaid leave is deducted at basic ÷ days per month.
2. Edit any payslip if needed. You can override the contributions by hand.
3. **Finalise** locks the month. Staff then see their payslips under **Payslips** and can print them or save them as PDF.

Downloads on each payroll:

| Download | Contents |
| --- | --- |
| Payroll summary (Excel) | Every payslip, with employee and employer amounts for each scheme |
| Bank payment file (CSV) | Name, ID, bank, account and net pay |
| Scheme CSV | For each scheme: ID, member no., wages, employee and employer amounts, ready to adapt to any authority's upload format |
| KWSP / PERKESO files | Shown when the Malaysia preset is in use. KWSP is the i-Akaun e-Caruman CSV. PERKESO is the ASSIST combined SOCSO + EIS text file, 278 characters per line, which needs the employer code on the SOCSO scheme. |

## Changing the database model

After changing entities, add a migration for **both** providers:

```bash
dotnet tool install -g dotnet-ef --version 8.0.11
cd server/Managey.Api
dotnet ef migrations add <Name> --context SqliteAppDbContext --output-dir Migrations/Sqlite
dotnet ef migrations add <Name> --context SqlServerAppDbContext --output-dir Migrations/SqlServer
```
