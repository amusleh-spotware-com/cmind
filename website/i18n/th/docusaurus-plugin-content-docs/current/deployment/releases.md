---
description: "GitHub Releases: อิมเมจคอนเทนเนอร์ที่กำหนดเวอร์ชัน (GHCR), Helm chart และไบนารี CtraderCliNode — วิธีรับ release และรันแอปจากมัน"
---

# Releases และการรัน release

cMind ถูกส่งมอบเป็น **GitHub Releases** ที่กำหนดเวอร์ชัน แต่ละ release จะเผยแพร่สิ่งต่อไปนี้สำหรับหนึ่ง SemVer tag:

- **อิมเมจคอนเทนเนอร์** บน GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`
  ติดแท็กด้วยเวอร์ชัน (เช่น `1.0.0-alpha.1`) และ `sha-<commit>` ลงลายเซ็น (cosign keyless) พร้อมการรับรองแหล่งที่มาของบิลด์
  และ SBOM แบบ SPDX
- **Helm chart** — ผลักไปยัง `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` และแนบกับ release ในชื่อ
  `cmind-<version>.tgz`
- **ไบนารี CtraderCliNode** — ZIP แบบ self-contained ต่อแพลตฟอร์ม (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) สำหรับรัน node agent ระยะไกลโดยไม่ต้องมี .NET SDK
- **`SHA256SUMS.txt`** ครอบคลุมทุกอาร์ติแฟกต์ที่แนบมา

> **Alpha** ในตอนนี้ทุก release เป็น pre-release (`-alpha.N`) คาดว่าจะมีการเปลี่ยนแปลงที่ทำให้เข้ากันไม่ได้ระหว่าง alpha
> ยังไม่มีการรับประกันการอัปเกรด/การย้ายข้อมูล ให้ปักหมุดเวอร์ชันที่แน่นอน — อย่าใช้ `latest`

## การกำหนดเวอร์ชัน

SemVer 2.0.0 รูปแบบแท็ก `vX.Y.Z[-suffix]` ส่วนต่อท้าย (`-alpha.N`, `-beta.N`, `-rc.N`) จะเผยแพร่ **pre-release** ของ
GitHub; แท็กอิมเมจและเวอร์ชันของ Helm chart ทั้งคู่เท่ากับเวอร์ชันโดยไม่มี `v` นำหน้า แอปที่กำลังทำงานจะแสดงค่านี้ที่
`GET /version` และในส่วนท้ายของ UI (`Core.VersionInfo`)

## เลือก release

เรียกดู **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** และคัดลอกแท็กที่ต้องการ (เช่น
`v1.0.0-alpha.1`) ตรวจสอบอิมเมจก่อนรัน:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## รัน — Kubernetes (Helm, แนะนำ)

`appVersion` ของ chart ปักหมุดแท็กอิมเมจที่ตรงกันไว้แล้ว ดังนั้นคุณส่งเพียงเวอร์ชันของ chart เท่านั้น

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<secret ของคลัสเตอร์ 32+ อักขระ>'
```

แพ็กเกจ GHCR แบบส่วนตัวต้องใช้ image pull secret — สร้างหนึ่งอันแล้วส่งเข้าไป:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-ที่มี-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

ตัวเลือก chart ทั้งหมด, ingress, Postgres ภายนอก และการปรับขนาด: ดู
**[การปรับใช้ Kubernetes](kubernetes.md)** และ **[การปรับขนาด](scaling.md)** ตรวจสอบ:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version คืนค่าเวอร์ชันของ release
```

## รัน — Docker (โฮสต์เดียว, ดูอย่างรวดเร็ว)

รันโฮสต์ Web โดยตรงจากอิมเมจ release ของมัน ต้องใช้ Postgres และ Docker socket (โฮสต์ Web สร้าง/รัน cBot ผ่าน
Docker CLI ในเครื่อง)

```bash
VERSION=1.0.0-alpha.1
docker network create cmind

docker run -d --name cmind-pg --network cmind \
  -e POSTGRES_PASSWORD=change-me -e POSTGRES_DB=cmind postgres:17

docker run -d --name cmind-web --network cmind -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e ConnectionStrings__Default='Host=cmind-pg;Database=cmind;Username=postgres;Password=change-me' \
  -e App__Owner__Email='owner@example.com' \
  -e App__Owner__Password='Change-Me-Str0ng!' \
  ghcr.io/amusleh-spotware-com/cmind-web:$VERSION
```

เปิด `http://localhost:8080` เพิ่มเซิร์ฟเวอร์ MCP (`cmind-mcp`) และ node agent ด้วยวิธีเดียวกัน; สำหรับโทโพโลยีแบบหลาย
บริการเต็มรูปแบบให้ใช้ Helm chart ดู **[การพัฒนาในเครื่อง](local.md)** สำหรับเส้นทาง Aspire `dotnet run` เมื่อทำงานจาก
ซอร์สโค้ดแทนที่จะเป็น release

## รัน node agent ระยะไกลจากไบนารี

โฮสต์ระยะไกลที่ให้ความจุสำหรับการรัน/backtest สามารถรัน `CtraderCliNode` ได้โดยไม่ต้องติดตั้ง .NET ดาวน์โหลด ZIP ของ
แพลตฟอร์มจาก release แตกไฟล์ และรัน — มันจะลงทะเบียนตัวเองกับโฮสต์ Web และส่ง heartbeat

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<โฮสต์-web-ของคุณ>' \
NodeAgent__DiscoveryJoinToken='<secret ของคลัสเตอร์ 32+ อักขระ อันเดียวกัน>' \
./CtraderCliNode
```

โฮสต์ต้องรัน Docker (agent เรียกใช้อิมเมจคอนโซล cTrader ผ่าน Docker CLI) ดู
**[การปรับใช้ Kubernetes](kubernetes.md)** สำหรับการรัน node agent เป็น pod แบบ privileged

## การสร้าง release (ผู้ดูแล)

Release ถูกสร้างโดย `.github/workflows/release.yml` เมื่อมีการ push แท็ก `v*` ใด ๆ — กระบวนการอยู่ใน
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** ที่รากของ repo
