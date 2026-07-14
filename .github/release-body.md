> **Pre-release (alpha).** Breaking changes may land between alphas with no upgrade guarantee. Pin an exact version — do not use `latest`.

### Artifacts

- **Images** (GHCR, signed + provenance + SBOM): `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`
- **Helm chart**: `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` (also attached as `.tgz`)
- **CtraderCliNode binaries**: self-contained zips (`linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`)
- **`SHA256SUMS.txt`**: checksums for every attached asset

### Run it

Install with Helm (chart `appVersion` pins the matching image tag):

```bash
helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version <this-version> --namespace cmind --create-namespace
```

Full instructions — Docker single-host, remote node binaries, image signature verification — in the
[Releases & running a release](https://amusleh-spotware-com.github.io/cmind/docs/deployment/releases) docs.

---
