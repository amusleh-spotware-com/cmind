---
description: "Helm chart: deploy/helm/cmind. نشر الويب و MCP وعقد التسجيل الذاتي والإضافة اختياري Postgres."
---

# نشر Kubernetes - خطوة بخطوة

Helm chart: `deploy/helm/cmind`. نشر الويب و MCP وعقد التسجيل الذاتي والإضافة اختياري Postgres.

> **التحقق** end-to-end على كتلة `kind` المحلية: جميع الكبسولات تصل إلى `Ready`، وكيل العقدة التسجيل الذاتي مع اسم DNS بدون رأس لكل pod، `/health` + `/version` إرجاع 200، وكيل مصغر تلقائي ميل غير قابل للوصول. التدفق أدناه = ما تم اختباره.

## 0. المتطلبات الأساسية

- كتلة Kubernetes (مُدارة EKS/AKS/GKE، أو محلية `kind`/`k3d`/`minikube`).
- `kubectl` (يشير إلى السياق المستهدف) و `helm` 3.
- تسجيل حاوية يمكن للكتلة سحبها من (تخطي محلي `kind` - تحميل الصور بدلاً من ذلك).

## 1. بناء الصور الثلاث

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

دفع (`docker push <registry>/cmind-web:1.0.0`، إلخ)، **أو** لكتلة `kind` المحلية تحميل مباشر:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. اختر الأسرار

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 حرف؛ سر الكتلة المشترك لاكتشاف العقدة التلقائي
```

## 3. تثبيت الرسم البياني

بناءً على السجل (مجموعة مُدارة):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

محلية `kind` (صور محملة، بدون Postgres خارجي، وكلاء غير امتيازات):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> على `kind`/containerd لا Docker socket مضيف، لذا `web.dockerSocket.enabled=false` (منشئ في التطبيق/LocalNode غير متاح) و `nodeAgent.privileged=false` (الوكيل لا يزال **التسجيل الذاتي**؛ فقط لا يمكن تشغيل حاويات cTrader بدون DinD). بالنسبة لتنفيذ العمل الحقيقي، قم بتشغيل الوكلاء على مجموعة عقد حيث `nodeAgent.privileged=true` المسموح بها.

لا ثنائي `helm`؟ اعرض وطبق:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. انتظر النشر

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

التوقع: `cmind-web`، `cmind-mcp`، `cmind-postgres` (النشرات) و `cmind-node-agent-0` (StatefulSet) الكل `Ready`. جاهزية الويب (`/health`) تمر فقط بمجرد ترحيل قاعدة البيانات (الهجرات تعمل عند بدء التشغيل).

## 5. التحقق من الاكتشاف التلقائي

```bash
# يجب أن يظهر وكيل العقدة في قاعدة البيانات مع اسم DNS بدون رأس لكل pod و IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

مثال (التحقق منه):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

قدرة التوسع عن طريق إضافة نسخ - كل pod جديد يسجل ذاتياً في غضون فترة نبض واحدة:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

المصالحة الركود (التحقق منها): تقليل وكيل، يتحول إلى `IsReachable=f` بعد `discovery.heartbeatTtl`؛ مرة أخرى حتى، يعود على الإنترنت.

## 6. الوصول إلى UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080 - تسجيل الدخول مع صاحب محبطة
```

وصول خارجي: ضع `web.ingress.enabled=true`، `web.ingress.host`، و TLS.

## لماذا وكلاء العقد هي StatefulSet

التوزيع الرئيسي يعمل إلى **محددة** الوكيل بواسطة URL، لذا كل وكيل يحتاج مستقر، اسم DNS يمكن معالجته بشكل فردي. الرسم البياني يستخدم StatefulSet + Headless Service؛ كل pod يعلن `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` والتسجيل الذاتي تحت اسم pod. نفس آلية الاكتشاف التي تستخدم عقد cTrader CLI العارية - انظر [../operations/node-discovery.md](../operations/node-discovery.md).

## مستوى عرض الويب (SignalR backplane، S6)

تطبيق الويب = Blazor Server + SignalR (لوحة تحكم حية، سجلات hub). للتشغيل **أكثر من نسخة ويب واحدة**، قم بتعيين سلسلة اتصال `signalr` لنقطة نهاية Redis - يسجل التطبيق بعد ذلك **SignalR Redis backplane** (`AddStackExchangeRedis`) حتى رسائل hub ومفاوضات الدوائر팬 عبر النسخ واعادة الاتصال على pod مختلفة تبقى حي. لا سلسلة اتصال `signalr` = نسخة واحدة في الذاكرة (دون تغيير). الزوج مع تقاربية الجلسة في ingress لأسلس Blazor Server الدوائر.

## نسخ-وكيل autoscaling والمرونة

وكيل النسخ يستضيف مقابس التداول التي تعيش لفترة طويلة، لذا يتوسع على **العمل وليس CPU**. مع `copyAgent.keda.enabled=true` الرسم البياني يثبت KEDA `ScaledObject` الذي يستعلم Postgres عن عدد الملف الشخصي النسخ المتكرر ويتدرج النسخ بحيث يستضيف كل pod حول `copyAgent.keda.profilesPerPod` (default 25)، بين `minReplicas`/`maxReplicas`. KEDA تقرأ قاعدة البيانات عبر `TriggerAuthentication` المرتبطة بـ `copyAgent.keda.connectionSecretKey` مفتاح سري. عندما `copyAgent.replicas > 1` (أو KEDA مقياس بعد 1) الرسم البياني يضيف أيضاً `topologySpreadConstraints` (انتشار عبر العقد) و `PodDisruptionBudget` (`minAvailable: 1`)؛ على مقياس في / الترقية المتداول كل pod يحرر العقود على `SIGTERM` (`terminationGracePeriodSeconds`، default 30) حتى الناجي يستعيد على الفور - انظر [scaling.md](scaling.md).

## القيم الرئيسية

| القيمة | الغرض |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | إحداثيات الصورة (`local` + `Never` لـ kind). |
| `secrets.existingSecret` | استخدم Secret خارجي/مختوم بدلاً من قيم يدير الرسم البياني. |
| `postgres.enabled` | `true` = Postgres في الكتلة (dev). `false` + `externalDatabase.connectionString` لمُدار قاعدة بيانات (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS، HPA على CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | عدد الوكلاء، امتياز DinD، الوضع، القدرة. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` لمنشئ ويب/LocalNode (عقد وقت تشغيل Docker فقط). |
| `observability.otlpEndpoint` | شحن السجلات+traces+metrics لمجمع OTLP. |

