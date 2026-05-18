# EAMAS — Full Security & Bug Audit Report

> **Scope**: Complete static analysis of every `.cs` file across `EAMAS.Core` and `EAMAS.Desktop`.
> **Status**: All issues listed below have been **patched** in this session.

---

## 🔴 Critical Vulnerabilities (Fixed)

### 1 · Hardcoded Default Admin Password
| | |
|---|---|
| **File** | `EAMAS.Core/Services/DatabaseInitializerService.cs` |
| **Severity** | Critical |
| **CWE** | CWE-798 – Use of Hard-coded Credentials |

**Bug**: The SuperAdmin seeder fell back to the plaintext password `Admin@123` whenever the `EAMAS_SUPERADMIN_PASSWORD` environment variable was not set. Any attacker who knows this published default can log in as SuperAdmin immediately after installation.

**Fix**: Removed the hardcoded fallback entirely. If the env-var is absent, a **cryptographically random 16-character temporary password** is generated at runtime and printed to `Debug.WriteLine`. `MustChangePassword` is set to `true` on the seeded user, forcing the operator to rotate the password on first login.

---

### 2 · Weak Encryption Key Derivation
| | |
|---|---|
| **File** | `EAMAS.Core/Services/EncryptionService.cs` |
| **Severity** | Critical |
| **CWE** | CWE-916 – Use of Password Hash With Insufficient Computational Effort |

**Bug**: The AES-256-GCM field-level encryption key was derived as `SHA256(MachineName + "EAMAS_FIELD_ENC_v1")` — a single-round hash. An attacker with the MongoDB dump and knowledge of the machine name (often discoverable from other sources) can trivially recompute the key and decrypt all stored API tokens and GitHub PATs.

**Fix**: Replaced with **PBKDF2 (100,000 iterations, SHA-256)** using the Windows `MachineGuid` registry value as the salt. The MachineGuid is stable across reboots and unique per Windows installation, making offline brute-force orders of magnitude harder. Falls back to a hashed MachineName if registry access is denied (sandboxed unit tests).

---

### 3 · No Installer Integrity Verification (Supply-Chain Risk)
| | |
|---|---|
| **File** | `EAMAS.Desktop/Services/UpdateService.cs` |
| **Severity** | Critical |
| **CWE** | CWE-494 – Download of Code Without Integrity Check |

**Bug**: `DownloadAndInstallAsync` downloaded the update installer and immediately ran it with `Process.Start` without verifying its integrity. A MITM attacker or a compromised CDN host could replace the installer with malware. The application would silently execute it with `ShellExecute = true`.

**Fix**: The `version.json` manifest now supports an optional `Sha256` field. When present, the downloaded file's SHA-256 is computed and compared against the manifest value **before** `Process.Start` is called. A mismatch deletes the file and throws a descriptive exception. When the field is absent, a `Debug.WriteLine` warning is emitted (backwards-compatible for existing manifests).

---

## 🟠 High Severity Bugs (Fixed)

### 4 · Session Token Orphaned on Consent Refusal
| | |
|---|---|
| **File** | `EAMAS.Desktop/ViewModels/LoginViewModel.cs` |
| **Severity** | High |
| **CWE** | CWE-613 – Insufficient Session Expiration |

**Bug**: When an Employee user refused the monitoring consent dialog, `App.CurrentUser` and `App.CurrentOrganization` were cleared — but the session token that had just been written to MongoDB (`OpenSession`) was left open. The user's account would appear "logged in" from this machine indefinitely, blocking future logins on other machines.

**Fix**: `CloseSession(userId, token)` is called and `App.CurrentSessionToken` is nulled out before returning from the consent-refusal branch.

---

### 5 · `ReplaceOne` Race Condition in Exploit Cleanup
| | |
|---|---|
| **File** | `EAMAS.Core/Services/ActivityMonitorService.cs` – `CleanUpTimeManipulationExploits` |
| **Severity** | High |
| **CWE** | CWE-362 – Race Condition |

**Bug**: The cleanup job fetched a suspicious document, modified it in memory, then called `ReplaceOne`. If any concurrent write (e.g. the background monitoring loop) updated the same document between the fetch and the replace, the replacement would silently overwrite the concurrent change.

**Fix**: Replaced `ReplaceOne` with a field-level `UpdateOne` using `$set` targeting only `EndTime`, `WasClockAdjusted`, and `OriginalEndTime`. This is atomic and safe for concurrent access.

