# E-commerce integration

Separate IntegrationAssertion authentication validates configured RS256 public keys, exact client/audience/time/type, single-use jti and minimal server scopes. Clients also require active registry entries. Staff cookies never substitute for machine credentials. ProductMapping is explicit per caller; variants remain owned by e-commerce.

InventorySyncSnapshot/Line materialize a serializable immutable full positive-physical-balance capture, ordered rows, SHA256 and monotonic capture generation. ExternalOrderDeduction/Line provide atomic selected-lot stock-out, unique client/event and client/order identities, safe identical retry and immutable ledger/audit. No legacy source changes or checkout reservations. Read docs/e-commerce-integration.md before configuring any client. Expired snapshot/replay retention is currently operator-planned, not automatically purged.
