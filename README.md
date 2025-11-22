# Task Management API
Task Management API and chat application with real-time features using ASP.NET Core and SignalR.
Daily Report team project every member can create tasks, assign them, and communicate in real-time.
Notifications are sent to users when tasks are updated or new messages are posted.
Counters are implemented to track task progress and user activity.
Open source project for learning purposes.
Add friendly and groupchat features.
Save chat history in database. Optimalize performance for large number of users. Image scaling and compression.
Add Model AI gemini 2.5 flash chat bot support.

## Description
A Task Management API built with ASP.NET Core (.NET 8). The project uses Entity Framework Core for data access and SignalR for real-time features.
Framework mongo db for secondary project `WebAPI`.

## Requirements
- .NET 8 SDK
- SQL Server (or a compatible database)
- MongoDB branch for secondary project `WebAPI`

## Configuration
Before running the application, update `appsettings.json` or set environment variables:
- `ConnectionStrings:DefaultConnection` — database connection string.
- `JWT:SecretKey` — secret key used to sign JWT tokens.

## Project structure (brief)
- `Managerment/Program.cs` — app startup, services and middleware configuration.
- `Managerment/ApplicationContext` — EF Core `DbContext` and related models.
- `Managerment/Hubs` — SignalR hubs (e.g. `TaskHub`).
- `WebAPI/Utils/Middlewares` — custom middleware (e.g. `JWTAuthenticationMiddleware`).

## Notes
- The API uses JWT for authentication and SignalR for real-time updates.
- On first run, create the database and apply migrations if present.
- Secondary project `WebAPI` convert new mongo db to sql db on startup.
---
