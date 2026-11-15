# Architecture & Design Decisions

This document explains *what* was built, *why*, and which trade-offs were accepted, so each decision can be defended in review.

## 1. Solution layout and dependency rule

```
src/
  Verus.Companies.Domain          Company entity + business constants. Depends on nothing.
  Verus.Companies.Application     Use cases, DTOs, ports (interfaces), validation, relevance, search.
  Verus.Companies.Infrastructure  Adapters: in-memory repository, HTTP website inspector.
  Verus.Companies.Api             Thin controllers, exception handling, composition root.
tests/Verus.Companies.Tests       Unit + HTTP-level tests, plus architecture tests.
client/                           Angular app.
```

Dependencies point inward only: `Api -> Application -> Domain`, and `Infrastructure -> Application`. The Application layer *defines* the interfaces it needs (`ICompanyRepository`, `IWebsiteMetadataProvider`) and Infrastructure *implements* them (Dependency Inversion). The Api project is the only place that knows both sides (composition root).

**Why separate projects rather than folders?** The compiler enforces the boundaries, so a shortcut like a controller reaching into the store does not compile. `ArchitectureTests` additionally asserts the rules that references alone cannot (Application uses no ASP.NET Core types, Domain depends on nothing, the in-memory store is not public, the service contract never exposes domain types).

**Honest trade-off:** four projects is more ceremony than a 3-endpoint exercise strictly needs. It was chosen because the brief emphasises extensibility ("a database can be introduced later with minimal changes") and layering; the cost is a few extra files.

**Note on the Application framework reference:** Application references the ASP.NET shared framework only to get `Microsoft.Extensions.*` abstractions (logging, options, DI) without extra packages. No ASP.NET Core types are used; the architecture test enforces that. Switching to the standalone `Microsoft.Extensions.*.Abstractions` NuGet packages is a csproj-only change.

## 2. Request flow (POST /api/companies)

1. **ASP.NET model binding + DataAnnotations** reject malformed JSON and shape errors with `400 ValidationProblemDetails`.
2. **`CompaniesController`** calls `ICompanyService.CreateAsync` and returns `201 Created` + `Location`. No logic, no try/catch.
3. **`CompanyService`** re-validates the same attributes (defence in depth: the service enforces its own rules for any caller), then checks **relevance**, then builds the `Company` entity and saves it through `ICompanyRepository`.
4. Failures are thrown as `RequestValidationException`; **`GlobalExceptionHandler`** is the single place that turns exceptions into RFC 7807 responses (400 for validation, generic 500 otherwise, with the real error logged and never leaked).

## 3. API design

| Operation | Route | Success | Errors |
|---|---|---|---|
| Create | `POST /api/companies` | `201` + `Location` + body | `400` validation / relevance |
| List all | `GET /api/companies` | `200` (`[]` when empty) | |
| Filter by name | `GET /api/companies?name=acm` | `200` | |
| Filter by domain | `GET /api/companies?domain=acme.com` | `200` | |
| Relevance search | `GET /api/companies?q=acme` | `200`, best match first, `searchScore` populated | |
| Get one | `GET /api/companies/{id}` | `200` | `404` |

Decisions:
- **Search is a query string on the collection, not a `/search` sub-route.** Filtering a collection is still the same resource; this is the conventional REST shape and composes (`?name=a&domain=b`, filters are ANDed).
- **Empty result is `200 []`, not `404`.** A 404 means the *resource* does not exist; an empty search is a valid answer. Only `GET /{id}` returns 404.
- **`{id:guid}` route constraint.** A malformed id does not match the route and yields 404, which is accurate (no such resource) and avoids a second error path.
- **DTOs everywhere.** `CreateCompanyRequest` / `CompanyResponse` are the only external contracts; the `Company` entity and storage structures never leave the application layer (asserted by a test).
- **Errors use `application/problem+json`** (RFC 7807) with a `detail` summary and an `errors` map keyed by field (`Name`, `WebsiteUrl`, `Relevance`).
- **`CompanyQuery` is the extension seam.** Pagination (`page`, `pageSize`), sorting (`sort`) or new filters are added as members of that record; service and controller signatures keep their shape.
- **Duplicates are not rejected.** The brief does not define uniqueness and specifies `201/200` or `400` only. A uniqueness rule (e.g. same normalized domain -> `409 Conflict`) would be added in `CompanyService` and needs no structural change.
- **No `UseHttpsRedirection` in Development**, so the documented `http://localhost:5000` works with curl/Postman/Angular without redirects or dev-certificate friction; it is enabled outside Development.

## 4. Validation strategy

Two tiers, deliberately separated:

| Tier | Rules | Where |
|---|---|---|
| **Shape** | Name required, 3-100 chars after trimming; URL required, absolute `http(s)`, real DNS hostname with a dot, no credentials/IP/localhost | DataAnnotations on the DTO (`CompanyName`, `HttpUrl`), with the URL rule in one `WebsiteUrlParser` |
| **Semantic** | Name must be relevant to the website | `INameWebsiteRelevanceEvaluator` |