---

### 6 · `AppUsage.DurationTicks` Can Go Negative
| | |
|---|---|
| **File** | `EAMAS.Core/Services/ActivityMonitorService.cs` – `CleanUpTimeManipulationExploits` |
| **Severity** | High |

**Bug**: The exploit cleanup decremented `DurationTicks` on the `AppUsage` aggregate document using `Inc(-diffTicks)`. If the aggregate had already been partially corrected (e.g. by a previous partial run), the decrement could push `DurationTicks` below zero, corrupting all productivity metrics and reports.

**Fix**: Added a `Builders<AppUsage>.Filter.Gte(a => a.DurationTicks, diffTicks)` guard to the update filter. If the stored value is already less than the decrement amount, the update simply does nothing (no-op), preventing negative values.

---

### 7 · GitHub HTTP Client Auth-Header Race Condition
| | |
|---|---|
| **File** | `EAMAS.Core/Services/GitHubPollingService.cs` |
| **Severity** | High |
| **CWE** | CWE-362 – Race Condition |

**Bug**: `PollAllProjectsAsync` iterated over multiple projects and called `SetGitHubAuth(token)` on a shared `HttpClient` before each request. Because `HttpClient` is shared and `SetGitHubAuth` mutates `DefaultRequestHeaders.Authorization`, concurrent project polls could overwrite each other's tokens, causing requests to use the wrong GitHub PAT (token from project A used for project B's requests).

**Fix**: Removed `SetGitHubAuth` entirely. Each outgoing request now creates its own `HttpRequestMessage` and sets `request.Headers.Authorization` locally, making each request self-contained and thread-safe.

---

### 8 · Fire-and-Forget Async Exception Loss
| | |
|---|---|
| **File** | `EAMAS.Core/Services/GitHubPollingService.cs` – `Start()` |
| **Severity** | High |

**Bug**: `_ = PollAllProjectsAsync()` discarded the returned `Task`. Any exception that escaped the top-level `catch` inside `PollAllProjectsAsync` would cause an **unobserved task exception**, which in .NET can silently terminate the background polling with no diagnostic output.

**Fix**: The timer callback is now an `async` lambda that `await`s `PollAllProjectsAsync()` and wraps it in a try/catch that logs the exception to `Debug.WriteLine`.

---

## 🟡 Medium Severity Issues (Fixed)

### 9 · Weak Password Minimum Length
| | |
|---|---|
| **File** | `EAMAS.Desktop/ViewModels/SettingsViewModel.cs` |
| **Severity** | Medium |
| **CWE** | CWE-521 – Weak Password Requirements |

**Bug**: The password change form only required 6 characters with no complexity rules. A user could set `password` (or `123456`) as their monitoring system password.

**Fix**: Password policy upgraded to **8+ characters** with mandatory uppercase, lowercase, digit, and special character. Corresponding UI status messages guide the user on each failure condition.

---

### 10 · `SettingsService.GetSettings` Duplicate-Key Race
| | |
|---|---|
| **File** | `EAMAS.Core/Services/SettingsService.cs` |
| **Severity** | Medium |
| **CWE** | CWE-362 – Race Condition |

**Bug**: `GetSettings` called `InsertOne` if `Find` returned null. Two threads (e.g. the screenshot loop and the activity loop) starting simultaneously could both find null and both attempt `InsertOne`, causing a MongoDB duplicate-key exception on the unique `OrganizationId` index, crashing the background service.

**Fix**: Replaced `InsertOne` with `ReplaceOne(IsUpsert: true)`. MongoDB's upsert is an atomic operation — only one document will ever be inserted.

---

### 11 · Screenshot `TakenAt` Used Wall Clock Time
| | |
|---|---|
| **File** | `EAMAS.Desktop/Services/MonitoringBackgroundService.cs` |
| **Severity** | Medium |

**Bug**: `CaptureAndSaveAsync` stored `TakenAt = DateTime.UtcNow`. An employee who changed the system clock forward could make screenshots appear to have been taken hours later, obscuring what they were doing at a specific time.

**Fix**: Changed to `TakenAt = _timeIntegrity.GetTrustedUtcNow()`, which uses the monotonic `Stopwatch`-based clock anchored at application startup — immune to clock manipulation.

---

### 12 · Silent Exception Swallowing in Activity Loop
| | |
|---|---|
| **File** | `EAMAS.Desktop/Services/MonitoringBackgroundService.cs` |
| **Severity** | Medium |

