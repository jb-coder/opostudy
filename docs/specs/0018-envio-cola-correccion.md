# 0018 — Supuestos prácticos: envío y cola de corrección

- **Issue:** #55
- **Milestone:** v0.3.0
- **Estado:** lista

> Tercera spec de la v0.3.0. Parte del modelo de la 0016 (`PracticalCase`, `PracticalCasePart`, `GradingCriterion`, `ModelAnswer`) y de la 0017 (supuestos de 25 puntos con `Section`). La 0019 añadirá Gemini detrás de `ILlmCorrector`. La 0020 y la 0021 harán la app.
> Términos nuevos del glosario del ADR 0001: `GradingStatus`, `GradingAnswer`, `CriterionScore`, `GradingQuota`, `ILlmCorrector`. Ya están escritos y se commitean en la rama de la feature.

## Objetivo
El usuario envía sus respuestas a un supuesto y recibe una corrección asíncrona: una nota por criterio de la rúbrica, por apartado y total, con comentarios. La API encola el envío en una tabla. El Worker la procesa con `FOR UPDATE SKIP LOCKED` y llama a un `ILlmCorrector`. En esta spec, el único corrector es uno **falso y determinista**, así que todo el flujo se prueba sin coste ni red. Cada usuario tiene una cuota mensual de correcciones.

## Alcance
**Incluye:**
- Agregado `Grading` (con `GradingAnswer` y `CriterionScore`) en `OpoStudy.Domain/Gradings/`, configuración EF Core y migración `AddGradings` (skill `new-ef-migration`).
- `ILlmCorrector`, sus modelos de entrada y salida, `FakeLlmCorrector` y `GradingProcessor` (procesa un elemento de la cola) en `OpoStudy.Infrastructure/Grading/`.
- `GradingWorker` sustituye su espera infinita por un bucle que usa `GradingProcessor`.
- Endpoints de envío, lectura, historial y cuota (skill `new-endpoint`), con DTOs en `OpoStudy.Contracts/Gradings/`.
- Opciones `Grading` (cuota, reintentos, intervalos, proveedor) validadas al arrancar.

**No incluye:**
- Gemini ni ningún proveedor real, prompt, salida JSON estructurada ni registro de tokens reales (0019). Las columnas de tokens se crean ya, pero el falso las deja a `null`.
- App: pantallas, cliente Refit ni `ApiJsonContext` (0020/0021).
- XP, objetivo diario o racha por corregir supuestos (0021, con `ProgressRecorder`).
- Notificaciones push al terminar: la app hará polling (decisión de la v0.3.0).
- Cancelar o borrar una corrección concreta (se borran todas con `DELETE /me`).
- Supuestos tipo test (fuera de la v0.3.0, spec 0017).

## Diseño

### Dominio (`OpoStudy.Domain/Gradings/`)
- **`GradingStatus`**: `Pending`, `Processing`, `Completed`, `Failed`.
- **`Grading`** (agregado):
  - Campos: `Id` (v7), `UserId`, `PracticalCaseId`, `Status`, `SubmittedAt`, `NextAttemptAt`, `StartedAt?`, `CompletedAt?`, `Attempts`, `MaxPoints` (copia), `Score?`, `Feedback?`, `FailureCode?`, `Corrector?`, `InputTokens?`, `OutputTokens?` y `Answers` (ordenadas por `Order`).
  - `Submit(Guid userId, PracticalCase practicalCase, IReadOnlyList<string> answers, DateTimeOffset now)`: exige el supuesto activo, una respuesta por apartado en el orden de `order` y que al menos una no esté en blanco. Copia en cada `GradingAnswer` los criterios del apartado y su `MaxPoints`: es la copia anunciada en la 0016. Deja `Pending`, con `NextAttemptAt = now` y `Attempts = 0`.
  - `Start(now)`: pasa de `Pending` (o de `Processing` caducado, lo decide la consulta de la cola) a `Processing`, suma 1 a `Attempts` y fija `StartedAt`.
  - `Complete(GradingOutcome outcome, now)`: solo desde `Processing`. Valida el resultado antes de cambiar nada: mismo número de apartados y de criterios por apartado, `Awarded` entre 0 y `Points` en múltiplos de 0,5 y comentarios dentro de los límites. Si falla → `grading.invalid_result`. Si no, calcula `Score` de cada apartado y el total (sumas), y guarda `Corrector` y los tokens.
  - `Retry(now, delay)`: de `Processing` a `Pending`, con `NextAttemptAt = now + delay`.
  - `Fail(string code, now)`: de `Processing` a `Failed`, con `FailureCode` y `CompletedAt`.
  - Cualquier transición desde un estado no permitido → `grading.invalid_status` (es un error de programación, no llega al usuario).