- The numeric limits live once, in `CompanyRules` (Domain). The `Company` constructor also enforces them, so an invalid entity cannot exist regardless of caller (invariants at the core, validation at the boundary).
- "At least a few characters" is interpreted as **3**. This rejects names like "3M"/"HP"; it is a single constant if the business wants 2.
- Messages are user-facing ("Company name must be at least 3 characters long."), and all shape errors are reported together, not one at a time.
- The frontend only checks "required"; the backend is the single source of truth for every other rule, so the two cannot drift. Its messages are displayed verbatim.

## 5. Relevance strategy (name vs. website)

Goal: *modern, reasonable, explainable*. The design is a set of independent **scorers** (`IRelevanceScorer`), each producing a 0-1 score **with a human-readable reason**, composed by `NameWebsiteRelevanceEvaluator`.

**Normalization (shared):** lower-case, strip accents, drop apostrophes/`&`, split on punctuation, drop legal-form noise (`Inc`, `LLC`, `GmbH`, `Ltd`, `The`...). So "Café Müller GmbH" and `cafe-muller.de` compare as `cafe muller` vs `cafe muller`.

**1. `DomainSimilarityScorer` (offline, deterministic, always runs first).** The brand label of the registrable domain (`shop.acme-tools.co.uk` -> `acme-tools`) is compared with the name. Score = best of:
- *word coverage*: share of the name's words contained in (or typo-close to, Levenshtein >= 0.8) the label;
- *acronym*: label equals the initials of the name (International Business Machines -> `ibm`);
- *overall similarity*: normalized Levenshtein of the compacted strings.

**2. `WebsiteContentScorer` (content inspection, runs only if step 1 is not convincing).** Fetches the page and checks whether the name's words appear in `<title>` / `og:site_name` (full weight) or the description (x0.8, since long text matches words more easily).

**Composition.** Scorers run cheapest-first and evaluation stops as soon as one reaches the threshold. The final score is the **best** signal ("relevant if any evidence supports it"). Threshold default `0.5` (`Relevance:MinimumScore`).

**Why max, not an average?** An average would punish legitimate cases where the domain and the page disagree (Alphabet at `abc.xyz`: the domain scores ~0, the page title scores 1.0). The cost is that one weak-but-lucky signal can pass; the threshold and the per-scorer discounts mitigate that.

**Explainability.** A rejection reads: *"Company name 'Acme Corp' does not appear to be related to 'example.com' (relevance 0.29, required 0.50)."* The log additionally records each signal and its reason.

**Content inspection safety (it makes outbound requests on behalf of a user-supplied URL):**
- **SSRF protection in the socket connect callback**, so it is applied to the address actually connected to, for the first request and every redirect. A pre-flight "is this URL safe?" check would miss redirects and DNS rebinding. Loopback, private, link-local (cloud metadata `169.254.169.254`), CGNAT, multicast and IPv4-mapped IPv6 are blocked.
- Hard **timeout (3 s)**, **size cap (256 KB)**, HTML content types only, max 3 redirects.
- **Fail-soft:** any failure yields "no opinion" and the domain result stands; a slow or hostile site cannot break submissions.
- Can be turned off entirely (`WebsiteInspection:Enabled=false`) for offline use.

**Known limitations (state them openly):**
- The registrable-domain logic is a small heuristic (known second-level labels under 2-letter ccTLDs), not the full Public Suffix List, to avoid a dependency. Unusual suffixes can mis-identify the brand label.
- Pure string matching cannot know that unrelated-looking domains belong to a company (a rebrand, a holding company). Content inspection covers some of that; an embeddings/LLM scorer is the natural next step and would be one more `IRelevanceScorer` registration.
- Content parsing uses timeout-guarded regexes rather than a full HTML parser, and assumes UTF-8.
- JavaScript-rendered sites expose little metadata without executing scripts.

## 6. Search and ordering (`GET ?q=`)

Separate from submission-time relevance: this *ranks stored companies against a query*. Each query word scores against the name (and the domain label, weighted 0.8): exact word 1.0, word prefix 0.85 (type-ahead), substring 0.6, typo-tolerant match = similarity x 0.6; the field score is the mean over query words, a full-string match scores 1.0. Results below 0.3 are excluded and the rest are ordered by score, then name. The score is returned as `searchScore` so ordering is transparent.

Filtering and ranking run in the application layer over the repository's full list. Honest scaling note: with a database you would push `name`/`domain` filters down into the repository query (`FindAsync(criteria)`) and rank only the candidate set; relevance scoring itself is not expressible in SQL.

## 7. Persistence

