import fs from 'node:fs'
import axios from 'axios'
import FormData from 'form-data'

const form = new FormData()
form.append('file', fs.createReadStream('resume.pdf'))
form.append('application_id', 'candidate-123')
form.append('do_not_store_data', 'true')

const response = await axios.post(
  'https://hirelayer.co/api/v3/parser',
  form,
  {
    headers: {
      ...form.getHeaders(),
      'X-API-Key': process.env.HIRELAYER_API_KEY,
    },
    timeout: 150_000,
  }
)

const parsed = response.data
const candidateName = parsed.info_candidate?.full_name ?? null
console.log({
  hasCandidateName: Boolean(candidateName),
  experienceCount: parsed.work_experiences?.length ?? 0,
})
