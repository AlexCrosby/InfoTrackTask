# InfoTrackTask

A full-stack SPA built with an **ASP.NET Core 10** Web API backend and a **React + Vite + TypeScript** frontend. The backend uses an in-memory SQLite database, so there is no database setup required.

No CORS configuration is needed — the Vite dev server proxies all `/api` requests to the ASP.NET backend server-side, so the browser only ever communicates with a single origin. In production, the React app is served directly by ASP.NET, keeping them on the same origin.

At the end of this README, you can find an analysis and future improvements section.

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Required for the backend |
| [Node.js](https://nodejs.org/) | 18+ (LTS recommended) | Required for the frontend |
| [npm](https://www.npmjs.com/) | Bundled with Node.js | Used to install frontend dependencies |

To verify your installations:

```bash
dotnet --version
node --version
npm --version
```

---

## Running the Project

The project uses the ASP.NET Core SPA Proxy, which means **you only need to start the backend** — it will automatically launch the Vite dev server for the frontend alongside it.

### 1. Install frontend dependencies

Before running for the first time, install the Node packages:

```bash
cd infotracktask.client
npm install
```

### 2. Trust the development HTTPS certificate

The Vite dev server uses an ASP.NET Core developer certificate for HTTPS. If you haven't already trusted it, run:

```bash
dotnet dev-certs https --trust
```

### 3. Start the application

From the repository root, run:

```bash
cd InfoTrackTask.Server
dotnet run
```

The SPA proxy will start the Vite frontend automatically. Once both are ready, open your browser at:

- **Frontend (React):** `https://localhost:58136`
- **Backend API:** `https://localhost:7139`
- **OpenAPI / Scalar docs:** `https://localhost:7139/openapi/v1.json`

> **Note:** Port numbers are defined in `InfoTrackTask.Server/Properties/launchSettings.json` and `infotracktask.client/vite.config.ts`. If ports are in use, you can update them there.

---

## Running with Visual Studio / Rider

Open `InfoTrackTask.slnx` in Visual Studio 2022 (v17.10+) or JetBrains Rider and press **F5** (or click **Run**). Both projects are configured as startup projects and will launch together automatically.

---

## Running Tests

The test suite uses **xUnit** and lives in `InfoTrackTask.Server.Tests`.

```bash
dotnet test InfoTrackTask.Server.Tests
```

---

## Project Structure

```
InfoTrackTask/
├── InfoTrackTask.Server/          # ASP.NET Core 10 Web API
│   ├── Controllers/               # API controllers
│   ├── Data/                      # EF Core DbContext (in-memory SQLite)
│   ├── Entities/                  # Database entity models
│   ├── Models/                    # Request / response models
│   ├── Services/                  # Business logic & scraper service
│   └── Program.cs                 # App entry point & DI setup
├── InfoTrackTask.Server.Tests/    # xUnit test project
└── infotracktask.client/          # React + Vite + TypeScript frontend
    ├── src/                       # React source files
    └── vite.config.ts             # Vite config (proxy, HTTPS, ports)
```

---

## Key Technical Details

- **Database:** In-memory SQLite via Entity Framework Core — data is reset on every restart.
- **HTTPS:** The Vite dev server proxies API calls (`/api/*`) to the .NET backend over HTTPS. Developer certificates are generated automatically on first run.
- **SPA Integration:** The server is configured with `Microsoft.AspNetCore.SpaProxy`, which manages the frontend dev server lifecycle.


## Analysis

Initially I had planned to just scrape the solicitors page and display a list of summarised contact details for the solicitors. This ended up not being feasible due to two facts:
- The page only displays a maximum of 75 results.
- The listings were randomised on each request. Only the premium listings were constant (presumably because there was never more than 75 premium listings).

Since I was unable to find any ways to order the results, or force any pagination with query strings, this led me to conclude that the only way to get the full list of solicitors would be to scrape the site multiple times.

My first attempt at this led to a large delay between a user pressing search and the result appearing since it took many generations to build up the full list. To fix this I changed the API to return a stream. This way the front end could access the results as they were found and start displaying them immediately, while the backend continued to generate and process the next batch of results. Currently the system continues to attempt to find more results until the first batch where no new results are found (per location). This does not guarantee every solicitor result is found, but allows the server to move into the next location if multiple are given. This could be altered to run indefinitely, or be capped to a specific number of runs.

To further improve this I added an in-memory database cache which saved solicitors as they were found and served them up immediately if the user searched a second time, before continuing to scrape in the background.

For the sake of this task, I only implemented an in-memory database. This means that on restart, the database is wiped. Because of the short lifespan of this database, I did not implement any invalidating logic, so if a listing is removed from solicitors.com, it will remain in the database until the application is restarted. In a real database scenario, I would implement Time To Live logic so that if a certain solicitor is not found again after a certain timeframe, it would be removed.

Finally a major obstacle was the web scraping itself. Without being able to use third-party libraries to parse the page, I had to write some simple parser logic using Regex. While not ideal, this worked well enough for the purposes of this task, providing the layout never changes. However this is not optimal if the system ever needs expanding.

## Future Improvements

Due to the time limit of the task, I decided to prioritise backend functionality. Some improvements I would make are:
- Modify the frontend to display with infinite scrolling rather than just displaying every result.
- Better validation for the request parameters.
- Implement containers to containerise the application for ease of deployment
- Add more unit tests since I only added minor tests to validate my scraping logic was working before starting the frontend.