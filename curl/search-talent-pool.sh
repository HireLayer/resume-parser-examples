#!/usr/bin/env bash
# HireLayer Talent Pool Search: POST /api/v4/resumes/search
# 1 credit per search.
# Requires HIRELAYER_API_KEY and jq (free plan: 50 credits a month at https://hirelayer.co)
# Usage: ./search-talent-pool.sh <job_id>   (the job_id from extract-job-criteria.sh)
set -euo pipefail

job_id="${1:?Usage: ./search-talent-pool.sh <job_id>}"

# Returns the stored resumes closest to the job, closest first, with their application_id:
# send the ones you pick to score-resumes.sh --ids.
jq -n --arg job_id "$job_id" '{job_id: $job_id, limit: 20}' |
  curl --max-time 150 -X POST https://hirelayer.co/api/v4/resumes/search \
    -H "X-API-Key: $HIRELAYER_API_KEY" \
    -H "Content-Type: application/json" \
    --data-binary @-
