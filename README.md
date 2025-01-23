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
