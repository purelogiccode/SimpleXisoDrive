# Privacy and Networking

SimpleXisoDrive performs a small number of network requests. This page lists every request, when it
happens, what data it carries, and how to prevent it entirely.

---

## Network requests

| Trigger | Method | Endpoint | Data sent | Frequency |
| --- | --- | --- | --- | --- |
| Application start | `POST` | `https://www.purelogiccode.com/ApplicationStats/stats` | Application ID, application version | Once per launch (fire-and-forget) |
| Application start | `GET` | `https://api.github.com/repos/purelogiccode/SimpleXisoDrive/releases/latest` | `User-Agent: SimpleXisoDrive-UpdateChecker` | Once per launch |
| Warning/error logged or fatal crash | `POST` | `https://www.purelogiccode.com/bugreport/api/send-bug-report` | Diagnostic report (see below) | Capped at 8 per minute |

No other connections are made by the application.

---

## Launch statistics

`StatsService` reports an anonymous "application started" event. The payload contains only:

```json
{
  "applicationId": "simplexisodrive",
  "version": "1.3.0"
}
```

The request is sent on a background task and its result never affects the application. Timeouts
(10 seconds) and connection failures are silently logged at Debug level.

---

## Update check

`UpdateChecker` requests the latest release metadata for the public GitHub repository. GitHub
receives the request from your IP address and the `User-Agent` string, as with any GitHub API call.
The response is parsed locally to compare versions. Failures are logged locally at Information level
and are deliberately excluded from bug reports.

If no newer version exists, nothing is displayed. If stdin is redirected (for example, when the
application is started by a script), the interactive prompt is skipped.

---

## Bug reports

Bug reports are sent only when something goes wrong: a Serilog event at **Warning** level or higher,
an unhandled exception, or an unobserved task exception.

### Data included

| Field | Source | Example |
| --- | --- | --- |
| `message` | Rendered report | Environment + error + exception sections |
| `applicationName` | Constant | `SimpleXisoDrive` |
| `version` | Assembly metadata | `1.3.0` |
| `userInfo` | `Environment.UserName` | Windows account name |
| `environment` | Runtime information | OS description and architecture |
| `stackTrace` | Exception `ToString()` | Managed stack trace |

The report body also includes:

- local date and time with UTC offset;
- OS version string, OS and process architecture, 32/64-bit flags;
- Windows version, processor count;
- application base directory and temp path;
- exception type, message, source, and stack trace.

### What is never included

- The contents of the mounted image or any file on disk.
- File names or directory listings, unless they appear inside an exception message or stack trace
  (for example, when an I/O operation fails on a specific file).
- Passwords, credentials, or machine identifiers.

### Authentication

Both API endpoints require an API key. The key is not stored in plain text: it is double-encrypted
in the binary (AES-256-CBC over a SHA-256 XOR layer) and decrypted once at startup by
`ApiKeyProvider`. The key authorizes the application to submit reports; it is not a user credential.

---

## Running fully offline

The application works completely offline; network calls are advisory only. To prevent outbound
traffic, block the following hosts at your firewall or in `%SystemRoot%\System32\drivers\etc\hosts`:

```text
0.0.0.0 www.purelogiccode.com
0.0.0.0 api.github.com
```

When blocked:

- launch statistics and update checks fail silently (Debug/Information logs only);
- bug report submissions fail and the failure is written to `critical_error.log` next to the
  executable;
- all mounting, browsing, and reading continue to work normally.

No telemetry setting or configuration file exists; behaviour is fixed in the current version.

---

## Log data on disk

Diagnostic data also stays on your machine in:

| File | Retention |
| --- | --- |
| `logs\simplexisodrive-YYYYMMDD.log` | 7 daily files |
| `error.log` | Unbounded; delete manually |
| `critical_error.log` | Unbounded; delete manually |

Delete these files at any time to remove local diagnostic history.
