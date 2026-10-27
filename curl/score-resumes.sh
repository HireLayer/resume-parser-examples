#!/usr/bin/env bash
# HireLayer Resume Scorer: POST /api/v4/resumes/score
# 1 credit per resume evaluated, +1 per resume file parsed in the call; 0 for a pair already scored.
# Requires HIRELAYER_API_KEY and jq (free plan: 50 credits a month at https://hirelayer.co)
#
# Files:  ./score-resumes.sh [job.txt] [resume1.pdf resume2.pdf ...]
# By ids: ./score-resumes.sh --ids <job_id> <application_id> [<application_id> ...]
#
# Scores come back best first: rank, score (0 to 1), verdict, summary and criteria[].
# precise_ranking re-ranks the 10 best against each other and adds ranking_reason, at no extra cost.
set -euo pipefail

url=https://hirelayer.co/api/v4/resumes/score

if [[ "${1:-}" == "--ids" ]]; then
  # A stored job and stored resumes: plain JSON, no files.
  job_id="${2:?Usage: ./score-resumes.sh --ids <job_id> <application_id> [...]}"
  shift 2
  (( $# > 0 )) || { echo "Give at least one application_id" >&2; exit 1; }

  jq -n --arg job_id "$job_id" \
    '{job: {job_id: $job_id}, resumes: [$ARGS.positional[] | {application_id: .}], precise_ranking: true}' \
    --args "$@" |
    curl --max-time 150 -X POST "$url" \
      -H "X-API-Key: $HIRELAYER_API_KEY" \
      -H "Content-Type: application/json" \
      --data-binary @-
  exit
fi

# A job description and resume files: the JSON goes in the "request" part,
# and each file in the part it names ({"file": "cv1"} reads the part cv1).
job_file="${1:-job.txt}"
if (( $# > 0 )); then shift; fi
if (( $# == 0 )); then set -- resume.pdf; fi

request=$(mktemp)
trap 'rm -f "$request"' EXIT

jq -n --rawfile text "$job_file" \
  '{job: {text: $text},
    resumes: [$ARGS.positional | to_entries[] | {file: "cv\(.key + 1)", reference: (.value | split("/") | last)}],
    precise_ranking: true}' \
  --args "$@" > "$request"

parts=()
i=0
for resume in "$@"; do
  i=$((i + 1))
  parts+=(-F "cv$i=@$resume")
done

curl --max-time 150 -X POST "$url" \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -F "request=<$request;type=application/json" \
  "${parts[@]}"
