# EunSlip — Coding Agent Context

**Document status:** Coding Ready  
**Project:** EunSlip — Payroll Slip Delivery  
**Version:** 1.0  
**Prepared:** 2026-07-20  
**Target organization:** PT. EUNSUNG INDONESIA  
**Implementation boundary:** This document defines the approved product and technical context up to the point at which coding may begin.

---

## 1. Agent Operating Instructions

Treat this document as the authoritative source for version 1.

Before writing code:

1. Read the entire document, especially Scope, Business Rules, Excel Contract, Security, Recovery, Acceptance Criteria, and Implementation Order.
2. Inspect the repository if one has already been initialized. Preserve existing conventions unless they conflict with an explicit requirement here.
3. Do not broaden scope, silently change public behavior, or replace an approved business rule with a technical assumption.
4. Record any unavoidable deviation as an Architecture Decision Record before implementing it.
5. Run a clean baseline build and tests before changing code.

During implementation:

- Keep changes focused on the current implementation task.
- Add tests with the behavior they protect.
- Never place payroll values, PDF content, OAuth tokens, authorization codes, or credentials in logs.
- Do not add a second implementation layer merely to satisfy a pattern. Interfaces are required only at external boundaries listed in this document.
- Do not introduce a server, cloud database, user-login subsystem, archive storage, or automatic updater.

A task is complete only when its acceptance criteria and required tests pass.

---

## 2. Product Summary

EunSlip is a Windows desktop application for the accounting team. It imports one approved payroll Excel workbook, validates each employee row, generates one salary-slip PDF per employee, and sends each PDF in an individual email through one Google Workspace/Gmail payroll account.

The final business outcome is:

> Every valid employee in an approved payroll batch receives their own salary-slip PDF by email, while accounting can review the batch before sending and see which messages succeeded or failed.

### Primary actor

**Accounting user**

The application has no internal login. Access is controlled by Windows access to the computer.

### Key constraints

- Desktop application only.
- Windows 10 and Windows 11, 64-bit.
- Installed with an administrator-run `.exe` installer.
- Daily use does not require administrator privileges.
- Multiple computers may have independent installations.
- Each installation has its own local history and configuration.
- No synchronization exists between computers.
- All Windows accounts that can run EunSlip on the same computer share the same history, stamp, settings, and Gmail connection.
- Exactly one EunSlip instance may run per computer.

---

## 3. Approved Scope

### In scope

- Import one `.xlsx` payroll workbook using the first worksheet.
- Enforce an exact 27-column template.
- Support 1–500 employee rows per batch.
- Enter one payroll period and one payment date per batch.
- Validate template, identity fields, emails, numeric values, formulas' cached values, duplicates, totals, and OT Hours.
- Show blocking errors and non-blocking calculation warnings.
- Show a table summary for all employees.
- Generate one preview PDF for the first valid employee.
- Open the preview with the default Windows PDF application.
- Generate one A4 portrait salary-slip PDF per employee.
- Require an active company-stamp image.
- Edit a common plain-text email subject and body before sending.
- Send one email per employee with exactly one attachment.
- Use Gmail API with Google OAuth.
- Send sequentially and retry failures up to three total attempts per recipient.
- Record minimal local history.
- Retry failed recipients by selecting the Excel file again.
- Recover an interrupted batch by selecting the same payroll data again.
- Support Indonesian and English UI; Indonesian is the default.
- Manual installation and manual upgrade through an `.exe` installer.
- Technical logs retained for 30 days.

### Out of scope

- Editing payroll values inside EunSlip.
- Employee master data.
- HR-system or attendance-system integration.
- Multiple Excel templates.
- Mapping arbitrary Excel columns.
- Password-protected PDFs.
- Selecting only some valid employees from a batch.
- CC, BCC, or bulk-recipient email.
- Rich-text or HTML email editing.
- PDF archive storage.
- Excel archive storage.
- Exporting history.
- Central server or shared database.
- Cross-computer duplicate prevention.
- Internal users, roles, passwords, or approval workflow.
- Automatic update checks.
- Automatic database backup.
- Digital code signing in version 1.
- A built-in PDF viewer.
- A special test mode.
- SMTP/internal-mail provider in version 1.

### Future consideration, not version 1

The email boundary should not embed payroll rules inside Gmail-specific code, so a future SMTP provider can be added without rewriting the payroll workflow. No SMTP implementation is required now.

---

## 4. Primary User Flow

1. Accounting opens EunSlip.
2. EunSlip verifies that no other instance is running.
3. The home page shows Gmail connection status, stamp status, language, recent batch, and any interrupted batch.
4. Accounting starts a payroll process.
5. Accounting selects an `.xlsx` workbook.
6. Accounting enters:
   - payroll period, displayed as `JULY 2025`;
   - payment date, displayed as `11-Mei-2026`.
7. EunSlip reads the first worksheet and validates the file.
8. Blocking errors prevent continuation. Data must be corrected in Excel outside EunSlip and uploaded again.
9. Calculation mismatches are warnings. Accounting may explicitly confirm and continue.
10. EunSlip shows:
    - all employee rows and validation status;
    - one preview generated from the first valid row;
    - common email subject and body;
    - Gmail and stamp readiness.
11. Accounting opens the preview in the default Windows PDF application.
12. Accounting confirms sending.
13. EunSlip creates a temporary PDF, sends one individual email, records the result, and removes the temporary PDF for each employee in sequence.
14. The batch continues until every valid employee has reached a final result.
15. The result screen shows succeeded and failed counts.
16. Failed recipients may be retried by selecting the workbook again.
17. Interrupted batches may be resumed only when the selected workbook produces the same canonical payroll fingerprint.

