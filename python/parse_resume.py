import os
import requests

api_key = os.environ["HIRELAYER_API_KEY"]
url = "https://hirelayer.co/api/v3/parser"

with open("resume.pdf", "rb") as resume:
    response = requests.post(
        url,
        headers={"X-API-Key": api_key},
        files={"file": ("resume.pdf", resume, "application/pdf")},
        data={
            "application_id": "candidate-123",
            "do_not_store_data": "true",
        },
        timeout=150,
    )

response.raise_for_status()
parsed = response.json()

candidate_name = parsed.get("info_candidate", {}).get("full_name")
experience = parsed.get("work_experiences", [])
print({"has_candidate_name": bool(candidate_name), "experience_count": len(experience)})
