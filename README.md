# FilipinaMorena

A lean dating-app starter with an Angular frontend, ASP.NET Core API, MongoDB, email/password authentication, authenticator-app MFA, and Google/Facebook sign-in.

## Stack

- Angular 22 standalone components
- Bootstrap 5 and Bootstrap Icons
- ASP.NET Core 10 Web API
- MongoDB 8
- JWT bearer authentication
- RFC 6238 TOTP codes for authenticator-app MFA
- Google and Facebook OAuth

MongoDB uses collections rather than relational tables. The API creates these collections and indexes on startup:

- `users`: profile identity, password hash, linked external identities, and MFA settings
- `externalLoginStates`: short-lived OAuth return state with a TTL index

## Run locally

Prerequisites: .NET 10 SDK, Node.js 24+, npm, and Docker.

1. Start MongoDB with `docker compose up -d mongodb`.
2. Configure secrets from `src/FilipinaMorena.Api`:

   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:SigningKey" "replace-with-a-random-key-at-least-32-characters"
   dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
   dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
   dotnet user-secrets set "Authentication:Facebook:AppId" "your-facebook-app-id"
   dotnet user-secrets set "Authentication:Facebook:AppSecret" "your-facebook-app-secret"
   ```

3. Run `dotnet run --launch-profile https` from `src/FilipinaMorena.Api`.
4. Run `npm install` and `npm start` from `src/filipinamorena-web`.
5. Open `http://localhost:4200`.

The Angular development server proxies `/api` to `https://localhost:7043`.

## OAuth setup

Register these development callback URLs with the providers:

- Google: `https://localhost:7043/signin-google`
- Facebook: `https://localhost:7043/signin-facebook`

The buttons remain visible when provider credentials are absent, but the API returns a clear `503` configuration response instead of failing inside the authentication handler.

## Authentication behavior

- `POST /api/auth/register` creates a password account.
- `POST /api/auth/login` validates the password and, once MFA is enabled, requires `totpCode`.
- `POST /api/auth/mfa/setup` returns a secret and `otpauth://` URI for an authenticated user.
- `POST /api/auth/mfa/verify` verifies the first code and enables MFA.
- `GET /api/auth/external/google` and `/facebook` begin provider login.

Google/Facebook are alternative login methods, while password plus TOTP is the app's true two-factor flow. Tokens are kept in `sessionStorage`, so closing the browser session clears them.

## Before production

- Replace local secrets and MongoDB credentials with a managed secret store.
- Serve the SPA and API only through HTTPS and set the real `FrontendUrl`.
- Encrypt TOTP secrets at rest and add MFA recovery codes.
- Add email verification, password reset, rate limiting, lockouts, audit events, and account-linking confirmation.
