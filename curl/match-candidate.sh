#!/usr/bin/env bash
# Requires HIRELAYER_API_KEY (free plan: 50 credits a month at https://hirelayer.co)
set -euo pipefail

curl -X POST https://hirelayer.co/api/v1/matching/job-candidate \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "job_text": "Senior Frontend Engineer, Paris (hybrid). You will build our recruiting platform with React and TypeScript. Requirements: 5+ years of frontend development, strong React and TypeScript skills, fluent English. Nice to have: experience with Next.js.",
    "candidate_text": "Alex Morgan, Senior Software Engineer in Paris. 8 years of experience. Since 2022, leads a team building a React and TypeScript SaaS platform at Northstar Labs. Previously built Node.js APIs. Professional English.",
    "matching_criteria": [
      {
        "id": "crit_1",
        "label": "Maîtrise de React",
        "weight": 3,
        "is_mandatory": true,
        "rationale": "React est explicitement exigé pour le poste."
      },
      {
        "id": "crit_2",
        "label": "Maîtrise de TypeScript",
        "weight": 3,
        "is_mandatory": true,
        "rationale": "TypeScript est explicitement exigé pour le poste."
      },
      {
        "id": "crit_3",
        "label": "Au moins 5 ans d’expérience en développement frontend",
        "weight": 3,
        "is_mandatory": true,
        "rationale": "L’offre demande plus de cinq ans d’expérience frontend."
      },
      {
        "id": "crit_4",
        "label": "Anglais courant",
        "weight": 2,
        "is_mandatory": true,
        "rationale": "Un anglais courant est demandé."
      },
      {
        "id": "crit_5",
        "label": "Expérience avec Next.js",
        "weight": 1,
        "is_mandatory": false,
        "rationale": "Next.js est présenté comme un atout."
      }
    ]
  }'
