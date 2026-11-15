# Verus Companies

Company submission API (.NET 10, ASP.NET Core) with an Angular frontend.
Submitted companies are validated (including name-to-website relevance), stored in memory, and can be listed, filtered and searched.

> Design decisions, trade-offs and extension points are documented in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ and npm

## Run the backend
```bash
dotnet run --project src/Verus.Companies.Api
```
Listens on **http://localhost:5000** and **https://localhost:7000** (health check: `GET /health`).

## Run the frontend
```bash
cd client
npm install
npm start
```
Open **http://localhost:4200** (the backend must be running).

## Run the tests
```bash
dotnet test            # backend: unit + HTTP-level tests
cd client && npm test  # frontend
```

## Continuous integration
`.github/workflows/ci.yml` runs on every push and pull request: the backend job restores, builds in Release (warnings are errors) and runs `dotnet test` (TRX results are uploaded as an artifact); the frontend job runs `npm ci`, `ng build` and `ng test`.

## Try the API
```bash
# create (201)
curl -i -X POST http://localhost:5000/api/companies -H "Content-Type: application/json" \
  -d '{"name":"Example","websiteUrl":"https://www.example.com"}'

# irrelevant name -> 400 with a concise message
curl -i -X POST http://localhost:5000/api/companies -H "Content-Type: application/json" \
  -d '{"name":"Acme Corp","websiteUrl":"https://www.example.com"}'

curl http://localhost:5000/api/companies                       # all
curl "http://localhost:5000/api/companies?name=exam"           # partial name
curl "http://localhost:5000/api/companies?domain=example.com"  # domain
curl "http://localhost:5000/api/companies?q=example"           # relevance search, best match first
curl http://localhost:5000/api/companies/{id}                  # one (404 if unknown)
```

## Configuration (`src/Verus.Companies.Api/appsettings.json`)
| Setting | Default | Meaning |
|---|---|---|
| `Relevance:MinimumScore` | `0.5` | Minimum name-to-website relevance (0-1) to accept a company |
| `WebsiteInspection:Enabled` | `true` | Allow fetching the site's title/description when the domain alone is not convincing. Set `false` to work fully offline |
| `WebsiteInspection:TimeoutSeconds` | `3` | Timeout for that fetch |
| `Cors:AllowedOrigins` | `["http://localhost:4200"]` | Origins allowed to call the API |
