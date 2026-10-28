#!/usr/bin/env bash
# HireLayer Skills Normalizer: POST /api/v4/skills/normalize
# 1 credit per call.
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

# Each skill found comes back with a stable id, its canonical name, families, domains
# and a broader skill; "language" sets the language of the names returned.
curl --max-time 150 -X POST https://hirelayer.co/api/v4/skills/normalize \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Google Sheet/Excel",
    "language": "en"
  }'
