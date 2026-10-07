#!/usr/bin/env bash
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

curl -X POST https://hirelayer.co/api/v1/matching/job-candidates/rank \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "job_text": "Senior Frontend Engineer, Paris (hybrid). You will build our recruiting platform with React and TypeScript. Requirements: 5+ years of frontend development, strong React and TypeScript skills, fluent English. Nice to have: experience with Next.js.",
    "candidates": [
      {
        "id": "candidate_1",
        "candidate_text": "Alex Morgan, Senior Software Engineer in Paris. 8 years of experience. Since 2022, leads a team building a React and TypeScript SaaS platform at Northstar Labs. Previously built Node.js APIs. Professional English."
      },
      {
        "id": "candidate_2",
        "candidate_text": "Frontend developer in Lyon with 3 years of React experience on e-commerce sites. JavaScript, some TypeScript. Conversational English."
      },
      {
        "id": "candidate_3",
        "candidate_text": "Full-stack JavaScript developer with 6 years of experience, mostly Vue.js and PHP. Fluent English."
      }
    ]
  }'
