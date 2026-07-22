# EunSlip v1 — Full Product Redesign and TASK 13/14 Stabilization

**Date:** 2026-07-22

**Status:** Proposed for user review

**Decision:** Option 3 — broad redesign
**Authoritative inputs:** `EunSlip_Coding_Agent_Context.md`, `Design Reference.md`, `payslip-layout-reference.png`, Vorth instructions, and the 2026-07-22 desktop E2E audit.

## 1. Decision Summary

EunSlip will receive a broad UI and workflow redesign before TASK 15 and TASK 16. This is not a new product architecture and not a rewrite of the core payroll engine. The existing .NET 10 WPF modular monolith, MVVM composition, Core/Infrastructure boundaries, SQLite persistence, Gmail integration, and PDFsharp pipeline remain in place.

The redesign changes the complete desktop shell, every page layout, the payroll wizard's lifecycle and visual hierarchy, History recovery/retry behavior, and the payslip document layout. It also closes the functional acceptance gaps discovered in TASK 13/14.

The guiding result is a professional internal payroll tool that feels deliberate, dense enough for operational use, and safe around irreversible sends.

## 2. Goals

1. Make the payroll flow reliable from workbook selection through confirmation, sending, and results.
2. Make Gmail and stamp readiness truthful and current wherever they are displayed.
3. Turn History into an operational master-detail workspace rather than a passive table.
4. Implement real failed-retry and interrupted-recovery entry paths back into the wizard.
5. Redesign all pages using the provided design tokens and the user's preference for firm, professional typography.
6. Produce a payslip PDF whose geometry follows the supplied reference and never overlaps or clips.
7. Protect in-progress sending from navigation or window-close interruption.
8. Add sufficient regression coverage and stable automation hooks to make the release candidate repeatable.

## 3. Non-goals

1. Installer construction, production packaging, and deployment remain TASK 15.
2. Real Gmail production UAT and final release-candidate sign-off remain TASK 16.
3. No cloud backend, web frontend, or cross-platform migration.
4. No new UI framework or third-party component suite.
5. No schema rewrite or replacement of SQLite.
6. No change to payroll calculation rules, workbook schema, Gmail retry policy, or PII encryption policy unless required to repair an identified correctness defect.
7. No decorative gradients, drop shadows, animation-heavy transitions, or ornamental illustrations.

## 4. Evidence Driving the Redesign

### 4.1 Payroll confirmation blocker

The desktop E2E audit reached Langkah 4 using a valid two-row dummy workbook. Home and Settings resolved Gmail and stamp as ready, while the wizard displayed both as `False` and disabled `KONFIRMASI & KIRIM`.

The root cause is lifecycle wiring: `PayrollWizardViewModel.LoadedAsync` refreshes prerequisites, but `WizardView` never invokes it. A secondary problem is that prerequisite property changes do not invalidate all derived readiness properties and the confirm command.

### 4.2 History behavior defects

The selected-batch and empty-placeholder visibility converters are reversed. Selecting a batch therefore hides its table/detail content and shows the instruction to select a batch.

Retry and recovery actions only write a status message. They do not transfer a batch context into the wizard, require the same source workbook, verify the fingerprint, filter recipients, or select the correct `AttemptType`.

### 4.3 Incomplete operational states

- Navigation and window close are not guarded while sending.
- Home declares recent/interrupted properties but does not populate them.
- History actions are enabled for any selected batch instead of being status-specific.
- Attempt timing and error metadata are stored but not queryable for the History detail view.
- The first async settings frame can misleadingly show `False` before the real state appears.

### 4.4 UI defects observed across pages

- Typography is too small, thin, and visually uneven for an operational desktop tool.
- The primary/secondary action hierarchy is inconsistent.
- The outlined button template does not render its declared border.
- Active navigation is not persistent or unambiguous.
- Boolean values appear as raw `True`/`False`; in Langkah 4 they visually concatenate with labels.
- Validation table rows clip text vertically.
- Settings uses excessive vertical space and requires unnecessary scrolling at the baseline viewport.
- Whitespace is abundant but not structured into a consistent grid.
- Several important controls have no stable automation identifiers.

### 4.5 PDF defects

- The income/deduction total rule intersects label text.
- Numeric values sit too close to the page border.
- Stamp placement is disconnected from the authorization/signature region.
- The content occupies an unbalanced portion of the page.
- Existing tests verify basic content and stamp embedding, but not collision-free geometry.

## 5. Product and Visual Direction

### 5.1 Tone

