# Resume parser API examples

Working examples for the [HireLayer](https://hirelayer.co) recruiting APIs: parse a resume (CV) into JSON, turn a job description into typed criteria, score and rank resumes against a job, search your talent pool, recommend jobs for a resume, and normalize free-text skills.

All six APIs live under `https://hirelayer.co/api/v4` and use one API key and one credit balance. Failed work is not billed, and the [free plan](https://hirelayer.co/pricing) includes 50 credits a month, no card required.

| API | Endpoint | What it returns | Price | Example |
|---|---|---|---|---|
| [Resume Parser](https://hirelayer.co/products/resume-parsing-api) | `POST /api/v4/resumes/parse` | Contact details, work history, education, languages (CEFR levels), typed skills and the candidate photo as JSON, from 15 file formats: PDF, Word, images, HTML, Markdown and more | 1 credit per resume; 0 when the same file is already stored | [curl](curl/parse-resume.sh) · [Python](python/parse_resume.py) · [Node.js](node/parse-resume.mjs) · [PHP](php/parse_resume.php) · [C#](csharp/Program.cs) |
| [Job Criteria Extractor](https://hirelayer.co/products/job-parsing-api) | `POST /api/v4/jobs/extract-criteria` | Typed criteria (skill, experience, education, language…), each required, preferred or bonus, with a weight and a threshold | Free | [curl](curl/extract-job-criteria.sh) |
| [Resume Scorer](https://hirelayer.co/products/candidate-job-matching-api) | `POST /api/v4/resumes/score` | Resumes scored against a job, best first: a 0–1 score, a verdict, a summary and the evidence for every criterion. `precise_ranking` re-ranks the best against each other, with a reason for each place | 1 credit per resume evaluated, +1 per resume file parsed in the call, +1 per call with `precise_ranking`; 0 for a pair already scored | [curl](curl/score-resumes.sh) · [Python](python/score_resumes.py) · [Node.js](node/score-resumes.mjs) · [PHP](php/score_resumes.php) · [C#](csharp/Program.cs) |
| [Talent Pool Search](https://hirelayer.co/products/talent-pool-search-api) | `POST /api/v4/resumes/search` | The stored resumes closest to a job, closest first, to choose which ones to score | 1 credit per search | [curl](curl/search-talent-pool.sh) |
| [Job Recommendations](https://hirelayer.co/products/job-recommendation-api) | `POST /api/v4/jobs/search` | The open stored jobs closest to a resume, closest first | 1 credit per search, +1 when the resume is sent as a file | [curl](curl/recommend-jobs.sh) |
| [Skills Normalizer](https://hirelayer.co/products/skills-matching-api) | `POST /api/v4/skills/normalize` | Free-text skills resolved to a taxonomy of 11,061 skills with stable ids, families and domains | 1 credit per call | [curl](curl/normalize-skills.sh) |

## Quickstart

1. Create a free account at [hirelayer.co](https://hirelayer.co) and copy an API key from the dashboard.
2. Export it: `export HIRELAYER_API_KEY=...`
3. Put a job description in `job.txt` and a resume next to it as `resume.pdf`, then score the resume against the job:

```bash
jq -n --rawfile text job.txt '{job: {text: $text}, resumes: [{file: "cv1"}]}' > request.json
curl -X POST https://hirelayer.co/api/v4/resumes/score \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -F "request=<request.json;type=application/json" \
  -F "cv1=@resume.pdf"
```

The job's criteria are extracted, the resume is parsed, and both are stored. Read `scores[0].score` (0 to 1), `verdict`, `summary`, and `criteria[]` for the evidence behind each criterion. Keep `job.job_id` and `scores[0].application_id`: sent instead of the text and the file, they score the same pair again for free.

The same call, and the parser, in each language:

```bash
./curl/score-resumes.sh job.txt resume.pdf
python python/score_resumes.py job.txt resume.pdf       # pip install requests
node node/score-resumes.mjs job.txt resume.pdf          # npm install axios form-data
php php/score_resumes.php job.txt resume.pdf            # composer require guzzlehttp/guzzle:^7.8
dotnet run --project csharp -- score job.txt resume.pdf # .NET 8, see the guide below

python python/parse_resume.py resume.pdf                # the parsed resume is under "resume"
dotnet run --project csharp -- parse resume.pdf
```

The Python, Node.js, PHP and C# programs read `HIRELAYER_API_KEY`, wait up to 150 seconds, retry `429` after `Retry-After` and `502`/`503` with backoff, and branch on `error.code` for any other error, printing the call's `request_id` to quote to support ([error codes](https://hirelayer.co/api-docs/errors)). A score still running after about 100 seconds answers `202` with a `results_url`, which they read until no resume is pending.

The curl scripts chain through the ids each call returns:

```bash
./curl/extract-job-criteria.sh                      # the job is stored under its job_id
./curl/search-talent-pool.sh <job_id>               # the closest stored resumes
./curl/score-resumes.sh --ids <job_id> <application_id> <application_id>
./curl/recommend-jobs.sh <application_id>           # the open jobs closest to a resume
./curl/normalize-skills.sh
```

## Async with a signed webhook

Add `webhook_url` and the call answers `202` at once with a `request_id`; the result is posted to your URL when it is ready, signed in the `HireLayer-Signature` header. For a parse it is a form field, for a score a field of the JSON:

```bash
curl -X POST https://hirelayer.co/api/v4/resumes/parse \
  -H "X-API-Key: $HIRELAYER_API_KEY" \
  -F "file=@resume.pdf" \
  -F "webhook_url=https://example.com/hooks/hirelayer"
```

Check the signature (an HMAC-SHA256 of the timestamp and the raw body, keyed with the webhook secret from your dashboard) before trusting the body, answer with any 2xx, and deduplicate on `event_id`. A scoring call too large to answer at once also answers `202`, with a `results_url` to read the scores from. Details: [webhooks](https://hirelayer.co/api-docs/webhooks).

## Step-by-step guides

- [Parse a resume PDF into JSON with Python or Node.js](https://hirelayer.co/blog/resume-pdf-to-json-api)
- [Parse a resume to JSON in PHP with Guzzle or Laravel](https://hirelayer.co/blog/parse-resume-to-json-php)
- [Parse a resume to JSON in C# with HttpClient (.NET 8)](https://hirelayer.co/blog/parse-resume-to-json-csharp)
- [Parse a resume to JSON in Java with java.net.http](https://hirelayer.co/blog/parse-resume-to-json-java)
- [Build a screening pipeline: parse resumes, extract criteria, search your talent pool and score](https://hirelayer.co/api-docs/screening-pipeline)

## Reference

- [API documentation](https://hirelayer.co/api-docs) · [OpenAPI 3.1](https://hirelayer.co/openapi.json) · [llms.txt](https://hirelayer.co/llms.txt)
- [Errors and retries](https://hirelayer.co/api-docs/errors) · [Limits and credits](https://hirelayer.co/api-docs/limits)
- [MCP server](https://github.com/HireLayer/hirelayer-mcp) for Claude, ChatGPT, Cursor and VS Code
- [Free online resume parser](https://hirelayer.co/tools/resume-parser), no account needed

Resumes are parsed in Paris, France. Set `do_not_store_data=true` and the call keeps no file, parse, job or score.

HireLayer at hirelayer.co sells recruiting APIs. It is not the HireLayer job-search app for candidates (hirelayer.ai), which is a separate company.

## License

MIT
