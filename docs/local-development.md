# Local development

Required: .NET 10 SDK (the repository SDK policy applies), local dotnet-ef 10 tool, SQL Server (TCP on Mac/Linux; LocalDB only Windows), Node 22+ and npm. Docker Desktop is optional for the existing local SQL Server container. DBeaver is optional. Use the existing database; do not create a replacement or reset it.

1. `dotnet dev-certs https --trust`.
2. Configure a real SQL connection using User Secrets (replace placeholders, don't commit credentials):

```sh
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=localhost,1433;Database=TinkliWH;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True;Persist Security Info=False' --project src/Wros.Api
```

TrustServerCertificate=True is for local self-signed SQL only; deployment requires trusted SQL certificates and least-privilege login. Existing configured connections take precedence over the sample name.

3. If creating Development users for the first time, copy `src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.example.cs` to `DevelopmentSeedCredentials.local.cs` in the same folder. Enter your own passwords (minimum 8 characters, no composition rules). The local file is Git-ignored. This preserves the previously approved local-file seed workflow; no seed environment flag is needed. Do not put values in the tracked example. Existing users are not reset by migrations/startup. Production does not run this Development-only hook. Release builds exclude the private source and use the empty example, so use the default Debug configuration for local seeding.
4. Restore/build/apply migrations, explicitly selecting Development:

```sh
dotnet restore Wros.slnx --locked-mode
dotnet tool restore
dotnet build Wros.slnx --no-restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

5. Another terminal:

```sh
cd src/wros-web
npm ci
npm run dev
```

Open **http://localhost:5173**. Vite proxies `/api` to HTTPS API `https://localhost:7243`; browser staff requests stay same-origin. First open the API readiness URL in the browser and trust the development certificate if required. API also listens on port 5243 only to redirect to HTTPS. Production SPA hosting must be same-origin HTTPS; hosting/deployment is outside this change.

6. Verify API `/api/v1/health/live`, `/api/v1/health/ready`, development `/openapi/v1.json`. Login as existing `wros.admin` / `wros.staff` using privately configured passwords. Staff initial grant remains inventory.view only; SuperAdmin can assign further role permissions.
7. Optional integration setup is in [e-commerce integration](e-commerce-integration.md). With no allowed clients configured, integration remains inaccessible. Do not put machine private keys in browser or Git.

Production builds:

```sh
dotnet build Wros.slnx -c Release
npm --prefix src/wros-web run build
```

Manual checks: phase-1, phase-1.5 and phases 2–8 documents in this folder. No test framework or automated suite is installed.