---

## 5. Excel Workbook Contract

The supplied handoff workbook `EunSlip_Payroll_Template.xlsx` is the approved contract example.

### Workbook rules

- File type: `.xlsx`.
- Read the first worksheet only, regardless of worksheet name.
- Ignore all other worksheets.
- The first row contains headers.
- Completely empty rows are ignored.
- One non-empty row represents one employee.
- Maximum: 500 employees.
- Columns may not be missing, reordered, renamed, duplicated, or added.
- The workbook must have been calculated and saved by Microsoft Excel before import when formulas are present.
- EunSlip reads stored/cached formula results; it does not calculate Excel formulas and does not automate Microsoft Excel.

### Exact column order

| Col | Header | Data rule |
|---|---|---|
| A | `NIK` | Required text; unique within workbook |
| B | `NAMA` | Required text |
| C | `Departement` | Text; blank allowed and displayed as blank |
| D | `Position` | Text; blank allowed and displayed as blank |
| E | `Join Date` | Valid Excel date |
| F | `Salary Status` | Text; blank allowed and displayed as blank |
| G | `email` | Required valid email; unique within workbook |
| H | `Basic` | Integer nominal; blank means 0; negative allowed |
| I | `Jabatan` | Integer nominal; blank means 0; negative allowed |
| J | `Tunjangan Lembur` | Integer nominal; blank means 0; negative allowed |
| K | `Haid` | Integer nominal; blank means 0; negative allowed |
| L | `Tunjangan Lain-lain` | Integer nominal; blank means 0; negative allowed |
| M | `Koreksi` | Integer nominal; blank means 0; negative allowed |
| N | `Kompensasi` | Integer nominal; blank means 0; negative allowed |
| O | `Cuti` | Integer nominal; blank means 0; negative allowed |
| P | `transport` | Integer nominal; blank means 0; negative allowed |
| Q | `Lembur` | Integer nominal; blank means 0; negative allowed |
| R | `Incentive` | Integer nominal; blank means 0; negative allowed |
| S | `Pph21` | Integer nominal; blank means 0; negative allowed |
| T | `JHT 2%` | Integer nominal; blank means 0; negative allowed |
| U | `JP 1%` | Integer nominal; blank means 0; negative allowed |
| V | `BPJS 1%` | Integer nominal; blank means 0; negative allowed |
| W | `Kehadiran` | Integer nominal; blank means 0; negative allowed |
| X | `Total Potongan` | Required numeric cached value; integer |
| Y | `Total` | Required numeric cached value; integer |
| Z | `Nett` | Required numeric cached value; integer |
| AA | `OT Hours` | Blank means 0; maximum one decimal digit |

### Calculation checks

EunSlip calculates comparison values:

```text
Expected Total =
    Basic + Jabatan + Tunjangan Lembur + Haid
    + Tunjangan Lain-lain + Koreksi + Kompensasi
    + Cuti + transport + Lembur + Incentive

Expected Total Potongan =
    Pph21 + JHT 2% + JP 1% + BPJS 1% + Kehadiran

Expected Nett =
    Total - Total Potongan
```

A mismatch is a warning, not a blocking error. The PDF uses the values stored in Excel for `Total`, `Total Potongan`, and `Nett`.

### Formula rules

- A formula cell must contain a readable cached value.
- Missing cached value for a required numeric cell is a blocking error.
- EunSlip must not recalculate or silently replace Excel values.

### Numeric-format rules

Nominal PDF output:

- Positive: `500,000`
- Negative: `-500,000`
- Zero: `0`
- No `Rp`
- No decimal places
- Excel cell colors and presentation formats are ignored

Nominal values containing any fractional component are blocking errors.

OT Hours:

- blank → `0`
- `12.0` → `12`
- `12.5` → `12.5`
- more than one decimal digit → blocking error

---

## 6. Validation Model

### Blocking errors

A batch cannot continue when any of the following exists:

- file is not a readable `.xlsx`;
- no worksheet exists;
- first worksheet has no usable data;
- header name, order, or count differs from the 27-column contract;
- more than 500 employee rows;
- NIK, name, or email is blank;
- email format is invalid;
- duplicate NIK within workbook;
- duplicate email within workbook;
- `Join Date` is invalid;
- required numeric/cached value cannot be read;
- a nominal contains a fractional value;
- `OT Hours` has more than one decimal digit;
- preview PDF cannot be generated;
- stamp is missing or unreadable before generation/sending;
- local database is corrupt and has not been reset;
- Gmail authorization is unavailable before confirmation.

### Warnings

Accounting may explicitly confirm and continue when:

- stored `Total` differs from `Expected Total`;
- stored `Total Potongan` differs from `Expected Total Potongan`;
- stored `Nett` differs from `Expected Nett`;
- the same `period + NIK` was previously sent successfully on this computer.

Warnings must identify employee, field, stored value, and expected value where relevant.

### Editing rule

There is no edit action for imported payroll data. Corrections happen in Excel outside EunSlip, followed by a new import.

---

## 7. Salary-Slip PDF Specification

### Page

- Exactly one A4 portrait page.
- No second page.
- Layout must remain readable; fixed component count makes a one-page design mandatory.
- Use a digital, clean layout inspired by the supplied scan rather than reproducing scan distortion.
- Include an outer border.

### Header

Centered:

```text
PT. EUNSUNG INDONESIA
SALARY JULY 2025
```