- **`GradingAnswer`**: `Id` (v7), `GradingId`, `Order`, `Text` (vacío si el apartado se deja en blanco), `MaxPoints`, `Criteria` (`IReadOnlyList<CriterionScore>`), `Score?` y `Feedback?`.
- **`CriterionScore`**: `Description`, `Points` y `Awarded?` (`decimal`, `null` hasta que se corrige).
- **`GradingOutcome`** (entrada de `Complete`): `GradingOutcome(IReadOnlyList<PartOutcome> Parts, string Feedback, string Corrector, int? InputTokens, int? OutputTokens)` y `PartOutcome(IReadOnlyList<decimal> Awarded, string Feedback)`. Vive en Domain porque lo valida el agregado.
- **Límites** (en `Limits`): `PartAnswer` = 8000 caracteres por respuesta; `GradingFeedback` = 4000 caracteres, tanto el general como el de cada apartado; `Corrector` = 100.
- **Códigos de error** (`DomainRuleException`, mensajes en español): `grading.case_inactive`, `grading.invalid_answer_count`, `grading.invalid_answer` (longitud), `grading.empty` (todas en blanco), `grading.invalid_result` y `grading.invalid_status`.

### Corrector y cola (`OpoStudy.Infrastructure/Grading/`)
- **`ILlmCorrector`**: `Task<GradingOutcome> GradeAsync(GradingRequest request, CancellationToken ct)`.
  - `GradingRequest(string CaseStatement, IReadOnlyList<GradingPartRequest> Parts)`.
  - `GradingPartRequest(string Statement, string ModelAnswer, IReadOnlyList<GradingCriterion> Criteria, string Answer)`.
  - **Nunca** recibe el id del usuario, su uid ni nada personal: solo el supuesto y las respuestas.
  - Errores: `LlmTransientException` (timeout, 429, 5xx: se reintenta) y cualquier otra excepción (definitiva).
- **`FakeLlmCorrector`** (`Corrector = "fake"`), determinista:
  - Por apartado, `ratio = min(1, caracteres sin espacios de la respuesta / 300)`.
  - Cada criterio recibe `floor(Points × ratio × 2) / 2`.
  - Comentario: «Corrección de prueba (sin LLM): …».
  - Tokens `null`. Sin red ni esperas.
