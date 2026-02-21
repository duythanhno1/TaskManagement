# Task Management API

Task Management API and chat application with real-time features using ASP.NET Core and SignalR.  
Daily Report team project — every member can create tasks, assign them, and communicate in real-time.

## Tech Stack

| Công nghệ | Mục đích |
|-----------|----------|
| .NET 8 / ASP.NET Core | Web API framework |
| Entity Framework Core 8 | ORM / Data access |
| SQL Server | Database |
| SignalR | Real-time communication |
| JWT Bearer | Authentication |
| Built-in Rate Limiter | API spam protection |

## Project Structure

```
Managerment/
├── Controllers/          # API endpoints
│   ├── AuthController       # Login, Register
│   ├── TaskController       # CRUD tasks, assign
│   ├── ChatController       # Chat groups, messages, reactions
│   └── NotificationController # User notifications
├── Services/             # Business logic
│   ├── AuthService          # Authentication logic
│   ├── TaskService          # Task CRUD + cache
│   ├── ChatService          # Chat + cursor pagination
│   ├── NotificationService  # Notifications + bulk push
│   └── ServiceResult        # Generic response wrapper
├── Interfaces/           # Service contracts (DI)
├── Model/                # Entity models
│   ├── User, TaskItem
│   ├── ChatGroup, ChatGroupMember, ChatMessage
│   ├── MessageReaction, MessageReadStatus
│   └── Notification
├── DTO/                  # Data Transfer Objects
├── Hubs/                 # SignalR hubs
│   ├── TaskHub              # Task real-time updates
│   └── ChatHub              # Chat real-time messaging
├── MiddleWare/
│   ├── JWTAuthenticationMiddleware
│   └── GlobalExceptionMiddleware
├── ApplicationContext/   # EF Core DbContext
├── Util/                 # JWT handler, helpers
├── Program.cs            # App startup & config
└── appsettings.json      # Configuration
```

## Requirements

- .NET 8 SDK
- SQL Server

