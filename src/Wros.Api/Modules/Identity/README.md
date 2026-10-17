# Identity — Phase 1

ASP.NET Core Identity, cookies, permission policies and administration. Roles/permissions come from the data migration. DevelopmentUserSeed.cs runs on EF database update through UseSeeding and UseAsyncSeeding, including when no migrations are pending. No opt-in flag, seed environment variables, appsettings account credentials or startup seeding are used.

Set the two passwords in the ignored DevelopmentSeedCredentials.local.cs C# file. The empty tracked example is the fallback for clean clones. Existing users, passwords, role memberships and staff grants are preserved; new users and audit entries commit atomically. Outside Development no demo-user callback is registered. See the root README for the single database-update command.

All warehouse access remains global. Staff initially receive only inventory.view. SuperAdmin administration additionally requires the SuperAdmin role, independent of grants assigned to staff.
