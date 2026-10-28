// Score resumes against a job with the HireLayer Resume Scorer (POST /api/v4/resumes/score).
//
//   npm install axios form-data
//   node score-resumes.mjs [job.txt] [resume1.pdf resume2.pdf ...]
//
// 1 credit per resume evaluated, +1 per resume file parsed in the call; 0 for a pair already scored.
import { readFile } from 'node:fs/promises'
import { basename } from 'node:path'
import axios from 'axios'
import FormData from 'form-data'

const API_URL = 'https://hirelayer.co/api/v4'
const TIMEOUT_MS = 150_000 // the client timeout the API docs recommend
const MAX_ATTEMPTS = 4

const apiKey = process.env.HIRELAYER_API_KEY
if (!apiKey) throw new Error('HIRELAYER_API_KEY is not set')

/** An answer in the v4 error format: {"error": {"code", "message", "param", "doc_url"}, "request_id"}. */
class HireLayerError extends Error {
  constructor(status, error, requestId) {
    super(`HTTP ${status} ${error.code}: ${error.message}${requestId ? ` (request_id ${requestId})` : ''}`)
    this.status = status
    this.code = error.code
    this.param = error.param
    this.docUrl = error.doc_url
    this.requestId = requestId // quote it to HireLayer support
  }
}

/** POST a form built by makeForm (again on each attempt). Retries 429 after Retry-After, and 502/503 with backoff. */
async function post(path, makeForm) {
  for (let attempt = 1; ; attempt++) {
    const form = makeForm()
    const response = await axios.post(API_URL + path, form, {
      headers: { ...form.getHeaders(), 'X-API-Key': apiKey },
      timeout: TIMEOUT_MS,
      validateStatus: () => true, // read 4xx/5xx bodies yourself
    })
    if ([429, 502, 503].includes(response.status) && attempt < MAX_ATTEMPTS) {
      await new Promise((resolve) => setTimeout(resolve, retryDelayMs(response, attempt)))
      continue
    }
    if (response.status >= 400) {
      const error = response.data?.error ?? { code: 'unexpected_response', message: String(response.data).slice(0, 200) }
      const requestId = response.data?.request_id ?? response.headers['hirelayer-request-id']
      throw new HireLayerError(response.status, error, requestId)
    }
    return response
  }
}

function retryDelayMs(response, attempt) {
  const header = Number.parseInt(response.headers['retry-after'] ?? '', 10)
  const retryAfter = Number.isNaN(header) ? null : header
  if (response.status === 429 && retryAfter !== null) return retryAfter * 1000
  return Math.max(retryAfter ?? 0, 2 ** attempt) * 1000 // 2, 4, 8 s
}

const jobPath = process.argv[2] ?? 'job.txt'
const resumePaths = process.argv.length > 3 ? process.argv.slice(3) : ['resume.pdf']

const jobText = await readFile(jobPath, 'utf8')
const files = await Promise.all(resumePaths.map((path) => readFile(path))) // read once, so a retry sends the same bytes

// The call as JSON in the "request" part; each resume file in the part it names.
const request = {
  job: { text: jobText },
  resumes: resumePaths.map((path, i) => ({ file: `cv${i + 1}`, reference: basename(path) })),
}

let response
try {
  response = await post('/resumes/score', () => {
    const form = new FormData()
    form.append('request', JSON.stringify(request), { contentType: 'application/json' })
    files.forEach((file, i) => form.append(`cv${i + 1}`, file, { filename: basename(resumePaths[i]) }))
    return form
  })
} catch (error) {
  if (!(error instanceof HireLayerError)) throw error
  if (error.code === 'insufficient_credits') console.error('Out of credits: see https://hirelayer.co/pricing')
  else if (error.code === 'job_unreadable') console.error(`${jobPath} could not be read as a job description (not billed)`)
  else console.error(`${error.message} (${error.docUrl})`)
  process.exit(1)
}

const result = response.data
let scores = result.scores
if (response.status === 202) {
  // A large or slow call: the scores are computed in the background.
  // Read results_url until no resume is pending (its scores are in "data").
  console.log(`Still scoring, reading ${result.results_url}`)
  do {
    await new Promise((resolve) => setTimeout(resolve, 5_000))
    const read = await axios.get(result.results_url, { headers: { 'X-API-Key': apiKey }, timeout: TIMEOUT_MS })
    scores = read.data.data
  } while (scores.some((score) => score.evaluation_status === 'pending'))
}

for (const score of scores) {
  // best first
  if (score.evaluation_status !== 'completed') {
    console.log(`${score.rank}. ${score.reference}: ${score.evaluation_status} (${score.error?.code})`)
    continue
  }
  console.log(`${score.rank}. ${score.reference}: ${score.score.toFixed(2)} ${score.verdict ?? '(no verdict)'}`)
  console.log(`   ${score.summary}`)
}
console.log(`job_id: ${result.job?.job_id ?? result.job_id}`)
if (result.credits_used !== undefined) console.log(`credits used: ${result.credits_used}`)
