#!/usr/bin/env python3
import json
import os
import sys
import requests
from pathlib import Path
from mimetypes import guess_type

BASE_URL = "https://localhost:7202"
TEST_DATA_DIR = Path(__file__).parent / "test_data" / "files"


def get_auth_token():
    """Read token from .env file."""
    env_file = Path(__file__).parent / ".env"
    if not env_file.exists():
        print("Error: .env file not found. Run getToken.py first.")
        sys.exit(1)

    with open(env_file) as f:
        for line in f:
            if line.startswith("DOOMSDAY_TOKEN="):
                return line.split("=", 1)[1].strip()

    print("Error: DOOMSDAY_TOKEN not found in .env")
    sys.exit(1)



def get_file_path(filename=None):
    """Get the file path from testdata directory."""
    if not TEST_DATA_DIR.exists():
        print(f"Test data directory not found: {TEST_DATA_DIR}")
        sys.exit(1)

    if filename:
        file_path = TEST_DATA_DIR / filename
        if not file_path.exists():
            print(f"File not found: {file_path}")
            sys.exit(1)
        return file_path

    files = list(TEST_DATA_DIR.glob("*"))
    if not files:
        print(f"No files found in {TEST_DATA_DIR}")
        sys.exit(1)

    print(f"Found {len(files)} file(s) in test data directory:")
    for i, f in enumerate(files, 1):
        print(f"  {i}. {f.name}")

    if len(files) == 1:
        return files[0]

    choice = input("Select file number (default: 1): ").strip()
    index = int(choice) - 1 if choice else 0
    if 0 <= index < len(files):
        return files[index]

    print("Invalid selection")
    sys.exit(1)


def create_file_metadata(file_path):
    """Create CreateFileInput from file."""
    file_size = file_path.stat().st_size
    content_type, _ = guess_type(file_path)
    if not content_type:
        content_type = "application/octet-stream"

    return {
        "fileName": file_path.name,
        "contentType": content_type,
        "sizeBytes": file_size,
        "metadata": {},
    }


def post_file(auth_token, file_metadata):
    """POST to /files to create file metadata and get upload URL."""
    headers = {"Authorization": f"Bearer {auth_token}"}

    print(f"\nPOSTing file metadata to {BASE_URL}/files...")
    response = requests.post(
        f"{BASE_URL}/files", json=file_metadata, headers=headers, verify=False
    )

    if response.status_code != 201:
        print(f"Error: POST /files returned {response.status_code}")
        print(f"Response: {response.text}")
        sys.exit(1)

    result = response.json()
    upload_url = result.get("uploadUrl")
    file_id = result.get("file", {}).get("id")

    print(f"✓ File created with ID: {file_id}")
    print(f"✓ Upload URL received")

    return upload_url, file_id


def upload_file_to_s3(file_path, upload_url):
    """PUT file to presigned S3 URL."""
    print(f"\nUploading {file_path.name} to S3...")

    with open(file_path, "rb") as f:
        file_content = f.read()

    response = requests.put(upload_url, data=file_content)

    if response.status_code != 200:
        print(f"Error: PUT to S3 returned {response.status_code}")
        print(f"Response: {response.text}")
        sys.exit(1)

    print(f"✓ File uploaded successfully ({len(file_content)} bytes)")


def main():
    file_path = get_file_path()
    print(f"Using file: {file_path.name}")

    auth_token = get_auth_token()
    file_metadata = create_file_metadata(file_path)

    upload_url, file_id = post_file(auth_token, file_metadata)
    upload_file_to_s3(file_path, upload_url)

    print(f"\n✓ Upload complete!")
    print(f"  File ID: {file_id}")
    print(f"  File name: {file_path.name}")
    print(f"  File size: {file_metadata['sizeBytes']} bytes")


if __name__ == "__main__":
    main()
