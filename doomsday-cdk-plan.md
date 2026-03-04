# Doomsday CDK Infrastructure Plan

**Prerequisite for:** `authentication-cognito-plan.md`

This plan sets up an AWS CDK project to manage Project Doomsday infrastructure as code. The immediate goal is provisioning the Cognito User Pool so the auth plan can proceed, but the CDK project is set up to grow to own all project infrastructure over time.

---

## Prerequisites (your machine)

```bash
# Node.js 18+ required for CDK CLI
node --version

# Install CDK CLI globally
npm install -g aws-cdk

# Verify
cdk --version
```

You also need the AWS CLI configured with the `DoomsdayAdmin-027903755990` profile (already present in `appsettings.Development.json`):

```bash
aws sts get-caller-identity --profile DoomsdayAdmin-027903755990
```

---

## Step 1 — Create the CDK Project

Create an `infra/` directory at the repo root and initialize a TypeScript CDK app inside it.

> **Why TypeScript?** CDK TypeScript has the most complete construct library, the best community examples, and the fastest release cadence. CDK for .NET (C#) exists but lags slightly. Keeping infra in a separate language from the server is normal and fine.

```bash
mkdir infra
cd infra
cdk init app --language typescript
```

This generates:
```
infra/
  bin/
    infra.ts          # App entrypoint — defines which stacks to instantiate
  lib/
    infra-stack.ts    # Main stack — this is where resources are defined
  cdk.json            # CDK config (app entrypoint, feature flags)
  package.json
  tsconfig.json
```

Rename the generated stack to something meaningful. In `bin/infra.ts`:

```typescript
import { DoomsdayStack } from '../lib/doomsday-stack';
// rename infra-stack.ts → doomsday-stack.ts
```

---

## Step 2 — Bootstrap CDK in the AWS Account

CDK bootstrap creates the S3 bucket and IAM roles CDK needs to deploy stacks. Run this once per account/region:

```bash
cd infra
cdk bootstrap aws://027903755990/us-west-2 --profile DoomsdayAdmin-027903755990
```

You'll see a `CDKToolkit` CloudFormation stack created in the account. This is a one-time operation.

---

## Step 3 — Define the Cognito Stack

In `infra/lib/doomsday-stack.ts`, define a Cognito User Pool and App erd4lient:

```typescript
import * as cdk from 'aws-cdk-lib';
import * as cognito from 'aws-cdk-lib/aws-cognito';
import { Construct } from 'constructs';

export class DoomsdayStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    // --- Cognito User Pool ---
    const userPool = new cognito.UserPool(this, 'DoomsdayUserPool', {
      userPoolName: 'project-doomsday-user-pool',
      selfSignUpEnabled: true,
      signInAliases: { email: true },
      autoVerify: { email: true },
      passwordPolicy: {
        minLength: 8,
        requireLowercase: true,
        requireUppercase: true,
        requireDigits: true,
        requireSymbols: false,
      },
      accountRecovery: cognito.AccountRecovery.EMAIL_ONLY,
      removalPolicy: cdk.RemovalPolicy.RETAIN, // never accidentally delete users
    });

    // --- App Client (public, for mobile/SPA) ---
    const appClient = userPool.addClient('DoomsdayAppClient', {
      userPoolClientName: 'project-doomsday-app-client',
      authFlows: {
        userSrp: true,         // standard secure remote password
        userPassword: true,    // needed for CLI-based token retrieval in dev
      },
      preventUserExistenceErrors: true,
    });

    // --- CloudFormation Outputs ---
    // These are the two values needed in appsettings.Development.json
    new cdk.CfnOutput(this, 'UserPoolId', {
      value: userPool.userPoolId,
      description: 'Cognito User Pool ID',
      exportName: 'DoomsdayUserPoolId',
    });

    new cdk.CfnOutput(this, 'UserPoolClientId', {
      value: appClient.userPoolClientId,
      description: 'Cognito App Client ID (Audience)',
      exportName: 'DoomsdayUserPoolClientId',
    });

    new cdk.CfnOutput(this, 'CognitoAuthority', {
      value: `https://cognito-idp.${this.region}.amazonaws.com/${userPool.userPoolId}`,
      description: 'JWT Bearer Authority URL for appsettings',
      exportName: 'DoomsdayCognitoAuthority',
    });
  }
}
```

---

## Step 4 — Configure the CDK App Entrypoint

In `infra/bin/infra.ts` (or renamed `bin/doomsday.ts`):

```typescript
import * as cdk from 'aws-cdk-lib';
import { DoomsdayStack } from '../lib/doomsday-stack';

