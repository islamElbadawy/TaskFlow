# TaskFlow Web App

Angular 22.3 frontend for **TaskFlow**, built with signals (`signal`, `computed`, `linkedSignal`, `httpResource`), Signal Forms, zoneless change detection + `OnPush`, Tailwind CSS v4 design tokens, Angular CDK a11y/dialogs, SignalR real-time notifications, full English/Arabic (RTL) runtime i18n, and System/Light/Dark theming.

## Prerequisites

- **Node.js ≥ 24.15** (or `>= 26.0.0` LTS; developed on `v26.13.1`)
- Running TaskFlow ASP.NET Core backend at `https://localhost:7103`

## Running locally

1. Start the backend API (`https://localhost:7103`).
2. Install dependencies (if needed) and run the dev server on port `4200`:

```bash
npm install
npm start
```

`ng serve` uses [proxy.conf.json](proxy.conf.json) to forward `/api` and `/hubs` (WebSockets) to `https://localhost:7103`, avoiding CORS preflights, dev-cert trust errors, and hidden `Retry-After` headers on `429` responses.

### Seeded dev logins (password `P@ssw0rd`)

- `admin@taskflow.dev` (Admin)
- `manager@taskflow.dev` (Manager)
- `user@taskflow.dev` (User)
- `user2@taskflow.dev` (User)
- `inactive@taskflow.dev` (Deactivated)

## Scripts

- `npm start` — dev server on `http://localhost:4200` with API/SignalR proxy
- `npm run build` — production build (`dist/taskflow`)
- `npm test -- --watch=false` — unit tests (Vitest)
- `npm run lint` — ESLint (`angular-eslint`)
- `npm run format` — Prettier

## Structure

- `src/app/core/` — `config`, `storage`, `api` (models, limits, typed API services, `BoardApi` with position fallback), `auth` (store, guards, session bridge), `http` (interceptors, `ApiError`, ProblemDetails mapper), `i18n` (EN/AR service, pipes, title strategy, Arabic notification formatter), `realtime` (SignalR client, notifications store), `theme`, `toast`, `domain` (pure permissions, due-date, board-order functions)
- `src/app/shared/` — hand-built UI kit (`shared/ui/`) and motion helpers (`shared/motion/`)
- `src/app/layout/` — authenticated shell (`AppShell`) with sliding tab indicator, bell panel, account menu
- `src/app/features/` — lazy-loaded `auth`, `dashboard`, `tasks` (list, board, detail, form), `notifications`, `profile`, `admin`, `not-found`
- `src/i18n/` — `en.ts` and lazy-loaded `ar.ts` (`satisfies Dictionary`)
- `prototype/index.html` — standalone interactive UI review prototype

## Known gaps

1. **Server notification messages are English-only:** In Arabic mode, `formatNotificationMessage` parses known English server patterns by notification type and renders Arabic copy, falling back to the server text if an unknown pattern appears.
2. **Session renewal:** JWT tokens last 24h without a refresh-token endpoint, so users re-authenticate after 24 hours.
3. **Production deployment / CORS:** `proxy.conf.json` applies to `ng serve` only. In production, either serve the SPA and API behind the same origin/reverse proxy (recommended), or set `APP_CONFIG.apiUrl` (`Retry-After` is already exposed in backend CORS).
