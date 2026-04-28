FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Infrastructure/Shared.Infrastructure.csproj src/Shared/Shared.Infrastructure/
COPY src/Services/Auth/Auth.API/Auth.API.csproj src/Services/Auth/Auth.API/
RUN dotnet restore src/Services/Auth/Auth.API/Auth.API.csproj
COPY src/ src/
RUN dotnet publish src/Services/Auth/Auth.API/Auth.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5001 5011
ENTRYPOINT ["dotnet", "Auth.API.dll"]
