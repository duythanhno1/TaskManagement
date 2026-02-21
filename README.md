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
| **Auth** | JWT Bearer Token, BCrypt password hashing |
| **Logging** | Serilog (Console + Rolling File) |
| **Validation** | FluentValidation |
| **Security** | Rate Limiting, CORS, Global Exception Handler |
| **Monitoring** | Health Checks, Audit Trail |
| **Compression** | Brotli + Gzip |
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
┌──────────┐
│ Audit    │ ← Auto-track Create/Update/Delete
│ Trail    │   via SaveChangesAsync override
└──────────┘
```

## 🔧 Middleware Pipeline

```
Request → GlobalExceptionHandler → Serilog Request Logging
        → Response Compression → CORS → Rate Limiter
        → JWT Auth → Controller → Response
```

## 📋 Features

- **Task CRUD** — Create, Read, Update, Delete, Assign with SignalR broadcast
- **Chat** — Group chat, cursor-based pagination, reactions (emoji whitelist), read receipts (batched 500/call)
- **Notifications** — Real-time push via SignalR, bulk creation
- **Caching** — Distributed cache (Redis/In-memory) with auto-invalidation on CRUD
- **Audit Trail** — Auto-log entity changes (old/new values as JSON) via EF Core ChangeTracker
- **Rate Limiting** — Auth: 5/30s, Chat: 30/10s, General: 100/60s (configurable)
- **Validation** — FluentValidation: password strength, email format, reaction whitelist
- **Logging** — Serilog structured logs with TraceId, request duration, rolling daily files
- **i18n** — Multi-language response messages (VI/EN/JA) via `Accept-Language` header

## ⚡ Quick Start

```bash
# Config database & JWT in appsettings.json, then:
dotnet ef database update
dotnet run
```

Swagger UI: `http://localhost:5124/swagger`  
Health Check: `GET /health`

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

## 📊 Database Schema

```
Users ──┬── TaskItems (AssignedTo)
        ├── ChatGroups ── ChatGroupMembers
        ├── ChatMessages ──┬── MessageReactions
        │                  └── MessageReadStatuses
        ├── Notifications
        └── AuditLogs (auto-generated)
```

---

**License:** Open source for learning purposes.
