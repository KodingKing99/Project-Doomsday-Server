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
