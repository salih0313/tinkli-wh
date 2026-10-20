# First production SuperAdmin

This owner-authorized bootstrap creates one real initial administrator, not Development demo accounts. It is disabled by default. It runs after the existing startup migrations and before HTTP requests are accepted. No new schema migration is required; existing Identity roles and audit tables must be migrated normally. Legacy `Seed:Enabled` and `Seed:SampleDataEnabled` stay false.

Set these values in the production host's environment/secret configuration:

| Setting | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ProductionAdminSeed__Enabled` | `true` for initial bootstrap |
| `ProductionAdminSeed__UserName` | Your chosen administrator username (1–64 characters) |
| `ProductionAdminSeed__Password` | Your private temporary password; minimum eight characters under the current Identity policy |

Never commit the password to appsettings, source files or deployment scripts. For IIS, supply secrets using your server's protected configuration mechanism and restart the application pool after changing settings. Do not use the Development credential source file in production.

For an interactive shell deployment, build first:

```sh
dotnet restore Wros.slnx --locked-mode
dotnet publish src/Wros.Api -c Release -o ./artifacts/api --no-restore
```

macOS/Linux bash example (password prompt does not echo or enter shell history):

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ProductionAdminSeed__Enabled=true
read -r -p 'Initial administrator username: ' ProductionAdminSeed__UserName
export ProductionAdminSeed__UserName
read -r -s -p 'Temporary password: ' ProductionAdminSeed__Password
export ProductionAdminSeed__Password
dotnet artifacts/api/Wros.Api.dll
# After stopping the process following successful bootstrap:
unset ProductionAdminSeed__Enabled ProductionAdminSeed__UserName ProductionAdminSeed__Password
```

PowerShell 7 example:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ProductionAdminSeed__Enabled = 'true'
$env:ProductionAdminSeed__UserName = Read-Host 'Initial administrator username'
$env:ProductionAdminSeed__Password = Read-Host 'Temporary password' -MaskInput
dotnet artifacts/api/Wros.Api.dll
# After stopping the process following successful bootstrap:
Remove-Item Env:ProductionAdminSeed__Enabled, Env:ProductionAdminSeed__UserName, Env:ProductionAdminSeed__Password
```

Keep the existing production SQL connection, HTTPS, CORS and other required settings. Starting the API already applies pending migrations; this bootstrap does not change that behavior. For hosted deployments configure the same three bootstrap values in the host rather than launching a second application instance against the database.

## Manual acceptance

Use a separate production-configured staging database first, with existing migrations and no SuperAdmin.

1. Leave bootstrap disabled and start the API. Expected: no bootstrap account is created.
2. Enable bootstrap with a unique username and valid private password, then start. Expected: exactly one active SuperAdmin with `MustChangePassword=true` and one `ProductionAdminSeeded` audit entry. No password appears in audit data.
3. Log in through the production frontend, change the temporary password and log in again. Expected: password change is required before other business operations; afterwards SuperAdmin can manage users.
4. Restart with bootstrap still enabled. Expected: no duplicate, password reset or role change. The original temporary password no longer works after your password change.
5. Disable bootstrap, remove its username/password from server configuration, and restart. Expected: login with the changed password still works.
6. On a separate fresh staging database, try a missing/invalid password or a username already owned by an ordinary user. Expected: startup fails, and no new account, promotion or partial role/audit change is committed. Correct the configuration and retry. Existing SuperAdmin or successful bootstrap audit records cause an intentional no-op instead of credential validation.

The operation uses an exclusive transaction-owned SQL application lock to serialize bootstrap across API instances. Existing SuperAdmin assignments or a prior successful bootstrap audit entry prevent further creation, even if the administrator later loses its role. Account recovery and additional production users use the normal administrator workflow; this is not a password-reset mechanism. Audit actor is null because no human session exists at startup, with the explicit `production-admin-bootstrap` correlation ID.

Please verify and approve this bootstrap before using it against the live database. Implementation has not accessed or modified production data.
