#!/usr/bin/env bash
# HireLayer Resume Parser: POST /api/v4/resumes/parse
# 1 credit per resume; 0 when the same file is already stored.
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
# Usage: ./parse-resume.sh [resume.pdf]
set -euo pipefail

# The parsed resume comes back under "resume", stored under "application_id"
# (yours when you send one, as here).
curl --max-time 150 -X POST https://hirelayer.co/api/v4/resumes/parse \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -F "file=@${1:-resume.pdf}" \
  -F "application_id=candidate-123"
