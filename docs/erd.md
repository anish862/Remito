# Remito entity relationship diagram

```mermaid
erDiagram
    USER ||--o{ DEVICE : registers
    USER ||--o{ ALERT : owns
    USER ||--o{ FAVOURITE : saves
    USER o|--o{ REDIRECT_EVENT : initiates
    USER o|--o{ AD_IMPRESSION : generates
    USER o|--o{ AUDIT_LOG : performs
    CURRENCY ||--o{ CORRIDOR : source_currency
    CURRENCY ||--o{ CORRIDOR : target_currency
    RATE_SOURCE ||--o{ MARKET_RATE : provides
    CORRIDOR ||--o{ EXCHANGE_RATE : offers
    EXCHANGE ||--o{ EXCHANGE_RATE : quotes
    CORRIDOR ||--o{ EXCHANGE_FEE : charges
    EXCHANGE ||--o{ EXCHANGE_FEE : sets
    EXCHANGE ||--o{ REDIRECT_EVENT : receives
    CORRIDOR ||--o{ REDIRECT_EVENT : for
    REDIRECT_EVENT ||--o| REFERRAL_CONVERSION : converts
    REFERRAL_CONVERSION ||--o| COMMISSION_LEDGER : settles
    CURRENCY ||--o{ COMMISSION_LEDGER : denominates
    AD_CAMPAIGN ||--o{ AD_CREATIVE : contains
    AD_CREATIVE ||--o{ AD_IMPRESSION : records

    USER {
        uuid Id PK
        string ExternalId UK
        string Email
        datetimeoffset CreatedAt
        datetimeoffset DeletedAt
    }
    DEVICE {
        uuid Id PK
        uuid UserId FK
        string FcmToken UK
        string Platform
    }
    CURRENCY {
        string Code PK
        string Name
        string Symbol
        int DecimalPlaces
    }
    CORRIDOR {
        uuid Id PK
        string FromCurrency FK
        string ToCurrency FK
        boolean IsActive
    }
    RATE_SOURCE {
        uuid Id PK
        string Name UK
        string Type
        int Priority
        boolean IsActive
    }
    MARKET_RATE {
        uuid Id PK
        string Pair
        decimal Bid
        decimal Ask
        decimal Mid
        uuid SourceId FK
        datetimeoffset FetchedAt
        boolean IsStale
    }
    RATE_HISTORY {
        uuid Id PK
        string Pair
        string Bucket
        datetimeoffset Timestamp
        decimal Value
    }
    EXCHANGE {
        uuid Id PK
        string Slug UK
        string Name
        string Status
        string Tier
        boolean IsPlaceholder
    }
    EXCHANGE_RATE {
        uuid Id PK
        uuid ExchangeId FK
        uuid CorridorId FK
        decimal Rate
        datetimeoffset FetchedAt
        boolean IsStale
    }
    EXCHANGE_FEE {
        uuid Id PK
        uuid ExchangeId FK
        uuid CorridorId FK
        decimal MinAmount
        decimal MaxAmount
        decimal FlatFee
        decimal PercentFee
    }
    FAVOURITE {
        uuid Id PK
        uuid UserId FK
        string Type
        uuid TargetId
    }
    ALERT {
        uuid Id PK
        uuid UserId FK
        string Pair
        string Direction
        decimal Target
        boolean IsActive
    }
    REDIRECT_EVENT {
        uuid Id PK
        string ClickId UK
        uuid UserId FK
        uuid ExchangeId FK
        uuid CorridorId FK
        decimal Amount
    }
    REFERRAL_CONVERSION {
        uuid Id PK
        string ClickId FK
        string ExternalRef UK
        decimal Amount
    }
    COMMISSION_LEDGER {
        uuid Id PK
        uuid ConversionId FK
        decimal Amount
        string CurrencyCode FK
    }
    AD_CAMPAIGN {
        uuid Id PK
        string Name
        datetimeoffset StartsAt
        datetimeoffset EndsAt
    }
    AD_CREATIVE {
        uuid Id PK
        uuid CampaignId FK
        string Slot
        string ImageUrl
        string TargetUrl
    }
    AD_IMPRESSION {
        uuid Id PK
        uuid CreativeId FK
        uuid UserId FK
        datetimeoffset CreatedAt
    }
    NEWS_ITEM {
        uuid Id PK
        string Slug UK
        string Title
        boolean IsPublished
        datetimeoffset PublishedAt
    }
    AUDIT_LOG {
        uuid Id PK
        uuid UserId FK
        string Action
        string EntityType
        string EntityId
        datetimeoffset CreatedAt
    }
```

Currencies are keyed by ISO-style three-letter codes; each corridor references
its source and destination currency. `Favourite.TargetId` is intentionally a
polymorphic key selected by `Favourite.Type`. `RateHistory.Pair` and
`Alert.Pair` use the `AAA/BBB` pair notation. User deletion is soft deletion
through `DeletedAt`; exchange visibility is managed by `Status` rather than
deleting exchange rows.