- **`GradingProcessor.ProcessNextAsync(ct) → bool`** (`true` si ha procesado algo). Funciona en tres pasos para no tener una transacción abierta mientras corrige:
  1. **Reclamar** (transacción corta): `SELECT … FROM gradings WHERE (status = 'Pending' AND next_attempt_at <= now) OR (status = 'Processing' AND started_at < now - LeaseTimeout) ORDER BY submitted_at LIMIT 1 FOR UPDATE SKIP LOCKED` (SQL crudo con `FromSql`), seguido de `Start(now)` y commit.
  2. **Corregir** fuera de transacción. Carga el supuesto actual (enunciados y respuestas modelo) y usa los criterios copiados en la corrección. Llama a `GradeAsync` con un timeout de `CorrectorTimeout`.
  3. **Guardar**:
     - Si va bien → `Complete`. Si `Complete` lanza `grading.invalid_result` → `Fail("grading.invalid_result")`.
     - `LlmTransientException` o timeout → `Retry` con un retardo de `RetryDelays[Attempts-1]` mientras `Attempts < MaxAttempts`; si no, `Fail("grading.provider_unavailable")`.
     - Otra excepción → `Fail("grading.provider_error")`.
     - Se registra sin texto de respuestas ni uid: id de la corrección, estado, intentos y duración.
  - Si el supuesto está inactivo al corregir, se corrige igual: el envío ya se aceptó.
  - Un `Processing` caducado (el Worker murió a medias) se reclama de nuevo y cuenta como intento.
- **`GradingOptions`** (sección `Grading`, `ValidateOnStart`):

  | Opción | Valor por defecto | Usa |
  |---|---|---|
  | `Provider` | `"Fake"`; cualquier otro valor falla al arrancar hasta la 0019 | Worker |
  | `MonthlyQuota` | 10 | API |
  | `MaxAttempts` | 3 | Worker |
  | `RetryDelays` | `[00:01:00, 00:05:00]` | Worker |
  | `CorrectorTimeout` | `00:02:00` | Worker |
  | `LeaseTimeout` | `00:10:00`, mayor que `CorrectorTimeout` | Worker |
  | `PollInterval` | `00:00:05` | Worker |

  `AddGrading(IConfiguration)` en Infrastructure registra las opciones, `ILlmCorrector` según `Provider`, `GradingProcessor` y `TimeProvider`.
- **`GradingWorker`**: crea un scope por iteración. Procesa de uno en uno mientras haya trabajo y, si no hay, espera `PollInterval`. Una excepción inesperada se registra y no detiene el bucle. Respeta el `stoppingToken`.

### API (`Features/Gradings/GradingsEndpoints.cs`, tag `Gradings`)
| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| POST | `/api/v1/practical-cases/{id:guid}/gradings` | Usuario | `SubmitGradingRequest` → `202 GradingResponse` + `Location: /api/v1/gradings/{id}`; `404 practical_case.not_found` (no existe, inactivo u oposición inactiva); `409 grading.already_pending`; `422 grading.quota_exceeded`, `grading.invalid_answer_count`, `grading.invalid_answer`, `grading.empty` |
| GET | `/api/v1/gradings/{id:guid}` | Dueño | → `200 GradingResponse`; `404 grading.not_found` si no existe o es de otro usuario |
| GET | `/api/v1/gradings?skip=0&take=20` | Usuario | → `200 IReadOnlyList<GradingSummaryResponse>`, de la más reciente a la más antigua; mismas reglas de `skip`/`take` que `GET /quizzes` |
| GET | `/api/v1/me/grading-quota` | Usuario | → `200 GradingQuotaResponse` |

```csharp
public sealed record SubmitGradingRequest(IReadOnlyList<string> Answers); // una por apartado, en orden
public sealed record GradingResponse(
    Guid Id, Guid PracticalCaseId, string CaseTitle, string Status, int Attempts,
    DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt, int MaxPoints, decimal? Score,
    string? Feedback, string? FailureCode, IReadOnlyList<GradingAnswerResponse> Answers);
public sealed record GradingAnswerResponse(
    int Order, string Statement, string Text, int MaxPoints, decimal? Score, string? Feedback,
    string? ModelAnswer, IReadOnlyList<CriterionScoreResponse>? Criteria);
public sealed record CriterionScoreResponse(string Description, int Points, decimal Awarded);
public sealed record GradingSummaryResponse(
    Guid Id, Guid PracticalCaseId, string CaseTitle, string? Section, string Status,
    DateTimeOffset SubmittedAt, int MaxPoints, decimal? Score);
public sealed record GradingQuotaResponse(int Limit, int Used, int Remaining, DateTimeOffset ResetsAt);
```
- `Status` va como string (`"Pending"`, `"Processing"`, `"Completed"` o `"Failed"`), igual que `QuizResponse.Mode`.
- `Criteria` y `ModelAnswer` **solo** se devuelven con `Completed`; en el resto de estados van a `null`. Así nadie ve la rúbrica antes de corregir. `ModelAnswer` y `Statement` salen del supuesto actual; los criterios, de la copia.
- **Cuota** (`GradingQuota`):
  - Cuentan las correcciones del mes natural en la zona horaria del usuario (`User.TimeZone`) que están en `Pending`, `Processing` o `Completed`. Las `Failed` no cuentan.
  - `ResetsAt` es el inicio del mes siguiente en esa zona, expresado en UTC.
  - Se comprueba antes de crear la corrección → `grading.quota_exceeded` (mensaje: «Has agotado tus {N} correcciones de este mes.»).
