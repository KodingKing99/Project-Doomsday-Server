#!/usr/bin/env python3
import boto3
import getpass
import subprocess
import json
import keyring as kr
from pathlib import Path

keyRingKey = "doomsday_creds"

username = input("Username: (enter if unchanged) ")
password = getpass.getpass("Password: (enter if unchanged)")

if username != "":
    kr.set_password(keyRingKey, "username", username)
storedU = kr.get_password(keyRingKey, "username")

if password != "":
    kr.set_password(keyRingKey, storedU, password) 

storedP = kr.get_password(keyRingKey, storedU)


client = boto3.client("cognito-idp", region_name="us-west-2")

response = client.initiate_auth(
    AuthFlow="USER_PASSWORD_AUTH",
    ClientId="76npcq0oi5d857gekl9u8iue6e",
    AuthParameters={"USERNAME": storedU, "PASSWORD": storedP},
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

# Save token to .env file for other scripts to use
env_file = Path(__file__).parent / ".env"
with open(env_file, "w") as f:
    f.write(f"DOOMSDAY_TOKEN={authToken}\n")
print(f"(Saved token to {env_file})")
