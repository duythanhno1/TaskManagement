# Task Management API

> This is a mini source — a living reference that gets updated as I learn new patterns and best practices.

Real-time Task Management & Chat API built with **ASP.NET Core 8** — enterprise-grade architecture.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 8 / ASP.NET Core Web API |
| **ORM** | Entity Framework Core 8 |
| **Database** | SQL Server |
| **Cache** | Redis (IDistributedCache) + In-memory fallback |
| **Real-time** | SignalR WebSocket + Redis Backplane |
| **Auth** | JWT (120min) + Refresh Token (30d) + BCrypt |
| **Logging** | Serilog (daily rolling files) |
| **Validation** | FluentValidation |
| **Security** | Rate Limiting, CORS, Global Exception Handler |
| **Monitoring** | Prometheus + Grafana, Health Checks |
| **Background Jobs** | Hangfire (SqlServer) |
| **Compression** | Brotli + Gzip |
| **Container** | Docker + Docker Compose |
| **i18n** | Multi-language (VI, EN, JA) |

## Features

- **Task CRUD** — Create, Read, Update, Delete, Assign with real-time SignalR broadcast + DueDate
- **Task Search** — Filter by keyword, status, assignee, date range with `PagedResult<T>` pagination
- **Chat** — Group chat, cursor-based pagination, emoji reactions, batched read receipts
- **Notifications** — Real-time push via SignalR, bulk creation, deadline reminders
- **Auth** — JWT + Refresh Token with rotation & revoke, BCrypt hashing
- **Soft Delete** — `ISoftDeletable` + EF Core Global Query Filter, auto-cleanup after 30 days
- **Audit Trail** — Auto-log entity changes via EF Core ChangeTracker (old/new values as JSON)
- **Caching** — Redis distributed cache with auto fallback to in-memory
- **Rate Limiting** — Configurable per endpoint (Auth: 5/30s, Chat: 30/10s, General: 100/60s)
- **Scaling** — Redis Backplane for SignalR multi-instance deployment
- **Monitoring** — Prometheus metrics at `/metrics`, Grafana dashboard
- **Background Jobs** — Hangfire: token cleanup, soft-delete cleanup, deadline reminders
- **i18n** — Multi-language responses via `Accept-Language` header

## Quick Start

```bash
dotnet ef database update
dotnet run
```

| Endpoint | URL |
|----------|-----|
| Swagger UI | `http://localhost:5124/swagger` |
| Health Check | `GET /health` |
| Hangfire | `http://localhost:5124/hangfire` |
| Prometheus | `http://localhost:5124/metrics` |
| Grafana | `http://localhost:3001` (admin/admin) |

## Auth Flow

```
POST /auth/login    → { accessToken (120min), refreshToken (30 days) }
POST /auth/refresh  → Rotate: new accessToken + new refreshToken
POST /auth/revoke   → Revoke all tokens (logout)
```

## Background Jobs (Hangfire)

| Job | Schedule | Description |
|-----|----------|-------------|
| `TokenCleanupJob` | Daily 2 AM | Remove expired/revoked refresh tokens |
| `SoftDeleteCleanupJob` | Weekly Sunday 3 AM | Hard-delete records soft-deleted > 30 days |
| `TaskDeadlineReminderJob` | Hourly | Notify assigned users about tasks due within 24h |

## Docker

```bash
docker-compose up -d
```

Services: API (8080), SQL Server (1433), Redis (6379), Prometheus (9090), Grafana (3001)

---

**License:** Open source for learning purposes.
