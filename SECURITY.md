# Security Policy

cMind self-hosts, handles trading credentials — security matters. Like any platform touching live funds, run own security review, follow hardening guidance in [website/docs/deployment/](website/docs/deployment/) before production.

## Reporting a vulnerability

**Do not open a public issue for security vulnerabilities.**

Report privately via [GitHub Security Advisories](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
("Report a vulnerability" on **Security** tab), or email maintainer.

Include:

- Issue description + impact.
- Repro steps or proof of concept.
- Affected component(s) and version/commit.

Get acknowledgement soon as possible. Allow reasonable time for fix before public disclosure.

## Supported versions

Only latest `main` supported.

## Security model & hardening notes

- **Secrets at rest** encrypted via ASP.NET Core Data Protection (`ISecretProtector`,
  X.509-protected key ring). Never log or store secrets plaintext.
- **Passwords** hashed with Argon2id (Konscious). Login has failed-attempt lockout.
- **External nodes** authenticate main node with short-lived per-node HS256 JWT
  (5-min expiry) signed with per-node shared secret (≥ 32 chars). Agent only runs
  images matching `AllowedImagePrefix`, execs docker via `ArgumentList` (no shell).
- **MCP keys** are `mcpk_<hex>`, SHA-256 hashed at rest, shown once.

### Production checklist

- Terminate **TLS** in front of both Web app and each `ExternalNode` agent; keep
  agents on private network.
- Keep every node shared secret ≥ 32 chars; rotate by updating both node's stored
  secret and agent's `NodeAgent:JwtSecret`.
- Provide `DataProtectionCertBase64` / `DataProtectionCertPassword` so key ring
  encrypted at rest (in dev these may be empty → keys stored unencrypted).
- Ensure node clocks **NTP-synced** — JWT validation tolerates only 30s skew.
- Keep health endpoints and OpenAPI behind auth or private network in production.