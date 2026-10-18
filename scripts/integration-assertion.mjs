// Operator/developer utility, not a test. Never commit or print a private key.
import { readFileSync } from 'node:fs'
import { randomUUID, sign } from 'node:crypto'
const [clientId, audience, keyId, privateKeyPath] = process.argv.slice(2)
if (!clientId || !audience?.startsWith('https://') || !keyId || !privateKeyPath) {
  console.error('Usage: node scripts/integration-assertion.mjs CLIENT_ID HTTPS_AUDIENCE KEY_ID PRIVATE_KEY_PATH')
  process.exit(1)
}
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url')
const now = Math.floor(Date.now() / 1000)
const unsigned = `${encode({ alg: 'RS256', typ: 'wros-client+jwt', kid: keyId })}.${encode({ iss: clientId, sub: clientId, aud: audience, iat: now, nbf: now - 1, exp: now + 90, jti: randomUUID() })}`
try {
  const signature = sign('RSA-SHA256', Buffer.from(unsigned), readFileSync(privateKeyPath)).toString('base64url')
  process.stdout.write(`${unsigned}.${signature}`)
} catch {
  console.error('Unable to sign assertion. Check the private key file and permissions.')
  process.exit(1)
}