The second line is `SALARY ` plus the payroll period entered for the batch.

### Employee identity area

Two-column structure following the reference:

Left:

- `NIK`
- `NAME`

Right:

- `Departement`
- `Position`
- `Join Date`
- `Salary`

Mapping:

| Excel | PDF label |
|---|---|
| `NIK` | `NIK` |
| `NAMA` | `NAME` |
| `Departement` | `Departement` |
| `Position` | `Position` |
| `Join Date` | `Join Date` |
| `Salary Status` | `Salary` |

`Join Date` format: `01-May-2017`.

### Income and deduction area

Two columns:

**Income**

- Basic
- Jabatan
- Tunjangan Lembur
- Haid
- Tunjangan Lain-lain
- Koreksi
- Kompensasi
- Cuti
- Transport
- Lembur
- Insentive

**Deduction**

- Pph21
- JHT 2%
- JP 1%
- BPJS 1%
- Kehadiran

All components appear even when the value is zero.

Keep the PDF labels aligned with the supplied reference. The Excel header `Incentive` is displayed as `Insentive` on the PDF. No data mapping changes are allowed.

### Totals and footer

Show:

- income `Total`;
- deduction `Total`;
- `Nett Income`;
- `OT Hours`;
- `Payment Date`;
- static text `Made By`;
- static text `ACC`;
- active company stamp.

Payment date format uses Indonesian month names:

`11-Mei-2026`

OT Hours format:

`OT Hours : 12.5`

### Stamp

- Required on every PDF.
- Configured from Settings after installation.
- Not bundled with installer.
- Accepted formats: PNG, JPG, JPEG.
- Copy the selected image into shared application data.
- Use a fixed stamp area.
- Scale proportionally to fit.
- Never stretch or crop.
- Generation is blocked when no valid stamp is active.

### File name

```text
Slip_Gaji_[Periode]_[NIK].pdf
```

Rules:

- Normalize unsafe Windows filename characters.
- Use the period value in a deterministic filename-safe form.
- NIK is the collision-prevention identifier.

### Preview

- Generate the same PDF form that would be attached.
- Use the first valid employee row.
- Open through the default Windows PDF application.
- Preview files are temporary.
- Attempt cleanup when the wizard ends.
- If the external viewer holds a lock, cleanup must occur during the next startup.

---

## 8. Email Specification

### Provider

Version 1 uses Google Workspace/Gmail through Gmail API.

### OAuth

- OAuth client type: Desktop app.
- One authorization per computer.
- The same Gmail connection is shared by all Windows users on that computer.
- Use the minimum functional scopes:
  - `https://www.googleapis.com/auth/gmail.send`
  - basic identity scope `openid email` to display the connected account.
- Store refresh/access-token material outside SQLite.
- Protect token material with Windows DPAPI at machine scope.
- A disconnected or invalid account blocks confirmation until login succeeds again.
- No Gmail password is stored.

### Message

- One separate message per employee.
- `To`: exactly the employee email from Excel.
- No CC.
- No BCC.
- Exactly one PDF attachment.
- Plain-text body.
- Subject and body are identical for all employees in a batch.
- No template variables.

Visible sender name:

```text
PT. EUNSUNG INDONESIA
```

The payroll Gmail account should also be configured with this display name in Google Workspace/Gmail; UAT must verify the visible sender because Gmail account settings can govern final presentation.

### Default template

Subject:

```text
Slip Gaji Karyawan
```

Body:

```text
Yth. Bapak/Ibu,

Terlampir kami sampaikan slip gaji Anda.

Mohon menjaga kerahasiaan dokumen ini dan tidak meneruskannya kepada pihak lain.

Terima kasih.

PT. EUNSUNG INDONESIA
```

On the first use, load this template. After a completed sending session, store the latest subject and body as the default for the next batch.

### Send order and retry

- Sequential sending only.
- No parallel message sends.
- A batch cannot be manually cancelled after confirmation.
- Maximum three total attempts per recipient, including the first attempt.
- Honor a server `Retry-After` value if supplied; otherwise use a simple bounded backoff between attempts.
- Continue to the next recipient after three failed attempts, including when many failures appear to have a common Gmail cause.
- Retry actions are new send sessions with their own maximum of three attempts.

### Recipient status

```text
Pending
Sending
Sent
Failed
```

### Batch status

```text
Draft
Ready
Sending
Completed
Interrupted
```

A completed batch may contain failed recipients. Do not add a separate `CompletedWithFailures` status; derive that condition from recipient results.

### Result recording

For every attempt record:

- batch identifier;
- recipient identifier;
- attempt number;
- attempt type: Normal, FailedRetry, RecoveryRetry;
- start and finish timestamps;
- status;
- sanitized error category and message;
- Gmail message identifier when returned.

`Sent` is recorded only after Gmail confirms the send request.

---

## 9. Failed Retry and Interrupted Recovery

### Failed retry

To run `Kirim Ulang Gagal`:

1. Select a completed batch containing failed recipients.
2. Re-enter/select the same batch period context.
3. Select the Excel workbook again.
4. Import and validate it.
5. Verify payroll fingerprint matches the original batch.
6. Regenerate PDFs only for NIKs whose latest final status is `Failed`.
7. Send only those recipients.

No Excel, full payroll dataset, or failed PDF is retained between sessions.

### Fingerprint

Build a canonical representation from:

- payroll period;
- payment date;
- all 27 normalized fields for every employee;
- normalized email;
- normalized numeric values.

Sort rows by NIK before hashing, so row-order changes do not change the fingerprint. Any payroll-data change does change the fingerprint.

