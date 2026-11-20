# 0014 — Modo offline

- **Issue:** #41
- **Milestone:** v0.2.0
- **Estado:** lista

## Objetivo
Poder estudiar sin conexión (metro, avión, pueblo sin cobertura o sin Tailscale). La app guarda en SQLite el contenido de las oposiciones que el usuario abre. Sin conexión, corrige los tests con esa copia local y encola las respuestas. Al recuperar la conexión, las sincroniza y el servidor las cuenta para el objetivo diario, la racha y el XP según la hora real de cada respuesta.

## Decisiones
- **El offline es un respaldo.** Con conexión, todo sigue como en las specs 0009-0015: corrige el servidor y el XP y la racha llegan al momento. La app solo corrige en local si no hay conexión o si se corta a mitad de test. Un test que pasa a offline sigue en offline hasta que termina o se sale de él. Así no se mezclan respuestas online y offline del mismo test.
- **Se descarga la oposición entera, automáticamente:** temas, preguntas activas, opciones con la correcta, aclaración y referencia legal. Se descarga al abrir la oposición con conexión y se revalida con `ETag` (304 si no ha cambiado). Hoy son ~50 preguntas y con el temario completo serán unos pocos MB.
- **La respuesta correcta sale antes de responder, pero solo en este paquete.** Es contenido público (BOE), la app no tiene clasificaciones y el usuario solo podría hacerse trampas a sí mismo. Los endpoints de test siguen sin revelarla.
- **SQLite con `Microsoft.Data.Sqlite` y SQL a mano**, como dice el ADR. No usa EF Core ni sqlite-net: es compatible con el trimming y se prueba en `Mobile.Core.Tests` sobre Windows. El contenido se guarda como un JSON por oposición (en memoria se filtra rápido). El test local y sus respuestas pendientes van en tablas, con transacciones.
- **Sincronización idempotente por test.** El id del test lo genera quien lo crea: el servidor en línea y la app (`Guid.CreateVersion7()`) sin conexión. La app envía el test entero con sus respuestas pendientes a un único endpoint. El servidor crea el test si no existe, ignora las respuestas que ya tiene y aplica las nuevas en una transacción. Reintentar la misma petición no cambia nada.
- **Cada respuesta cuenta por su hora real.** La app envía `AnsweredAt`. El servidor lo acota a `[ahora − 7 días, ahora]` y procesa las respuestas en orden cronológico con `ProgressRecorder`, usando esa hora en lugar de la del servidor: día local, liquidación de la racha (`Settle`), objetivo, bonus y XP.
- **La app sincroniza antes de leer la racha.** Si un día ya se liquidó antes de que llegaran sus respuestas (racha perdida o protector gastado), **no se repara**: las respuestas suman al día y dan XP, pero la racha no cambia. Es una limitación aceptada. Con un solo dispositivo solo ocurre si falla la sincronización previa.
- **Sin repaso de falladas offline:** necesita el historial del servidor. Un repaso empezado en línea sí puede terminarse offline si se corta la conexión (todas sus preguntas están en la caché).
- **La cola es por usuario** (uid de Firebase). Cerrar sesión no la borra: se sincroniza cuando ese usuario vuelve a entrar. Borrar la cuenta borra su cola. La caché de contenido es común.

## Alcance
**Incluye:**
- API: descarga del contenido offline de una oposición con `ETag` y endpoint de sincronización de un test.
- `ProgressRecorder` con la hora de la respuesta en lugar de la del servidor.
- App: base de datos SQLite, descarga y caché del contenido, tests offline (por tema y aleatorio), paso a offline a mitad de test, cola de respuestas pendientes, sincronización automática e indicador de estado sin conexión.

