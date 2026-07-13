---
description: "Backtest Integrity Lab — deterministic, fund-grade overfitting statistics (Probabilistic & Deflated Sharpe, t-stat) biến một raw backtest thành Robust / Fragile / Overfit verdict, điều chỉnh cho việc bạn đã thử bao nhiêu cấu hình."
---

# Backtest Integrity Lab

Các platform bán lẻ hiển thị Sharpe hoặc net profit của backtest và dừng lại ở đó. Các tổ chức không bao giờ tin tưởng một
raw backtest — họ hỏi liệu kết quả có tồn tại **điều chỉnh cho selection bias và số lượng
cấu hình đã thử**. Backtest Integrity Lab mang kiểm tra đó đến cMind. Nó là **deterministic
math** (không AI, không external calls), vì vậy verdict có thể reproduce và mọi số đều có thể giải thích.

Mở nó tại **cBots → Integrity** (`/quant/integrity`).

## Nó tính toán gì

Cho một return series (hoặc một equity/balance curve) và số parameter sets bạn đã thử để đạt được
nó, analyzer reports:

- **Sharpe ratio** — per-period và annualized (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — confidence rằng *true* Sharpe beat benchmark,
  tính đến track-record length, skewness và kurtosis (Bailey & López de Prado, 2012). Một record ngắn hoặc
  fat-tailed làm giảm nó.
- **Deflated Sharpe Ratio (DSR)** — PSR measured against a **deflated benchmark**: Sharpe bạn sẽ
  expect từ *best of N random trials* under null (False Strategy Theorem). Càng nhiều
  cấu hình bạn thử, thanh bar càng cao — đây là thứ bắt được overfitting.
- **t-statistic** của mean return. Theo Harvey, Liu & Zhu, một genuine edge nên clear **t ≥ 3.0**,
  không phải textbook 2.0.
- **Skewness / kurtosis** của returns, được feed vào PSR/DSR corrections.

## The verdict

| Verdict | Meaning | Rule |
|---|---|---|
| **Robust** | Edge tồn tại qua các trials bạn đã chạy. | DSR ≥ 95% **and** PSR ≥ 95% **and** \|t\| ≥ 3.0 |
| **Fragile** | Statistically alive nhưng không thuyết phục — không nên size up trên cái này một mình. | between the two |
| **Overfit** | Rất có thể là artifact của selection bias, không phải real edge. | DSR < 90% |

Mỗi result mang một plain-English rationale để "why" không bao giờ bị ẩn.

## Probability of Backtest Overfitting (across trials)

Feeding a trial *count* tốt; feeding **actual out-of-sample series của mọi cấu hình bạn
thử** tốt hơn. Paste chúng vào optional **trial grid** (một series per line) và cMind chạy
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): nó splits
observations thành groups, và for every way of choosing half as in-sample nó pick in-sample
best configuration và checks liệu winner đó land in bottom half **out-of-sample**.
**Probability of Backtest Overfitting (PBO)** là fraction của splits nơi winner failed to
generalize. PBO gần 0 có nghĩa best configuration là genuinely best; PBO của 0.5 hoặc hơn có nghĩa your
selection process đang pick noise — verdict trở thành **Overfit** bất kể winner trông tốt như thế nào.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Khi native cTrader Console optimizer hạ cánh, cMind sẽ feed surface trial đầy đủ vào đây
tự động.

## Trials — số đó quan trọng

`Trials` là **số parameter sets bạn đã test** trước khi pick cái này. Testing một strategy và
testing mười nghìn và giữ best là những thứ hoàn toàn khác nhau: cái thứ hai manufacture một
high in-sample Sharpe by chance. Feeding honest trial count là cả điểm — nó raises the
deflation và có thể move một "great" backtest thành **Overfit**. Khi native cTrader Console optimizer
hạ cánh, cMind feed nó real grid size tự động.

## Inputs

- **Periodic returns** — một số per period (ví dụ `0.01` = +1%). Ít nhất hai.
- **Equity / balance curve** — cMind derives consecutive simple returns cho bạn.
- Hoặc chạy nó thẳng trên một completed backtest: `POST /api/quant/integrity/backtest/{instanceId}` reads the
  stored report's equity curve.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Returns verdict, all metrics, và rationale. `POST /api/quant/integrity/backtest/{id}` runs same
analysis on a completed backtest bạn sở hữu.

## Tại sao nó đáng tin cậy

Statistics là pure functions trong domain core (`Core.Quant`) với zero infrastructure
dependencies — chúng không thể bị kéo xuống bởi network blip, và chúng được pin bởi golden-vector unit
tests đối với published formulas. Normal CDF/inverse là closed-form approximations
(Abramowitz-Stegun / Acklam), vì vậy same inputs luôn yield same verdict.
