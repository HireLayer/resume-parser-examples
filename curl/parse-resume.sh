#!/usr/bin/env bash
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

curl -X POST https://hirelayer.co/api/v3/parser \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -F "file=@resume.pdf;type=application/pdf" \
  -F "application_id=app_123"
