FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Infrastructure/Shared.Infrastructure.csproj src/Shared/Shared.Infrastructure/
COPY src/Services/Auth/Auth.API/Protos/ src/Services/Auth/Auth.API/Protos/
COPY src/Services/Chat/Chat.API/Chat.API.csproj src/Services/Chat/Chat.API/
RUN dotnet restore src/Services/Chat/Chat.API/Chat.API.csproj
COPY src/ src/
RUN dotnet publish src/Services/Chat/Chat.API/Chat.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5003
ENTRYPOINT ["dotnet", "Chat.API.dll"]
