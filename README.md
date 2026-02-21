# Task Management API

Real-time Task Management & Chat API built with **ASP.NET Core 8** — enterprise-grade architecture.

## 🛠 Tech Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 8 / ASP.NET Core Web API |
| **ORM** | Entity Framework Core 8 |
| **Database** | SQL Server |
| **Cache** | Redis (IDistributedCache) + In-memory fallback |
| **Real-time** | SignalR WebSocket |
| **Auth** | JWT Bearer Token + Refresh Token, BCrypt password hashing |
| **Logging** | Serilog (Rolling File + Console) |
| **Validation** | FluentValidation |
| **Security** | Rate Limiting, CORS, Global Exception Handler |
| **Monitoring** | Health Checks, Audit Trail |
| **Compression** | Brotli + Gzip |
| **Background Jobs** | Hangfire (SqlServer storage) |
| **Container** | Docker + Docker Compose |
| **i18n** | Multi-language (VI, EN, JA) |

## 📐 Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Client (Frontend)                     │
└──────────┬──────────────────────────────┬───────────────┘
           │ REST API                     │ WebSocket
           ▼                              ▼
┌──────────────────┐           ┌──────────────────┐
│   Controllers    │           │   SignalR Hubs    │
│  (Rate Limited)  │           │ TaskHub / ChatHub │
└────────┬─────────┘           └────────┬─────────┘
         │ FluentValidation              │
         ▼                               │
┌──────────────────┐                     │
│    Services      │◄────────────────────┘
│  (Business Logic)│
└────────┬─────────┘
         │
    ┌────┴────┐
    ▼         ▼
┌────────┐ ┌────────┐
│  EF    │ │ Redis  │
│ Core   │ │ Cache  │
│(SQL DB)│ │        │
└────┬───┘ └────────┘
     │
     ▼
┌──────────┐     ┌──────────────┐
│ Audit    │     │  Hangfire    │
│ Trail    │     │  Background  │
│ (auto)   │     │  Jobs (3)    │
└──────────┘     └──────────────┘
```

## 🔧 Middleware Pipeline

```
Request → GlobalExceptionHandler → Serilog Request Logging
        → Response Compression → CORS → Rate Limiter
        → JWT Auth → Controller → Response
```

## 📋 Features

- **Task CRUD** — Create, Read, Update, Delete, Assign with SignalR broadcast + DueDate deadline
- **Task Search** — Filter by keyword, status, assignee, date range with pagination
- **Chat** — Group chat, cursor-based pagination, reactions (emoji whitelist), read receipts (batched 500/call)
- **Notifications** — Real-time push via SignalR, bulk creation, deadline reminders
- **Auth** — JWT (120min) + Refresh Token (30 days) with rotation, BCrypt hashing
- **Caching** — Distributed cache (Redis/In-memory) with auto-invalidation on CRUD
- **Soft Delete** — `IsDeleted` flag + EF Core Global Query Filter, auto-cleanup after 30 days
- **Audit Trail** — Auto-log entity changes (old/new values as JSON) via EF Core ChangeTracker
- **Rate Limiting** — Auth: 5/30s, Chat: 30/10s, General: 100/60s (configurable)
- **Validation** — FluentValidation: password strength, email format, reaction whitelist
- **Logging** — Serilog structured logs with TraceId, request duration, rolling daily files
- **i18n** — Multi-language response messages (VI/EN/JA) via `Accept-Language` header
- **Background Jobs** — Hangfire: token cleanup (daily), soft-delete cleanup (weekly), deadline reminders (hourly)
- **Pagination** — `PagedResult<T>` wrapper with totalCount, page, pageSize, totalPages, hasNext

## ⚡ Quick Start

```bash
# Config database & JWT in appsettings.json, then:
dotnet ef database update
dotnet run
```

Swagger UI: `http://localhost:5124/swagger`  
Health Check: `GET /health`  
Hangfire Dashboard: `http://localhost:5124/hangfire`

## 📁 Config (`appsettings.json`)

```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "JWT": { "SecretKey": "..." },
  "Redis": { "ConnectionString": "", "InstanceName": "TaskMgmt_" },
  "RateLimit": {
    "Auth": { "PermitLimit": 5, "WindowSeconds": 30 },
    "Chat": { "PermitLimit": 30, "WindowSeconds": 10 },
    "General": { "PermitLimit": 100, "WindowSeconds": 60 }
  },
  "Cache": { "SlidingExpirationMinutes": 5, "AbsoluteExpirationMinutes": 30 },
  "Cors": { "AllowedOrigins": ["http://localhost:3000"] }
}
```

> Redis không bắt buộc — để `ConnectionString` rỗng sẽ tự fallback in-memory cache.

## 🔐 Auth Flow

```
POST /auth/login       → { accessToken (120min), refreshToken (30 days) }
POST /auth/refresh     → Rotate: new accessToken + new refreshToken
POST /auth/revoke      → Revoke all refresh tokens (logout)
```

## 📊 Database Schema

```
Users ──┬── TaskItems (AssignedTo, DueDate)
        ├── ChatGroups ── ChatGroupMembers
        ├── ChatMessages ──┬── MessageReactions
        │                  └── MessageReadStatuses
        ├── Notifications
        ├── RefreshTokens
        └── AuditLogs (auto-generated)
```

## ⏰ Background Jobs (Hangfire)

| Job | Schedule | Description |
|-----|----------|-------------|
| `TokenCleanupJob` | Daily 2:00 AM | Remove expired/revoked refresh tokens |
| `SoftDeleteCleanupJob` | Sunday 3:00 AM | Hard-delete records soft-deleted > 30 days |
| `TaskDeadlineReminderJob` | Hourly | Notify users about tasks due within 24h |

---

**License:** Open source for learning purposes.
