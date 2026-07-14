---
description: "إصدارات GitHub: صور حاويات مُصدَّرة بإصدارات (GHCR)، ومخطط Helm، وثنائيات CtraderCliNode — كيفية الحصول على إصدار وتشغيل التطبيق منه."
---

# الإصدارات وتشغيل إصدار

يُشحَن cMind على هيئة **إصدارات GitHub** مُرقَّمة بإصدارات. ينشر كل إصدار، لوسم SemVer واحد:

- **صور الحاويات** على GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`،
  موسومة بالإصدار (مثل `1.0.0-alpha.1`) و`sha-<commit>`. موقَّعة (cosign keyless) مع إثباتات مصدر البناء
  وملف SBOM بصيغة SPDX.
- **مخطط Helm** — مدفوع إلى `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` ومُرفَق بالإصدار
  باسم `cmind-<version>.tgz`.
- **ثنائيات CtraderCliNode** — ملفات ZIP قائمة بذاتها لكل منصة (`linux-x64`، `linux-arm64`، `win-x64`،
  `osx-arm64`) لتشغيل وكيل عقدة بعيد دون حزمة .NET SDK.
- **`SHA256SUMS.txt`** يغطي كل عنصر مُرفَق.

> **ألفا.** كل إصدار حاليًا هو إصدار مسبق (`-alpha.N`). توقّع تغييرات كاسرة بين إصدارات ألفا؛ لا يوجد بعد ضمان
> للترقية/الترحيل. ثبّت إصدارًا محددًا — لا تستخدم `latest` أبدًا.

## ترقيم الإصدارات

SemVer 2.0.0. صيغة الوسم `vX.Y.Z[-suffix]`. اللاحقة (`-alpha.N`، `-beta.N`، `-rc.N`) تنشر **إصدارًا مسبقًا**
على GitHub؛ ووسم الصورة وإصدار مخطط Helm كلاهما يساوي الإصدار دون `v` البادئة. يعرضه التطبيق قيد التشغيل عبر
`GET /version` وفي تذييل الواجهة (`Core.VersionInfo`).

## اختيار إصدار

تصفّح **[الإصدارات](https://github.com/amusleh-spotware-com/cmind/releases)** وانسخ الوسم المطلوب (مثل
`v1.0.0-alpha.1`). تحقّق من الصورة قبل تشغيلها:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## التشغيل — Kubernetes (Helm، موصى به)

يثبّت `appVersion` الخاص بالمخطط وسم الصورة المطابق مسبقًا، لذا تمرّر فقط إصدار المخطط.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<سر عنقود 32 حرفًا أو أكثر>'
```

تحتاج حزم GHCR الخاصة إلى سرّ سحب الصورة — أنشئ واحدًا ومرّره:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-بصلاحية-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

خيارات المخطط الكاملة وIngress وPostgres الخارجي والتوسيع: راجع **[نشر Kubernetes](kubernetes.md)**
و**[التوسيع](scaling.md)**. التحقق:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ؛ GET /version يعيد إصدار الإصدار
```

## التشغيل — Docker (مضيف واحد، نظرة سريعة)

شغّل مضيف الويب مباشرة من صورة إصداره. يحتاج إلى Postgres ومقبس Docker (يبني/يشغّل مضيف الويب روبوتات cBot عبر
واجهة Docker CLI المحلية).

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

افتح `http://localhost:8080`. أضِف خادم MCP (`cmind-mcp`) ووكلاء العُقد بالطريقة نفسها؛ لطوبولوجيا الخدمات
المتعددة الكاملة استخدم مخطط Helm. راجع **[التطوير المحلي](local.md)** لمسار Aspire `dotnet run` عند العمل من
المصدر بدلًا من إصدار.

## تشغيل وكيل عقدة بعيد من ثنائي

يمكن للمضيفات البعيدة التي توفّر سعة تشغيل/اختبار رجعي تشغيل `CtraderCliNode` دون تثبيت .NET. نزّل ملف ZIP
الخاص بالمنصة من الإصدار، وفكّ ضغطه، وشغّله — يسجّل نفسه تلقائيًا لدى مضيف الويب ويرسل نبضات.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<مضيف-الويب-الخاص-بك>' \
NodeAgent__DiscoveryJoinToken='<نفس سر العنقود 32 حرفًا أو أكثر>' \
./CtraderCliNode
```

يجب أن يشغّل المضيف Docker (ينفّذ الوكيل صورة وحدة تحكم cTrader عبر واجهة Docker CLI). راجع
**[نشر Kubernetes](kubernetes.md)** لتشغيل وكلاء العُقد كـ pods ذات امتياز.

## إنشاء إصدار (القائمون على الصيانة)

تُنتَج الإصدارات بواسطة `.github/workflows/release.yml` عند دفع أي وسم `v*` — العملية موثّقة في
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** في جذر المستودع.
