# Salenix_Crypto — external library plan

Date: 2026-11-11. Status: APPROVED and BUILT 2026-11-11 (all 4 extra functions, NodaTime 3.3.7, no JWT sign). Code in `crypto/`, 84 unit tests passed, zip `crypto/dist/Salenix_Crypto.zip` (not uploaded). Rebuild: `powershell -ExecutionPolicy Bypass -File .\crypto\pack.ps1`.

## 0. What the docs say (checked 2026-11-11)
| Topic | Current state | Source |
|---|---|---|
| .NET version | **.NET 10** (available in ODC since 2026-06-22). .NET 8 support **ended 2026-11-10** → do not use net8.0. | Upgrading custom code libraries to a new .NET version |
| NuGet package | `OutSystems.ExternalLibraries.SDK` 1.7.0 (2026-08-28) | nuget.org |
| Attributes | One `[OSInterface]` per library, on a public interface; implemented by a public class with a public parameterless constructor; every public interface method = server action; `[OSAction]` (ReturnName, ReturnType, Description), `[OSParameter]` (DataType, Description), `[OSStructure]` / `[OSStructureField]` | SDK reference |
| Types | string, int, long, bool, byte[], decimal, float, double, DateTime, OSStructure structs, lists; optional params via default values. `OSDataType.Date` exists; the docs do not show a DateTime → Date example, so the smoke test in step 7 checks it. | SDK reference |
| Runtime | Linux container, stateless (no state in class fields), each call is an HTTPS request, input payload limit 5.5 MB, no assumptions about assembly paths | SDK readme, best practices |
| Packaging | `dotnet publish -c Release --no-self-contained`; zip the CONTENT of `bin/Release/net10.0/publish/` (files at zip root) | SDK readme |
| Portal | Integrate → External Logic → Create library → upload ZIP → Continue → (View errors) → Review file contents → Publish library → release a version | Extend your apps with custom code |

Your machine already has .NET SDK **10.0.300 and 10.0.401** (`dotnet --list-sdks`) → no install needed.

## 1. What the source really uses (differences from the BRD / request)
1. **Ask Mira does not use a JWT library.** `source/src/lib/askmira/core.ts` has a hand-written **HS256 verify only** (never signs): header `alg` must be `HS256`; signature = HMAC-SHA256(secret, `header.payload`) compared with `timingSafeEqual`; claims `iss = "askmira"`, `aud` contains `"askmira:saleinx"`, `exp` required, `iat` optional, leeway 10 s; then `use` (provision/query/act/revoke/launch), `jti` (spent once via AskMiraUsedToken), `bh` = SHA-256 hex of the raw request body, `sub` (server calls) / `tid` (launch). The per-workspace secret is `derive()` = HMAC-SHA256 **hex** with key = server secret (`ASKMIRA_LINK_SECRET` or `AUTH_SECRET`, ≥ 32 chars) over `askmira-saleinx:v1:{tenantId}:{nonce}`; that hex string is then used as the **UTF-8 key** of the token HMAC. `peek()` reads claims before verification to choose the secret.
   → Needed: **JwtVerifyHs256 + JwtPeekClaims; no JWT sign.**
2. `source/src/lib/auth/jwt.ts` (jose, SignJWT/jwtVerify) is the app's own login session cookie. ODC replaces it with its own auth → **not needed**.
3. Ask Mira connect uses **PKCE**: challenge = SHA-256 of the verifier as **base64url** (not hex) → extra function `Sha256Base64Url`.
4. Ask Mira `launch` gives the browser a Saleinx session from a token. ODC cannot create an ODC login from a foreign token → OPEN design question for the Ask Mira phase (not for this library).
5. **Dedup** (`source/src/lib/dedup/queries.ts`) uses `GREATEST(similarity(stored, typed), word_similarity(typed, stored)) >= 0.55` plus exact match; the contact query uses `similarity` only. There is no "prefix" match (CLAUDE.md decision 9) → extra function **WordSimilarity** (same pg_trgm algorithm). Recorded as OPEN in CLAUDE.md.
6. `NormalizedName` = `normalizeCompanyName` (NFKD, strip accents, lower, non `[a-z0-9]` → space, collapse spaces, drop legal suffix tokens ltd/limited/pvt/…; fallback if all tokens are suffixes). NFKD is not possible in OutSystems → recommended extra functions **NormalizeCompanyName / NormalizeContactName** (port 1:1).
7. Invitations (`source/src/lib/auth/tokens.ts`): token = 32 random bytes base64url, stored hash = SHA-256 hex → `NewToken(32)` + `Sha256Hex`.
8. Later (Comms phase, not in this build): Gmail tokens are encrypted with AES-256-GCM (`source/src/lib/email/gmail/crypto.ts`) — a future `AesGcmEncrypt/Decrypt` belongs in this same library.

