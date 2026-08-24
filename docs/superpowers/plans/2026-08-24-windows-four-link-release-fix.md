# Windows Four-Link Release Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the production cross-thread crash and make the Windows Setup supply or configure every software prerequisite needed by LAN, Wi-Fi Direct, Bluetooth, and USB.

**Architecture:** UI-bound pairing snapshots are marshalled through Avalonia's UI dispatcher at the presentation boundary. ADB is treated as an application-private tool resolved from the install directory, while Setup owns firewall provisioning and launches the optional VB-CABLE installer through inbox Windows PowerShell. Hardware capabilities remain diagnostics, not bundled drivers.

**Tech Stack:** .NET 10, Avalonia, xUnit, PowerShell/Pester, Inno Setup 6, Android Platform Tools.

**Spec:** `docs/bugs/2026-08-24-user-machine-four-link-failure.md`

## Global Constraints

- Execute tasks serially and do not use subagents.
- Preserve community/official source parity for application-core fixes.
- Do not require a machine-wide PATH mutation.
- Do not silently install third-party drivers without user consent.
- Every production-code change follows a witnessed red-green test cycle.
- Do not push or publish remote branches during this repair.

---

### Task 1: Pairing UI thread boundary

**Files:**
- Create: `docs/bugs/2026-08-24-user-machine-four-link-failure.md`
- Modify: `windows/MoDi.Presentation/P2p/PairedDevicesViewModel.cs`
- Test: `windows/MoDi.Presentation.Tests/P2p/PairingOverlayViewTests.cs`

**Interfaces:**
- Consumes: `Avalonia.Threading.Dispatcher.UIThread`
- Produces: pairing snapshot application that always runs on the Avalonia UI thread

- [ ] Add a rendered-view regression test that publishes a pairing snapshot from `Task.Run` while a device button is bound.
- [ ] Run the focused test and confirm it fails with Avalonia thread ownership verification.
- [ ] Replace the nullable captured-context fallback with Avalonia dispatcher marshalling.
- [ ] Run focused presentation tests and commit the independently verified fix.

### Task 2: Application-private ADB

**Files:**
- Modify: `windows/MoDi.Desktop/Links/Usb/UsbDeviceHelper.cs`
- Test: `windows/MoDi.Desktop.Tests/Links/UsbDeviceHelperTests.cs`
- Modify: `scripts/publish/Build-CommunityRelease.ps1`
- Modify: `scripts/publish/Build-GiteeRelease.ps1`
- Test: `scripts/publish/tests/ReleaseArtifacts.Tests.ps1`
- Modify: `THIRD-PARTY-NOTICES.md`

**Interfaces:**
- Produces: `tools/adb/adb.exe` with its companion DLLs in every Windows distribution
- Produces: USB process launches from `AppContext.BaseDirectory/tools/adb/adb.exe`

- [ ] Add a failing process-resolution test for the application-private ADB path.
- [ ] Implement deterministic private-path resolution with a PATH fallback only for developer runs.
- [ ] Add a failing release-artifact test requiring the ADB runtime set.
- [ ] Extend the release build to accept and verify a pinned Platform Tools archive, copy only required runtime files and notices, then make the artifact test pass.
- [ ] Run Desktop and Pester focused suites and commit.

### Task 3: Installer firewall lifecycle

**Files:**
- Modify: `scripts/publish/MoDi.Setup.iss`
- Test: `scripts/publish/tests/SetupContract.Tests.ps1`

**Interfaces:**
- Produces: inbound private/domain firewall rules scoped to `MoDi.Desktop.exe` for UDP 12345, 12347, and mDNS 5353
- Produces: uninstall removal of only MoDi-owned firewall rules

- [ ] Add a failing Setup contract test that compiles the installer and validates the generated install/uninstall command table.
- [ ] Add scoped firewall creation and cleanup entries.
- [ ] Run the Setup contract suite and commit.

### Task 4: VB-CABLE launch compatibility

**Files:**
- Modify: `scripts/publish/Install-VbCable.ps1`
- Modify: `scripts/publish/MoDi.Setup.iss`
- Test: `scripts/publish/tests/VbCableInstaller.Tests.ps1`

**Interfaces:**
- Consumes: inbox Windows PowerShell 5.1
- Produces: explicit, elevated, opt-in VB-CABLE installer launch with publisher-signature validation

- [ ] Add failing tests for Windows PowerShell 5.1 compatibility and rejection of an untrusted extracted installer.
- [ ] Remove the PowerShell 7 requirement and add an injectable signature verifier for fixture testing.
- [ ] Launch `powershell.exe` explicitly from Setup and retain the unchecked user-consent task.
- [ ] Run the focused Pester suite and commit.

### Task 5: Full distribution regression

**Files:**
- Modify: `docs/发布/发布总检查清单.md`
- Modify: root progress documents that describe Windows prerequisites

**Interfaces:**
- Produces: a rebuilt official Windows directory and Setup candidate

- [ ] Run all Windows solution tests and all publishing Pester suites.
- [ ] Build the self-contained official distribution with private ADB and inspect its file manifest.
- [ ] Compile Setup and verify version, signature state, payload, firewall entries, and VB-CABLE launch command.
- [ ] Perform a local launch smoke test without relying on PATH ADB.
- [ ] Record exact evidence and commit documentation; do not publish remotely.
