"""Score resumes against a job with the HireLayer Resume Scorer (POST /api/v4/resumes/score).

    pip install requests
    python score_resumes.py [job.txt] [resume1.pdf resume2.pdf ...]

1 credit per resume evaluated, +1 per resume file parsed in the call; 0 for a pair already scored.
"""
import json
import mimetypes
import os
import sys
import time

import requests

API_URL = "https://hirelayer.co/api/v4"
TIMEOUT = 150  # seconds, the client timeout the API docs recommend
MAX_ATTEMPTS = 4
API_KEY = os.environ.get("HIRELAYER_API_KEY")


class HireLayerError(Exception):
    """An answer in the v4 error format: {"error": {"code", "message", "param", "doc_url"}, "request_id"}."""

    def __init__(self, status, error, request_id=None):
        self.status = status
        self.code = error.get("code")
        self.param = error.get("param")
        self.doc_url = error.get("doc_url")
        self.request_id = request_id  # quote it to HireLayer support
        support = f" (request_id {request_id})" if request_id else ""
        super().__init__(f"HTTP {status} {self.code}: {error.get('message')}{support}")


def get(url):
    """GET a URL the API returned, such as results_url. Same retries as post."""
    for attempt in range(1, MAX_ATTEMPTS + 1):
        response = requests.get(url, headers={"X-API-Key": API_KEY}, timeout=TIMEOUT)
        if response.status_code in (429, 502, 503) and attempt < MAX_ATTEMPTS:
            time.sleep(retry_delay(response, attempt))
            continue
        if response.status_code >= 400:
            raise api_error(response)
        return response


def post(path, **kwargs):
    """POST to the API. Retries 429 after Retry-After, and 502/503 with backoff."""
    for attempt in range(1, MAX_ATTEMPTS + 1):
        response = requests.post(
            API_URL + path,
            headers={"X-API-Key": API_KEY},
            timeout=TIMEOUT,
            **kwargs,
        )
        if response.status_code in (429, 502, 503) and attempt < MAX_ATTEMPTS:
            time.sleep(retry_delay(response, attempt))
            continue
        if response.status_code >= 400:
            raise api_error(response)
        return response


def retry_delay(response, attempt):
    header = response.headers.get("Retry-After", "")
    retry_after = int(header) if header.isdigit() else None
    if response.status_code == 429 and retry_after is not None:
        return retry_after
    return max(retry_after or 0, 2**attempt)  # 2, 4, 8 s


def api_error(response):
    request_id = response.headers.get("HireLayer-Request-Id")
    try:
        body = response.json()
        error = body["error"]
        request_id = body.get("request_id", request_id)
    except (ValueError, KeyError, TypeError, AttributeError):
        error = {"code": "unexpected_response", "message": response.text[:200]}
    return HireLayerError(response.status_code, error, request_id)


def main():
    if not API_KEY:
        sys.exit("HIRELAYER_API_KEY is not set")
    job_path = sys.argv[1] if len(sys.argv) > 1 else "job.txt"
    resume_paths = sys.argv[2:] or ["resume.pdf"]

    with open(job_path, encoding="utf-8") as file:
        job_text = file.read()

    # The call as JSON in the "request" part; each resume file in the part it names.
    request = {
        "job": {"text": job_text},
        "resumes": [
            {"file": f"cv{i}", "reference": os.path.basename(path)}
            for i, path in enumerate(resume_paths, start=1)
        ],
    }
    parts = [("request", (None, json.dumps(request), "application/json"))]
    for i, path in enumerate(resume_paths, start=1):
        with open(path, "rb") as file:
            content = file.read()  # read once, so a retry sends the same bytes
        content_type = mimetypes.guess_type(path)[0] or "application/octet-stream"
        parts.append((f"cv{i}", (os.path.basename(path), content, content_type)))

    try:
        response = post("/resumes/score", files=parts)
    except HireLayerError as error:
        if error.code == "insufficient_credits":
            sys.exit("Out of credits: see https://hirelayer.co/pricing")
        if error.code == "job_unreadable":
            sys.exit(f"{job_path} could not be read as a job description (not billed)")
        sys.exit(f"{error} ({error.doc_url})")

    result = response.json()
    scores = result.get("scores")
    if response.status_code == 202:
        # A large or slow call: the scores are computed in the background.
        # Read results_url until no resume is pending (its scores are in "data").
        print("Still scoring, reading", result["results_url"])
        while True:
            time.sleep(5)
            scores = get(result["results_url"]).json()["data"]
            if all(score["evaluation_status"] != "pending" for score in scores):
                break

    for score in scores:  # best first
        if score["evaluation_status"] != "completed":
            code = (score.get("error") or {}).get("code")
            print(f"{score['rank']}. {score['reference']}: {score['evaluation_status']} ({code})")
            continue
        print(f"{score['rank']}. {score['reference']}: {score['score']:.2f} {score.get('verdict') or '(no verdict)'}")
        print(f"   {score.get('summary')}")

    print("job_id:", result["job"]["job_id"] if "job" in result else result["job_id"])
    if "credits_used" in result:
        print("credits used:", result["credits_used"])


if __name__ == "__main__":
    main()