## 2. Design
- Solution folder `C:\Users\Hp\salenix\crypto\`: `Salenix_Crypto\` (classlib, net10.0) and `Salenix_Crypto.Tests\` (xUnit, net10.0).
- Packages: `OutSystems.ExternalLibraries.SDK` 1.7.0, `NodaTime` (latest 3.x stable, exact version pinned on build day). NodaTime carries its own IANA database, so `Asia/Kolkata` works the same on Windows (tests) and on the Linux container, without relying on the container's tzdata.
- `[OSInterface(Name = "Salenix_Crypto")] public interface ISalenixCrypto`, class `SalenixCrypto : ISalenixCrypto`, no fields (stateless). Pure logic lives in internal static classes so tests can pass a fixed "now".
- Errors: `throw new Exception("CRYPTO_ERROR: <text>")` (same `UPPER_SNAKE: ` pattern as decision 13); unknown zone → `TIMEZONE_UNKNOWN: <zone>`. Callers keep the usual two handlers.
- All strings are UTF-8 before hashing (same as Node).

## 3. Functions
| # | Signature (C#) | OutSystems output | Called by (Salenix action) |
|---|---|---|---|
| 1a | `DateTime ConvertUtcToZone(DateTime UtcDateTime, string IanaZone)` | LocalDateTime (Date Time) | display/report helpers that need tenant-local time (Performance periods, Sales close-date slip, timers that run "at local midnight") |
| 1b | `[OSAction(ReturnType = OSDataType.Date)] DateTime TodayIn(string IanaZone)` | Today (Date) | **Foundation_TodayFor(TenantId)** (reads OrganizationSettings.Timezone, default Asia/Kolkata) — every "today" in all apps goes through it |
| 2a | `string NewToken(int Bytes = 32)` | Token | Foundation Invitation_Create (F6); AskMira_ConnectApprove (connect code); AskMira_ConnectSwap (secret nonce) |
| 2b | `string Sha256Hex(string Value)` | Hash | Invitation_Create / Invitation_Accept (OrgInvitation.TokenHash); AskMira connect code hash; AskMira body hash check (`bh`) |
| 2c | `string Sha256Base64Url(string Value)` *(added, source needs it)* | Hash | AskMira_ConnectSwap (PKCE challenge) |
| 3a | `string HmacSha256Hex(string Key, string Message)` | Signature | AskMira_DeriveSecret(TenantId, Nonce) with secret Setting AskMiraLinkSecret |
| 3b | `bool ConstantTimeEquals(string A, string B)` | IsEqual | AskMira PKCE compare, `bh` compare; any token compare |
| 4a | `[OSParameter(Decimals = 8)] decimal TrigramSimilarity(string A, string B)` | Similarity | Sales Company_FindMatches, Contact_FindMatches, dedup scan job |
| 4b | `decimal WordSimilarity(string Needle, string Haystack)` *(added, source needs it)* | Similarity | Company_FindMatches, dedup scan job (score = Max(4a, 4b)) |
| 5a | `JwtVerifyResult JwtVerifyHs256(string Token, string Secret, string Issuer, string Audience, int LeewaySeconds = 10)` | Result {IsValid, Reason, ClaimsJson} | AskMira_VerifyAssertion (exposed REST endpoints of Salenix_AskMira) |
| 5b | `string JwtPeekClaims(string Token)` | ClaimsJson ("" if malformed) | AskMira_VerifyAssertion (pick the workspace before verify) |
| 6 | `string NormalizeCompanyName(string Name)`, `string NormalizeContactName(string FirstName, string LastName)` *(recommended)* | Normalized | Sales Company_Create/Update, Contact_Create/Update, Company/Contact_FindMatches |

### Behaviour details
- **1a**: input treated as UTC whatever its Kind (Local → ToUniversalTime; Unspecified → SpecifyKind Utc); output Kind Unspecified (wall clock of the zone). Empty or unknown zone → `TIMEZONE_UNKNOWN: …`. Only IANA names (Windows names like "India Standard Time" are refused, same on every OS).
- **1b**: `ConvertUtcToZone(UtcNow, zone).Date`.
- **2a**: `RandomNumberGenerator.GetBytes(Bytes)` → base64url without padding (32 bytes → 43 chars, same as Node `toString('base64url')`). Bytes outside 16..64 → `CRYPTO_ERROR: Bytes must be between 16 and 64.`
- **2b/2c**: SHA-256 of UTF-8; hex lower case / base64url no padding.
- **3a**: key = UTF-8 bytes of Key (same as Node `createHmac('sha256', string)`); hex lower case. Empty key → CRYPTO_ERROR.
- **3b**: different byte length → False at once (same as source `safeEqual`); else `CryptographicOperations.FixedTimeEquals`.
- **4a** (pg_trgm `similarity`): lower case; split into words on every non letter/digit char; each word padded `"  " + w + " "`; all 3-char windows; set of distinct trigrams per string; result = common / (n1 + n2 − common), computed as float like Postgres (float4) and returned as decimal rounded to 8 places; either set empty → 0.
- **4b** (pg_trgm `word_similarity(needle, haystack)`): port of `calc_word_similarity` / `iterate_word_similarity` (non-strict mode) from `contrib/pg_trgm/trgm_op.c`: best ratio between the needle's trigram set and any continuous extent of the haystack's ordered trigrams.
- **5a**: split on "." (exactly 3 parts) → header must parse and have `alg = HS256` → HMAC-SHA256(UTF-8 Secret, `p0.p1`) fixed-time compared with base64url-decoded p2 → payload JSON object → `iss = Issuer`, `aud` (string or array) contains Audience, `exp` numeric and `exp + leeway >= now`, `iat` (if present) `iat - leeway <= now`. Reasons: `malformed`, `bad_alg`, `bad_signature`, `bad_issuer`, `bad_audience`, `expired`, `not_yet_valid`. On success ClaimsJson = payload JSON; the AskMira action checks `use`, `jti`, `bh`, `sub`/`tid` itself (business rules stay in OutSystems).

## 4. Unit tests (xUnit, `dotnet test`)
**Timezone**
- `ConvertUtcToZone(2026-11-10 20:00 UTC, "Asia/Kolkata")` = 2026-11-11 01:30.
- `ConvertUtcToZone(2026-11-11 05:00 UTC, "UTC")` = 2026-11-11 05:00.
- DST: `America/New_York` 2026-03-08 06:30 UTC = 01:30; 07:30 UTC = 03:30.
- Kind Unspecified input gives the same result as Kind Utc.
- `TodayIn("Asia/Kolkata", now = 2026-11-10 18:29:59 UTC)` = 2026-11-10; `now = 18:30:00 UTC` = 2026-11-11 (internal overload with now).
- `"Mars/Base"`, `""`, `"India Standard Time"` → TIMEZONE_UNKNOWN.

**Tokens / hashes**
- `NewToken(32)`: length 43, only `[A-Za-z0-9_-]`, 1000 calls all different; `NewToken(8)` and `NewToken(65)` → CRYPTO_ERROR.
- `Sha256Hex("")` = `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; `Sha256Hex("abc")` = `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`; one non-ASCII value (e.g. `"₹"`) checked against Node output.
- `Sha256Base64Url`: RFC 7636 appendix B vector — verifier `dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk` → `E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM`.

