#!/usr/bin/env python3
import boto3
import getpass
import subprocess
import json

username = input("Username: ")
password = getpass.getpass("Password: ")

client = boto3.client("cognito-idp", region_name="us-west-2")

response = client.initiate_auth(
    AuthFlow="USER_PASSWORD_AUTH",
    ClientId="76npcq0oi5d857gekl9u8iue6e",
    AuthParameters={"USERNAME": username, "PASSWORD": password},
)

auth = response.get("AuthenticationResult", {})

lines = [
    "=== Cognito Tokens ===",
    "",
    "ID Token:",
    auth.get("IdToken", "N/A"),
    "",
    "Access Token:",
    auth.get("AccessToken", "N/A"),
    "",
    "Refresh Token:",
    auth.get("RefreshToken", "N/A"),
    "",
    f"Expires In: {auth.get('ExpiresIn', 'N/A')} seconds",
    f"Token Type: {auth.get('TokenType', 'N/A')}",
]

output = "\n".join(lines)
print(output)

authToken = auth.get("AccessToken", "N/A")

subprocess.run("pbcopy", input=authToken.encode(), check=True)
print("\n(Copied auth token to clipboard)")