Worksheet name and file metadata are not included.

Use a cryptographic hash such as SHA-256.

### Interrupted batch

If the application or computer stops during sending:

- previously committed `Sent` results remain successful;
- batch becomes `Interrupted`;
- any recipient left in `Sending` is reset to `Pending` on startup and flagged for `RecoveryRetry`;
- accounting selects the workbook again;
- the fingerprint must match;
- EunSlip resends all recipients not committed as `Sent`;
- this includes automatic resend of recipients that may have reached Gmail before the local commit, accepting the approved risk of duplicate email.

Do not create a separate `Uncertain` recipient state.

---

## 10. User Interface

### Navigation

- Beranda / Home
- Proses Payroll / Payroll Process
- Riwayat / History
- Pengaturan / Settings
- Tentang Aplikasi / About

### Home

Show:

- Gmail connected/disconnected;
- connected email address;
- stamp ready/missing;
- active language;
- most recent batch;
- interrupted batch notice;
- primary action to start payroll.

### Payroll wizard

#### Step 1 — Select payroll

Fields:

- Excel file
- payroll period
- payment date

#### Step 2 — Validate

Table columns:

- status
- NIK
- name
- email
- error/warning summary

Provide expandable/detail view for calculation warnings. No edit action.

#### Step 3 — Preview and email

Show:

- first-valid-employee preview action;
- subject;
- plain-text body;
- recipient count;
- period and payment date;
- Gmail readiness;
- stamp readiness.

#### Step 4 — Confirm

Show:

- recipient count;
- Gmail account;
- payroll period;
- duplicate-send warnings;
- explicit statement that sending cannot be stopped after confirmation.

#### Step 5 — Send

Show:

- `x of y`;
- current NIK and name;
- succeeded count;
- failed count;
- current attempt;
- message asking the user not to close the application.

Disable normal navigation and intercept ordinary close requests while sending.

#### Step 6 — Results

Show:

- total recipients;
- sent;
- failed;
- duration;
- failed-recipient list and concise reason;
- retry failed action when applicable.

### History

Batch list:

- period;
- payment date;
- processing timestamp;
- recipient count;
- sent;
- failed;
- status.

Batch details show recipient and attempt metadata but never payroll nominal values.

Actions:

- view;
- retry failed;
- recover interrupted;
- permanently delete history with confirmation.

No export.

### Settings

**Gmail**

- connect;
- show connected account;
- reconnect;
- disconnect.

**Stamp**

- select image;
- preview;
- replace;
- remove.

**Language**

- Indonesian;
- English.

Language defaults to Indonesian. To minimize UI complexity, a language change is saved and applied after restarting EunSlip.

### About

Show:

- EunSlip name;
- version;
- company;
- support-contact section only when a support contact is configured at deployment; otherwise hide the section;
- log-folder location;
- third-party license notices.

### UX rules

- Errors explain the corrective action.
- Do not rely on color alone.
- Support keyboard navigation.
- Remain usable at Windows display scaling 100–150%.
- Accounting-facing errors do not show stack traces.
- All destructive actions use confirmation dialogs.

---

## 11. Architecture

### Chosen approach

A compact three-project modular monolith:

```text
EunSlip.Desktop
EunSlip.Core
EunSlip.Infrastructure
```

Dependency direction:

```text
Desktop -> Core
Desktop -> Infrastructure
Infrastructure -> Core
Core -> no project dependency
```

### Responsibilities

#### `EunSlip.Desktop`

- WPF/XAML
- ViewModels
- wizard/navigation
- dialogs
- localization resources
- UI composition root and dependency injection
- single-instance startup handling

#### `EunSlip.Core`

- payroll models
- validation rules
- calculation comparisons
- batch orchestration
- retry policy
- recovery selection
- fingerprint
- formatting policies
- statuses
- application use cases

Core contains no WPF, OpenXML, PDF, Gmail, SQLite, DPAPI, or file-system types.

#### `EunSlip.Infrastructure`

- Open XML workbook reader
- PDF generator
- Gmail OAuth and sender
- MIME construction
- SQLite repository
- encryption and DPAPI
- shared file storage
- temp cleanup
- technical file logging
- Windows process/default-app integration

### External-boundary interfaces

Keep interfaces only at these boundaries:

```text
IPayrollWorkbookReader
IPayslipPdfGenerator
IGmailSender
IAppRepository
ISecretStore
ISharedFileStore
```

Validation, fingerprinting, retry policy, filename formatting, email-data preparation, and batch coordination are concrete Core services unless a real second implementation appears.

### Recommended solution structure

```text
EunSlip/
├── src/
│   ├── EunSlip.Desktop/
│   │   ├── App.xaml
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Navigation/
│   │   ├── Dialogs/
│   │   ├── Localization/
│   │   └── Composition/
│   ├── EunSlip.Core/
│   │   ├── Payroll/
│   │   ├── Validation/
│   │   ├── Batches/
│   │   ├── Sending/
│   │   ├── Recovery/
│   │   ├── History/
│   │   ├── Settings/
│   │   └── Common/
│   └── EunSlip.Infrastructure/
│       ├── Excel/
│       ├── Pdf/
│       ├── Gmail/
│       ├── Persistence/
│       ├── Security/
│       ├── FileSystem/
│       ├── Logging/
│       └── Windows/
├── tests/
│   ├── EunSlip.Core.Tests/
│   └── EunSlip.Infrastructure.Tests/
├── installer/
├── test-data/
└── docs/
```

---

## 12. Technical Stack

