# Identity — Phase 1

ASP.NET Core Identity, cookie sessions, database permission policies and administration. Roles, permissions and optional Development users are inserted by the SeedIdentityData EF migration. No startup/standalone seed service or appsettings account credentials remain. See root README for migration environment variables, strong password prompts and the one-time opt-in behavior.

All warehouse access remains global. Staff roles initially receive only inventory.view, as approved by the owner. Role grants are checked live. The data migration skips existing accounts/roles and never resets passwords or overwrites edited staff grants. SuperAdmin administration requires that role, independent of grants assigned to staff.
