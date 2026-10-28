#!/usr/bin/env bash
# HireLayer Job Recommendations: POST /api/v4/jobs/search
# 1 credit per search, +1 when the resume is sent as a file.
# Requires HIRELAYER_API_KEY and jq (free plan: 50 credits a month at https://hirelayer.co)
# Usage: ./recommend-jobs.sh <application_id>   or   ./recommend-jobs.sh resume.pdf
set -euo pipefail

resume="${1:?Usage: ./recommend-jobs.sh <application_id | resume file>}"
url=https://hirelayer.co/api/v4/jobs/search

# Returns the open stored jobs closest to the resume, closest first, with their job_id and title.
if [[ -f "$resume" ]]; then
  # A resume file: parsed and stored, then searched.
  curl --max-time 150 -X POST "$url" \
    -H "X-API-Key: $HIRELAYER_API_KEY" \
    -F "file=@$resume" \
    -F "limit=10"
else
  # A stored resume, by the application_id the parser returned.
  jq -n --arg application_id "$resume" '{application_id: $application_id, limit: 10}' |
    curl --max-time 150 -X POST "$url" \
      -H "X-API-Key: $HIRELAYER_API_KEY" \
      -H "Content-Type: application/json" \
      --data-binary @-
fi
