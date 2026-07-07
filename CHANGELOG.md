# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- MIT `LICENSE`, `CONTRIBUTING.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`.
- GitHub issue/PR templates, CI (build + test), CodeQL analysis, and Dependabot config.
- `.dockerignore`.
- Security headers middleware (`X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy`) on the Web app.
- Rate limiting on authentication endpoints (login brute-force mitigation).

### Changed

- Hardened the auth cookie (`HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always`).
- OpenAPI document is now exposed only in the Development environment.

<!-- Link references -->
[Unreleased]: https://github.com/<owner>/cMind/commits/main
