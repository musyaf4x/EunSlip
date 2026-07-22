# Maximized Window and Setup Guide Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Start EunSlip maximized and restore the complete setup tutorial in the current Settings UI.

**Architecture:** Keep the change XAML-only. Protect both user-visible contracts with source-level Desktop tests already used by this repository.

**Tech Stack:** .NET 10, WPF XAML, xUnit

## Global Constraints

- Preserve the redesigned Settings layout, styles, bindings, and behavior.
- Add no dependencies or code-behind.

---

### Task 1: Startup State and Setup Tutorial

**Files:**
- Modify: `tests/EunSlip.Desktop.Tests/ThemeContractTests.cs`
- Modify: `src/EunSlip.Desktop/MainWindow.xaml`
- Modify: `src/EunSlip.Desktop/Views/SettingsView.xaml`

**Interfaces:**
- Consumes: existing WPF `WindowState` property and Settings `Expander`.
- Produces: maximized startup and seven visible tutorial steps.

- [ ] **Step 1: Write failing contract tests**

Add assertions that `MainWindow.xaml` contains `WindowState="Maximized"` and that `SettingsView.xaml` contains all numbered Google Cloud/OAuth instructions from steps 1 through 7.

- [ ] **Step 2: Verify RED**

Run: `dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~ThemeContractTests"`

Expected: FAIL because the window state and complete tutorial are absent.

- [ ] **Step 3: Implement the minimal XAML change**

Add `WindowState="Maximized"` to `MainWindow`; replace the one-sentence guide with the previous seven-step tutorial using wrapping `TextBlock` elements inside the existing expander.

- [ ] **Step 4: Verify GREEN**

Run the same filtered Desktop tests and expect all `ThemeContractTests` to pass.

- [ ] **Step 5: Verify the solution**

Run `dotnet build EunSlip.slnx --no-restore` and `dotnet test EunSlip.slnx --no-build` with zero failures.

- [ ] **Step 6: Commit**

Commit the test and two XAML files with message `ui: maximize startup and restore setup guide`.
