# Study guide: learn this project well enough to defend it

You didn't write every line yourself, so an interviewer's second or third follow-up question is where this project either helps or hurts you. This guide takes about **6 to 8 hours** over 2 or 3 days. Do it in order. Each step says what to read, what to understand, and a small change to make yourself, because changing code is how you find out whether you understood it.

Tick each box only when you can explain it out loud without looking.

---

## Day 1: the backend (about 3 to 4 hours)

### Step 1. Run it and watch one request (30 min)

- [ ] Start Postgres and the API (README, "Local development"), open `http://localhost:5080/swagger`.
- [ ] Upload any image through Swagger with the Fake provider. Look at the JSON that comes back.
- [ ] Open the database (`psql` or DBeaver) and look at the `invoices` and `invoice_files` tables. Find the `Flags` jsonb column.

**You should be able to say:** "A request goes controller → service → extractor → validator → EF Core → Postgres."

### Step 2. The domain layer: `backend/src/GstInvoice.Core` (60 min)

Read in this order:

1. `Models/ExtractedInvoice.cs`: why is every field nullable?
2. `Validation/GstinValidator.cs`: the GSTIN structure and the mod-36 checksum.
3. `Validation/InvoiceValidator.cs`: each rule, and why problems are flags, not exceptions.
4. `Extraction/IInvoiceExtractor.cs`, `FakeInvoiceExtractor.cs` and `GeminiInvoiceExtractor.cs`: one interface, three providers. Compare how Claude (SDK) and Gemini (raw REST + typed HttpClient) are called.

- [ ] Explain the GSTIN checksum on paper for `27AAPFU0939F1ZV`.
- [ ] Explain why CGST+SGST means intra-state and IGST means inter-state.
- [ ] **Change it yourself:** add a rule that flags an invoice whose total is over ₹10 lakh as a warning. Add a test for it in `InvoiceValidatorTests.cs` and run `dotnet test`.

### Step 3. The AI call: `ClaudeInvoiceExtractor.cs` (45 min)

- [ ] What is in the request: the file block (image or PDF), the instructions, and `OutputConfig` with `Effort` and a JSON schema.
- [ ] What "structured outputs" guarantees (the JSON parses and matches the schema) and what it does NOT guarantee (that values are correct).
- [ ] Why `additionalProperties: false` and every field listed in `required`, with `null` allowed.
- [ ] How errors are handled: rate limit, API error, `refusal` and `max_tokens` stop reasons, bad JSON.
- [ ] Where the API key comes from (environment variable read by `AnthropicClient`), and why it is never in `appsettings.json` or the Angular app.
- [ ] **Change it yourself:** add a `PlaceOfSupply` field end to end: schema, record, entity, migration (`dotnet ef migrations add AddPlaceOfSupply -p src/GstInvoice.Api -s src/GstInvoice.Api -o Data/Migrations`), DTO, form.

### Step 4. The API layer: `backend/src/GstInvoice.Api` (60 min)

1. `Program.cs`: dependency injection, choosing the provider from config, `DATABASE_URL` parsing, serving the Angular build from wwwroot, CORS, health check, migrations on startup.
2. `Controllers/InvoicesController.cs`: upload limits, content types, ProblemDetails errors, status codes (201, 404, 409, 415, 502).
3. `Services/InvoiceService.cs`: the upload flow, the duplicate check, approve rules.
4. `Data/AppDbContext.cs` and `Data/Invoice.cs`: precision on money, jsonb flags, the separate file table, indexes.

- [ ] Explain service lifetimes: why `InvoiceValidator` is a singleton and `InvoiceService` is scoped (the DbContext is scoped).
- [ ] Explain why money is `decimal`, never `double`.
- [ ] Explain why list queries never touch file bytes.
- [ ] **Change it yourself:** add `GET /api/invoices?status=Approved` filtering.

### Step 5. Tests (30 min)

- [ ] Read all four test files. Notice the service tests use the EF Core in-memory provider and the fake extractor.
- [ ] Know the limitation: the in-memory provider is not real Postgres (no jsonb, no constraints). The honest next step is Testcontainers with a real Postgres.

---

## Day 2: frontend, eval, DevOps (about 3 hours)