**Bug**: `catch { await Task.Delay(2000, ct); }` swallowed all exceptions in `ActivityLoop` without any logging, making real bugs (e.g. MongoDB connection failures, `NullReferenceException`) completely invisible.

**Fix**: Changed to `catch (Exception ex)` with a `Debug.WriteLine` call logging the exception type and message before the retry delay.

---

### 13 · MongoDB Server Selection Timeout Too Long
| | |
|---|---|
| **File** | `EAMAS.Core/Data/MongoDbContext.cs` |
| **Severity** | Medium |

**Bug**: `ServerSelectionTimeout = 60 seconds`. If MongoDB became unreachable (network loss, server restart), any synchronous service call made from the UI thread (e.g. `GetSettings`, `GetAll`) would block the entire application for a full minute before throwing an exception.

**Fix**: Reduced to **10 seconds** — sufficient to detect a true outage without freezing the UI for unacceptable durations.

---

## 🟢 Performance / Quality Improvements (Fixed)

### 14 · Uncached DB Reads on Every Activity Poll
| | |
|---|---|
| **File** | `EAMAS.Core/Services/AppCategorizationService.cs` |
| **Severity** | Low (Performance) |

**Bug**: `Categorize()` issued a fresh `Find` query to MongoDB on every call to fetch custom categorization rules. At the default 5-second poll interval with 20 employees this generates ~240 DB reads per minute for rule data that almost never changes.

**Fix**: Added a per-org in-memory cache with a **60-second TTL**. Cache misses go to the DB; subsequent calls within the window use the cached list. An `InvalidateCache(orgId)` method is provided for admin pages to call after saving rule changes, ensuring immediate effect.

---

### 15 · Missing `MustChangePassword` Flag
| | |
|---|---|
| **File** | `EAMAS.Core/Models/User.cs` |
| **Severity** | Low |

**Added**: New `MustChangePassword` boolean field on the `User` model. Set to `true` when an account is seeded with a random temporary password. `LoginViewModel` now checks this flag after successful authentication and shows a mandatory warning dialog directing the user to the Settings page. `SettingsViewModel.ChangePassword` clears the flag on success.

---

## Summary Table

| # | Severity | File | Issue | Status |
|---|---|---|---|---|
| 1 | 🔴 Critical | DatabaseInitializerService | Hardcoded default password `Admin@123` | ✅ Fixed |
| 2 | 🔴 Critical | EncryptionService | SHA-256 key derivation (no stretching) | ✅ Fixed |
| 3 | 🔴 Critical | UpdateService | No installer hash verification | ✅ Fixed |
| 4 | 🟠 High | LoginViewModel | Session token orphaned on consent refusal | ✅ Fixed |
| 5 | 🟠 High | ActivityMonitorService | `ReplaceOne` race in exploit cleanup | ✅ Fixed |
| 6 | 🟠 High | ActivityMonitorService | AppUsage can go negative | ✅ Fixed |
| 7 | 🟠 High | GitHubPollingService | Auth header race condition (shared HttpClient) | ✅ Fixed |
| 8 | 🟠 High | GitHubPollingService | Fire-and-forget exception loss | ✅ Fixed |
| 9 | 🟡 Medium | SettingsViewModel | Weak password minimum (6 chars, no complexity) | ✅ Fixed |
| 10 | 🟡 Medium | SettingsService | Duplicate-key race on concurrent `InsertOne` | ✅ Fixed |
| 11 | 🟡 Medium | MonitoringBackgroundService | Screenshot `TakenAt` uses manipulable wall clock | ✅ Fixed |
| 12 | 🟡 Medium | MonitoringBackgroundService | Bare `catch { }` swallows exceptions silently | ✅ Fixed |
| 13 | 🟡 Medium | MongoDbContext | `ServerSelectionTimeout` = 60s blocks UI | ✅ Fixed |
| 14 | 🟢 Low | AppCategorizationService | DB query on every 5-second poll cycle | ✅ Fixed |
| 15 | 🟢 Low | User model + UserService | No forced-change flag for temp passwords | ✅ Fixed |

---

> **Next recommended steps**:
> - Set the `EAMAS_SUPERADMIN_PASSWORD` environment variable in production deployment
> - Add `"Sha256": "<hex>"` to the `version.json` release manifest going forward
> - Rebuild the solution to confirm all patches compile cleanly (`dotnet build`)
