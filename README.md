# Pig Pocket

Pig Pocket is maintained as a monorepo containing the ASP.NET Core API and the
Expo/React Native mobile application.

## Repository structure

- `/` — ASP.NET Core API backed by MongoDB
- `/PigPocket.Mobile` — Expo/React Native client

## Requirements

- .NET 10 SDK
- MongoDB

## Configuration

Keep secrets out of `appsettings.json`. Override configuration with environment
variables or a local configuration file that is excluded by `.gitignore`.

```powershell
$env:MongoDb__ConnectionString = "mongodb://localhost:27017"
$env:MongoDb__DatabaseName = "PigPocket"
$env:Jwt__Key = "replace-with-a-long-random-secret"
$env:Mono__SecretKey = "your-mono-secret-key"
$env:Mono__WebhookSecret = "your-mono-webhook-secret"
```

## Run locally

```powershell
dotnet restore
dotnet run
```

The development HTTP profile listens on `http://localhost:5022`.

See `PigPocket.Mobile/README.md` for mobile application setup and run commands.

## Email authentication

The API loads ignored `appsettings.Local.json`, then environment overrides. Set
`Resend:ApiKey`, `Resend:From`, and a random `Jwt:Key` there, or use environment
variables `Resend__ApiKey`, `Resend__From`, and `Jwt__Key`. Local secrets are excluded
from publishing; configure them separately on the deployed server.

The local sender is `Piggy Pockets <onboarding@resend.dev>`. Resend's test sender
can only send to the email associated with your Resend account. To support other
users, verify a domain in Resend and set `Resend:From` to an address on that domain.

| Endpoint | JSON body | Result |
| --- | --- | --- |
| `POST /api/auth/register` | `email`, `password`, optional `firstName`, `lastName` | Sends verification code; no tokens yet |
| `POST /api/auth/verify-email` | `email`, `code` | Verifies email; returns JWT and refresh token |
| `POST /api/auth/resend-verification` | `email` | Sends another verification code |
| `POST /api/auth/login` | `email`, `password` | Returns tokens for verified accounts |
| `POST /api/auth/forgot-password` | `email` | Sends reset code if account exists |
| `POST /api/auth/reset-password` | `email`, `code`, `newPassword` | Changes password and revokes every session |
| `POST /api/auth/refresh` | `refreshToken` | Rotates refresh token and issues JWT |
| `POST /api/auth/logout` | Bearer JWT; no body | Revokes current session |
| `GET /api/auth/me` | Bearer JWT | Protected welcome response |

Passwords require 8–128 characters. Codes are six digits, expire after 10 minutes,
allow five attempts, and have a 45-second resend cooldown. Only keyed code hashes
and refresh-token hashes are stored. JWTs last 15 minutes; sessions last 30 days.
Every authenticated request checks the session in MongoDB, so logout and password
reset invalidate JWTs immediately. Refresh tokens rotate atomically and cannot be
reused. Existing accounts must verify their email before signing in.

The client shows verification, forgot-password, reset-password, and welcome screens.
It keeps tokens in memory and refreshes them while open; restarting requires login.
The verification layout follows the supplied reference using the existing mascot.
Use `Cors:Origins` (an array) to configure allowed web origins; localhost ports
8081, 8082, and 19006 are allowed by default. Auth requests are limited to 30 per minute
per connection IP. Configure trusted forwarded headers when deploying behind a proxy.

If email delivery fails during registration, the account remains unverified. Use
the signup screen's "Already signed up? Verify your email" link to retry delivery.
"Change email" returns to signup without editing an existing account's email.

Run checks with MongoDB running:

```powershell
dotnet run --project tests/AuthFlow
npm run typecheck --prefix PigPocket.Mobile
```

The integration harness creates and removes an isolated `PigPocket_AuthTest_*`
database and intercepts email delivery; it sends no real emails. It covers code
expiry, attempt limits, resend cooldown, verification, refresh rotation, password
reset, session revocation, and email delivery failure recovery.
