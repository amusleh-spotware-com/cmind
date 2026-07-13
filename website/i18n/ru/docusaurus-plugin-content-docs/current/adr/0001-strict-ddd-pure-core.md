---
title: 0001 — Строгий DDD с чистым Core
description: Почему логика домена живет на aggregates в проекте Core с нулевыми зависимостями инфраструктуры.
---

# 0001 — Строгий DDD с чистым `Core`

## Контекст

Это приложение перемещает реальные деньги. Правила бизнеса, разбросанные по endpoints, background services и Razor компонентам, разлагаются в untestable, непоследовательное поведение — ровно там, где ошибка стоит пользователю капитала.

## Решение

Логика домена живет **на aggregates, value objects и domain services** в `src/Core`, которая компилируется с **нулевыми инфраструктурными зависимостями** (нет EF, HttpClient, Docker или ASP.NET). Endpoints, MCP инструменты, компоненты и `BackgroundService`s **организуют** — они никогда не решают. Правила:

- Нет public setters; изменения состояния через intention-revealing методы, которые охраняют инварианты.
- Aggregates ссылаются друг на друга по **strong ID**, не navigation property.
- Один `SaveChanges` мутирует **один** aggregate; кросс-aggregate потоки используют domain events.
- Primitives, пересекающие boundary домена, обернуты в value objects.
- Нарушения инвариантов выбрасывают Core `DomainException`, не framework исключение.

## Последствия

- Правила домена unit-testable без базы данных или веб-хоста.
- `Core` чистота machine-enforced `ArchitectureGuardTests` и не пройдет build если нарушена.
- Есть больше ceremony (value objects, strong IDs, domain events), чем анемичная модель — это deliberate стоимость сохранения правил перемещения денег правильными и в одном месте.
