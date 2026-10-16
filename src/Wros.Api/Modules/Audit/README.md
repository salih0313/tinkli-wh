# Audit — Phase 1

Append-only security and account-management events with UTC timestamps, server-validated actor identity, safe explicit change projections and correlation IDs. Anonymous login attempts and migration seeding have no authenticated actor. No audit edit/delete API exists. Audit identifiers intentionally remain historical references without cascading foreign keys.

User-management and permission mutations persist their audit events in the same SQL transaction. Identity owns its password verification and lockout counter operations; failed/successful authentication is audited without credential payloads. No business-module auditing is implemented before those modules exist.
