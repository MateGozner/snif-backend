# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SNIF.sln .
COPY SNIF.API/SNIF.API.csproj SNIF.API/
COPY SNIF.Core/SNIF.Core.csproj SNIF.Core/
COPY SNIF.Application/SNIF.Application.csproj SNIF.Application/
COPY SNIF.Infrastructure/SNIF.Infrastructure.csproj SNIF.Infrastructure/
COPY SNIF.SignalR/SNIF.SignalR.csproj SNIF.SignalR/
COPY SNIF.Messaging/SNIF.Messaging.csproj SNIF.Messaging/
RUN dotnet restore SNIF.sln

COPY . .
RUN dotnet publish SNIF.API/SNIF.API.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SNIF.API.dll"]