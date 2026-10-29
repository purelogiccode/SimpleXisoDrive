# Security Policy

## Supported versions

Security fixes are provided for the latest release. Older releases are not supported; please update
before reporting an issue.

| Version | Supported |
| --- | --- |
| Latest release | :white_check_mark: |
| Older releases | :x: |

## Reporting a vulnerability

Please **do not** report security issues in public GitHub issues, discussions, or pull requests.

Use GitHub's private vulnerability reporting:

1. Open the [Security tab](https://github.com/purelogiccode/SimpleXisoDrive/security).
2. Click **Report a vulnerability** (or use
   [this form](https://github.com/purelogiccode/SimpleXisoDrive/security/advisories/new)).
3. Describe the issue, including the affected version, platform, reproduction steps, and impact.

You will receive a response as soon as possible, usually within a few business days. Once the report
is confirmed, a fix will be prepared and a security advisory published. Credit will be given in the
advisory and release notes unless you ask to stay anonymous.

## Scope

In scope:

- The application and its libraries in this repository (`SimpleXisoDrive`, `SimpleXisoDrive.Core`,
  `SimpleXisoDrive.Unix`, and `FuseSharp`).
- Parsing of XISO, CISO, CHD, and ZArchive images, and the virtual filesystem/mount layer.
- The update checker, bug-report upload, and other network-facing features.

Out of scope:

- Vulnerabilities in third-party components such as Dokan, FUSE 3/macFUSE, or the .NET runtime.
  Please report those to the respective projects.
- Issues already tracked by Dependabot or in a published dependency advisory.
- Attacks that require physical access to the machine or an already-compromised system.

## Dependency updates

Dependabot alerts, Dependabot security updates, code scanning, secret scanning, and push protection
are enabled for this repository, so known vulnerable dependencies and committed secrets are reported
automatically.