**HMAC / compare**
- RFC 4231 case 2: key `Jefe`, message `what do ya want for nothing?` → `5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843`.
- `derive()` vector produced by running the source's Node code once on build day (same key/tenant/nonce must give the same hex).
- `ConstantTimeEquals`: equal → True; same length different → False; different length → False; both empty → True.

**Trigram (10 required pairs + 2 edge cases)**
Expected values come from a line-by-line port of pg_trgm (`trgm_op.c`) run on 2026-11-11 (no Postgres on this machine). The trigram sets and the 0.8 word_similarity of `word`/`two words` are the example printed in the PostgreSQL pg_trgm docs. Optional confirmation on any Postgres: `CREATE EXTENSION pg_trgm; SELECT similarity(a,b), word_similarity(a,b) FROM (VALUES ('word','two words'), …) v(a,b);`
Tolerance 0.000001.

| # | A | B | similarity | word_similarity(A, B) |
|---|---|---|---|---|
| 1 | word | two words | 0.36363637 | 0.8 |
| 2 | acme | acme | 1 | 1 |
| 3 | acme | acme corp | 0.5 | 1 |
| 4 | tata steel | tata steels | 0.76923078 | 0.90909094 |
| 5 | infosys | infosis | 0.45454547 | 0.625 |
| 6 | reliance industries | reliance industry | 0.72727275 | 0.8 |
| 7 | hdfc bank | icici bank | 0.33333334 | 0.5 |
| 8 | abc | xyz | 0 | 0 |
| 9 | larsen toubro | larsen and toubro | 0.77777779 | 0.77777779 |
| 10 | Wipro-Tech | wipro tech | 1 | 1 |
| 11 | "" | "" | 0 | 0 |
| 12 | ab | abc | 0.4 | 0.66666669 |

