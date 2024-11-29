# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SNIF.API/SNIF.API.csproj", "SNIF.API/"]
COPY ["SNIF.Core/SNIF.Core.csproj", "SNIF.Core/"]
COPY ["SNIF.Infrastructure/SNIF.Infrastructure.csproj", "SNIF.Infrastructure/"]
COPY ["SNIF.Application/SNIF.Application.csproj", "SNIF.Application/"]


RUN dotnet restore "SNIF.API/SNIF.API.csproj"
RUN dotnet restore "SNIF.Infrastructure/SNIF.Infrastructure.csproj"


COPY . .

WORKDIR "/src/SNIF.Infrastructure"
RUN dotnet build "SNIF.Infrastructure.csproj" -c Release -o /app/build

WORKDIR "/src/SNIF.API"
RUN dotnet build "SNIF.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SNIF.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SNIF.API.dll"]