- **Una sola corrección en curso por usuario**. Un índice único parcial lo garantiza también con peticiones concurrentes: si hay violación de ese índice (comprobar el nombre con `DbConstraintNames`) → `409 grading.already_pending` («Ya tienes una corrección en curso. Espera a que termine.»). Esa regla limita además la carrera de la cuota a un envío.
- Errores nuevos en `Api/Errors/DomainErrors`: `grading.not_found` (404, «La corrección no existe.»), `grading.already_pending` (409), `grading.quota_exceeded` (422) y los de validación del dominio (422).
- Consultas de lectura con `AsNoTracking` y proyección, como `PracticalCasesEndpoints`.

### Datos (migración `AddGradings`)
- **`gradings`**:
  - Columnas: `id` uuid PK; `user_id` FK → `users` (**cascade**); `practical_case_id` FK → `practical_cases` (restrict); `status` varchar(20) (string, como `QuizMode`); `submitted_at`, `next_attempt_at`, `started_at` null y `completed_at` null (timestamptz); `attempts` int; `max_points` int; `score` numeric(5,2) null; `feedback` varchar(4000) null; `failure_code` varchar(100) null; `corrector` varchar(100) null; `input_tokens` y `output_tokens` int null; concurrencia con `xmin` (como `users`).
  - Índices: `(user_id, submitted_at desc)`; `(status, next_attempt_at)` para la cola; **único parcial** `(user_id) WHERE status IN ('Pending','Processing')`.
- **`grading_answers`**:
  - Columnas: `id` uuid PK; `grading_id` FK (cascade); `order` int; `text` varchar(8000); `max_points` int; `criteria` jsonb, con `OwnsMany(...).ToJson("criteria")` como en la 0016 (`description`, `points` y `awarded`); `score` numeric(5,2) null; `feedback` varchar(4000) null.
  - Único `(grading_id, order)`.
- `DELETE /me` borra las correcciones del usuario por cascada. Hay que comprobarlo con un test.

### Worker y AppHost
- `Program.cs` del Worker: `builder.Services.AddGrading(builder.Configuration)`. `appsettings.json` del Worker y de la API: sección `Grading` con `Provider: "Fake"` y los valores por defecto.
- AppHost: sin cambios (el Worker ya tiene la BD y espera a la API, que aplica las migraciones).

### App
Sin cambios (0020/0021).

## Seguridad
- Todas las rutas exigen JWT. Una corrección solo la ve su dueño: se filtra por `UserId` y, si es de otro usuario, se responde 404, no 403.
- Validación: tamaño de cada respuesta (8000), número de respuestas = número de apartados y `{id}` con `:guid`. El limitador de peticiones por usuario ya cubre la ruta. La cuota mensual y la regla de una sola corrección en curso acotan el coste del LLM (0019).
- Privacidad: al corrector solo llegan el enunciado, los apartados, la rúbrica, la respuesta modelo y las respuestas del usuario. Nunca el uid, el email ni el id del usuario. Los logs no incluyen el texto de las respuestas ni el uid.
- Sin secretos en esta spec: el corrector falso no tiene key. La API key de Gemini llega en la 0019 (user-secrets o Docker secret).

