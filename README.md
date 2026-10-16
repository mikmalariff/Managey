# Managey

HR and approvals software for small and medium businesses. It runs on your own Windows Server, and staff open it in a browser on the office network.

## Features

- **Attendance**: clock in and out, late marking, team and company reports, CSV export
- **Leave**: leave types and yearly entitlement, per-person overrides, balances, half days, overlap and balance checks; weekends and public holidays are not counted
- **Requests and approvals**: leave, claims, purchases and general requests, each with attachments and a configurable multi-step approval chain (direct manager, HR, admin or a specific person)
- **People**: employee directory, departments, reporting lines, roles (Admin, HR, Manager, Staff), bulk import and update from Excel or CSV
- **Payroll**: monthly payroll with basic salary, fixed allowances and deductions, approved claims, unpaid leave, EPF, SOCSO, EIS and PCB; printable payslips for staff, Excel summary and bank file for HR
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
6. **Setup → Payroll**: check the EPF, SOCSO and EIS rates.

To bring in existing staff, go to **People → Import from Excel**, download the template, fill it in and upload it. You see a preview first, and nothing is saved until every row is valid. New employees get a temporary password; download the list when the import finishes.

## Payroll notes

- Set each employee's basic salary, PCB, bank details and statutory category under **People → (employee) → Pay details**. Only HR and admin can see these.
- **Payroll → Run payroll** creates a draft payslip for every active employee with a basic salary. Approved claims are added as reimbursements, once only, and approved leave of an unpaid leave type is deducted at basic ÷ days per month.
- **SOCSO, EIS and LINDUNG 24 Jam (SKBBK)** come from PERKESO's official contribution tables (65 wage bands, ceiling RM6,000, schedule in force from 1 June 2026). Employees can be set to SOCSO First or Second category, and EIS or SKBBK can be switched off per person.
- **EPF** uses the Third Schedule wage bands. Rates for the standard, 60-and-above and foreign-worker categories are under **Setup → Payroll**. PCB is not calculated: enter the amount from the LHDN calculator.
- HR can type exact amounts on any payslip before finalising.
- **Finalise** locks the month and lets staff see their payslips under **Payslips**, where they can print them or save them as PDF.

### Monthly statutory files

Fill in the employer details under **Setup → Company** first.

| File | Where to upload | Format |
| --- | --- | --- |
| **KWSP file** (`KWSP-yyyymm.csv`) | i-Akaun (Majikan) → Contribution → Upload file (TXT/CSV) | No header. Member no., IC (`######-##-####`), name, wages, employer share, employee share. The contribution month is the month after the wage month. |
| **PERKESO file** (`PERKESO-….txt`) | ASSIST portal | Combined SOCSO + EIS text file, mandatory from 1 October 2026. Fixed width, 278 characters per line, amounts in sen. Includes the SKBBK employee share. |
| **Bank file** | Your bank's bulk payment | CSV: name, IC, bank, account, amount. |

Both portals show a summary before you pay. Check it against the payroll totals the first time you use each file.

### EA forms

**Payroll → EA forms** builds Borang EA (C.P.8A) for each employee from the year's finalised payslips. You can print them one at a time, or all at once with one per A4 page. Staff can also open their own EA under **Payslips**. Each pay line has an EA category that decides where it goes on the form: salary/overtime → B1(a), bonus/commission → B1(b), taxable allowance → B1(c), tax-exempt → F, zakat → D3, CP38 → D2. Claims count as reimbursements and are left out.

## Changing the database model

After changing entities, add a migration for **both** providers:

```bash
dotnet tool install -g dotnet-ef --version 8.0.11
cd server/Managey.Api
dotnet ef migrations add <Name> --context SqliteAppDbContext --output-dir Migrations/Sqlite
dotnet ef migrations add <Name> --context SqlServerAppDbContext --output-dir Migrations/SqlServer
```
