#!/usr/bin/env bash
# Simple script to POST orders to the running app for testing.
# Usage: PRODUCT_ID=... ./tools/create_orders.sh [COUNT]

COUNT=${1:-10}
PRODUCT_ID=${PRODUCT_ID:-}
BASE=${BASE:-http://localhost:5000}

if [ -z "$PRODUCT_ID" ]; then
  echo "PRODUCT_ID not set — fetching random product from API..."
  PRODUCT_ID=$(curl -s $BASE/api/products/random | grep -Eo '"ProductId"\s*:\s*"[^"]+"' | sed -E 's/.*:"([^"]+)"/\1/')
  if [ -z "$PRODUCT_ID" ]; then
    echo "Failed to fetch product id. Please set PRODUCT_ID manually."
    exit 1
  fi
  echo "Using product: $PRODUCT_ID"
fi

for i in $(seq 1 $COUNT); do
  cat <<EOF > /tmp/order.json
{
  "customerEmail": "test+${i}@example.com",
  "channel": "Web",
  "items": [ { "productId": "${PRODUCT_ID}", "quantity": 1 } ]
}
EOF
  echo "Posting order #$i"
  curl -s -X POST "$BASE/Orders/Create" -H "Content-Type: application/json" -d @/tmp/order.json || true
  sleep 0.5
done

echo "Done."
