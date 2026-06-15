#!/usr/bin/env bash
set -euo pipefail

# Update this if your local app is listening on a different port.
URL="${URL:-http://localhost:5000/webhooks/transactions}"

# The webhook secret must match the one configured in your app's settings.
# Default to the value from src/appsettings.json for local testing.
WEBHOOK_SECRET="${WEBHOOK_SECRET:-0d64e89abe00ef0a033820d2edbf10667c81fe148ae204fff6278aaf584d5042}"

payload=$(cat <<'EOF'
{
  "transaction_id": "txn_1001",
  "amount": 300.50,
  "currency": "USD",
  "type": "purchase",
  "merchant": "Example Store",
  "occurred_at": "2026-06-15T12:34:56Z"
}
EOF
)

signature=$(printf '%s' "$payload" | openssl dgst -sha256 -hmac "$WEBHOOK_SECRET" -hex | awk '{print $NF}')
signature="sha256=$signature"

curl -i -X POST "$URL" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -H "X-Webhook-Signature: $signature" \
  -d "$payload"