The product should feel like a precise payroll console: sober, confident, readable, and safe. The visual language will adapt the editorial discipline in `Design Reference.md` to a data-heavy WPF application. It will not imitate an ecommerce page literally.

### 5.2 Typography

The UI will use installed Windows fonts only. This avoids shipping or licensing a font package.

- Functional family: `Segoe UI`, the Windows-safe substitute for Geist.
- Display family: `Segoe UI` rather than a serif display face. The user explicitly prefers firm professional text, so EunSlip will not use the reference's italic editorial serif.
- Headings: Bold, 28–32 DIPs for page titles; Semibold, 20–24 DIPs for section titles.
- Body and form fields: Regular, 14–16 DIPs with comfortable line height.
- Navigation, buttons, eyebrows, and table headers: Bold, uppercase where the label is short, with modest positive character spacing.
- Technical metadata such as timestamps and NIK hints may use `Consolas` at 13–14 DIPs when alignment materially helps scanning.
- Raw Boolean words will never be user-facing. They become localized status labels and badges.

### 5.3 Color tokens

The application will map the reference tokens into WPF resources:

| Resource role | Value | Use |
|---|---:|---|
| Carbon Ink | `#1A211E` | Primary text, primary action, active navigation |
| Paper White | `#FFFFFF` | Main canvas |
| Obsidian | `#0C0C0C` | Reserved dark emphasis panel |
| Fog | `#EEF1F0` | Soft panels, inputs, secondary surfaces |
| Mist | `#E0E0E0` | Dividers and disabled states |
| Graphite | `#606562` | Secondary copy and metadata |
| Ash Border | `#CCCFCD` | Input and card hairlines |
| Slate | `#363537` | Secondary strong text |
| Ember Red | `#CC2E39` | Error, destructive action, and rare irreversible-send emphasis only |

Success and warning semantics must remain accessible. They will be expressed through text/icon/state first, then a restrained semantic tint if needed; color alone will not carry meaning.

### 5.4 Shape, spacing, and elevation

- Base spacing unit: 4 DIPs.
- Common spacing: 8, 12, 16, 20, 24, 32, 40, and 48 DIPs.
- Input/button/navigation radius: 4 DIPs.
- Card radius: 8 DIPs.
- Status badge radius: pill.
- Hairlines: 1 DIP.
- No shadows or faux elevation.
- Separation comes from tone, borders, and whitespace.
- Default card padding: 24 DIPs.
- Interactive controls must maintain at least a 40 DIP target height.

### 5.5 Responsive desktop constraints

- Baseline design target: 1180 × 760.
- Minimum supported window: 1024 × 680.
- The shell must remain usable at Windows scaling from 100% through 150%.
- Pages use flexible grids and local scrolling only when content genuinely exceeds the viewport.
- Tables receive minimum row heights and text trimming/tooltips rather than vertical clipping.
- Page titles and primary actions remain visible without scrolling at the baseline viewport.

## 6. Application Shell Redesign

### 6.1 Structure

The shell remains a left navigation rail plus content canvas, but is recomposed as follows:

- Rail width becomes deliberate and fixed within a narrow range.
- Brand block sits at the top with product name and concise operational subtitle.
- Navigation occupies the central rail and has a persistent active state.
- A compact environment/status block sits at the bottom: app version, active language, and developer attribution.
- Content uses a consistent page frame with a title row, optional supporting copy, action region, and a maximum readable width.

### 6.2 Navigation states

Each navigation item supports default, hover, keyboard focus, active, and disabled states. Active state uses Carbon Ink fill with Paper White text or an equally unambiguous high-contrast treatment. It cannot depend on pointer position.

`MainViewModel` will expose the current section explicitly and update derived active-state properties when the content changes.

### 6.3 Sending lock

Once sending starts:

- All navigation commands are disabled.
- The window close request is intercepted.
- The user sees a clear explanation that the operation must finish.
- The application does not offer a fake cancel button when cancellation would violate the stated workflow.
- Normal navigation and close behavior return after results or a terminal failure.

## 7. Page Designs

### 7.1 Home

Home becomes the operational starting point rather than a passive status grid.

Top area:

- Strong title and one-sentence description.
- Primary `PROSES PAYROLL` action.
- If an interrupted batch exists, the interruption notice takes precedence as a visible recovery panel with an action into History or recovery.

Status area:

- Gmail status card with localized state and connected account hint.
- Stamp status card with localized state.
- Active language card.
- Loading states say `Memeriksa…`, never `False`.

