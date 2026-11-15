#!/usr/bin/env python3
"""Manual live-Textract check: upload a receipt, extract twice, confirm the second call is cached."""
import json
import sys
import time

import requests

import uploadToS3 as upload

EXTRACT_PATH = "/files/{id}/extractExpenseInfo"


def extract(auth_token, file_id, force=False):
    url = f"{upload.BASE_URL}{EXTRACT_PATH.format(id=file_id)}"
    started = time.perf_counter()
    response = requests.post(
        url,
        params={"force": "true"} if force else None,
        headers={"Authorization": f"Bearer {auth_token}"},
        verify=False,
    )
    elapsed = time.perf_counter() - started

    if response.status_code != 200:
        print(f"Error: POST {url} returned {response.status_code}")
        print(f"Response: {response.text}")
        sys.exit(1)

    return response.json(), elapsed


def print_summary(extracted):
    expenses = extracted.get("expenses", [])
    print(f"  Extracted at: {extracted.get('extractedAtUtc')}  ({len(expenses)} expense(s))")
    for expense in expenses:
        vendor = (expense.get("vendorName") or {}).get("text")
        total = (expense.get("total") or {}).get("amount")
        print(f"  Vendor: {vendor}  Total: {total}  Line items: {len(expense.get('lineItems', []))}")


def main():
    file_path = upload.get_file_path()
    auth_token = upload.get_auth_token()

    upload_url, file_id = upload.post_file(auth_token, upload.create_file_metadata(file_path))
    upload.upload_file_to_s3(file_path, upload_url)

    print("\nFirst call (runs Textract):")
    first, first_seconds = extract(auth_token, file_id)
    print_summary(first)
    print(f"  {first_seconds:.2f}s")

    print("\nSecond call (should be served from Mongo, no Textract call):")
    second, second_seconds = extract(auth_token, file_id)
    print(f"  {second_seconds:.2f}s  identical to first: {first == second}")

    if "--dump" in sys.argv:
        print(json.dumps(first, indent=2))


if __name__ == "__main__":
    main()