## Configuration

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=.;Initial Catalog=Managerment;..."
  },
  "JWT": {
    "SecretKey": "YourSecretKeyHere_AtLeast32Characters"
  },
  "RateLimit": {
    "Auth":    { "PermitLimit": 5,   "WindowSeconds": 30 },
    "Chat":    { "PermitLimit": 30,  "WindowSeconds": 10 },
    "General": { "PermitLimit": 100, "WindowSeconds": 60 }
  },
  "Cache": {
    "SlidingExpirationMinutes": 5,
    "AbsoluteExpirationMinutes": 30
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

## Getting Started

```bash
# Apply migrations
dotnet ef database update

# Run
dotnet run
```

API mặc định chạy tại `https://localhost:5001` — Swagger UI tại `/swagger`.

---

## API Endpoints

### Auth — `api/v1/auth`

> Rate limit: **5 requests / 30 giây** per IP

| Method | Endpoint | Mô tả | Body |
|--------|----------|--------|------|
| POST | `/register` | Đăng ký tài khoản | `{ email, fullName, password, phoneNumber, role }` |
| POST | `/login` | Đăng nhập, nhận JWT | `{ email, password }` |

**Response Login:**
```json
{ "Message": "Login Success", "Token": "eyJhbGciOi..." }
```

---

### Tasks — `api/v1/tasks`

> Rate limit: **100 requests / 60 giây** per User  
> Requires: `Authorization: Bearer <token>`

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/` | Lấy tất cả tasks |
| GET | `/my-tasks` | Lấy tasks được assign cho user hiện tại |
| GET | `/{id}` | Lấy task theo ID |
| POST | `/` | Tạo task mới |
| PUT | `/{id}` | Cập nhật task |
| PUT | `/assign` | Assign task cho user khác |
| DELETE | `/{id}` | Xóa task |
| GET | `/users` | Lấy danh sách users |

**Task Status values:** `Todo`, `InProgress`, `Done`

**Create Task:**
```json
{ "taskName": "Fix bug", "description": "...", "assignedTo": 2 }
```

**Update Task:**
```json
{ "taskId": 1, "taskName": "Updated", "description": "...", "status": "InProgress", "assignedTo": 3 }
```

> **Caching:** Tasks được cache trong memory (configurable sliding 5m / absolute 30m). Cache tự động invalidate khi CRUD.

---

### Chat — `api/v1/chat`

> Rate limit: **100 req/60s** (general) — **30 req/10s** cho send message & react  
> Requires: `Authorization: Bearer <token>`

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| POST | `/groups` | Tạo chat group |
| GET | `/groups` | Lấy danh sách groups của user |
| GET | `/groups/{groupId}/messages` | Lấy tin nhắn (cursor pagination) |
| POST | `/messages` | Gửi tin nhắn |
| DELETE | `/messages/{messageId}` | Xóa tin nhắn (soft delete) |
| POST | `/messages/{messageId}/reactions` | React tin nhắn |
| DELETE | `/messages/{messageId}/reactions` | Xóa reaction |
| PUT | `/groups/{groupId}/read` | Đánh dấu đã đọc |

**Create Group:**
```json
{ "groupName": "Team A", "memberUserIds": [1, 2, 3], "isDirectMessage": false }
```

**Send Message:**
```json
{ "groupId": 1, "content": "Hello team!" }
```

**React Message:**
```json
{ "messageId": 5, "reactionType": "❤️" }
```

#### Cursor-based Pagination (Lazy Loading)

```
# Lần đầu — lấy 50 tin nhắn mới nhất
GET /api/v1/chat/groups/1/messages?pageSize=50

# Response:
{
  "Data": [...],
  "NextCursor": 123,
  "HasMore": true
}

# Lần tiếp — load thêm tin cũ hơn
GET /api/v1/chat/groups/1/messages?cursor=123&pageSize=50

# Khi HasMore = false → hết tin nhắn
```

#### Mark as Read (Batch)

Mỗi lần gọi đánh dấu tối đa **500 tin nhắn**. Nếu `HasMore = true`, client cần gọi lại.

```json
// Response
{ "MarkedCount": 500, "HasMore": true }
```

---

### Notifications — `api/v1/notifications`

> Rate limit: **100 requests / 60 giây** per User

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/` | Lấy notifications (pagination) |
| PUT | `/{id}/read` | Đánh dấu 1 notification đã đọc |
| PUT | `/read-all` | Đánh dấu tất cả đã đọc |

**Query params:** `?page=1&pageSize=20`

**Notification types:** `NewMessage`, `GroupInvite`, `Reaction`, `TaskAssignment`

---

### Health Check

```
GET /health
→ "Healthy" hoặc "Unhealthy" (kiểm tra DB connection)
```

---

## Real-time Events (SignalR)

### TaskHub — `/taskhub`

| Event | Mô tả | Payload |
|-------|---------|---------|
| `ReceiveTaskUpdate` | Task được tạo/sửa | `taskId, taskName, description, assignedTo, status` |
| `ReceiveTaskDelete` | Task bị xóa | `taskId` |
| `ReceiveTaskAssignmentNotification` | Được assign task | `message` |

### ChatHub — `/chathub`

| Event | Mô tả | Payload |
|-------|---------|---------|
| `ReceiveMessage` | Tin nhắn mới | `{ messageId, groupId, content, sentAt, sender }` |
| `MessageDeleted` | Tin nhắn bị xóa | `{ messageId, groupId }` |
| `ReceiveReaction` | Reaction mới | `{ messageId, groupId, reactionType, user }` |
| `ReactionRemoved` | Reaction bị xóa | `{ messageId, groupId, userId }` |
| `MessageRead` | Đánh dấu đã đọc | `{ groupId, userId, readCount, hasMore }` |
| `ReceiveNotification` | Notification mới | `{ notificationId, type, title, content, ... }` |

**Client join/leave group:**
```javascript
connection.invoke("JoinGroup", groupId);
connection.invoke("LeaveGroup", groupId);
```

---

## Middleware Pipeline

```
Request
  → GlobalExceptionMiddleware   (bắt mọi exception → JSON 500)
  → ResponseCompression         (gzip/brotli)
  → CORS
  → RateLimiter                 (429 nếu quá giới hạn)
  → JWTAuthenticationMiddleware (validate token)
  → Authentication / Authorization
  → Controller
```

## Rate Limiting

| Policy | Áp dụng | Giới hạn | Config key |
|--------|---------|----------|-----------|
| `auth` | Login, Register | 5 req / 30s per IP | `RateLimit:Auth` |
| `chat` | Send message, React | 30 req / 10s per User | `RateLimit:Chat` |
| `general` | Tất cả API khác | 100 req / 60s per User | `RateLimit:General` |

**Response khi bị limit:**
```json
HTTP 429
{ "Message": "Too many requests. Please try again later.", "RetryAfter": 10 }
```

## Performance Optimizations

- **AsSplitQuery** — tránh cartesian explosion khi load groups + members + messages
- **Cursor-based pagination** — O(1) thay vì O(n) cho deep pages
- **Reaction summary** — group by type + count thay vì trả full user list
- **Bulk notifications** — 1 INSERT cho tất cả members thay vì N lần
- **Batched mark-as-read** — tối đa 500/lần, tránh OOM
- **Memory cache** — tasks cache với configurable TTL
- **Response compression** — Brotli + Gzip

## Database Schema

```
Users ──┬── TaskItems (AssignedTo)
        ├── ChatGroupMembers ── ChatGroups
        ├── ChatMessages ──┬── MessageReactions
        │                  └── MessageReadStatuses
        └── Notifications
```

---

## License

Open source project for learning purposes.
