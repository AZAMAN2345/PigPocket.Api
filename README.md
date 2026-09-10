# Pig Pocket API

ASP.NET Core API for Pig Pocket, backed by MongoDB.

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