Note pair 5: similarity 0.45 is below 0.55 but word_similarity 0.625 is above → the source flags "infosis" as a duplicate of "infosys" only because of word_similarity.

**JWT**
- Token signed in the test (HMAC helper) with iss/aud/exp → IsValid, ClaimsJson contains `sub`.
- One changed payload char → bad_signature; wrong secret → bad_signature; `alg: none` → bad_alg; 2 parts → malformed; exp = now − 11 → expired; exp = now − 9 → valid (leeway); iat = now + 11 → not_yet_valid; aud as array containing Audience → valid; wrong iss → bad_issuer.
- One token produced by Node `crypto.createHmac` (source style) verifies in C#.
- `JwtPeekClaims("a.b")` = "".

**Normalize** (if 6 approved): `"Acmé Pvt. Ltd."` → `acme`; `"Ltd"` → `ltd`; `"  Tata   Steel Limited "` → `tata steel`; contact `("José", "O'Neil")` → `jose o neil`.

## 5. Your steps (Windows)
1. **.NET SDK**: already installed (10.0.300, 10.0.401). Check: `dotnet --list-sdks`. (Only if missing: `winget install Microsoft.DotNet.SDK.10`.)
2. **Code**: I create `C:\Users\Hp\salenix\crypto\` with both projects after you approve this plan.
3. **Tests**:
   ```powershell
   cd C:\Users\Hp\salenix\crypto
   dotnet test
   ```
   All green before packaging.
4. **Build + publish**:
   ```powershell
   dotnet publish .\Salenix_Crypto\Salenix_Crypto.csproj -c Release --no-self-contained
   ```
   Output folder: `C:\Users\Hp\salenix\crypto\Salenix_Crypto\bin\Release\net10.0\publish\`
5. **Zip** (files at the zip root, not the folder):
   ```powershell
   New-Item -ItemType Directory -Force .\dist | Out-Null
   Compress-Archive -Path .\Salenix_Crypto\bin\Release\net10.0\publish\* -DestinationPath .\dist\Salenix_Crypto.zip -Force
   ```
   Zip lands at `C:\Users\Hp\salenix\crypto\dist\Salenix_Crypto.zip`. (I will also add a `pack.ps1` that runs test → publish → zip.)
6. **ODC Portal** (`vtpl.outsystems.dev`): Integrate → External Logic → **Create library** → upload `Salenix_Crypto.zip` → **Continue** → if errors: **View errors**, send them to me → **Review file contents** (check the 13 actions + JwtVerifyResult structure) → **Publish library** → wait for **Finished successfully** → release the version (Version history tab). Later updates: same screen, upload the new zip on the existing library.
7. **Smoke test in Development**: add the reference by hand in ODC Studio (Salenix_DevTools first, fresh copy, reference change only, publish from Studio). One TestConsole section calls TodayIn("Asia/Kolkata"), ConvertUtcToZone(CurrDateTime(), "Asia/Kolkata"), Sha256Hex("abc"), TrigramSimilarity("infosys","infosis"). Checks: TodayIn returns a Date (not shifted by a day), ConvertUtcToZone is +5:30 from UTC (ODC does not re-shift the returned value).
8. Then: reference in Salenix_Foundation (by hand) → Mentor builds Foundation_TodayFor, later invitations.

## 6. Open questions for you
1. Approve the 4 additions the source needs: Sha256Base64Url, WordSimilarity, JwtPeekClaims, NormalizeCompanyName/NormalizeContactName.
2. NodaTime (own tz database, recommended) vs plain `TimeZoneInfo` (depends on the container's tzdata, no extra package).
3. JWT sign is not built (source never signs). OK?
