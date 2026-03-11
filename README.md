# SNIF - Social Network For Furry Friends

## Projektspecifikáció

### Áttekintés

SNIF egy specializált platform, amely kisállatok számára teszi lehetővé a párkeresést. Az alkalmazás elsődleges célja, hogy segítsen az állattartóknak megfelelő partnert találni kisállatuk számára, legyen szó akár tenyésztésről, játszótársról vagy hosszútávú kapcsolatról.

### Technológiai Stack

- Frontend: Next.js, TypeScript, TailwindCSS
- Backend: .NET Core, Entity Framework
- Adatbázis: PostgreSQL
- Egyéb: RabbitMQ (üzenetkezelés)

### Funkcionális Követelmények

#### 1. Rich Client Alkalmazás (R)

A kliensoldal egy modern, reszponzív webalkalmazás formájában valósul meg Next.js segítségével. Az alkalmazás dinamikusan építi fel a tartalmat, és API hívásokon keresztül kommunikál a szerverrel. A felhasználói felület lehetővé teszi a kisállatok profiljainak egyszerű kezelését és a párosítási folyamat intuitív vezérlését.

#### 2. REST API Implementáció (R)

A szerveroldali API négy fő entitást kezel:

- Kisállatok (profilok, preferenciák)
- Felhasználók (tulajdonosok adatai)
- Párosítások (match-ek kezelése)
- Üzenetek (kommunikáció)

Az API végpontok RESTful konvenciókat követnek, és lehetővé teszik az entitások teljes körű kezelését.

#### 3. Kétirányú Kommunikáció (WS)

A WebSocket kapcsolat három fő területen kerül alkalmazásra:

- Azonnali üzenetküldés a tulajdonosok között
- Watchlist
- Online státusz követése

A SignalR technológia biztosítja a megbízható, kétirányú kommunikációt.

#### 4. Üzenetsor Implementáció (MQ)

RabbitMQ message broker segítségével az alábbi eseményeket kezeljük:

- Új párosítás értesítések
- Chat üzenetek kézbesítése
- Rendszerszintű értesítések
- Találkozó egyeztetések

#### 5. P2P Kommunikáció (P2P)

WebRTC technológia segítségével megvalósított funkciók:

- Videó chat az állatok bemutatásához
- Hanghívás lehetőség

## Configuration Checklist

### Safe To Commit

- `SNIF.API/appsettings.Production.json`: non-secret shape only, with empty values for environment-specific settings.
- `SNIF.API/appsettings.json`: non-secret defaults only. Do not place real provider keys, JWT keys, or database credentials here.
- `SNIF.API/appsettings.Development.json`: non-secret development-only behavior, such as log levels.

### Do Not Commit

- `firebase-admin-sdk.json`: Firebase Admin service account JSON used by backend push notifications.
- Real values for `ConnectionStrings`, `Jwt`, `LemonSqueezy`, Azure Blob credentials, RabbitMQ credentials, and TURN credentials.

### Localhost Bootstrap

`SNIF.API` now uses the built-in ASP.NET Core configuration order for local development:

1. `appsettings.json`
2. `appsettings.Development.json`
3. `dotnet user-secrets`
4. environment variables

That means local secrets belong in `dotnet user-secrets`, and environment variables can override them when needed.

Run these commands from `snif-backend` on a fresh machine:

```bash
dotnet user-secrets set --project SNIF.API/SNIF.API.csproj "ConnectionStrings:DefaultConnection" "Host=localhost;Database=snif;Username=postgres;Password=<your-local-postgres-password>"
dotnet user-secrets set --project SNIF.API/SNIF.API.csproj "Jwt:Key" "$(openssl rand -base64 64)"
dotnet user-secrets list --project SNIF.API/SNIF.API.csproj
dotnet run --project SNIF.API/SNIF.API.csproj
```

If you need a one-off override without changing user-secrets, use environment variables with `__` separators:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=snif;Username=postgres;Password=<override-password>"
export Jwt__Key="$(openssl rand -base64 64)"
dotnet run --project SNIF.API/SNIF.API.csproj
```

Minimum values required to boot locally:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`

`Jwt:Issuer` and `Jwt:Audience` already default to `http://localhost:3000` in tracked config. Firebase, LemonSqueezy, email provider credentials, and Azure storage credentials are optional for local startup and degrade to warnings or local fallbacks.

### Required Runtime Configuration

Use ASP.NET Core configuration keys directly in `appsettings.*.json`, or environment variables with `__` separators.

- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string. In Azure App Service the code also accepts `POSTGRESQLCONNSTR_*` and `CUSTOMCONNSTR_*` provider variables.
- `Jwt__Key`: required, minimum 64 bytes, and must not use tracked placeholder values.
- `Jwt__Issuer`: required.
- `Jwt__Audience`: required.
- `Google__ClientId`: required for web Google token validation.
- `Google__ClientIdIos`: required for iOS Google token validation.
- `Google__ClientIdAndroid`: required for Android Google token validation.
- `App__PublicBaseUrl`: required for account email confirmation and password reset links.
- `Firebase__AdminCredentialPath`: preferred for file-based Firebase Admin credentials.
- `Firebase__AdminCredentialJson`: optional inline Firebase Admin credential JSON. Prefer using an environment variable rather than committing JSON into config files.
- `GOOGLE_APPLICATION_CREDENTIALS`: supported fallback for Firebase Admin credential path.
- `Email__Provider`: `Logging`, `AzureCommunication`, or `Azure`. Leave unset or set `Logging` for local fallback logging.
- `Email__ConnectionString`: required when `Email__Provider` is `AzureCommunication` or `Azure`.
- `Email__SenderAddress`: required when `Email__Provider` is `AzureCommunication` or `Azure`. Must be a verified sender address on the configured Azure Email Communication Service domain.
- `LemonSqueezy__ApiKey`: required for checkout creation and reconciliation.
- `LemonSqueezy__StoreId`: required.
- `LemonSqueezy__SigningSecret`: required for webhook signature verification.
- `LemonSqueezy__BaseUrl`: defaults to `https://api.lemonsqueezy.com`.
- `LemonSqueezy__Variants__GoodBoyMonthly`: required.
- `LemonSqueezy__Variants__GoodBoyYearly`: required.
- `LemonSqueezy__Variants__AlphaPackMonthly`: required.
- `LemonSqueezy__Variants__AlphaPackYearly`: required.
- `LemonSqueezy__Variants__TreatBag10`: required.
- `LemonSqueezy__Variants__TreatBag50`: required.
- `LemonSqueezy__Variants__TreatBag100`: required.
- `Storage__Provider`: `Local` or `Azure`.
- `Storage__ConnectionString`: required when `Storage__Provider=Azure`.
- `Storage__ContainerName`: required for Azure Blob deployments.
- `Cors__AllowedOrigins__0..n`: required in non-development environments.

### Optional Runtime Configuration

- `Email__SenderDisplayName`: optional display name for account emails. The current implementation stores the value for future provider use; Azure sending currently requires `Email__SenderAddress` to be configured.
- `ApplicationInsights__ConnectionString`: recommended for production telemetry.
- `RabbitMQ__Enabled`: optional. When `true`, also configure `RabbitMQ__HostName`, `RabbitMQ__Port`, `RabbitMQ__UserName`, `RabbitMQ__Password`, `RabbitMQ__VirtualHost`, `RabbitMQ__WebSocketPort`, `RabbitMQ__Exchanges__Watchlist`, and `RabbitMQ__Exchanges__Matches`.
- `IceServers__StunUrl`: defaults to Google STUN.
- `IceServers__TurnUrl`, `IceServers__TurnUsername`, `IceServers__TurnCredential`: required only if production video calling needs TURN relay support.
- `Swagger__Enabled`: keep `false` outside local development.

### Current Repo Notes

- Firebase Admin now prefers `Firebase__AdminCredentialPath`, `Firebase__AdminCredentialJson`, or `GOOGLE_APPLICATION_CREDENTIALS`. For local development it will also probe ignored `firebase-admin-sdk.json` files in the API directory or repository root without copying them into build output.
- Account emails now use `IAccountEmailService` with a configuration-driven provider: `Logging` fallback by default, or Azure Communication Services when `Email__Provider` and the required email settings are present.
- The backend does not use shell-style `${ENV_VAR}` expansion inside JSON files. Use `dotnet user-secrets` for local secrets, or environment variables such as `Jwt__Key` when you need an override.
- `Firebase:ProjectId` was removed from the production template because current backend code does not consume it.

### Operational Follow-Up

- Generate and provision a unique JWT signing key for every deployed environment before startup.
- Configure Firebase Admin credentials in the runtime environment or local ignored files; do not commit service-account JSON.
- Review production `Cors__AllowedOrigins__*` values before release and keep loopback origins limited to development-only config.
- Continue keeping tracked LemonSqueezy values empty and inject real values only via deployment secrets.