const app = new cdk.App();

new DoomsdayStack(app, 'DoomsdayStack', {
  env: {
    account: '027903755990',
    region: 'us-west-2',
  },
});
```

Update `cdk.json` if you renamed the entrypoint file:

```json
{
  "app": "npx ts-node --prefer-ts-exts bin/doomsday.ts"
}
```

---

## Step 5 — Deploy

```bash
cd infra
npm install
cdk diff --profile DoomsdayAdmin-027903755990   # preview what will be created
cdk deploy --profile DoomsdayAdmin-027903755990
```

After deploy, CDK prints the stack outputs:

```
Outputs:
DoomsdayStack.CognitoAuthority  = https://cognito-idp.us-west-2.amazonaws.com/us-west-2_XXXXXXXXX
DoomsdayStack.UserPoolClientId  = 1abc2defg3hijklmno
DoomsdayStack.UserPoolId        = us-west-2_XXXXXXXXX
```

You can also retrieve them at any time:

```bash
aws cloudformation describe-stacks \
  --stack-name DoomsdayStack \
  --query "Stacks[0].Outputs" \
  --profile DoomsdayAdmin-027903755990
```

---

## Step 6 — Create a Test User

With the User Pool deployed:

```bash
aws cognito-idp admin-create-user \
  --user-pool-id us-west-2_XXXXXXXXX \
  --username testuser@example.com \
  --temporary-password TempPass123! \
  --profile DoomsdayAdmin-027903755990 \
  --region us-west-2
```

Then set a permanent password (required before the user can authenticate):

```bash
aws cognito-idp admin-set-user-password \
  --user-pool-id us-west-2_XXXXXXXXX \
  --username testuser@example.com \
  --password YourPassword123! \
  --permanent \
  --profile DoomsdayAdmin-027903755990 \
  --region us-west-2
```

---

## Step 7 — Wire Outputs into App Config

Copy the CDK output values into `appsettings.Development.json` (or a gitignored `appsettings.Local.json`). See `authentication-cognito-plan.md` Step 1 for the exact config shape.

> **Production:** Don't put real values in committed config files. Instead, read them from environment variables, AWS Systems Manager Parameter Store, or Secrets Manager at runtime. CDK can write outputs to SSM automatically — add this as a follow-up once the basic flow is working.

---

## Implementation Order

1. [ ] Install Node.js 18+ and CDK CLI (`npm install -g aws-cdk`)
2. [ ] Verify AWS CLI profile works (`aws sts get-caller-identity`)
3. [ ] `mkdir infra && cd infra && cdk init app --language typescript`
4. [ ] Rename generated files, define `DoomsdayStack` with Cognito resources
5. [ ] `cdk bootstrap aws://027903755990/us-west-2 --profile DoomsdayAdmin-027903755990`
6. [ ] `cdk deploy --profile DoomsdayAdmin-027903755990`
7. [ ] Create a test user via CLI
8. [ ] Copy stack outputs into `appsettings.Development.json`
9. [ ] Proceed to `authentication-cognito-plan.md`

---

## Repo Structure After This Plan

```
ProjectDoomsdayServer/
  infra/
    bin/
      infra.ts                  # App entrypoint — instantiates DoomsdayStack
    lib/
      doomsday-stack.ts         # Cognito UserPool, AppClient, CfnOutputs
    cdk.out/                    ← gitignored (synthesized CloudFormation)
    cdk.json
    package.json
    tsconfig.json
    node_modules/               ← gitignored
  src/
    ...
  doomsday-cdk-plan.md
  authentication-cognito-plan.md
```

add `infra/node_modules/` and `infra/cdk.out/` to `.gitignore` if not already covered.

---

## Future Infrastructure to Add to This Stack

Once Cognito is working, this CDK stack is the right place to bring in:

- **S3 bucket** — `project-doomsday-files-bucket` currently exists manually. Import it with `s3.Bucket.fromBucketName()` for reference, or do a full import via `cdk import` to bring it under CDK management.
- **IAM role/policy** — Scoped S3 access policy for the server (e.g. `s3:PutObject` only under the `{userId}/` prefix).
- **SSM Parameter Store** — Write CDK outputs as SSM parameters so the app can read them at startup without manual config copying.
- **MongoDB Atlas / DocumentDB** — If the database ever moves off localhost.
- **ECS / App Runner / Lambda** — Compute layer if you deploy the API to AWS.
