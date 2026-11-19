# 0005 — Autenticación con Firebase en la API

- **Issue:** #15
- **Milestone:** v0.1.0-alpha
- **Estado:** lista

## Objetivo
La API acepta solo peticiones con un ID token de Firebase Auth válido, da de alta al `User` la primera vez que llega y le permite ver y borrar su cuenta. Es la base de la autorización por recurso de #16 y del login de la app (#18). La API no guarda credenciales de Firebase: solo valida el JWT con las claves públicas de Google.

## Alcance
**Incluye:**
- Validación del JWT de Firebase (`AddJwtBearer`) con el projectId en la configuración.
- Política de autorización **por defecto** (fallback): todo endpoint exige usuario autenticado salvo los marcados explícitamente como anónimos.
- Alta automática del `User` en su primer acceso, segura ante peticiones concurrentes.
- `GET /api/v1/me` y `DELETE /api/v1/me`.
- Rate limiting básico por usuario en `/api/v1`.
- Esquema Bearer en el documento OpenAPI (para probar desde Scalar).
- Esquema de autenticación de prueba en los tests de integración.

**No incluye:**
- Borrar la cuenta en Firebase (lo hace la app con el SDK del cliente tras llamar a `DELETE /me`, #18). La API no tiene credenciales de administración de Firebase.
- Facebook (#20), App Check y exposición a Internet (v0.5).
- Roles o administración.

## Diseño
### Dominio
Sin cambios: `User` ya existe (`Id`, `FirebaseUid`, `CreatedAt`, `ActiveExamId?`) y `users.firebase_uid` es único.

### Autenticación (`src/OpoStudy.Api/Authentication/`)
- `FirebaseAuthOptions` (sección `Firebase`): `ProjectId` obligatorio, validado al arrancar (`ValidateDataAnnotations` + `ValidateOnStart`). Sin él la API no arranca, en ningún entorno.
- `AddFirebaseAuthentication(this WebApplicationBuilder)`:
  - `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` con `Authority = https://securetoken.google.com/<ProjectId>`, `ValidIssuer` = esa misma URL, `ValidAudience = <ProjectId>`, `MapInboundClaims = false`. Se validan emisor, audiencia, caducidad y firma (claves vía discovery de la Authority).
  - `AddAuthorizationBuilder().SetFallbackPolicy(...RequireAuthenticatedUser())`.
- `ClaimsPrincipalExtensions.GetFirebaseUid()`: devuelve el claim `sub` (en Firebase es el uid). Si falta, lanza `InvalidOperationException` (no debería ocurrir tras la autenticación).
- Endpoints anónimos explícitos (`.AllowAnonymous()`): `/health`, `/alive`, `GET /api/v1/version` y, en Development, OpenAPI y Scalar.
- 401 y 403 salen como ProblemDetails (`application/problem+json`): `AddProblemDetails` + `UseStatusCodePages` ya lo cubren para respuestas sin cuerpo; si el challenge de JwtBearer escribe algo, se ajusta en `OnChallenge`. Se mantiene la cabecera `WWW-Authenticate: Bearer`.
- Nunca se registra en logs el uid ni el token.

### Usuario actual (`src/OpoStudy.Api/Features/Users/`)
- `CurrentUserService` (scoped) con `Task<User> GetOrCreateAsync(CancellationToken)`:
  1. Busca el `User` por `FirebaseUid` (uid del token, vía `IHttpContextAccessor` o recibiendo el `ClaimsPrincipal`).
  2. Si no existe, lo crea con `User.Create(uid, TimeProvider.GetUtcNow())` y guarda.
  3. Si el `SaveChanges` falla por violación de unicidad (`PostgresException` con `SqlState == PostgresErrorCodes.UniqueViolation`), descarta la entidad del `ChangeTracker` y vuelve a leer: otra petición lo creó a la vez.
- `TimeProvider.System` registrado como singleton si no lo está ya.
- Lo usarán los endpoints de #16; aquí lo usa `GET /me`.

### API
| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/me` | Usuario | — → `200 UserProfileResponse` (crea el `User` si no existe) |
| DELETE | `/api/v1/me` | Usuario | — → `204` (borra el `User` y, en cascada, sus `QuizAttempt` y `QuizAnswer`; idempotente: `204` también si no existía) |

- `UserProfileResponse(Guid Id, DateTimeOffset CreatedAt, Guid? ActiveExamId)` en `OpoStudy.Contracts/Users/`. **No** incluye el `FirebaseUid`.
- `UsersEndpoints.MapUsersEndpoints()` con `.WithName()`, `.WithTags("Users")`, `.WithSummary()`.
- `DELETE /me` borra con `ExecuteDeleteAsync` filtrando por `FirebaseUid` (una sola sentencia; la cascada la hace Postgres). Si la app vuelve a llamar con el mismo token antes de borrar la cuenta de Firebase, se crearía un usuario nuevo vacío: es aceptable y la app (#18) borra la cuenta de Firebase justo después.

### Rate limiting
- `AddRateLimiter` con una política con nombre `"per-user"`: ventana fija, partición por uid (`sub`) y, si no hay usuario, por IP remota.
- Límites en configuración (`RateLimiting:PermitLimit`, por defecto 120; `RateLimiting:WindowSeconds`, por defecto 60), validados al arrancar.
- `UseRateLimiter()` va **después** de `UseAuthentication()`/`UseAuthorization()` para que la partición vea al usuario.
- Se aplica al grupo `/api/v1` con `.RequireRateLimiting("per-user")`. `/health` y `/alive` quedan fuera.
- Rechazo: `429` con ProblemDetails (título en español: "Demasiadas peticiones. Inténtalo de nuevo en unos segundos.") y cabecera `Retry-After` si el lease la trae.

### OpenAPI
- Un document transformer añade el esquema de seguridad `Bearer` (HTTP, formato JWT) para poder pegar un token en Scalar. Solo afecta a la documentación de Development.

### Configuración y AppHost
- `src/OpoStudy.AppHost`: `builder.AddParameter("firebase-project-id")` y `.WithEnvironment("Firebase__ProjectId", firebaseProjectId)` en `api`. El valor va en los user-secrets del AppHost (`Parameters:firebase-project-id`); si falta, el dashboard de Aspire lo pide.
- Ejecutar la API sin AppHost: `dotnet user-secrets set Firebase:ProjectId <id> --project src/OpoStudy.Api` (añadir `UserSecretsId` al csproj de la API).
- El projectId no es un secreto en sentido estricto (va dentro de la app), pero se trata como configuración del entorno y no se versiona.
- Paquete nuevo: `Microsoft.AspNetCore.Authentication.JwtBearer` (versión en `Directory.Packages.props`).
- README: sección breve de configuración local con los dos comandos anteriores.

### Datos
Sin cambios de esquema ni migraciones.

### App
Nada (#18).

## Seguridad
- Fallback policy: un endpoint nuevo es privado salvo que se marque `AllowAnonymous`.
- El uid sale **siempre** del token, nunca de la ruta ni del body.
- Se valida firma, emisor, audiencia y caducidad del token; no se acepta HTTP sin TLS en producción (`RequireHttpsMetadata` queda a `true`, el discovery es HTTPS).
- `UserProfileResponse` no expone el `FirebaseUid`; los logs no registran uid ni token.
- Ningún secreto nuevo en el repo. La configuración local va en user-secrets.

## Criterios de aceptación
- [ ] Una petición sin token, o con un token inválido, a `/api/v1/me` devuelve 401 con ProblemDetails.
- [ ] `/health`, `/alive` y `GET /api/v1/version` siguen respondiendo sin token.
- [ ] `GET /me` crea el usuario la primera vez y devuelve el mismo `Id` en las siguientes; peticiones concurrentes de un uid nuevo crean un solo `User`.
- [ ] `DELETE /me` devuelve 204, borra el usuario y sus tests en cascada, y es idempotente.
- [ ] Superar el límite devuelve 429 con ProblemDetails; el límite es por usuario.
- [ ] La API no arranca sin `Firebase:ProjectId`.
- [ ] Tests de integración con un esquema de prueba, sin llamar a Firebase.
- [ ] Ningún secreto en el repo.

## Plan de tests
- Unitarios: ninguno nuevo (la lógica vive en la API).
- Integración (`tests/OpoStudy.Api.IntegrationTests`):
  - `ApiFactory` fija `Firebase:ProjectId=test-project` y, en `ConfigureTestServices`, registra un `TestAuthHandler` (esquema `"Test"`, que pasa a ser el esquema por defecto de autenticación y challenge) que autentica con `sub` = valor de la cabecera `X-Test-User`; sin cabecera devuelve `NoResult` → 401. Helper `CreateClientFor(string uid)`.
  - `UsersEndpointsTests`: sin token → 401 ProblemDetails; primer `GET /me` crea y el segundo devuelve el mismo `Id`; N `GET /me` en paralelo con un uid nuevo → un solo registro en `users`; `DELETE /me` → 204, el usuario y sus `quiz_attempts` desaparecen (sembrar un intento con el DbContext); `DELETE /me` sin usuario → 204; el `FirebaseUid` no aparece en el JSON.
  - `JwtBearerTests`: con el esquema real (sin `TestAuthHandler`), un token mal formado → 401 ProblemDetails, sin red (no se llega a pedir las claves).
  - `RateLimitingTests`: factory derivada con `RateLimiting:PermitLimit=2` → la 3.ª petición del mismo uid da 429 ProblemDetails y la de otro uid da 200.
  - `ApiDocsExposureTests`/`SystemInfoEndpointsTests` siguen verdes sin token.
  - Arranque sin `Firebase:ProjectId` → falla con `OptionsValidationException`.
- Architecture: si procede, que los endpoints de `Features/` no usen `AllowAnonymous` salvo `SystemInfo` (opcional, solo si es sencillo con NetArchTest; si no, omitir).
- Manual: AppHost arriba con el parámetro; `/health` 200; `GET /api/v1/me` sin token → 401 ProblemDetails; con un ID token real de Firebase (si ya hay proyecto creado) → 200.