- Runtime: .NET 10 LTS
- UI: WPF
- MVVM: `CommunityToolkit.Mvvm`
- Dependency injection: `Microsoft.Extensions.DependencyInjection`
- Excel: `DocumentFormat.OpenXml`
- PDF: MigraDoc + PDFsharp
- Database: SQLite via `Microsoft.Data.Sqlite`, no Entity Framework
- Gmail: Google Gmail API .NET client
- MIME: MimeKit
- Logging: one structured rolling-file pipeline integrated with `Microsoft.Extensions.Logging`
- Tests: xUnit
- Installer: Inno Setup
- Localization: `.resx`, Indonesian and English
- Build: self-contained `win-x64`
- Installer output: `EunSlip-Setup-x64.exe`
- Application executable: `EunSlip.exe`

Package versions must be pinned to stable releases compatible with .NET 10 at implementation time. Do not use preview packages unless no stable option exists and an ADR documents the reason.

---

## 13. Local Storage and Security

### Shared location

```text
C:\ProgramData\EunSlip\
├── database\
├── stamp\
├── oauth\
├── temp\
├── logs\
└── runtime\
```

The installer creates this location and grants ordinary local users the access required to run the application.

Program binaries are installed separately from data so upgrades and uninstall do not remove application data.

### Database

SQLite stores only:

- application settings;
- payroll-batch metadata;
- period and payment date;
- payroll fingerprint;
- encrypted NIK and email;
- recipient status;
- send attempts;
- sanitized error metadata.

SQLite does not store:

- employee name after process completion;
- department;
- position;
- salary status;
- join date;
- income components;
- deduction components;
- totals;
- nett income;
- OT Hours;
- PDF bytes;
- Excel bytes.

### Tables

```text
ApplicationSettings
PayrollBatches
BatchRecipients
SendAttempts
```

Use SQLite `PRAGMA user_version` or an equivalently simple internal version value for schema migration; no separate schema-version table is required.

### Encryption

- Generate one random AES key per installation/computer.
- Protect the AES key using Windows DPAPI with `LocalMachine` scope.
- Encrypt NIK and email using AES-GCM with a unique nonce per value.
- Store ciphertext, nonce, authentication tag, and format/version metadata.
- Do not store separate plaintext or search HMAC.
- For duplicate checks, load relevant period records and decrypt in memory.

The security boundary assumes ordinary application use on a trusted accounting computer. Machine-scope DPAPI deliberately makes the credential available across Windows accounts on the same computer. Folder ACLs are a necessary compensating control.

### Gmail token

- Store separately from SQLite.
- Protect using machine-scope DPAPI.
- Never log token contents.
- Disconnect deletes local token material but preserves history.

### Stamp storage

Copy the selected file into shared application data. Do not rely on the original source path.

### Temporary data

- Do not copy the source Excel workbook.
- Create per-batch temporary directories.
- Delete each send PDF after that recipient finishes.
- Clear leftover temporary files on startup.
- Never log PDF content or salary values.

### History retention

- History is permanent until accounting manually deletes a batch.
- Deletion permanently removes batch, recipients, and attempts after confirmation.
- Run database compaction/cleanup only when no batch is active.
- No application-level backup exists.

### Database corruption

When database integrity fails:

- block payroll processing;
- show a corrective explanation;
- offer `Reset Database` only after explicit multi-step confirmation;
- reset loses all history;
- preserve Gmail token and stamp;
- never silently replace the database.

---

## 14. Single Instance, Installation, Upgrade, and Uninstall

### Single instance

Only one instance may run per computer, even across Windows users.

Use an exclusive shared lock file:

```text
C:\ProgramData\EunSlip\runtime\eunslip.lock
```

The first process holds the handle for its lifetime. A second process shows an “application is already in use” message and exits. It must not close or take over the first instance.

### Installer

- Installer: Inno Setup.
- Administrator privileges required.
- Install self-contained .NET 10 Windows x64 output.
- Normal execution does not elevate.
- Version 1 installer and executable are unsigned.
- Documentation must warn that SmartScreen/antivirus may require IT approval or whitelisting.
- Do not bypass or disable Windows security features.

### Upgrade

Manual only.

An upgrade must preserve:

- database;
- Gmail authorization;
- stamp;
- language;
- last email template;
- logs according to retention rules.

Schema migration must be transactional. If migration fails, do not leave a partially migrated database.

### Uninstall

Remove application binaries but preserve:

- history/database;
- Gmail token;
- stamp;
- language;
- email template.

Permanent removal of data is a separate application/IT action.

---

## 15. Logging and Observability

### Technical logs

- Rolling file logs.
- Automatic retention: 30 days.
- Include timestamp, severity, operation, batch ID, recipient internal ID, error category, and sanitized technical details.
- Never include salary values, PDF content, NIK plaintext, email plaintext unless strictly necessary for an accounting-facing screen, token, authorization code, or OAuth credential.
- Release UI never displays a stack trace.

### Operational progress

The active sending screen is the operational monitor. No telemetry server or cloud monitoring is in scope.

---

## 16. Error Handling

Error categories should be stable and UI messages should map to user actions.

Suggested categories:

```text
WorkbookUnreadable
WorkbookTemplateMismatch
WorkbookCachedValueMissing
PayrollValidationFailed
StampMissing
StampUnreadable
PreviewGenerationFailed
PdfGenerationFailed
GmailNotConnected
GmailAuthorizationFailed
EmailSendFailed
DatabaseUnavailable
DatabaseCorrupt
DatabaseMigrationFailed
TemporaryFileCleanupFailed
UnexpectedError
```

