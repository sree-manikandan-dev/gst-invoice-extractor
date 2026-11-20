# Deploy for ₹0

Everything here is on a free plan with no card needed at sign-up (as of November 2026; free tiers change, so check each page before you rely on it).

| Part | Service | Free limits that matter |
| --- | --- | --- |
| App (API + Angular in one container) | [Render](https://render.com) free web service | Sleeps after 15 minutes idle; the first visit after that takes about a minute. 750 instance hours a month. |
| Database | [Neon](https://neon.com) free Postgres | 1 GB storage per project, 100 compute hours a month, scales to zero when idle. Doesn't expire. |
| AI | [Google Gemini API](https://aistudio.google.com) free tier (Flash models) | Rate limited (a few requests a minute). Google may use free-tier inputs to improve its products, so **upload only sample or masked invoices**. |
| CI | GitHub Actions | Free for public repos. |

Why not Render's own Postgres? Its free database is deleted 30 days after you create it. Why not Claude? There is no free API tier, so the app keeps Claude as a paid option (`Extraction__Provider=Claude`) and uses Gemini for the free demo.

## Steps (about 20 minutes)

### 1. Neon database

1. Sign up at [neon.com](https://neon.com) with GitHub.
2. Create a project (pick the Singapore region, the closest to Kerala). Name the database `gst`.
3. Copy the connection string. It looks like `postgresql://user:password@ep-xxx.ap-southeast-1.aws.neon.tech/gst?sslmode=require`.

The app creates its tables on first start (EF Core migrations), so there is nothing else to do here.

### 2. Gemini API key

1. Go to [Google AI Studio](https://aistudio.google.com) and sign in with a Google account.
2. Click **Get API key**, then **Create API key**. Copy it.

### 3. Render

1. Sign up at [render.com](https://render.com) with GitHub and allow access to the `gst-invoice-extractor` repo.
2. Click **New**, then **Blueprint**, and pick the repo. Render reads `render.yaml`.
3. When it asks for the secret values, paste:
   - `GEMINI_API_KEY`: the key from step 2
   - `DATABASE_URL`: the Neon connection string from step 1
4. Click **Apply**. The first build takes 5 to 10 minutes. You get a URL like `https://gst-invoice-extractor.onrender.com`.

Every push to `main` redeploys automatically.

### 4. Check it

- Open `https://<your-app>.onrender.com/health`. It should say `Healthy`.
- Upload one of the images in `eval-data/synthetic/` and check the fields on the review page.

## Before you share the link

- There is no login: anyone with the URL can upload and see invoices. Only use sample or masked bills, and label it a demo in the README.
- The first load after 15 idle minutes is slow. Open the link yourself a minute before an interview or demo.
- Gemini's free limits are per minute and per day. If uploads start failing with a rate-limit message, wait and retry.

## Run the eval for free

```bash
cd backend
export GEMINI_API_KEY=...
dotnet run --project src/GstInvoice.Eval -- --provider gemini --out eval-results.md
```

It pauses and continues if it hits the free-tier rate limit.