## Criterios de aceptación
- [ ] `POST /practical-cases/{id}/gradings` con respuestas válidas → 202, `Location` correcta y estado `Pending`. La fila guarda la copia de los criterios.
- [ ] Con el Worker en marcha (AppHost), la corrección pasa a `Completed` en segundos con la puntuación del corrector falso. `GET /gradings/{id}` devuelve criterios con `Awarded`, `Score` por apartado y total, y `ModelAnswer`.
- [ ] Mientras no está `Completed`, la respuesta no contiene ni criterios ni respuesta modelo.
- [ ] Un segundo envío con otro en curso → 409 `grading.already_pending`, también con dos peticiones simultáneas.
- [ ] Con la cuota gastada → 422 `grading.quota_exceeded`. Las `Failed` no cuentan, y el mes se calcula en la zona horaria del usuario.
- [ ] `GET /me/grading-quota` devuelve `Limit`, `Used`, `Remaining` y `ResetsAt` correctos.
- [ ] Número de respuestas distinto del de apartados, una respuesta de más de 8000 caracteres o todas en blanco → 422 con su `code`.
- [ ] La corrección de otro usuario o una inexistente → 404 `grading.not_found`. Una petición anónima → 401.
- [ ] Un error transitorio del corrector reintenta según `RetryDelays`; al agotar `MaxAttempts` queda `Failed` con `grading.provider_unavailable`. Un resultado inválido → `Failed` con `grading.invalid_result`.
- [ ] Un `Processing` con el lease caducado se vuelve a procesar.
- [ ] Dos procesadores a la vez nunca cogen la misma corrección (`SKIP LOCKED`).
- [ ] `DELETE /me` borra las correcciones del usuario.
- [ ] `Grading:Provider` con un valor desconocido hace fallar el arranque del Worker.

## Plan de tests
- **Unitarios (`OpoStudy.Domain.Tests`):**
  - `Submit`: copia de criterios y `MaxPoints`; número de respuestas incorrecto; respuesta demasiado larga; todas en blanco; supuesto inactivo.
  - Transiciones válidas e inválidas (`grading.invalid_status`).
  - `Complete`: calcula las puntuaciones; rechaza un número de apartados o criterios distinto, `Awarded` negativo, por encima de `Points` o no múltiplo de 0,5, y comentarios demasiado largos, sin cambiar nada.
  - `Retry` y `Fail`.
- **`FakeLlmCorrector`:** determinista; respuesta vacía → 0; respuesta larga → máximo; redondeo a 0,5.
- **`GradingProcessorTests` (Infrastructure.Tests, Testcontainers, corrector sustituido con NSubstitute y `FakeTimeProvider`):**
  - Cola vacía → `false`.
  - Camino feliz → `Completed`.
  - No coge un `Pending` con `next_attempt_at` futuro.
  - Transitorio → `Retry` con el retardo correcto; agotado → `Failed`.
  - Excepción definitiva → `provider_error`; resultado inválido → `invalid_result`.
  - Lease caducado → se reclama; lease vigente → no.
  - Dos `ProcessNextAsync` concurrentes con dos filas → cada uno coge una distinta.
  - Con una sola fila, uno la coge y el otro devuelve `false`.
- **Integración API (`GradingsEndpointsTests`):**
  - 202 con `Location`; 404 de supuesto inexistente o inactivo.
  - 422 de cada validación y de cuota agotada (con la cuota configurada a 2 en `ApiFactory`).
  - Las `Failed` no cuentan para la cuota.
  - 409 en curso, incluido un envío concurrente con `Task.WhenAll`.
  - Detalle propio, ajeno (404) y sin criterios antes de `Completed`.
  - Detalle completo tras completar la corrección con el dominio en la BD.
  - Historial paginado y ordenado.
  - Cuota con zona horaria (usuario en `Pacific/Auckland` cerca del cambio de mes).
  - `DELETE /me` borra las correcciones.
  - 401 anónimo.