Recent activity:

- Most recent batch period, status, sent/failed counts, and timestamp.
- Empty state when no batch exists.
- Link/action to History.

`HomeViewModel` will load repository and recovery summaries in addition to Gmail/stamp state.

### 7.2 Payroll wizard

The wizard becomes a contained workspace with a persistent six-step progress rail/header:

1. Pilih Payroll
2. Validasi
3. Pratinjau & Email
4. Konfirmasi
5. Pengiriman
6. Hasil

Completed, current, upcoming, and blocked steps must be visually distinct and accessible.

#### Step 1 — Select

- A structured file picker field with a clearly bordered secondary button.
- Period and payment date in a balanced two-column row.
- Context panel when entering from Failed Retry or Recovery, including original period, batch status, and eligible recipient count.
- Inline validation near the relevant input.
- Primary action remains in a stable footer bar.

#### Step 2 — Validate

- Summary badges for valid, warning, and error counts.
- Table with non-clipping rows, useful column widths, and a readable empty state.
- Warning confirmation appears only when required and is explicit.
- In retry/recovery mode, the table indicates which rows are eligible for resend.
- Fingerprint mismatch is a blocking error with the expected corrective action; it cannot be bypassed.

#### Step 3 — Preview and email

- Preview action becomes a real outlined button.
- Subject and body fields receive standard input styles and labels.
- Recipient/readiness summary uses badges, not raw Boolean strings.
- Preview generation and opening are separated internally so PDF generation can be tested without launching an external viewer.
- A preview-launch failure must not imply that PDF generation itself failed.

#### Step 4 — Confirm

- A concise summary card: mode, period, payment date, recipient count, subject, Gmail account hint.
- Gmail and stamp prerequisite rows show `Memeriksa`, `Siap`, or `Belum siap` with icon and explanatory text.
- Prerequisites refresh when the wizard is opened and immediately before this step.
- The irreversible-send warning uses the single rare Ember Red emphasis in the viewport.
- `KONFIRMASI & KIRIM` activates only after readiness is current and valid.
- Derived readiness properties and command states are invalidated whenever Gmail, stamp, step, or busy state changes.

#### Step 5 — Send

- Progress presents current/total, succeeded, failed, current employee hint, and retry attempt.
- No navigation or window-close escape is available.
- The UI stays responsive while the existing asynchronous coordinator runs.
- Sensitive values remain masked where full display is unnecessary.

#### Step 6 — Results

- Clear sent/failed totals.
- Recipient results table with attempt count and user-safe error category/message.
- Failed recipients expose an action to return through the proper retry path.
- Completion actions lead to Home or History.

### 7.3 History

History becomes a two-pane master-detail operations page.

Master pane:

- Batch list ordered newest first.
- Period, created/started/completed time, status, sent count, and failed count.
- Status badges and concise density appropriate for scanning.
- Correct selected-row persistence and empty states.

Detail pane:

- Batch summary header.
- Recipient list with masked NIK hint, status, last update, latest attempt type, attempt number, completion time, error category, and safe error message.
- Sent Gmail message ID is not displayed unless the existing product context explicitly requires it.
- Placeholder is visible only when no batch is selected.

Actions:

- `KIRIM ULANG GAGAL` is enabled only for a completed batch with failed recipients.
- `PULIHKAN` is enabled only for an interrupted batch.
- `HAPUS` remains destructive and requires explicit in-app confirmation.
- Retry/recovery does not merely show a message; it issues a resume request to the shell and opens the wizard in the matching mode.

Repository support will be extended with a batch-scoped attempt query. Existing encryption and NIK-hint rules remain unchanged.

### 7.4 Settings

Settings becomes a compact grid of functional sections rather than one long vertical form.

- Gmail connection section with account, status, connect/disconnect actions, and OAuth setup disclosure.
- OAuth credentials section with encrypted-storage explanation and save state.
- Stamp section with status, thumbnail/metadata when safe, choose, and delete actions.
- Language section with active value and restart notice.
- Database/diagnostics section only where already supported by the current product scope.
- Async initialization uses localized loading labels.
- Destructive actions retain confirmation behavior.
- Sensitive credential text is never echoed after persistence.

At 1180 × 760, the principal status and actions should fit without forcing the user through a long scroll. Secondary instructions may live in disclosures.

### 7.5 About

About keeps its current legal content but receives the same page hierarchy and spacing system.