## المسبارات

liveness `/alive`، جاهزية `/health` (الويب) · `/version` (MCP) · `/health` (الوكيل) — خريطة في جميع البيئات.

## مجموعة اختبار داخل الكتلة

قم بتشغيل مجموعة نسخ التداول كـ Kubernetes `Job` ضد تطبيق مُنتشر، حتى يتم التقاط الانحدار داخل الكتلة بنفس طريقة محليًا. اختبارات النسخ تحتاج فقط إلى الويب + Postgres + ذاكرة التخزين المؤقت للرمز - **لا** وكلاء عقد امتيازات.

لقطة واحدة قابلة للتكرار (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # مجموعة نسخ حتمية (بدون أسرار)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # إعادة استخدام السياق kube الحالي
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # حي
```

اليد اليسرى / CI wiring — **حتمي (الافتراضي، بدون أسرار):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # صورة runner (SDK + مشاريع الاختبار المبنية)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**مجموعة حية** بالإضافة تحتاج ذاكرة التخزين المؤقت للرمز. cTrader **تحديث الرموز استخدام واحد**، حتى لا بد أن تكون الذاكرة **قابلة للكتابة**: Job نسخ Secret إلى emptyDir على `/app/secrets` عبر init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # أبداً خبز في الصورة
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| القيمة | الغرض |
|-------|---------|
| `tests.enabled` | عرض الاختبار `Job` (default `false`). |
| `tests.project` / `tests.filter` | أي مشروع + `dotnet test --filter` للتشغيل (الافتراضي: حتمي). |
| `tests.copySecret` | Secret اختياري مع تجاهل `.gitignore` `openapi-*.local.json`؛ نسخ إلى **قابل للكتابة** emptyDir على `/app/secrets` لمجموعة حية. فارغ ⇒ بدون جبل سري. |
| `tests.backoffLimit` | Job retry count (default `0`). |

`LiveCopySecrets` يسير حتى من `/app` للعثور على `secrets/`؛ اختبارات حي تخطي بنظافة عندما ذاكرة التخزين المؤقت غير موجودة. `Dockerfile.tests` SDK-based لذا يشغل نفس التأكيدات كـ محلي `dotnet test` — كلاهما حتمي (`101 passed`) ومجموعات حية كاملة (`8 passed`) التحقق من التشغيل داخل هذه الصورة محليًا ضد Docker قبل الشحن.

## الهدم

```bash
helm -n cmind uninstall cmind        # أو: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # محلي فقط
```

## تشغيل مجموعة داخل الكتلة متعددة الأنظمة (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` نظام تشغيل مستقل. تحويل مسار repo إلى نموذج أصلي (`cygpath -m`) بحيث Docker و helm و kubectl حل عليها على **Windows/git-bash** وكذلك Linux/macOS — التحقق نهاية إلى نهاية على Windows (kind cluster up → صور مبنية+محملة → رسم بياني مُنتشر → اختبار داخل الكتلة Job أخضر → tear down).

| البيئة | الأمر |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **أو** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (مفضل)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Prefer WSL على Windows.** تشغيل داخل WSL استخدام المسارات Linux الأصلية و Docker Desktop's WSL integration، تجنب جميع حالات الحافة ترجمة المسار - الخيار الأكثر قوة. يحتاج `docker`، `kind`، `helm`، `kubectl` و .NET SDK على WSL PATH (Docker Desktop يوفر `docker`؛ تثبيت الباقي في distro، على سبيل المثال `go install sigs.k8s.io/kind@latest`، the helm/kubectl release binaries). `scripts/k8s-e2e.ps1` غلاف يختار WSL مع `-Wsl`، يتراجع إلى git-bash خلاف ذلك.

`kind` + `helm` القابلة للتثبيت الذاتي إذا غاب (release binaries أو `choco install kind kubernetes-helm`); لا تعامل كـ غير متاح. انظر أيضاً [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
