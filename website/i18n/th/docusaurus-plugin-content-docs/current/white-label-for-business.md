---
slug: /white-label-for-business
title: White-label สำหรับธุรกิจ
description: Ship cMind เป็น branded product ของคุณเอง — สำหรับ prop firms brokers และ copy-trading businesses Rebrand surface ทั้งหมด ผ่าน config ไม่มี code changes
sidebar_position: 4
---

# White-label cMind สำหรับธุรกิจของคุณ 🏢

Run prop firm desk ของ broker หรือ copy-trading service cMind ถูก built จาก day เดียว เพื่อ **resold เป็น product ของคุณเอง** ทุก ๆ surface — name logo favicon colors แม้กระทั่ง installable phone app — bends เป็น brand ของคุณ Customers ของคุณเห็น *company ของคุณ* ไม่มี code changes ไม่มี fork เพียง config

:::tip TL;DR
Point `App:Branding` บน name colors และ logo ของคุณ Restart Done technical reference full อยู่ใน [White-label feature doc](./features/white-label.md)
:::

## สิ่งที่คุณสามารถ rebrand

| Surface | สิ่งที่เปลี่ยน |
|---|---|
| **Product name** | App bar text + browser tab title |
| **Logo & favicon** | Marks ของคุณทั่ว รวมถึง browser tab |
| **Colors** | Full palette — primary surfaces status colors — flows ผ่าน UI ทั้งหมด *และ* app CSS ของเขาเอง ผ่าน design tokens |
| **Installable app (PWA)** | add-to-home-screen name icon และ splash use brand ของคุณ |
| **Meta / SEO** | Description และ support URL คือ yours |
| **Custom CSS** | Inject polish ของคุณเอง สำหรับ last 5% |

ทุก ๆ อย่าง defaults ไป stock cMind identity ดังนั้นคุณเพียง override what คุณสนใจ

## 60-second rebrand

ตั้งค่าเหล่านี้บน deployment ของคุณ (JSON config หรือ environment variables):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Environment-variable form: `App__Branding__ProductName=AcmeFX` Colors validated ที่ startup — bad hex value fails boot ด้วย clear message แทน rendering broken page Nice และ loud ถูกต้อง when คุณต้อง

## "Powered by cMind" link

โดย **default** dashboard แสดง small tasteful **"Powered by cMind"** link ที่ points visitors กลับเป็น site นี้ มัน on โดย default เพราะเรา proud ของ project และ มันช่วย other traders หา — แต่มัน **your call**

- **Keep มัน** (default): subtle credit link บน dashboard Costs คุณไม่มี ช่วย project
- **Hide มัน**: ตั้ง `App__Branding__ShowSiteLink=false` และ มันหายไปทั้งหมด — perfect สำหรับ fully white-labeled deployment where product unmistakably *yours*

ดู [White-label feature doc](./features/white-label.md#powered-by-link) สำหรับ ตรงไหน exactly render

## Multi-tenant per-customer branding

เพราะ branding เป็นเพียง deployment config tenant ทุก ๆ deployment สามารถ carry identity ของเขาเอง Run instance แยก per customer หรือ drive branding from control plane ของคุณเอง — app อ่าน มันจาก `IOptionsMonitor` ดังนั้นมัน even สามารถ rebuild theme live when options เปลี่ยน

Pair นี้ด้วย:

- **[Feature toggles](./features/feature-toggles.md)** — decide capabilities ใด tenant แต่ละเห็น
- **[Prop-firm rules](./features/prop-firm.md)** — enforce challenge rules ของคุณ ด้วย live equity tracking
- **[Performance fees](./features/copy-performance-fees.md)** + **[provider marketplace](./features/copy-provider-marketplace.md)** — monetize copy trading
- **[Compliance](./features/compliance.md)** — keep audit trail regulator ของคุณ จะ ask สำหรับ

## Assets & hosting

Drop logo/favicon ของคุณ เป็น Web app `wwwroot/branding/` (หรือ point `LogoUrl`/`FaviconUrl` ที่ absolute URL ใด ๆ) Deploy however suits คุณ — [Docker](./deployment/local.md) [Kubernetes](./deployment/kubernetes.md) [Azure](./deployment/cloud-azure.md) หรือ [AWS](./deployment/cloud-aws.md)

พร้อม ทำให้มันเป็นของคุณ เริ่มต้น ด้วย [technical white-label reference →](./features/white-label.md)
