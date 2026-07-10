# Security Policy

cMind self-hosts and handles trading credentials, so security matters. As with any
platform that touches live funds, run your own security review and follow the hardening
guidance in [docs/deployment/](docs/deployment/) before production use.

## Reporting a vulnerability

**Do not open a public issue for security vulnerabilities.**

Report privately via [GitHub Security Advisories](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
("Report a vulnerability" on the **Security** tab), or email the maintainer.

Please include:

- A description of the issue and its impact.
- Steps to reproduce or a proof of concept.
- Affected component(s) and version/commit.

You'll get an acknowledgement as soon as possible. Please allow reasonable time for a
fix before public disclosure.

## Supported versions

Only the latest `main` is supported.

## Security model & hardening notes

- **Secrets at rest** are encrypted via ASP.NET Core Data Protection (`ISecretProtector`,
  X.509-protected key ring). Never log or store secrets in plaintext.
- **Passwords** are hashed with Argon2id (Konscious). Login has failed-attempt lockout.
- **External nodes** authenticate the main node with a short-lived per-node HS256 JWT
  (5-min expiry) signed with a per-node shared secret (≥ 32 chars). The agent only runs
  images matching `AllowedImagePrefix` and execs docker via `ArgumentList` (no shell).
- **MCP keys** are `mcpk_<hex>`, SHA-256 hashed at rest, shown once.

### Production checklist

- Terminate **TLS** in front of both the Web app and each `ExternalNode` agent; keep
  agents on a private network.
- Keep every node shared secret ≥ 32 chars; rotate by updating both the node's stored
  secret and the agent's `NodeAgent:JwtSecret`.
- Provide `DataProtectionCertBase64` / `DataProtectionCertPassword` so the key ring is
  encrypted at rest (in dev these may be empty → keys stored unencrypted).
- Ensure node clocks are **NTP-synced** — JWT validation tolerates only 30s of skew.
- Keep health endpoints and OpenAPI behind auth or a private network in production.