Principles:

- Validation and business errors are not logged as crashes.
- Infrastructure exceptions are translated before reaching ViewModels.
- Temporary cleanup failure does not alter a committed email result.
- An email failure for one recipient does not stop the remaining recipients.
- Global Gmail failure is not specially classified for batch stopping in version 1; all recipients still receive their normal retry path.

---

## 17. Database Record Model

### `ApplicationSettings`

Suggested values:

- `UiLanguage`
- `LastEmailSubject`
- `LastEmailBody`
- `ActiveStampRelativePath`
- `ConnectedGoogleEmail`
- `DatabaseVersion` only if not using `PRAGMA user_version`

OAuth tokens are not stored here.

### `PayrollBatches`

- `Id`
- `Period`
- `PaymentDate`
- `Fingerprint`
- `Status`
- `CreatedAtUtc`
- `StartedAtUtc`
- `CompletedAtUtc`
- `WarningConfirmed`
- summary counts

### `BatchRecipients`

- `Id`
- `BatchId`
- encrypted NIK envelope
- encrypted email envelope
- `Status`
- `LastUpdatedAtUtc`

### `SendAttempts`

- `Id`
- `RecipientId`
- `AttemptNumber`
- `AttemptType`
- `StartedAtUtc`
- `CompletedAtUtc`
- `Status`
- `ErrorCategory`
- sanitized `ErrorMessage`
- `GmailMessageId`

Do not persist employee name or payroll amounts.

---

## 18. Use Cases and Acceptance Criteria

### UC-001 Import valid payroll

**Given** the first worksheet follows the exact 27-column contract and contains valid rows  
**When** accounting selects it and enters a period and payment date  
**Then** EunSlip reads all rows, produces no blocking errors, and shows the validation summary.

### UC-002 Reject invalid template

**Given** a column is missing, renamed, added, or reordered  
**When** the workbook is imported  
**Then** EunSlip blocks continuation and identifies the expected and actual header location.

### UC-003 Confirm calculation warning

**Given** an employee's stored total differs from the system comparison  
**When** validation completes  
**Then** EunSlip displays a warning with employee and values, and continuation requires explicit confirmation.

### UC-004 Preview

**Given** at least one valid employee and a valid active stamp  
**When** accounting chooses Open Preview  
**Then** EunSlip creates one one-page A4 PDF for the first valid employee and opens it with the default Windows PDF application.

### UC-005 Send full batch

**Given** the batch is ready, Gmail is connected, stamp is valid, and subject/body are non-empty  
**When** accounting confirms  
**Then** EunSlip sends one sequential individual message to every valid employee and cannot be normally cancelled.

### UC-006 Retry transient failure

**Given** a send attempt fails  
**When** retry policy runs  
**Then** no more than three total attempts occur for that recipient and every attempt is recorded.

### UC-007 Continue after recipient failure

**Given** one employee fails all attempts  
**When** more recipients remain  
**Then** EunSlip marks that employee failed and continues.

### UC-008 Retry failed recipients

**Given** a completed batch has failed recipients  
**When** accounting selects the same payroll data again  
**Then** the fingerprint is verified and only failed NIKs are processed.

### UC-009 Block changed retry file

**Given** accounting selects a workbook whose payroll content differs  
**When** retry/recovery fingerprint is checked  
**Then** EunSlip blocks continuation and requires a new batch.

### UC-010 Recover interruption

**Given** EunSlip stopped while a batch was sending  
**When** it starts again  
**Then** the batch appears interrupted, `Sending` recipients are reset for recovery, and only recipients not committed `Sent` are resent after fingerprint verification.

### UC-011 Warn about duplicate historical send

**Given** the same period and NIK previously succeeded on this computer  
**When** a new batch is prepared  
**Then** EunSlip shows a warning but allows sending after confirmation.

### UC-012 Delete history

**Given** a batch exists  
**When** accounting confirms permanent deletion  
**Then** its batch, recipient, and attempt records are removed and cannot be viewed in EunSlip.

### UC-013 Database corruption

**Given** SQLite integrity validation fails  
**When** EunSlip starts  
**Then** payroll functions are blocked and reset is only performed after explicit confirmation.

### UC-014 Single instance

**Given** EunSlip is already open under any Windows user  
**When** another instance starts  
**Then** the second instance shows a notice and exits.

### UC-015 Upgrade and uninstall

**Given** EunSlip has history, token, stamp, and settings  
**When** it is upgraded or uninstalled  
**Then** shared data is preserved according to the approved rules.

---

## 19. Testing Strategy

### Core unit tests

Cover:

- exact header comparison;
- empty-row handling;
- maximum row count;
- required fields;
- email validation;
- NIK/email duplicates;
- integer nominal validation;
- negative nominal acceptance;
- OT Hours rules;
- total comparisons;
- formatting;
- filename normalization;
- canonical fingerprint;
- retry count;
- batch/recipient transitions;
- recovery recipient selection.

### Infrastructure tests

Cover:

- reading literal and formula cells through Open XML;
- cached formula value missing;
- first-worksheet-only behavior;
- PDF generation and one-page invariant;
- required text and stamp present in PDF;
- SQLite CRUD and transactional migration;
- AES-GCM round trip and tamper rejection;
- DPAPI key/token round trip on Windows;
- MIME has one To, no CC/BCC, one attachment, plain text;
- temporary-file deletion;
- single-instance lock behavior.

### Required test workbooks

