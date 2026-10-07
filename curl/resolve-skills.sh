#!/usr/bin/env bash
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

curl -X POST https://hirelayer.co/api/v1/skills/resolve \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Google Sheet/Excel",
    "language": "fr"
  }'
