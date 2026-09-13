# Contributing

Thanks for your interest in improving SimpleXisoDrive. This page describes how to report problems,
propose changes, and submit code.

---

## Ways to contribute

- **Report bugs** with console output and log files (see below).
- **Suggest features** through GitHub issues.
- **Improve documentation** in the `docs/` folder.
- **Submit code** for fixes, format support, and performance work.
- **Test** with different ISO variants and report which ones work or fail.

Repository: <https://github.com/purelogiccode/SimpleXisoDrive>

---

## Reporting bugs

Include as much of the following as possible:

1. Application version (shown in logs; currently 1.3.0).
2. Windows version and architecture (x64/ARM64).
3. Dokan version installed.
4. The exact command line used.
5. The complete console output.
6. The newest `logs\simplexisodrive-*.log` file.
7. `error.log` and, if present, `critical_error.log`.
8. The image's size and origin (dump tool, format, and layout/packer, if known).

Never attach copyrighted game data; a description of the failure and log excerpt is enough.

---

## Development setup

1. Install the **.NET 10 SDK**.
2. Clone the repository.
3. Build and test:

   ```shell
   dotnet build CSharp_SimpleXisoDrive.sln
   dotnet test CSharp_SimpleXisoDrive.sln
   ```

Dokan is only needed to run the application, not to build or test it.

See [Building](Building) and [Testing](Testing) for details.

---

## Code style

The project has a small, explicit style that reviewers expect:

| Area | Convention |
| --- | --- |
| Namespaces | File-scoped (`namespace SimpleXisoDrive;`) |
| Nullable | Enabled; annotate nullability accurately |
| Usings | Implicit usings enabled; add explicit usings for non-implicit types |
| Types | `var` where the type is apparent |
| Fields | `_camelCase` for private instance fields; `static readonly` for shared state |
| Constants | `PascalCase` for constants used across members, `UPPER_SNAKE` is not used |
| Braces | Allman style; braces even for single statements |
| Comments | Only where they add value; prefer clear names and XML docs |
| Public API | XML documentation comments on classes, constructors, methods, and properties |
| Async | Async methods end in `Async`; fire-and-forget must be deliberate and documented |
| Exceptions | Catch at boundaries; log with Serilog rather than `Console.WriteLine` in services |

The `.editorconfig` disables three analyzer rules (`MA0004`, `MA0051`, `MA0015`); everything else
should build cleanly with the enabled Meziantou and Roslynator analyzers.

### Safety rules for parser changes

- Keep all ISO stream access inside `IsoSt` so it stays serialized by the stream lock.
- Preserve cycle detection and iteration limits in `VfsContainer` traversals.
- Treat offsets and lengths from the image as untrusted input; validate against the stream length.
- Never introduce a write path; the volume is read-only by design.

---

## Tests

- Add or update xUnit tests for every behavioral change.
- Follow the naming convention `MethodOrFeature_Scenario_ExpectedResult`.
- Tests must not require Dokan, network access, admin rights, or a real ISO.
- Restore global state (current directory, temp files) in `finally` blocks.

Run before submitting:

```shell
dotnet build CSharp_SimpleXisoDrive.sln -c Release
dotnet test CSharp_SimpleXisoDrive.sln -c Release
```

---

## Pull request checklist

- [ ] The solution builds in Release with no new warnings.
- [ ] `dotnet test` passes.
- [ ] Public API changes have XML documentation.
- [ ] New behavior is covered by tests.
- [ ] Documentation in `docs/` is updated when user-visible behavior changes.
- [ ] The version is bumped in both `.csproj` files only when preparing a release.
- [ ] No secrets, personal paths, or unrelated formatting changes are included.

Use clear commit messages that describe the change, for example:

```text
Add XGD2 partition offset probing
Fix directory traversal aborting on empty names
```

---

## Documentation contributions

The pages in `docs/` are written in Markdown and mirror the GitHub wiki. When editing:

- keep links relative (`[Installation](Installation)`);
- update `_Sidebar.md` when adding a page;
- prefer precise, verifiable statements over marketing language;
- include exact error messages and file paths where relevant.

---

## License and contributions

SimpleXisoDrive is licensed under **GPL-3.0**. By submitting a contribution, you agree that it is
your own work and that it may be distributed under the same license.

---

## Code of conduct

Be respectful and constructive. Reports and reviews should focus on the technical content. Harassment
or abuse in issues or pull requests will not be tolerated.