- **Arquitectura:** `OpoStudy.Domain` sigue sin dependencias. `ILlmCorrector` vive en Infrastructure y la API no lo referencia.
- **Manual:** arrancar el AppHost, enviar un supuesto desde Scalar, ver en los logs del Worker que pasa a `Completed` y leerlo con `GET /gradings/{id}`.

## Decisiones de implementación
- **Namespace `OpoStudy.Infrastructure.Gradings`** (carpeta `Gradings/`, no `Grading/`): un namespace `...Grading` taparía al agregado `Grading` dentro de Infrastructure.
- **`AddGradingOptions` + `AddGrading`:** la API solo necesita la cuota, así que llama a `AddGradingOptions` (opciones validadas al arrancar) y nunca registra ni referencia `ILlmCorrector`; un test de arquitectura lo comprueba. `AddGrading` (Worker) añade el corrector, `GradingProcessor` y `TimeProvider`. Infrastructure referencia ahora `Microsoft.Extensions.Hosting` (para `ValidateOnStart` y el binder de configuración).
- **`RetryDelays` es `TimeSpan[]?` sin valor por defecto en la propiedad:** el binder de configuración añade los elementos configurados a un array con valor por defecto (saldrían duplicados). `RetryDelayAfter(attempts)` usa los valores por defecto si es `null` y repite el último retardo si hay más intentos que retardos. `appsettings.json` de la API y del Worker llevan la sección `Grading` con los valores por defecto.
- **Reclamar de la cola:** el `SELECT ... FOR UPDATE SKIP LOCKED` devuelve solo el id (`SqlQuery<Guid>`) y la corrección se carga después, en la misma transacción, con una consulta normal; así no se compone SQL crudo con los `Include`. La transacción va dentro de `CreateExecutionStrategy().ExecuteAsync(...)` porque Aspire activa los reintentos de Npgsql.
- **Cancelación por apagado:** si el `stoppingToken` se cancela durante la corrección, la excepción se propaga y la corrección queda en `Processing`; el lease caducará y se reclamará de nuevo. Una cancelación sin apagado es el timeout de `CorrectorTimeout` y se trata como transitoria.
- **Nombre del índice único parcial:** `ux_gradings_one_in_progress_per_user` (en `DbConstraintNames`), con filtro `status IN ('Pending', 'Processing')`, los valores reales guardados por la conversión a string del enum. El 409 `grading.already_pending` lo devuelve `DomainRuleExceptionHandler` (como los demás conflictos por índice), además de una comprobación previa en el endpoint.
- **Cuota:** `GradingQuota.PeriodAt(timeZone, now)` vive en Domain (reglas puras) y trata el caso de zonas sin medianoche el día del cambio horario. El mensaje de `grading.quota_exceeded` incluye el límite, por lo que `DomainErrors.Problem` tiene una sobrecarga con mensaje propio.
- **Lecturas:** `GET /gradings/{id}` carga la corrección y el supuesto con `AsNoTracking` (son pocas filas) en lugar de una proyección única; el historial sí usa proyección. Los textos de respuesta se guardan recortados (`Trim`).
- **Validación del resultado:** además de lo de la spec, `Complete` exige un `Corrector` no vacío (máx. 100) y tokens no negativos; los comentarios en blanco se guardan como `null`.
- **Tests de la cola:** `GradingProcessorTests` vacía la tabla `gradings` al empezar cada test porque la cola es global a la base de datos; por eso todos los tests que usan la cola están en una única clase.
- **Fuera de alcance, sin tocar:** el test manual con el AppHost (Scalar + logs del Worker) queda pendiente de hacerse a mano.