- `ICompanyRepository` (Application) is async with `CancellationToken`, even though memory is synchronous, so a database adds no signature changes.
- `InMemoryCompanyRepository` is `internal`, a singleton, backed by `ConcurrentDictionary`, and returns snapshots ordered by creation time.
- **`Company` is immutable**, so sharing instances across threads is safe without locks, and "update" later means "replace with a new instance".
- **Swapping to a database:** add a project with e.g. an EF Core repository implementing `ICompanyRepository` and change one registration in `Infrastructure/DependencyInjection.cs`. Nothing in Domain, Application or Api changes.

## 8. Logging and error handling

- Structured logging (`ILogger`, message templates with named properties) of: company created (id, domain, score), relevance rejection (as a warning, with all signals), search (filters + result count), not-found, content-inspection skips/timeouts, validation failures, and unhandled exceptions (error, with stack trace).
- Exceptions only for exceptional/flow-breaking cases; expected absence is `null` (`GetByIdAsync` -> 404). Validation uses an exception translated centrally. **Trade-off:** a `Result<T>` type avoids exceptions as control flow and makes failures explicit in signatures, at the cost of more plumbing; for a low-volume 400 path the simpler model was preferred, and swapping is localized to the service and the handler.
- Client cancellation is handled (no error log, status 499).

## 9. Testing

| Layer | What is verified |
|---|---|
| Unit | Text normalization, domain parsing, URL rules, each scorer, evaluator composition (short-circuit, best-signal, no-signal), service create/get/search behaviour, HTML metadata parsing, SSRF address classification, in-memory repository (incl. concurrency) |
| Architecture | Layering rules listed in section 1 |
| HTTP | Real pipeline via `WebApplicationFactory`: 201/Location, 400 shapes, relevance 400 message, malformed JSON, 200-empty, 200/404 by id, filters and ranked search, health. Network access is replaced by a fake provider |
| Frontend | API service, error-message mapping, form (required, success, server error), app (load, empty state, search) |

Time is injected (`TimeProvider`) and content inspection sits behind an interface, so tests are deterministic and offline.

## 10. Frontend

Angular standalone components with signals; no state library. `CompanyApi` is the only class that knows the HTTP shape; components stay presentational. Success and server validation messages are shown directly under the form; the list supports search and shows a relevance column only for `q` searches.

## 11. Extending the system

| Change | Where |
|---|---|
| Update / delete | New service methods + `PUT/PATCH/DELETE /api/companies/{id}`; repository gains `UpdateAsync`/`DeleteAsync`. Entity stays immutable (replace) |
| Pagination / sorting | Add `Page`, `PageSize`, `Sort` to `CompanyQuery`; return a paged envelope or `X-Total-Count`/`Link` headers |
| Database | New repository implementation + one DI line (section 7) |
| Duplicate protection | Rule in `CompanyService`, `409 Conflict` |
| Better relevance | New `IRelevanceScorer` (embeddings, LLM, Public Suffix List) registered in `AddApplication` |
| Auth, rate limiting | Middleware in the Api project only (content inspection is the abuse-prone endpoint) |

## 12. Decision log (quick reference)

| Decision | Alternative | Why this one |
|---|---|---|
| Clean architecture, 4 projects | Single project with folders | Compiler-enforced boundaries; DB swap is trivial |
| Controllers (thin) | Minimal APIs | Brief refers to controller layers; attributes document the contract |
| DataAnnotations + service re-validation | FluentValidation | Brief mentions data annotations; no extra dependency; one definition used at both boundaries |
| Exceptions for validation | `Result<T>` | Simpler; translated in one place |
| Max-of-signals relevance | Weighted average | Handles legitimate domain/content disagreement |
| Fail-soft content inspection | Fail-closed | A slow site must not block or break submissions |
| Immutable entity + `ConcurrentDictionary` | Locks around mutable objects | Thread-safe by construction |
| `200 []` for empty results | `404` | Absence of matches is not absence of a resource |
| No duplicate rejection | `409` on same domain | Not in the contract; trivial to add |

## Appendix: suggested commit sequence

1. `chore: solution skeleton, props, gitignore, README stub` - root files, `*.csproj`, `Program.cs`, `launchSettings.json`
2. `feat(domain): company entity and rules` - `Domain/`
3. `feat(app): contracts, ports and in-memory repository` - `Application/Abstractions`, `Application/Contracts`, `Infrastructure/InMemoryCompanyRepository.cs`, repository tests
4. `feat(app): request validation` - `Application/Validation`, validation tests
5. `feat(app): name-to-website relevance` - `Application/Relevance`, relevance tests
6. `feat(infra): website content inspection with SSRF guard` - `Infrastructure/{SsrfGuard,HtmlMetadataParser,HttpWebsiteMetadataProvider,...}`, their tests
7. `feat(app): company service with filtering and ranked search` - `Application/Services`, `Application/Search`, `DependencyInjection.cs`, service tests
8. `feat(api): REST endpoints and problem-details error handling` - `Api/`, API + architecture tests
9. `feat(client): Angular form, list and search` - `client/`
10. `docs: README and architecture notes` - `README.md`, `docs/`
