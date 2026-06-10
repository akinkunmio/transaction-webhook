CREATE TABLE transactions (
    id BIGSERIAL PRIMARY KEY,
    transaction_id TEXT NOT NULL UNIQUE,
    amount NUMERIC(18, 2) NOT NULL,
    currency TEXT NOT NULL,
    transaction_type TEXT NOT NULL,
    merchant_name TEXT NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    raw_payload JSONB NOT NULL,
    fee NUMERIC(18, 2) NOT NULL,
    net_amount NUMERIC(18, 2) NOT NULL,
    high_value BOOLEAN NOT NULL
);
