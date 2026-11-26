# 0034 — Repaso espaciado

- **Issue:** #92
- **Milestone:** v0.5.0
- **Estado:** lista

## Objetivo
Consolidar también lo que ya se sabe: cada pregunta respondida tiene una próxima fecha de repaso que se aleja con cada acierto y vuelve a mañana con un fallo (cajas de Leitner). El inicio dice cuántas preguntas tocan hoy y permite repasarlas en un test. Convive con el repaso de falladas de la spec 0012, que no cambia.

## Decisiones
- **Cajas de Leitner con intervalos que se duplican.** Cada pregunta respondida por un usuario está en una caja de 0 a 6 (`Box`). La próxima fecha (`DueOn`) es el día de la respuesta + `2^Box` días: caja 0 → 1 día, 1 → 2, 2 → 4, 3 → 8, 4 → 16, 5 → 32, 6 → 64 (tope).
- **Reglas al responder** (día = `user.TodayAt(answeredAt)`, en la zona horaria del usuario):
  - Primera respuesta: acierto → caja 1; fallo → caja 0.
  - Fallo, en cualquier momento → caja 0.
  - Acierto con la pregunta ya pendiente (`DueOn <= día`) → sube una caja (tope 6).
  - Acierto antes de su fecha (p. ej., le sale en un test aleatorio) → no cambia nada: así acertar varias veces el mismo día no la manda a dos meses.
  - En todos los casos que cambian la caja, `DueOn = día + 2^Box`, y `LastAnsweredAt = answeredAt`.
  - Una respuesta anterior a `LastAnsweredAt` (sincronización offline tardía) no cambia la programación.
- **Estado guardado en una tabla nueva**, `review_schedules`, actualizado en la misma transacción que la respuesta. Calcularlo desde el historial (como las falladas) exigiría ventanas en SQL difíciles de traducir con EF; la tabla además da una consulta de pendientes barata.
- **Todas las respuestas cuentan**: test por tema, aleatorio, repaso de falladas, repaso espaciado, sincronización offline y entrega de simulacros (las preguntas en blanco no). El punto único es `ProgressRecorder.RecordAnswerAsync`, que gana el `questionId`.
- **Pendiente hoy** = `DueOn <= user.TodayAt(now)` y pregunta activa.
- **El repaso espaciado es un modo de test más, `QuizMode.SpacedReview`**, que cuenta como cualquier test para objetivo diario, racha y XP. No hay XP extra.
- **Historial previo:** la migración crea la programación de lo ya respondido a partir de la última respuesta de cada pregunta (acierto → caja 1; fallo → caja 0), sin reconstruir rachas de aciertos.

## Alcance
**Incluye:**
- Dominio: entidad `ReviewSchedule` y modo `SpacedReview`.
- Datos: tabla `review_schedules` con migración y relleno inicial.
- API: actualización de la programación al registrar cada respuesta, `GET /reviews/due` y `CreateQuizRequest.DueOnly`.
- App: tarjeta «Repaso de hoy» en el inicio que crea el test y lo abre.

**No incluye:**
- Algoritmos adaptativos (SM-2, FSRS) ni facilidad por pregunta.
- Repaso espaciado sin conexión: crear el test exige red. Las respuestas offline sí actualizan la programación al sincronizarse.
- Interruptor de repaso espaciado en la pantalla de temas, recordatorio específico o notificación de pendientes.
- Estadísticas por caja.

## Diseño
### Glosario
Añadido al ADR 0001: Repaso espaciado → `SpacedReview`; Programación de repaso (de una pregunta) → `ReviewSchedule`; Caja (de Leitner) → `Box`; Fecha de repaso → `DueOn`.

### Dominio
**`Quizzes/ReviewSchedule`** (entidad, clave compuesta `UserId` + `QuestionId`):
- Propiedades: `UserId`, `QuestionId`, `Box` (int), `DueOn` (`DateOnly`), `LastAnsweredAt` (`DateTimeOffset`).
- `const int MaxBox = 6`; `static int IntervalDays(int box) => 1 << box`.
- `static ReviewSchedule Start(Guid userId, Guid questionId, bool isCorrect, DateOnly day, DateTimeOffset answeredAt)`: caja 1 o 0 según el acierto.
- `bool Register(bool isCorrect, DateOnly day, DateTimeOffset answeredAt)`: aplica las reglas de *Decisiones* y devuelve si ha cambiado algo. Valida con `Guard` (ids no vacíos) como el resto de entidades.
- `bool IsDueOn(DateOnly today) => DueOn <= today`.