- Product name, version, and customer scope form one compact identity panel.
- Vierth Labs attribution and contact form a second panel.
- License and disclaimer remain readable body copy, not oversized cards.
- Diagnostics path is presented as technical metadata.
- Third-party notices required for distribution will be completed during packaging readiness.

## 8. Workflow and State Architecture

### 8.1 Explicit wizard entry modes

Add a Desktop-layer immutable request model with:

- Mode: `Normal`, `FailedRetry`, or `RecoveryRetry`.
- Original batch ID for retry/recovery.
- Attempt type derived from mode.

This is application state, not a new external-boundary service.

### 8.2 Navigation coordination

- `MainViewModel` owns section navigation and subscribes to History resume requests.
- Normal payroll navigation initializes a clean normal-mode wizard.
- History retry/recovery initializes the wizard with the original batch context.
- Re-entering the wizard cannot accidentally retain an older mode or recipient filter.
- View lifecycle refreshes are explicit async calls from navigation and/or the view's Loaded event, with idempotent behavior.

### 8.3 Retry flow

1. User selects a completed batch with failures.
2. History obtains the failed NIK set from the existing recovery service.
3. Shell opens the wizard in `FailedRetry` mode.
4. User selects the same payroll workbook and supplies the original context.
5. Workbook validation runs normally.
6. The computed fingerprint must match the original batch fingerprint.
7. Rows are filtered to failed recipients only.
8. Sending reuses the original batch and records `AttemptType.FailedRetry`.
9. Already-sent recipients are never selected or resent.

### 8.4 Interrupted recovery flow

1. User selects an interrupted batch.
2. History opens the wizard in `RecoveryRetry` mode without prematurely sending or deleting data.
3. User reselects the same payroll workbook.
4. The fingerprint must match.
5. Immediately before sending, the existing recovery preparation reconciles committed sends and resets only unresolved sending recipients.
6. Rows are filtered to all recipients not already sent.
7. Attempts are recorded as `AttemptType.RecoveryRetry`.

### 8.5 Normal flow

Normal mode continues creating a new batch and recipient records only after workbook validation and confirmation requirements are satisfied. It records `AttemptType.Normal`.

### 8.6 Error handling

- User-facing messages are localized and actionable.
- Logs retain technical exception details without leaking protected NIK/email values.
- Loading, empty, blocked, validation-error, unexpected-error, and success states are visually distinct.
- An error refreshing prerequisites blocks confirmation and provides a Settings action rather than assuming `False` silently.
- Database and recovery mutations remain transactional through the repository implementation.

## 9. Payslip PDF Redesign

### 9.1 Layout model

The PDF generator will be decomposed into a small layout model plus drawing operations. The layout model defines immutable rectangles, baselines, and safe insets so geometry can be unit tested independently of PDF rendering.

The document remains A4 portrait and follows this order:

1. Outer document frame with safe page margin.
2. Centered company and salary-period header.
3. Two-column employee identity block.
4. Balanced income/deduction table with aligned labels and right-aligned values.
5. Total rows placed below their rules with guaranteed baseline clearance.
6. Strong nett-income band.
7. OT hours and payment date information row.
8. Connected authorization region with `Made By`, `ACC`, signature lines, and proportional stamp placement.

### 9.2 Geometry rules

- No text baseline may intersect a horizontal or vertical rule.
- Currency values maintain a safe right inset from both column and page borders.
- Income and deduction columns have equal visual weight.
- Row heights are derived from the selected font metrics plus minimum padding.
- Stamp preserves aspect ratio and fits within its authorization rectangle.
- Stamp cannot overlap signature labels, lines, page frame, or document data.
- The authorization region follows the supplied reference's visual rhythm instead of using an unrelated fixed lower-right coordinate.
- Empty payroll components render as zero without collapsing table alignment.

### 9.3 Typography

PDF typography remains neutral and highly compatible. A built-in sans family supported by PDFsharp will be used with bold weight for company, section labels, totals, and nett income. Body and numeric values stay regular with sufficient size and contrast.

### 9.4 Verification

- Unit tests assert bounds, insets, column equality, and non-intersection.
- Existing content and stamp embedding tests remain.
- A representative PDF is generated from the dummy workbook.
- The PDF is rasterized with the available `pdftoppm` runtime and visually compared against the reference before completion.

## 10. Accessibility and Automation

- All major navigation items, wizard inputs/actions, History actions, Settings controls, and confirmation buttons receive stable `AutomationProperties.AutomationId` values.
- Keyboard focus is visible on every interactive control.
- Focus order follows reading order.
- Buttons expose meaningful accessible names.
- Status is conveyed by text/icon, never color only.
- Table row height and font size prevent clipping at supported scale factors.
- Disabled actions include adjacent explanatory text where the reason is not obvious.

