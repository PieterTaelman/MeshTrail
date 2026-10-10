// TypeScript copies of Meshtrail.Core.Contracts/Accounts. Keep them in sync with the C# records.

export interface Profile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  createdAt: string;
}

export interface SignInResponse {
  accessToken: string;
  expiresAt: string;
  profile: Profile;
}

export interface RegisterRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
}

/** Same rule as UserAccount.PasswordMinLength in C#. */
export const PASSWORD_MIN_LENGTH = 10;
