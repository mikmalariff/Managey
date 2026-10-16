# Managey

HR and approvals software for small and medium businesses. It runs on your own Windows Server, and staff open it in a browser on the office network.

## Features

- **Attendance**: clock in and out, late marking, team and company reports, CSV export
- **Leave**: leave types and yearly entitlement, per-person overrides, balances, half days, overlap and balance checks; weekends and public holidays are not counted
- **Requests and approvals**: leave, claims, purchases and general requests, each with attachments and a configurable multi-step approval chain (direct manager, HR, admin or a specific person)
- **People**: employee directory, departments, reporting lines, roles (Admin, HR, Manager, Staff), bulk import and update from Excel or CSV
- **Payroll**: monthly payroll for any country. Basic salary, fixed allowances and deductions, approved claims and unpaid leave, plus contribution schemes you define (pension, social security, tax...). Printable payslips for staff; Excel summary, bank file and per-scheme reports for HR. Presets for Malaysia, Singapore and Indonesia are built in.
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

## Install

Full step-by-step guides (Bahasa Melayu):
- **[Panduan Pemasangan](docs/PANDUAN-PEMASANGAN.md)**: Windows Server / PC, cloud (Linux VM with automatic HTTPS), remote access, backup, updates, troubleshooting
- **[Panduan Pengguna](docs/PANDUAN-PENGGUNA.md)**: first-time setup, and daily use for staff, managers, HR and admins

In short:
1. Get the package. The **Package** GitHub Actions workflow builds `Managey-win-x64.zip` and `Managey-linux-x64.zip` on every push, and attaches them to a Release when you push a `v*` tag. You can also build them yourself with `scripts/publish.ps1` (or `publish.sh`). The packages are self-contained: the server needs no .NET or Node.js.
2. **Windows:** extract the zip, then run `install.ps1` in an elevated PowerShell. It installs a Windows Service, opens the firewall and prints the address to open. **Cloud Linux:** run `sudo ./install.sh your.domain.com`, which installs a systemd service and Caddy with free HTTPS.
3. Sign in as `admin` / `ChangeMe123!` and change the password.

Forgot the admin password? Run `Managey.Api.exe --reset-password admin NewPassword123` on the server.

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
| Percentage by wage band | Rates that change with salary. A band can round wages up to a step first, and add a fixed amount (for formulas like 0.6 × (wage − 500)). | Malaysia EPF, Singapore CPF |
| Fixed amount by wage band | Official contribution tables | Malaysia SOCSO and EIS |
| Fixed amount | The same amount for everyone | Union fee 10 a month |
| Amount per employee | Amounts the tax office sets per person | Monthly income tax (PCB, PAYE) |

Schemes can start from a given month, add new employees automatically, and hold the employer's reference number. Each employee is enrolled in schemes individually under **People → (employee) → Pay details**, with their member number. Bands can be pasted straight from a spreadsheet.

**Country presets** create ready-made schemes, which you can then edit:

- **Singapore**: CPF for each age band (2026 rates, S$8,000 Ordinary Wage ceiling, graduated rates below S$750, CPF rounding), Skills Development Levy, and the CDAC, ECF, MBMF and SINDA funds. Additional Wage (bonus) ceilings are not applied automatically.
- **Indonesia**: BPJS Ketenagakerjaan JHT 2%/3.7%, JP 1%/2% (cap Rp11,086,300 from March 2026, revised every March), JKK 0.24% (lowest risk class; adjust it to yours), JKM 0.3%, BPJS Kesehatan 1%/4% (cap Rp12 million), and PPh 21 per employee.

The **Malaysia** preset includes:
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