## 11. Testing Strategy

The repository's pragmatic TDD policy applies: tests are authored first, but a deliberately failing RED run may be skipped when the failure is logically guaranteed and running it would add no diagnostic value. Each implementation slice receives scoped build/tests; the full suite runs at phase and final gates.

### 11.1 Core and Infrastructure tests

- Retry recipient selection excludes sent recipients.
- Recovery selection and preparation reconcile committed sends correctly.
- Batch attempt queries return latest/batch-scoped metadata in deterministic order.
- Attempt type and batch status transitions are correct for all three modes.
- PDF layout geometry and content invariants.

### 11.2 Desktop tests

- Wizard initialization refreshes Gmail/stamp state.
- Entering Confirm performs a fresh prerequisite check.
- Confirm command invalidates and enables/disables correctly.
- History selection shows detail and hides placeholder.
- History action gating follows batch status/counts.
- Retry/recovery navigation transfers the correct mode and batch ID.
- Home summaries and interrupted state load correctly.
- Main navigation and close guard reflect sending state.
- Preview generation is tested independently from shell-launch behavior.
- Converters and localized status presentation contain no raw Boolean output.

### 11.3 Integration and manual E2E

- Build all projects with zero warnings/errors.
- Run the entire solution test suite.
- Run the app using non-production dummy data.
- Audit Home, all six wizard steps, History selected/empty states, Settings, and About at the baseline viewport.
- Repeat at minimum viewport and at 150% Windows scaling where feasible.
- Reach Langkah 4 and verify readiness is truthful; do not send without explicit UAT authority.
- Verify navigation/close guard using a controlled fake/integration sending state rather than real Gmail delivery.
- Generate and raster-inspect the reference payslip.

## 12. Delivery Slices

The broad redesign will still be implemented in bounded slices to limit regression risk:

1. Design-system resources and shell.
2. Wizard lifecycle, modes, and all six redesigned steps.
3. History repository queries, master-detail layout, retry, and recovery.
4. Home, Settings, and About redesign.
5. Payslip layout model and generator redesign.
6. Automation IDs, complete tests, E2E audit, and final verification.

No new third-party dependency is expected. Ponytail's complexity guard favors the existing WPF, CommunityToolkit.Mvvm, SQLite, PDFsharp, and current service boundaries.

## 13. Acceptance Criteria

The redesign is complete only when all of the following are true:

1. Langkah 4 shows the same current Gmail/stamp state as Settings and enables confirmation when both are ready.
2. No payroll email is transmitted during automated or design-review testing.
3. Selecting a History batch consistently shows its details.
4. History actions are status-aware and open a functioning retry/recovery wizard path.
5. Retry/recovery requires a matching payroll fingerprint and never resends an already-sent recipient.
6. Navigation and window close cannot interrupt active sending.
7. Home displays real configuration, recent-batch, interrupted-batch, and language state.
8. All pages use the unified tokens, firm typography, stable spacing, correct button hierarchy, and non-clipping tables.
9. The baseline and minimum supported window sizes remain usable without horizontal overflow.
10. The payslip contains no collisions, clipping, border crowding, or disconnected stamp placement and visually follows the supplied reference.
11. Build completes with zero warnings/errors.
12. All unit, integration, and desktop regression tests pass.
13. Manual desktop E2E and rasterized PDF review pass before TASK 15 begins.

## 14. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Broad XAML changes cause regressions | Deliver by page slices; keep scoped tests/build green after each slice |
| Retry/recovery mutates a batch too early | Treat History action as navigation only; prepare recovery immediately before confirmed send |
| Async state briefly displays false information | Use explicit loading states and idempotent refresh methods |
| PDF layout fixes one sample but breaks longer values | Centralize layout geometry and test worst-case label/value lengths |
| Added History metadata exposes sensitive values | Continue using NIK hints and user-safe error text; do not surface protected fields |
| UI polish creates unnecessary abstractions | Reuse WPF resources/styles and existing MVVM boundaries; add only application state required for run modes |
| External PDF viewer makes tests environment-dependent | Separate generation from optional shell opening and test them independently |

## 15. Review Decision Requested

Approval of this specification authorizes creation of the detailed implementation plan. It does not authorize real Gmail delivery, deletion of user data, installer publication, pushing, or release deployment.