### Step 6. Angular (60 min)

1. `app.config.ts` and `app.routes.ts`: standalone app, lazy-loaded review page, route params bound to inputs.
2. `core/invoice-api.service.ts`: HttpClient, FormData upload, ProblemDetails errors.
3. `pages/invoice-list`: signals, the new `@if` / `@for` control flow, upload then navigate.
4. `pages/invoice-detail`: reactive form, highlighting fields that have flags, why the PDF URL needs `DomSanitizer`.
5. `proxy.conf.json` (dev) and `nginx.conf` (prod): how `/api` reaches the backend in each.

- [ ] Explain signals vs RxJS observables in this code: where each is used and why.
- [ ] **Change it yourself:** add a "Needs review only" filter toggle on the list page.

### Step 7. The eval (45 min)

- [ ] Read `GstInvoice.Eval/Program.cs` and `Evaluation/FieldComparer.cs`.
- [ ] Run `python tools/make_synthetic_invoices.py`, then the eval with `--fake`, then a real run if you have a key.
- [ ] Explain why amounts use a 50 paise tolerance and why null equals 0 for amounts.
- [ ] Be honest about the limit: 10 synthetic invoices is a smoke test, not a benchmark. Say what you'd do: more real, messy bills, and track accuracy per prompt change.

### Step 8. Docker and CI/CD (60 min)

1. `backend/Dockerfile`: multi-stage build, restore caching, non-root user.
2. `frontend/Dockerfile`: build stage, then nginx.
3. `docker-compose.yml`: services, health check, `depends_on`, volumes, environment variables.
4. `.github/workflows/ci.yml`: jobs, `needs`, pushing images only from `main`, build cache.
5. `eval.yml` (manual, costs money) and `deploy.yml` (SSH, pull, restart).
6. The root `Dockerfile` and `render.yaml`: one container for free hosting, and why Render needs `$PORT`.

- [ ] Explain why the eval with a real model is not run on every PR.
- [ ] Explain how you'd get zero-downtime deploys (two instances behind a load balancer, or blue/green).
- [ ] **Change it yourself:** add a job to `ci.yml` that runs `dotnet format --verify-no-changes`.

---

## Day 3: rehearse (about 1 hour)

Answer each question out loud in under 90 seconds. If you stumble, reread the files named.

| Question | Where the answer lives |
| --- | --- |
| Walk me through what happens when I upload an invoice. | `InvoicesController`, `InvoiceService.UploadAsync` |
| Why validate after the LLM? Isn't the model good enough? | `InvoiceValidator`, README "Why this project" |
| How did you measure accuracy? Is 10 invoices enough? | `GstInvoice.Eval`, Step 7 |
| How would this handle 10,000 invoices a day? | README "Design decisions": queue + workers, retries, idempotency, LLM rate limits |
| What if the LLM is slow or down? | Error handling in `ClaudeInvoiceExtractor`; add timeouts, retries with backoff (Polly), a fallback model |
| What does one invoice cost? | Token counts stored per invoice and in the eval report |
| How do you keep the API key safe? | Environment variables, GitHub secrets, never in the frontend |
| Could text on an invoice trick the model (prompt injection)? | Schema-only output, no tools, rules afterwards; the model can't take any action |
| Why Postgres jsonb for flags but a table for files? | `AppDbContext` |
| Why decimal for money? | Step 4 |
| What would you change for production? | Auth and multi-tenancy, S3, queue, monitoring, Terraform, bigger eval set |
| What was hard / what would you do differently? | Your own answer. Have one real story ready. |

## Concepts to brush up on if any of this felt shaky

- **C# / .NET:** records and `with`, nullable reference types, async/await and `CancellationToken`, DI lifetimes, middleware order.
- **EF Core:** migrations, owned types and JSON columns, `AsNoTracking`, N+1 queries, indexes.
- **Angular:** standalone components, signals, reactive forms, lazy routes, HttpClient interceptors.
- **LLM basics:** tokens and pricing, structured outputs, effort/thinking, why evals matter, prompt injection.
- **DevOps:** Docker layers and caching, Compose networking, GitHub Actions jobs and secrets, what a container registry is.
