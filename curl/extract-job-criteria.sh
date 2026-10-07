#!/usr/bin/env bash
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

curl -X POST https://hirelayer.co/api/v1/jobs/extract-criteria \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "job_text": "Senior Frontend Engineer, Paris (hybrid). You will build our recruiting platform with React and TypeScript. Requirements: 5+ years of frontend development, strong React and TypeScript skills, fluent English. Nice to have: experience with Next.js."
  }'