- one valid row;
- 500 valid rows;
- wrong header;
- missing/additional/reordered columns;
- blank email;
- invalid email;
- duplicate NIK;
- duplicate email;
- invalid date;
- formula with no cached value;
- nominal decimal;
- negative values;
- blank nominal components;
- calculation mismatch;
- OT Hours blank;
- OT Hours integer;
- OT Hours one decimal;
- OT Hours more than one decimal;
- unsafe filename characters.

### UAT

Use dummy payroll data only.

Initial real Gmail test:

- use the owner's account as both sender and recipient;
- one employee row;
- verify authorization, received message, visible sender, attachment filename, PDF readability, stamp, temp cleanup, and minimal history;
- replace with the company's payroll Gmail account before production.

### Performance baseline

For 500 rows:

- import/validation runs off the UI thread;
- the UI remains responsive;
- sending reports progress;
- PDFs are disposed/deleted and memory does not grow without bound;
- no hard duration promise because Gmail and internet speed are external.

---

## 20. Implementation Order

### Phase 1 — Foundation

- Create solution and three projects.
- Configure nullable reference types, analyzers, warnings, dependency injection, and test projects.
- Implement shared application paths.
- Implement single-instance lock.
- Implement localization skeleton.
- Implement technical logging with redaction rules.
- Add Inno Setup skeleton.

### Phase 2 — Core payroll contract

- Define payroll row and batch models.
- Implement exact headers.
- Implement normalization and validation.
- Implement calculation warnings.
- Implement date/number/file formatting.
- Implement fingerprint.
- Add Core tests.

### Phase 3 — Excel adapter

- Implement first-worksheet Open XML reader.
- Read literals and cached formula values.
- Preserve row/column coordinates for errors.
- Add workbook fixtures and Infrastructure tests.

### Phase 4 — PDF and stamp

- Implement stamp setting/storage.
- Implement fixed one-page A4 PDF layout.
- Implement preview temp-file lifecycle.
- Add PDF structural/snapshot checks.

### Phase 5 — Persistence and security

- Implement SQLite schema and migrations.
- Implement AES-GCM encrypted NIK/email envelopes.
- Protect AES key using DPAPI LocalMachine.
- Implement settings/history/recovery queries.
- Implement integrity check and reset flow.

### Phase 6 — Gmail

- Implement desktop OAuth with `gmail.send`, `openid`, and `email`.
- Implement machine-shared token storage.
- Implement plain-text MIME with one PDF.
- Implement sequential send and three-attempt retry.
- Add adapter tests with mocked HTTP/client boundary.

### Phase 7 — WPF workflow

- Home.
- Payroll wizard steps.
- Validation summary.
- Preview launch.
- Confirmation.
- Sending progress.
- Results.
- History.
- Settings.
- About.
- Restart-required language switching.

### Phase 8 — Recovery and hardening

- Interrupted-batch startup detection.
- Failed retry and fingerprint matching.
- Duplicate-send warnings.
- Temp cleanup.
- close-window protection during sending.
- sanitized error UX.

### Phase 9 — Packaging and UAT

- Release self-contained win-x64.
- Create installer.
- Verify upgrade/uninstall persistence.
- Run 500-row baseline.
- Run one-recipient real Gmail UAT.
- Produce deployment and Google Cloud setup guide.

---

## 21. Coding Task Backlog

### TASK-001 Solution foundation

**Output:** buildable three-project solution and two test projects.  
**Tests:** clean build and test command.

### TASK-002 Shared paths and single instance

**Output:** ProgramData directory service and exclusive lock behavior.  
**Tests:** second process cannot start.

### TASK-003 Payroll domain model and validation

**Output:** model, validation result types, blocking errors, warnings.  
**Tests:** complete validation matrix.

### TASK-004 Excel reader

**Output:** approved workbook reader with cached formula support.  
**Tests:** workbook fixture suite.

### TASK-005 Fingerprint

**Output:** stable SHA-256 canonical fingerprint independent of row order and sheet name.  
**Tests:** same data reordered → same hash; changed field → different hash.

### TASK-006 PDF generator

**Output:** one-page A4 salary slip matching specification.  
**Tests:** text/content checks and one-page assertion.

### TASK-007 Stamp settings

**Output:** select, validate, copy, preview, replace, remove.  
**Tests:** invalid and oversized/unreadable images handled.

### TASK-008 Encryption and secret storage

**Output:** AES-GCM recipient encryption and DPAPI-protected key/token storage.  
**Tests:** round trip, nonce uniqueness, tamper rejection.

### TASK-009 SQLite repository

**Output:** settings, batch, recipient, and attempt persistence.  
**Tests:** transactions, migration rollback, delete history, integrity detection.

### TASK-010 Gmail OAuth and sender

**Output:** machine-shared desktop OAuth and individual Gmail send.  
**Tests:** MIME and mocked send outcomes.

### TASK-011 Batch coordinator

**Output:** sequential workflow, progress, three attempts, persistence after every attempt.  
**Tests:** success/failure combinations.

### TASK-012 Recovery and failed retry

**Output:** interrupted detection, `Sending -> Pending`, fingerprint gate, recipient selection.  
**Tests:** recovery and changed-file rejection.

### TASK-013 WPF payroll wizard

**Output:** complete six-step workflow.  
**Tests:** ViewModel command/state tests where useful.

### TASK-014 History and settings UI

**Output:** history details/delete, Gmail, stamp, language, about.  
**Tests:** core ViewModel behavior and repository integration.

### TASK-015 Installer and operational docs

**Output:** `EunSlip-Setup-x64.exe` build path, Google Cloud setup guide, deployment guide.  
**Tests:** clean install, upgrade, uninstall preservation.

