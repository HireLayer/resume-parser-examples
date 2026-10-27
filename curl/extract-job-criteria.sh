#!/usr/bin/env bash
# HireLayer Job Criteria Extractor: POST /api/v4/jobs/extract-criteria
# Free.
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

# The job is stored under the "job_id" of the answer: send that id to score-resumes.sh --ids
# and search-talent-pool.sh.
curl --max-time 150 -X POST https://hirelayer.co/api/v4/jobs/extract-criteria \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Senior Frontend Engineer",
    "text": "Senior Frontend Engineer, Paris (hybrid). You will build our recruiting platform with React and TypeScript. Requirements: 5+ years of frontend development, strong React and TypeScript skills, fluent English. Nice to have: experience with Next.js."
  }'