**No incluye:**
- Repaso de falladas offline, ni el contador de falladas sin conexión.
- Reparar rachas o devolver protectores por respuestas que llegan tarde.
- Caché de la racha, el nivel o el historial para verlos offline: esas tarjetas muestran su error de carga, como ahora.
- Actualizar el recordatorio diario (0013) con respuestas offline: puede saltar aunque se haya cumplido el objetivo sin conexión. Se corrige al sincronizar y volver al inicio.
- Compresión HTTP: la hará Caddy en el VPS (#21).
- Sincronizar entre varios dispositivos o resolver conflictos entre ellos.

## Diseño
### Dominio
Glosario (ADR 0001): `OfflineContent`, `PendingAnswer` y `QuizSync`. Ya añadidos.

- `QuizAttempt.Create` y `Answer` no cambian: ya reciben la hora (`now`). En la sincronización se les pasa la hora acotada del cliente.
- `QuizAttempt` necesita poder crearse con un id dado: nueva sobrecarga (o parámetro opcional) `Create(Guid id, …)` que valida `id != Guid.Empty`. La actual sigue generando el id.
- Sin cambios en `Streak`, `DailyActivity` ni `User`.

### API
Rutas bajo `/api/v1`. Todas exigen usuario autenticado.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/exams/{examId}/offline-content` | Usuario | `If-None-Match` opcional → `200 OfflineContentResponse` + `ETag` · `304` · `404 exam.not_found` |
| POST | `/quizzes/sync` | Usuario | `SyncQuizRequest` → `200 SyncQuizResponse` · `400` · `404` · `409` · `422` |

**`GET /exams/{examId}/offline-content`** (en `ExamsEndpoints`):
- Mismo 404 que `/topics` si la oposición no existe o no está activa.
- Proyección `AsNoTracking`: temas por `Number`, preguntas **activas** de cada tema (por `Key`) y opciones por `Order`.
- `Version` = SHA-256 en hex (minúsculas) del JSON serializado de `Topics`. Se envía como `ETag: "<version>"` y `Cache-Control: no-cache`. Si `If-None-Match` coincide (con o sin comillas), `304` sin cuerpo.
- Contratos nuevos en `Contracts/Offline`:
  - `OfflineContentResponse(Guid ExamId, string Version, ExamResponse Exam, IReadOnlyList<OfflineTopicResponse> Topics)`.
  - `OfflineTopicResponse(Guid Id, int Number, string Title, string? Section, IReadOnlyList<OfflineQuestionResponse> Questions)`.
  - `OfflineQuestionResponse(Guid Id, string Statement, string Explanation, string LegalReference, IReadOnlyList<OfflineOptionResponse> Options)`.
  - `OfflineOptionResponse(Guid Id, int Order, string Text, bool IsCorrect)`.

**`POST /quizzes/sync`** (en `QuizzesEndpoints`):
- Contratos en `Contracts/Quizzes`:
  - `SyncQuizRequest(Guid Id, Guid ExamId, Guid? TopicId, string Mode, DateTimeOffset StartedAt, IReadOnlyList<Guid> QuestionIds, IReadOnlyList<SyncAnswerRequest> Answers)`.
  - `SyncAnswerRequest(Guid QuestionId, Guid OptionId, DateTimeOffset AnsweredAt)`.
  - `SyncQuizResponse(Guid QuizId, int AcceptedCount, int DuplicateCount, int RejectedCount, int XpEarned, bool QuizFinished)`.
- Validación de entrada (`400 ValidationProblem`): `Id` y `ExamId` no vacíos; `QuestionIds` de 1 a `Limits.MaxQuestionsPerQuiz`, sin vacíos ni repetidos; `Answers` con como mucho tantas respuestas como preguntas, sin `QuestionId` repetido y con ids no vacíos.
- Hora: `clamp(t) = min(max(t, now − 7 días), now)` para `StartedAt` y cada `AnsweredAt`. La constante va en `Limits` (`MaxOfflineAge = 7 días`).
- Si **no existe** un test con ese `Id`:
  - `Mode` debe ser `ByTopic` (con `TopicId`) o `Random` (sin él); si no, `400`. `Review` no se crea offline.
  - La oposición debe estar activa y el tema ser suyo: si no, `404` (`exam.not_found` / `topic.not_found`).
  - Todas las `QuestionIds` deben existir y ser de esa oposición (y de ese tema en `ByTopic`). Se admiten preguntas desactivadas después, porque la caché puede estar desfasada. Si no se cumple: `422 quiz.invalid_questions`, «Las preguntas del test no son válidas.».
  - `QuizAttempt.Create(id, user.Id, …, clamp(StartedAt))`.
- Si **existe y es del usuario**: `QuestionIds` debe coincidir con las del intento en el mismo orden. Si no, `409 quiz.sync_conflict`, «El test no coincide con el guardado en el servidor.». `ExamId`, `TopicId`, `Mode` y `StartedAt` de la petición se ignoran: un test creado online (incluido un `Review`) puede terminarse offline.
- Si **existe y es de otro usuario**: `409 quiz.sync_conflict`, sin revelar nada más.
- Respuestas, ordenadas por `clamp(AnsweredAt)` ascendente:
  - Pregunta ya respondida en el intento (con la opción que sea) → `DuplicateCount`, sin cambios.
  - Pregunta que no es del intento u opción ajena → `RejectedCount`, sin cambios. No aborta el resto.
  - Si no: `attempt.Answer(question, optionId, at)` y `progressRecorder.RecordAnswerAsync(user, isCorrect, at, ct)` → `AcceptedCount`. `XpEarned` suma el XP de las respuestas y los bonus.
- Un único `SaveChangesAsync`. El conflicto de concurrencia da `409` como hasta ahora: la app reintentará.
- Códigos nuevos en `DomainErrors`: `quiz.invalid_questions` (422) y `quiz.sync_conflict` (409).

**`ProgressRecorder`**:
- `RecordAnswerAsync(user, isCorrect, at, ct)` ya recibe la hora. Se documenta que es la hora de la respuesta y no la del servidor, y `SubmitAnswer` le sigue pasando `timeProvider.GetUtcNow()`.
- Corrección necesaria para respuestas tardías: el bonus pasa a `XpRules.ForGoalMet(Math.Max(1, user.Streak.CurrentAt(day)))`. Si un día ya liquidado se cumple tarde, `RegisterGoalMet` lo ignora y `CurrentAt` puede valer 0, y `ForGoalMet(0)` lanza una excepción.

Añade los contratos nuevos a `ApiJsonContext` y al cliente Refit:
- `Task<IApiResponse<OfflineContentResponse>> GetOfflineContentAsync(Guid examId, [Header("If-None-Match")] string? ifNoneMatch, CancellationToken)`. Con `IApiResponse`, el 304 no lanza una excepción.
- `Task<SyncQuizResponse> SyncQuizAsync([Body] SyncQuizRequest request, CancellationToken)`.

### Datos
Servidor: sin cambios de esquema ni migración (`Model_HasNoPendingChangesToMigrate` en verde).

App: SQLite en `Mobile.Core/Offline`, paquete `Microsoft.Data.Sqlite` (versión en `Directory.Packages.props`; trae `SQLitePCLRaw.bundle_e_sqlite3` con el nativo de Android). Fichero `offline.db3` en `FileSystem.AppDataDirectory`; la ruta llega por opciones para usar un fichero temporal en los tests. Esquema versionado con `PRAGMA user_version`; la versión 1 crea:
- `offline_content(exam_id TEXT PRIMARY KEY, version TEXT NOT NULL, downloaded_at TEXT NOT NULL, payload TEXT NOT NULL)`: `payload` es el `OfflineContentResponse` serializado con `ApiJsonContext`.
- `local_quizzes(id TEXT PRIMARY KEY, user_id TEXT NOT NULL, exam_id TEXT NOT NULL, topic_id TEXT NULL, mode TEXT NOT NULL, title TEXT NOT NULL, started_at TEXT NOT NULL, question_ids TEXT NOT NULL, finished INTEGER NOT NULL DEFAULT 0)`: `question_ids` es un JSON con los ids en orden.
- `pending_answers(quiz_id TEXT NOT NULL REFERENCES local_quizzes(id) ON DELETE CASCADE, question_id TEXT NOT NULL, option_id TEXT NOT NULL, answered_at TEXT NOT NULL, is_correct INTEGER NOT NULL, synced INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (quiz_id, question_id))`. `synced = 1` marca las respuestas que ya están en el servidor: las adoptadas de un test online y las de un test sin terminar ya sincronizado, que así se puede seguir. La cola, el recuento de pendientes y la limpieza solo miran `synced = 0`.
- Fechas en ISO 8601 UTC (`"O"`), guids en texto. `PRAGMA foreign_keys = ON` en cada conexión.

### App
Usa la skill `maui-ui` (tema claro y oscuro, capturas en el emulador).

**Piezas en `Mobile.Core/Offline`** (interfaces para poder testearlas):
- `OfflineDatabase`: abre conexiones, aplica el esquema y expone operaciones con transacciones.
- `ConnectivityErrors.IsConnectivityError(Exception)`: `true` para `HttpRequestException` sin `StatusCode`, para el timeout del `HttpClient` (`TaskCanceledException` que no viene del token del llamador) y para `IOException`/`SocketException` internas. **No** usa solo `IConnectivity`: con Internet pero sin Tailscale la API tampoco responde.
- `INetworkStatus` (implementación en `OpoStudy.Mobile` sobre `Connectivity.Current`) con `bool HasInternet` y el evento `Changed`. Sirve para disparar la sincronización y para el banner. La decisión de pasar a offline la toma el fallo de la llamada.
- `IOfflineContentStore`:
  - `RefreshAsync(examId)`: llama con el `Version` guardado como `If-None-Match`; con 200, guarda. Los errores de conectividad se ignoran.
  - `GetAsync(examId)`: contenido cacheado, o `null`. Lo deserializa una vez y lo mantiene en memoria.
  - `ListExamsAsync()`: las oposiciones descargadas.
- `IOfflineQuizService`:
  - `CreateAsync(examId, topicId?, questionCount)`: elige al azar entre las preguntas cacheadas (del tema o de toda la oposición), crea el `local_quizzes` con `Guid.CreateVersion7()` y el usuario actual, y devuelve un `QuizResponse` montado desde la caché (`Feedback = null`). Si no hay preguntas, el mismo mensaje que el 422 `quiz.no_questions_available`.
  - `AdoptAsync(QuizResponse quiz, string title)`: guarda en `local_quizzes` un test creado online que pasa a offline, junto con sus respuestas ya hechas, solo para saber el orden y lo respondido. Esas respuestas **no** se encolan: ya están en el servidor.
  - `GetAsync(quizId)`: el test local como `QuizResponse`, con `Feedback` de las preguntas respondidas offline, calculado desde la caché.
  - `AnswerAsync(quizId, questionId, optionId)`: corrige con la caché, inserta en `pending_answers` (`DateTimeOffset` del `TimeProvider`) y devuelve el `AnswerFeedback` con los recuentos. Marca `finished` al responder la última. Si la pregunta no está en la caché, lanza una excepción y el ViewModel muestra el error de envío actual.
  - `GetLatestUnfinishedAsync()` y `PendingAnswerCountAsync()`, del usuario actual.
- `IQuizSyncService.FlushAsync(ct)`: con un `SemaphoreSlim` (una sola a la vez). Para cada test local del usuario actual con respuestas pendientes, por `started_at`, envía `SyncQuizAsync` con todas sus pendientes.
  - **200:** si el test está terminado, borra el test local con sus respuestas; si no, marca las enviadas como `synced = 1`.
  - **Error de conectividad, 401, 408, 429, 409 por concurrencia (`quiz.concurrent_update`, `concurrency.conflict`) o 5xx:** se deja para la próxima vez y se para el bucle.
  - **Cualquier otro 4xx** (400, 404, 422, `quiz.sync_conflict`): no se arreglará reintentando. Se borra el test local con sus pendientes y se registra en el log (sin ids de usuario).
  - Devuelve cuántas respuestas se han sincronizado.
  - Limpieza: borra los tests locales sin pendientes que estén terminados o tengan más de 30 días.
- **Usuario actual:** `IAuthService` gana `string? UserId`, que lee el claim `user_id` (o `sub`) del payload del `IdToken` guardado (base64url, sin validar: es nuestro propio token). Sin sesión, la cola no se lee ni se sincroniza. `DeleteAccount` borra los tests locales de ese usuario.

**Cuándo se sincroniza:**
- Al volver la conexión (`INetworkStatus.Changed` con Internet).
- Al aparecer la pantalla de inicio, **antes** de cargar la racha y el nivel (con su `CancellationToken`; si falla, se cargan igual).
- Al terminar un test offline, si hay Internet.
- Con un test en curso no se dispara la sincronización de ese mismo test: la de la pantalla de inicio llega después.

**Pantallas:**
- **Banner sin conexión**, como componente reutilizable en inicio, oposiciones, temas y test: «Sin conexión. Tus respuestas se guardan y se sincronizarán al recuperarla.». En inicio, si hay pendientes y hay conexión, «Sincronizando N respuestas…» mientras dura. Se muestra cuando la última llamada a la API ha fallado por conectividad o `HasInternet` es `false`, y se oculta tras una llamada con éxito.
- **Oposiciones** (`ExamsViewModel`): si `GetExamsAsync` falla por conectividad, lista las de `ListExamsAsync()` con el banner. Si no hay ninguna descargada: «Sin conexión y sin contenido descargado. Abre una oposición con conexión para poder estudiar sin ella.».
- **Temas** (`TopicsViewModel`):
  - Con conexión: carga como ahora y lanza `RefreshAsync(examId)` en segundo plano, sin bloquear ni mostrar errores.
  - Sin conexión: temas y recuentos desde la caché. El interruptor de falladas se desactiva con «El repaso de falladas necesita conexión.».
  - Crear un test que falla por conectividad (o con el banner activo) se hace con `IOfflineQuizService.CreateAsync`.
- **Test** (`QuizViewModel`):
  - Nueva propiedad `IsOffline`.
  - Carga: si `GetQuizAsync` falla por conectividad, prueba con `IOfflineQuizService.GetAsync`. Un test local se abre siempre en local.
  - Responder online que falla por conectividad: `AdoptAsync` del test en memoria (si no estaba adoptado), `AnswerAsync` local y `IsOffline = true`. A partir de ahí, todas las respuestas del test van a local.
  - Feedback offline: igual que online (correcta, aclaración y referencia legal), sin XP ni aviso de objetivo. En su lugar, la línea «XP pendiente de sincronizar».
  - Resumen offline: aciertos y fallos como ahora; en lugar del XP total, «Tus respuestas se sincronizarán cuando haya conexión.».
  - La confirmación al salir (0012) no cambia: el test local también se puede continuar.
- **Inicio** (`HomeViewModel`):
  - Sincroniza antes de cargar.
  - Tarjeta «Test a medias»: si hay un test local sin terminar más reciente que el del servidor, o si la carga del servidor falla por conectividad, muestra el local (`GetLatestUnfinishedAsync`). «Continuar» abre el test con su `QuizId` y `QuizTitle`, igual que ahora.
  - Las tarjetas de racha y nivel, sin conexión, muestran su error de carga actual.
- Registro en DI (`MauiProgram`): `OfflineDatabase` y los servicios como singletons, `INetworkStatus` y `Connectivity.Current`.

## Seguridad
- Ambos endpoints sacan el usuario del claim `sub` (`CurrentUserService`). La sincronización solo crea y modifica tests del usuario del token. Un `Id` de otro usuario da `409 quiz.sync_conflict`, sin datos.
- El servidor vuelve a corregir cada respuesta con su BD: la corrección local solo sirve para mostrar el feedback. El cliente no envía aciertos, XP ni días.
- La hora del cliente se acota a los últimos 7 días y nunca al futuro. Un cliente manipulado podría rellenar días de esa semana; es aceptable en una app personal sin clasificaciones (ver *Decisiones*).
- El paquete offline expone las respuestas correctas a cualquier usuario autenticado: decisión aceptada (contenido público, sin competición). Tiene el rate limiting por usuario del grupo `/api/v1`.
- Límites de tamaño: 100 preguntas y 100 respuestas por sincronización.
- La base de datos local está en el almacenamiento privado de la app. Solo guarda contenido, ids y horas: ni tokens ni el email. El uid de Firebase no se escribe en los logs.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [ ] `GET /exams/{id}/offline-content` devuelve temas, preguntas activas, opciones con `isCorrect`, aclaración y referencia legal, con `ETag`. Con `If-None-Match` igual devuelve 304. Si cambia el contenido, cambia el `ETag`.
- [ ] Sincronizar un test nuevo lo crea con su id, sus preguntas y las respuestas con su hora. Repetir la misma petición devuelve `DuplicateCount` = n, `AcceptedCount` = 0 y no cambia el XP.
- [ ] Sincronizar respuestas nuevas de un test creado online (incluido un `Review`) las añade. Si las preguntas no coinciden, 409 `quiz.sync_conflict`. Un id de test de otro usuario también da 409 y no lo modifica.
- [ ] Una respuesta con opción ajena cuenta como rechazada y no impide aplicar las demás.
- [ ] Con `FakeTimeProvider`: 10 respuestas offline de ayer sincronizadas hoy cumplen el objetivo de **ayer**, suman racha y dan el bonus. Las de hoy cuentan para hoy.
- [ ] Una respuesta con hora futura cuenta como ahora. Una de hace 10 días cuenta como de hace 7.
- [ ] Respuestas tardías de un día ya liquidado (racha perdida) suman al día y al XP sin error y sin cambiar la racha.
- [ ] `SubmitAnswer` y el resto de endpoints se comportan igual que antes (los tests existentes siguen en verde).
- [ ] App en modo avión (o sin Tailscale): se listan las oposiciones descargadas, se hace un test por tema y uno aleatorio con su aclaración, y se ve el banner.
- [ ] Al cortar la conexión a mitad de un test online, la siguiente respuesta se corrige en local sin perder las anteriores.
- [ ] Al recuperar la conexión, las respuestas se sincronizan solas, y la racha y el XP del inicio las reflejan. Cerrar la app con respuestas pendientes no las pierde.
- [ ] Las respuestas pendientes de un usuario no se envían con la sesión de otro.
- [ ] Verificado en el emulador en tema claro y oscuro.

## Plan de tests
- **Unitarios (Domain):** `QuizAttempt.Create` con id dado (y `Guid.Empty`, que falla).
- **Integración (API, Testcontainers, `ContentSeeder`, `FakeTimeProvider`):**
  - `OfflineContentEndpointsTests`:
    - Sin token, 401.
    - Contenido sin preguntas inactivas y con `isCorrect`.
    - 304 con `If-None-Match`.
    - `ETag` distinto tras cambiar el texto de una pregunta en la BD.
    - 404 con una oposición inactiva.
  - `QuizSyncEndpointsTests`:
    - Creación nueva.
    - Idempotencia (petición repetida).
    - Ampliación de un test online.
    - Desajuste de preguntas, 409.
    - Test de otro usuario, 409, sin cambios.
    - Validaciones 400: vacíos, repetidos, más de 100 y `Review` nuevo.
    - Tema de otra oposición, 404.
    - Pregunta de otra oposición, 422.
    - Opción ajena, rechazada.
    - Acotado de horas (futura y de 10 días).
    - Objetivo, racha y bonus de ayer con respuestas tardías.
    - Día ya liquidado sin excepción.
    - Las respuestas sincronizadas cuentan en `/mistakes`.
  - `Model_HasNoPendingChangesToMigrate` en verde.
- **Unitarios (Mobile.Core)**, con SQLite en un fichero temporal y `FakeTimeProvider`:
  - `OfflineDatabase`: crea el esquema y es idempotente al reabrir.
  - `OfflineContentStore`: guarda con 200, no cambia con 304 y no falla sin conexión.
  - `OfflineQuizService`:
    - Crear por tema y aleatorio sin superar las disponibles.
    - Corregir con la caché y guardar la pendiente.
    - Marcar `finished`.
    - `Adopt` sin encolar lo ya respondido.
    - Separación por usuario.
  - `QuizSyncService`:
    - 200 borra.
    - Conectividad, 409 por concurrencia y 5xx conservan.
    - 422 descarta.
    - No hay dos sincronizaciones a la vez.
    - Limpieza de tests antiguos.
  - `ConnectivityErrors`: cada tipo de excepción, y que la cancelación del llamador no cuenta como sin conexión.
  - `AuthService.UserId` desde un token de ejemplo.
  - ViewModels:
    - `ExamsViewModel` y `TopicsViewModel` con la API caída (caché, banner e interruptor de falladas desactivado).
    - `QuizViewModel`: paso a offline a mitad de test, feedback y resumen offline.
    - `HomeViewModel`: sincroniza antes de cargar y muestra el test a medias local.
  - Serialización: `ApiJsonContext` con los contratos nuevos.
- **Manual (emulador, AppHost):**
  1. Abrir la oposición con conexión (se descarga) y activar el modo avión.
  2. Hacer un test de 10 preguntas, cerrar la app y volver a abrirla: el banner sigue y el test a medias es el local.
  3. Desactivar el modo avión: se sincroniza y el inicio muestra el objetivo y el XP.
  4. Cortar la conexión a mitad de un test online.
  5. Para las respuestas de ayer, mover el reloj con `adb shell settings put global auto_time 0` y `adb shell cmd alarm set-time <epoch ms>` (restaurar `auto_time 1`): responder offline «ayer», volver a hoy y sincronizar.
  6. Capturas del banner, del feedback offline y del resumen en tema claro y oscuro.