### TASK-016 UAT release candidate

**Output:** signed-off Release build for internal UAT.  
**Tests:** automated suite, 500-row baseline, one-recipient real Gmail send.

---

## 22. Definition of Done

EunSlip version 1 is coding-complete only when:

- Release build succeeds for `win-x64`.
- All automated tests pass.
- No preview/production payroll data is committed to the repository.
- No OAuth token, authorization code, client credential file, personal test address, or stamp image is committed unless explicitly approved as dummy data.
- Exact Excel contract is enforced.
- One-page PDF output passes UAT.
- Every valid employee receives a separate message.
- Failed sends do not prevent remaining recipients.
- Retry never exceeds three total attempts per recipient/session.
- Failed retry and interrupted recovery work with fingerprint protection.
- SQLite stores no payroll nominal values.
- NIK and email are encrypted at rest.
- Technical logs contain no prohibited sensitive data.
- Installer works on Windows 10/11 x64 without preinstalled .NET.
- Upgrade and uninstall preserve shared data.
- Indonesian is the default UI language.
- Real one-recipient Gmail UAT succeeds.
- Deployment and OAuth setup instructions are complete.

---

## 23. Known Risks and Accepted Trade-offs

1. **Cross-computer duplicates:** No shared server means one computer cannot detect sends from another.
2. **Duplicate after crash:** A message may reach Gmail before `Sent` is committed; approved recovery resends it.
3. **No PDF password:** Security depends on correct recipient email and recipient mailbox access.
4. **Machine-shared Gmail token:** All local users able to run EunSlip can send through the payroll account.
5. **No backup:** Database loss means history loss.
6. **Unsigned installer:** SmartScreen or antivirus may require IT approval.
7. **Google quotas/policies:** Sending throughput and account restrictions are external to EunSlip.
8. **Cached formula dependence:** A workbook not calculated/saved in Excel can be rejected.
9. **Uninstall keeps data:** IT must perform a separate secure cleanup when a computer is retired.
10. **Email display name:** Final presentation may be governed by Gmail account/send-as settings and must be verified during UAT.

---

## 24. Architecture Decisions

### ADR-001 Native Windows

Use WPF/.NET rather than Electron or Python because the target is Windows-only, machine-level credential protection and installer integration are important, and long-term distribution risk is lower.

### ADR-002 Three projects

Use Desktop, Core, and Infrastructure. Do not split Domain and Application into separate projects because EunSlip has one bounded workflow and the additional mapping/interface overhead is not justified.

### ADR-003 Local SQLite

Use a local per-computer SQLite database because installations are independent and no server is approved.

### ADR-004 Gmail API

Use Gmail API and desktop OAuth for version 1. Keep Gmail code behind an external boundary so SMTP may be added later.

### ADR-005 No payroll archive

Do not retain Excel, PDFs, or full payroll rows. Retain only encrypted minimal recipient history and send metadata.

### ADR-006 One-page PDF

The fixed set of fields must fit one A4 portrait page. Pagination is explicitly not supported.

### ADR-007 Sequential send

Send sequentially to simplify status consistency and reduce concurrent pressure on Gmail.

### ADR-008 Machine-shared security scope

Use machine-scope credential protection because configuration is shared across Windows users by business decision. Accept its broader local-machine access implications.

---

## 25. Deployment Prerequisites

Before production deployment, IT/developer must:

1. Create a Google Cloud project.
2. Enable Gmail API.
3. Configure the Google Auth platform/consent screen.
4. Use Internal audience when applicable to the company's Google Workspace organization.
5. Create OAuth 2.0 Client ID with application type Desktop app.
6. Prepare the desktop OAuth credential for injection into the build/installer; keep the production file out of source control.
7. Configure or verify the payroll Gmail sender display name as `PT. EUNSUNG INDONESIA`.
8. Install EunSlip with administrator rights.
9. Configure the stamp image.
10. Connect the payroll Gmail account.
11. Run a dummy one-recipient UAT before using production payroll data.

---

## 26. Reference Files

- `EunSlip_Payroll_Template.xlsx` — approved Excel contract and example.
- `payslip-layout-reference.png` — visual layout reference supplied by the user.

Official implementation references:

- .NET release support: https://learn.microsoft.com/dotnet/core/releases-and-support
- WPF: https://learn.microsoft.com/dotnet/desktop/wpf/
- MVVM Toolkit: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/
- Open XML formulas/cached values: https://learn.microsoft.com/office/open-xml/spreadsheet/working-with-formulas
- Gmail send guide: https://developers.google.com/workspace/gmail/api/guides/sending
- Gmail `messages.send`: https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/send
- Google OAuth for installed applications: https://developers.google.com/identity/protocols/oauth2
- PDFsharp/MigraDoc: https://docs.pdfsharp.net/
- Microsoft.Data.Sqlite: https://learn.microsoft.com/dotnet/standard/data/sqlite/
- Windows DPAPI `ProtectedData`: https://learn.microsoft.com/dotnet/api/system.security.cryptography.protecteddata
- Self-contained publishing: https://learn.microsoft.com/dotnet/core/deploying/

---

## 27. Coding Readiness Sign-off

**Status:** Ready for coding  
**Approved architecture:** WPF, .NET 10 LTS, compact three-project modular monolith  
**Approved first implementation task:** TASK-001 Solution foundation  
**Outstanding business blockers:** None

Any future change to the Excel contract, salary-slip labels, email provider, data retention, security scope, or recovery behavior requires a documented decision before implementation.
