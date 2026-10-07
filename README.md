# Resume parser API examples

Working examples for the [HireLayer](https://hirelayer.co) recruiting APIs: parse a resume (CV) into JSON, turn a job description into weighted criteria, match and rank candidates against a job, and normalize free-text skills.

All five APIs use one API key and one credit balance. One successful call costs one credit, and the [free plan](https://hirelayer.co/pricing) includes 50 credits a month, no card required.

| API | Endpoint | What it returns | Example |
|---|---|---|---|
| [CV Extract](https://hirelayer.co/products/resume-parsing-api) | `POST /api/v3/parser` | Contact details, work history, education, languages (CEFR levels) and typed skills as JSON, from PDF, Word, image and 10 other formats | [curl](curl/parse-resume.sh) · [Python](python/parse_resume.py) · [Node.js](node/parse-resume.mjs) · [PHP](php/parse_resume.php) · [C#](csharp/Program.cs) |
| [Job Extract](https://hirelayer.co/products/job-parsing-api) | `POST /api/v1/jobs/extract-criteria` | Weighted criteria (1–3) with a mandatory flag and a rationale | [curl](curl/extract-job-criteria.sh) |
| [Match](https://hirelayer.co/products/candidate-job-matching-api) | `POST /api/v1/matching/job-candidate` | A 0–1 score with a status and an explanation for every criterion | [curl](curl/match-candidate.sh) |
| [Rank](https://hirelayer.co/products/candidate-ranking-api) | `POST /api/v1/matching/job-candidates/rank` | Up to 10 candidates ranked for one job, with a score and a rationale each | [curl](curl/rank-candidates.sh) |
| [Skills](https://hirelayer.co/products/skills-matching-api) | `POST /api/v1/skills/resolve` | Free-text skills resolved to a taxonomy of 11,061 skills with stable ids | [curl](curl/resolve-skills.sh) |

## Quickstart

1. Create a free account at [hirelayer.co](https://hirelayer.co) and copy an API key from the dashboard.
2. Export it: `export HIRELAYER_API_KEY=...`
3. Put a resume next to the script as `resume.pdf` and run one of the examples:

```bash
./curl/parse-resume.sh
python python/parse_resume.py          # pip install requests
node node/parse-resume.mjs             # npm install axios form-data
php php/parse_resume.php               # composer require guzzlehttp/guzzle:^7.8
dotnet run --project csharp            # .NET 8, see the guide below
```

Every request is synchronous: the parsed JSON comes back in the same response. Resume parsing can also answer asynchronously with a webhook (`Prefer: respond-async`), which suits automation platforms.

## Step-by-step guides

- [Parse a resume PDF into JSON with Python or Node.js](https://hirelayer.co/blog/resume-pdf-to-json-api)
- [Parse a resume to JSON in PHP with Guzzle or Laravel](https://hirelayer.co/blog/parse-resume-to-json-php)
- [Parse a resume to JSON in C# with HttpClient (.NET 8)](https://hirelayer.co/blog/parse-resume-to-json-csharp)
- [Parse a resume to JSON in Java with java.net.http](https://hirelayer.co/blog/parse-resume-to-json-java)
- [Build a screening pipeline: parse, extract criteria, rank and match](https://hirelayer.co/api-docs/screening-pipeline)

## Reference

- [API documentation](https://hirelayer.co/api-docs) · [OpenAPI 3.1](https://hirelayer.co/openapi.json) · [llms.txt](https://hirelayer.co/llms.txt)
- [Errors and retries](https://hirelayer.co/api-docs/errors) · [Limits and credits](https://hirelayer.co/api-docs/limits)
- [MCP server](https://github.com/HireLayer/hirelayer-mcp) for Claude, ChatGPT, Cursor and VS Code
- [Free online resume parser](https://hirelayer.co/tools/resume-parser), no account needed

Resumes are parsed in Paris, France. Set `do_not_store_data=true` and the file is not stored.

HireLayer at hirelayer.co sells recruiting APIs. It is not the HireLayer job-search app for candidates (hirelayer.ai), which is a separate company.

## License

MIT
