// Parse a resume with the HireLayer Resume Parser (POST /api/v4/resumes/parse).
//
//   npm install axios form-data
//   node parse-resume.mjs [resume.pdf]
//
// 1 credit per resume; 0 when the same file is already stored.
import { readFile } from 'node:fs/promises'
import { basename } from 'node:path'
import axios from 'axios'
import FormData from 'form-data'

const API_URL = 'https://hirelayer.co/api/v4'
const TIMEOUT_MS = 150_000 // the API answers or gives up within 145 s
const MAX_ATTEMPTS = 4

const apiKey = process.env.HIRELAYER_API_KEY
if (!apiKey) throw new Error('HIRELAYER_API_KEY is not set')

/** An answer in the v4 error format: {"error": {"code", "message", "param", "doc_url"}}. */
class HireLayerError extends Error {
  constructor(status, error) {
    super(`HTTP ${status} ${error.code}: ${error.message}`)
    this.status = status
    this.code = error.code
    this.param = error.param
    this.docUrl = error.doc_url
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
      throw new HireLayerError(response.status, error)
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

const path = process.argv[2] ?? 'resume.pdf'
const file = await readFile(path) // read once, so a retry sends the same bytes

let response
try {
  response = await post('/resumes/parse', () => {
    const form = new FormData()
    form.append('file', file, { filename: basename(path) })
    return form
  })
} catch (error) {
  if (!(error instanceof HireLayerError)) throw error
  if (error.code === 'insufficient_credits') console.error('Out of credits: see https://hirelayer.co/pricing')
  else if (error.code === 'resume_unreadable') console.error(`${path} could not be read as a resume (not billed)`)
  else console.error(`${error.message} (${error.docUrl})`)
  process.exit(1)
}

const result = response.data
const resume = result.resume // the parsed resume
console.log({
  applicationId: result.application_id,
  jobTitle: resume.info_candidate?.job_title ?? null,
  positions: resume.work_experiences?.length ?? 0,
  skills: (resume.skills ?? []).map((skill) => skill.skill_title),
  photoFound: Boolean(resume.info_resume?.face_url),
  warnings: result.warnings,
  creditsUsed: result.credits_used,
})
