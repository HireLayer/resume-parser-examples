"""Parse a resume with the HireLayer Resume Parser (POST /api/v4/resumes/parse).

    pip install requests
    python parse_resume.py [resume.pdf]

1 credit per resume; 0 when the same file is already stored.
"""
import mimetypes
import os
import sys
import time

import requests

API_URL = "https://hirelayer.co/api/v4"
TIMEOUT = 150  # the API answers or gives up within 145 s
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
    path = sys.argv[1] if len(sys.argv) > 1 else "resume.pdf"
    with open(path, "rb") as file:
        content = file.read()  # read once, so a retry sends the same bytes
    content_type = mimetypes.guess_type(path)[0] or "application/octet-stream"

    try:
        response = post(
            "/resumes/parse",
            files={"file": (os.path.basename(path), content, content_type)},
        )
    except HireLayerError as error:
        if error.code == "insufficient_credits":
            sys.exit("Out of credits: see https://hirelayer.co/pricing")
        if error.code == "resume_unreadable":
            sys.exit(f"{path} could not be read as a resume (not billed)")
        sys.exit(f"{error} ({error.doc_url})")

    result = response.json()
    resume = result["resume"]  # the parsed resume
    candidate = resume.get("info_candidate") or {}

    print("application_id:", result["application_id"])
    print("job title:", candidate.get("job_title"))
    print("positions:", len(resume.get("work_experiences") or []))
    print("skills:", ", ".join(skill["skill_title"] for skill in resume.get("skills") or []))
    print("photo found:", bool((resume.get("info_resume") or {}).get("face_url")))
    for warning in result["warnings"]:
        print("warning:", warning)
    print("credits used:", result["credits_used"])


if __name__ == "__main__":
    main()
