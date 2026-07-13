---
description: "Backtest Integrity Lab — deterministic, fund-grade overfitting statistics (Probabilistic & Deflated Sharpe, t-stat) that turn a raw backtest into a Robust / Fragile / Overfit verdict, correcting for how many configurations you tried."
---

# Backtest Integrity Lab

Retail platforms แสดง backtest's Sharpe หรือ net profit และหยุดที่นั่น Institutions ไม่มีวันไว้วางใจ
raw backtest — พวกเขาถามว่า ผลลัพธ์ยังคงอยู่ **correction สำหรับ selection bias และ number ของ
configurations ที่พยายาม** Backtest Integrity Lab นำการตรวจสอบนั้น ไป cMind มันเป็น **deterministic
math** (ไม่มี AI ไม่มี external calls) ดังนั้น verdict เป็น reproducible และ ทุกตัวเลข explainable

เปิดที่ **cBots → Integrity** (`/quant/integrity`)

## What it computes

Given return series (หรือ equity/balance curve) และ number ของ parameter sets ที่คุณพยายาม arrive
ที่มัน analyzer รายงาน:

- **Sharpe ratio** — per-period และ annualized (square-root-of-time)
- **Probabilistic Sharpe Ratio (PSR)** — confidence ที่ *true* Sharpe beats benchmark
  accounting สำหรับ track-record length skewness และ kurtosis (Bailey & López de Prado 2012) short หรือ
  fat-tailed record ลด มัน
- **Deflated Sharpe Ratio (DSR)** — PSR วัด against a **deflated benchmark**: Sharpe ที่คุณคาดหวัง
  จาก *best ของ N random trials* ภายใต้ null (the False Strategy Theorem) more
  configurations ที่คุณพยายาม higher bar — นี่คือสิ่งที่ catches overfitting
- **t-statistic** ของ mean return ตาม Harvey Liu & Zhu genuine edge ควร clear **t ≥ 3.0**
  ไม่ใช่ textbook 2.0
- **Skewness / kurtosis** ของ returns ซึ่ง feed PSR/DSR corrections

## The verdict

| Verdict | Meaning | Rule |
|---|---|---|
| **Robust** | edge ยังคงอยู่ trials ที่คุณเรียกใช้ | DSR ≥ 95% **และ** PSR ≥ 95% **และ** \|t\| ≥ 3.0 |
| **Fragile** | Statistically alive แต่ไม่ convincingly so — อย่า size up on this alone | between the two |
| **Overfit** | Most likely artifact ของ selection bias ไม่ใช่ real edge | DSR < 90% |

ทุก result carries plain-English rationale ดังนั้น "why" ไม่เคยซ่อน

## Probability ของ Backtest Overfitting (ข้าม trials)

Feeding trial *count* ดี; feeding **actual out-of-sample series ของ ทุก configuration ที่คุณ
พยายาม** ดีกว่า Paste พวกเขา ไปยัง optional **trial grid** (one series per line) และ cMind runs
**Combinatorially-Symmetric Cross-Validation** (Bailey Borwein López de Prado & Zhu 2015): มันแยก
observations ไป groups และ สำหรับ ทุกวิธี ของการเลือก half เป็น in-sample มันเลือก in-sample
best configuration และ checks ว่า winner นั้น lands ใน bottom half **out-of-sample** **Probability ของ
Backtest Overfitting (PBO)** fraction ของ splits ที่ winner failed ไป generalize PBO near 0 หมายถึง
best configuration genuinely best; PBO ของ 0.5 หรือ more หมายถึง selection process ของคุณ picking noise —
verdict กลาย **Overfit** regardless ของ how good winner ดู

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

เมื่อ native cTrader Console optimizer lands cMind จะ feed full trial surface ของมัน ที่นี่
อัตโนมัติ

## Trials — the number ที่ matters

`Trials` คือ **how many parameter sets ที่คุณ tested** ก่อน picking this one Testing one strategy และ
testing ten thousand และ keeping best wildly different things: second manufactures high in-sample Sharpe by
chance Feeding honest trial count คือ whole point — มันเพิ่ม deflation และ สามารถย้าย "great" backtest ไป
**Overfit** เมื่อ native cTrader Console optimizer lands cMind feeds sweep ของมัน real grid size
อัตโนมัติ

## Inputs

- **Periodic returns** — one number per period (เช่น `0.01` = +1%) อย่างน้อย two
- **Equity / balance curve** — cMind derives consecutive simple returns สำหรับคุณ
- หรือ run มัน straight on completed backtest: `POST /api/quant/integrity/backtest/{instanceId}` reads
  stored report's equity curve

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Returns verdict ทั้งหมด metrics และ rationale `POST /api/quant/integrity/backtest/{id}` runs
analysis เดียวกัน on completed backtest ที่คุณเป็นเจ้าของ

## Why มันเป็น reliable

statistics เป็น pure functions ใน domain core (`Core.Quant`) ที่มี zero infrastructure
dependencies — พวกเขา ไม่สามารถ ถูกถาย ลง โดย network blip และ พวกเขา pinned โดย golden-vector
unit tests against published formulas normal CDF/inverse เป็น closed-form approximations
(Abramowitz-Stegun / Acklam) ดังนั้น inputs เดียวกัน เสมอ yield verdict เดียวกัน