**`QuizMode.SpacedReview`** (nuevo valor, cabe en el texto de 20): en `QuizAttempt.Create` se comporta como `Review` (tema opcional). La sincronización offline sigue aceptando solo `ByTopic`/`Random`.

### API
**Registro de la programación.** `ProgressRecorder.RecordAnswerAsync(User user, Guid questionId, bool isCorrect, DateTimeOffset now, CancellationToken)`:
- Además de lo actual, busca la programación con `db.ReviewSchedules.FindAsync(user.Id, questionId)` (encuentra también las ya rastreadas en la misma petición, p. ej. en una sincronización) y llama a `Register`, o crea una con `Start` y la añade.
- Actualiza las tres llamadas existentes: `SubmitAnswer` y `SyncQuiz` (`QuizzesEndpoints`) y la entrega de simulacros (`MockExamsEndpoints`).
- Token de concurrencia `xmin` como en `DailyActivity`. Dos respuestas simultáneas a la misma pregunta nueva pueden chocar en la clave primaria al insertar: comprueba que esa `DbUpdateException` (violación de unicidad, `23505`) acaba en 409 como un conflicto de concurrencia; si no, añade el caso al handler existente.

**Rutas** bajo `/api/v1`, con usuario autenticado, operando solo sobre el usuario del token.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/reviews/due` | Usuario | — → `200 DueReviewSummaryResponse` |
| POST | `/quizzes` | Usuario | `CreateQuizRequest` (+ `DueOnly`) → `201 QuizResponse` · `400` · `422 quiz.no_due_reviews` |

**`GET /reviews/due`** (feature nueva `Features/Reviews/ReviewsEndpoints.cs`, `MapReviewsEndpoints`, tag `Reviews`; skill `new-endpoint`):
- Programaciones del usuario con `DueOn <= user.TodayAt(now)`, de preguntas activas de oposiciones activas, agrupadas por oposición.
- Contratos en `Contracts/Reviews`: `DueReviewSummaryResponse(int Total, IReadOnlyList<ExamDueReviewsResponse> Exams)` y `ExamDueReviewsResponse(Guid ExamId, string ExamName, int Count)`, solo oposiciones con `Count > 0`, ordenadas por nombre.
- Consulta entera en SQL (índice `(user_id, due_on)`).

**`POST /quizzes`**:
- `CreateQuizRequest` gana un último parámetro `bool DueOnly = false`. Con `MistakesOnly` y `DueOnly` a la vez → `400` (`dueOnly`: «Elige solo un tipo de repaso.»).
- Con `DueOnly`: mismas validaciones de oposición, tema y número de preguntas. Candidatas: las pendientes hoy del usuario en esa oposición (o tema). Orden: `DueOn` ascendente (las más atrasadas primero) y luego al azar; hasta `QuestionCount`. Modo `SpacedReview`.
- Sin ninguna → `422 quiz.no_due_reviews`, «No tienes preguntas para repasar hoy.» (en `DomainErrors`).
- `QuizResponse.Mode` y `QuizSummaryResponse.Mode` pueden valer `"SpacedReview"`: actualiza su comentario XML.

Añade los contratos nuevos a `ApiJsonContext` y al cliente Refit: `GetDueReviewsAsync()`.

### Datos
Skill `new-ef-migration`, migración `AddReviewSchedules`.
- Tabla `review_schedules`: `user_id` FK → `users` (cascade, así se borra con la cuenta), `question_id` FK → `questions` (cascade), PK `(user_id, question_id)`, `box` int, `due_on` date, `last_answered_at` timestamptz, `xmin`. Índice `(user_id, due_on)`.
- Relleno en la migración con SQL: por cada `(user_id, question_id)`, la última respuesta respondida (`DISTINCT ON` por `answered_at` desc y `quiz_attempt_id` desc, mismo desempate que las falladas) → `box` = 1 si acertó, 0 si falló; `last_answered_at` = su `answered_at`; `due_on` = día local de la respuesta (`(answered_at AT TIME ZONE users.time_zone)::date`, comprueba el formato de la columna de zona horaria) + 2 o 1 días. `Down` borra la tabla.

### App
Skill `maui-ui` (claro y oscuro, capturas en el emulador).

**`HomeViewModel` / `HomePage`** — tarjeta «Repaso de hoy», entre «Test a medias» y «Practicar»:
- Se carga con `GetDueReviewsAsync()` en el mismo refresco que racha, nivel y test a medias (también en `OnAppearingAsync`, porque cambia al volver de un test). Versionado como las demás cargas.
- Solo aparece con `Total > 0`: «{Total} preguntas para repasar hoy» («1 pregunta para repasar hoy») y «Las que ya sabías, antes de que se te olviden.». Botón «Repasar».
- Si falla la carga o no hay conexión, la tarjeta no aparece; sin mensaje.
- «Repasar»: si hay una sola oposición, la usa; si hay varias, `IDialogService.ChooseAsync` con sus nombres. Crea el test con `CreateQuizAsync(new(examId, null, Math.Min(count, 20), DueOnly: true))` y navega a `Routes.Quiz` con `QuizId` y `QuizTitle` = «Repaso de hoy». Mientras crea, el botón está deshabilitado.
- Error al crear: `ErrorMessage` de la tarjeta (`ApiErrorMessages.From`); un 422 `quiz.no_due_reviews` además recarga la cuenta.
- La lógica de textos va en Mobile.Core (p. ej. `DueReviewCard`), y `QuizTitles` gana `"SpacedReview"` → «Repaso de hoy» (lo usa la tarjeta «Test a medias»).

`QuizViewModel` no cambia: el resumen de un repaso espaciado es el normal.

## Seguridad
- El usuario sale siempre del token (`CurrentUserService`); la consulta de pendientes y la creación del test filtran por `UserId`. No hay ids de usuario en ruta ni cuerpo.
- `DueOnly` solo restringe las candidatas; la app sigue sin recibir la opción correcta antes de responder.
- Borrar la cuenta borra la programación (cascade).
- Sin secretos nuevos.

## Criterios de aceptación
- [x] Cada respuesta crea o actualiza la programación de su pregunta: el acierto con la pregunta pendiente sube una caja y aleja la fecha (1, 2, 4, 8, 16, 32, 64 días), el fallo vuelve a caja 0 (mañana), el acierto antes de su fecha no cambia nada y una respuesta más antigua que la última no cambia nada.
- [x] Actualizan la programación las respuestas de todos los modos, las sincronizadas sin conexión y las de simulacros (en blanco no).
- [x] `GET /reviews/due` cuenta solo las pendientes hoy (zona horaria del usuario) de preguntas activas, por oposición, y no depende de otros usuarios.
- [x] `POST /quizzes` con `DueOnly` crea un test `SpacedReview` solo con pendientes, más atrasadas primero y sin superar `QuestionCount`; sin pendientes → 422 `quiz.no_due_reviews`; con `MistakesOnly` a la vez → 400.
- [ ] La migración crea la programación de lo ya respondido y `Model_HasNoPendingChangesToMigrate` sigue en verde.
- [ ] El inicio muestra cuántas preguntas tocan hoy y «Repasar» abre el test (verificado en el emulador, claro y oscuro). Build sin warnings (backend y `net10.0-android`).

## Plan de tests
- **Unitarios Domain:** `ReviewSchedule.Start` (acierto/fallo); `Register`: acierto pendiente sube caja y fecha, tope en 6 (64 días), fallo → caja 0 y mañana, acierto antes de fecha sin cambios, respuesta antigua sin cambios; `IntervalDays`; `QuizAttempt.Create` con `SpacedReview` con y sin tema.
- **Integración** (`FakeTimeProvider` para avanzar días):
  - Responder por `answers` crea la programación; fallar la resetea; avanzar el reloj hasta `DueOn` la hace pendiente en `GET /reviews/due`.
  - Sincronización offline con una respuesta más antigua que la última respondida en línea (no cambia la programación). Una misma pregunta no puede llegar dos veces en una sincronización: la validación lo rechaza.
  - Entrega de simulacro: respondidas programadas, en blanco no.
  - `GET /reviews/due`: usuario nuevo (0), pregunta desactivada no cuenta, dos oposiciones, aislamiento entre usuarios, zona horaria (respuesta a las 23:30 locales).
  - `POST /quizzes` con `DueOnly`: por oposición y por tema, orden por `DueOn`, `QuestionCount`, 422 sin pendientes, 400 con los dos flags, `Mode = "SpacedReview"` en `QuizResponse` y `GET /quizzes`.
  - Borrar la cuenta borra la programación.
- **Unitarios Mobile.Core:** `HomeViewModel`: tarjeta visible solo con `Total > 0`, singular y plural, error de carga sin tarjeta; «Repasar» con una oposición crea el test con `DueOnly` y `min(count, 20)` y navega con el título; con varias pregunta cuál y cancelar no hace nada; 422 muestra el mensaje y recarga. `QuizTitles` con `"SpacedReview"`. Serialización de `DueReviewSummaryResponse` en `ApiJsonContext`.
- **Manual (AppHost + emulador):** con el relleno de la migración sobre datos reales, ver la cuenta en el inicio al día siguiente de responder; hacer el repaso y comprobar que la cuenta baja; capturas en claro y oscuro de la tarjeta.
