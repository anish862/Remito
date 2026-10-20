# Database schema review gate

**Status: approved.** The reviewer approved this schema on 2026-10-20 before
the first EF migration was generated.

## Schema review

- [x] Primary keys and required columns are present for all requested entities.
- [x] `Currency.Code` is the currency primary key, has length 3, and is used by
  both `Corridor` currency foreign keys and the commission ledger.
- [x] `Corridor(FromCurrency, ToCurrency)` is unique; currencies are seeded
  before the OMR-to-INR corridor.
- [x] Market, exchange, and historical rates use `decimal(18,6)`.
- [x] Monetary amounts and flat/minimum/maximum fees use `decimal(18,2)`;
  percentage fee is `decimal(9,6)`.
- [x] The latest-market-rate index orders by `(Pair, FetchedAt DESC)` and
  includes `Mid`, `SourceId`, and `IsStale`.
- [x] The best-exchange-rate index starts with `(CorridorId, IsStale, Rate)`.
- [x] Historical lookup is indexed and unique by
  `(Pair, Bucket, Timestamp DESC)`.
- [x] `RedirectEvent.ClickId` has a unique index; a referral conversion links
  to that click identifier and has an idempotency constraint.
- [x] Nullable fields have a deliberate use: user email/display name,
  optional app/store/gateway links, optional fee limits, optional redirect
  user, optional external conversion reference, and optional audit details.
- [x] User deletion is soft deletion; other entities are retained or managed
  through their status/publication flags rather than soft-delete columns.
- [x] Foreign keys use restrictive deletes to preserve referral and audit
  history; no accidental cascade deletes remain.
- [x] Seed data includes the 10 requested currencies, the active OMR-to-INR
  corridor, and 10 pending exchanges named `Placeholder Exchange 01` through
  `Placeholder Exchange 10` with `IsPlaceholder = true`.
- [x] EF model seed data is stable and migration-managed, so applying database
  migrations repeatedly does not duplicate seeded rows.

## Sign-off

- Reviewer: User approval in this session
- Date: 2026-10-20
- Decision: **Approved**
- Notes: Approved without requested schema changes.
