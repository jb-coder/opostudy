# 0006 — Endpoints de oposiciones, temas y tests con aclaración

- **Issue:** #16
- **Milestone:** v0.1.0
- **Estado:** implementada

> Las rutas siguen la regla del inglés (CLAUDE.md): `/exams`, `/topics`, `/quizzes`. La issue usaba los nombres en español de antes de la convención.

## Objetivo
Permitir que un usuario autenticado elija oposición y tema, haga un test (por tema o aleatorio) y reciba, al responder cada pregunta, si ha acertado, la opción correcta, la aclaración y la referencia legal guardadas en la base de datos. No hay llamadas al LLM. Es lo que consume la pantalla de test de la app (#19).

## Alcance
**Incluye:**
- Listado de oposiciones activas y de sus temas, con el número de preguntas disponibles.
- Crear un test (por tema o aleatorio de N preguntas) sin revelar las respuestas correctas.
- Leer un test propio (para retomarlo) y responder sus preguntas una a una.
- Historial de tests del usuario con proyecciones (nota heredada de #12).
- Traducción de `DomainRuleException` y `DbUpdateConcurrencyException` a ProblemDetails con mensajes de usuario propios (notas heredadas de #12).
- DTOs en `OpoStudy.Contracts` y tests de integración de cada endpoint.

**No incluye:**
- Elegir la oposición activa del usuario (`User.ChooseExam`): llegará con la app si hace falta.
- Estadísticas, rachas o repaso de falladas (v0.2).
- Abandonar o borrar un test concreto (se borran todos con `DELETE /me`).
- Cambiar los mensajes de `Guard` en el dominio: los usa el importador para diagnosticar contenido. La API no los muestra (ver *Errores*).

## Diseño
### Dominio
Sin cambios. Se usan `Exam`, `Topic`, `Question`, `AnswerOption`, `QuizAttempt` (`Create`, `Answer`, `CorrectCount`, `IncorrectCount`, `IsFinished`) y `Limits.MaxQuestionsPerQuiz` (100).

### API
Dos features en `src/OpoStudy.Api/Features/`: `Exams/ExamsEndpoints.cs` (`MapExamsEndpoints`, tag `Exams`) y `Quizzes/QuizzesEndpoints.cs` (`MapQuizzesEndpoints`, tag `Quizzes`), registrados en `Program.cs` sobre el grupo `/api/v1`. Todos exigen usuario (fallback policy) y tienen `.WithName()`, `.WithTags()`, `.WithSummary()` (resumen en español) y `.ProducesProblem(...)` de sus errores.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/exams` | Usuario | — → `200 IReadOnlyList<ExamResponse>` |
| GET | `/api/v1/exams/{examId:guid}/topics` | Usuario | — → `200 IReadOnlyList<TopicResponse>` · `404` |
| POST | `/api/v1/quizzes` | Usuario | `CreateQuizRequest` → `201 QuizResponse` + `Location` · `400` · `404` · `422` |
| GET | `/api/v1/quizzes` | Usuario | `?skip=0&take=20` → `200 IReadOnlyList<QuizSummaryResponse>` · `400` |
| GET | `/api/v1/quizzes/{quizId:guid}` | Usuario (dueño) | — → `200 QuizResponse` · `404` |
| POST | `/api/v1/quizzes/{quizId:guid}/answers` | Usuario (dueño) | `SubmitAnswerRequest` → `200 AnswerFeedbackResponse` · `400` · `404` · `409` · `422` |

**Detalle por endpoint**
- `GET /exams`: solo `IsActive`, ordenadas por `Name`. Proyección (`Select`), `AsNoTracking`.
- `GET /exams/{examId}/topics`: 404 si la oposición no existe o no está activa. Temas ordenados por `Number`; `QuestionCount` = preguntas **activas** del tema (subconsulta `Count` en la proyección, sin cargar preguntas).
- `POST /quizzes`:
  1. Valida el body: `ExamId` no vacío, `TopicId` no vacío si viene, `QuestionCount` entre 1 y `Limits.MaxQuestionsPerQuiz`. Si falla → `400 ValidationProblem` (mensajes en español, claves en camelCase).
  2. Oposición activa → si no, 404. Si viene `TopicId`, el tema debe ser de esa oposición → si no, 404.
  3. Modo: `ByTopic` si viene `TopicId`; `Random` si no (preguntas activas de todos los temas de la oposición).
  4. Elige al azar hasta `QuestionCount` ids de preguntas activas con `OrderBy(_ => EF.Functions.Random()).Take(n)` (solo los ids). Si hay menos disponibles, usa todas. Si no hay ninguna → `422` con código `quiz.no_questions_available`.
  5. `QuizAttempt.Create(user.Id, examId, topicId, mode, ids, timeProvider.GetUtcNow())`, guarda y devuelve `201` con `Location: /api/v1/quizzes/{id}` y el `QuizResponse` (todas las `Feedback` a `null`).
- `GET /quizzes`: historial del usuario, `StartedAt` descendente, paginado con `skip` (≥ 0, por defecto 0) y `take` (1-50, por defecto 20); fuera de rango → `400 ValidationProblem`. **Proyección** directa a `QuizSummaryResponse` (recuentos con `Count` sobre `Answers` en la consulta, título del tema con la navegación/join): nunca materializa `QuizAttempt` ni sus respuestas.
- `GET /quizzes/{quizId}`: carga el intento con `Where(q => q.Id == quizId && q.UserId == user.Id)`; si no hay resultado → 404 (igual si existe pero es de otro usuario: no se distingue). Carga las preguntas del intento con sus opciones en **una** consulta (`Where(q => ids.Contains(q.Id))`) y monta el `QuizResponse` en el orden de `Answers`. Las preguntas ya respondidas llevan `Feedback`; las pendientes, `null`.
- `POST /quizzes/{quizId}/answers`:
  1. Valida el body (`QuestionId` y `OptionId` no vacíos) → `400`.
  2. Intento del usuario (mismo filtro de dueño) → si no, 404.
  3. Si `QuestionId` no está entre las preguntas del intento → `422` (`quiz.foreign_question`) sin consultar la pregunta. Si lo está, carga la `Question` (con opciones, tracking no necesario).
  4. `attempt.Answer(question, optionId, now)`; las reglas del dominio (`quiz.finished`, `quiz.already_answered`, `quiz.foreign_option`) se traducen como indica *Errores*.
  5. `SaveChangesAsync`. Si lanza `DbUpdateConcurrencyException` → `409`.
  6. Devuelve `AnswerFeedbackResponse` con el resultado, la aclaración, la referencia legal y el estado del test tras responder.

**Usuario actual:** todos los endpoints obtienen el `User` con `CurrentUserService.GetOrCreateAsync` (#15). El id del usuario nunca sale de la ruta ni del body.

### Contracts (`src/OpoStudy.Contracts/`)
`record` posicionales, sin dependencias del dominio:
- `Exams/ExamResponse(Guid Id, string Slug, string Name, string Organization, string Group)`.
- `Exams/TopicResponse(Guid Id, int Number, string Title, string? Section, int QuestionCount)`.
- `Quizzes/CreateQuizRequest(Guid ExamId, Guid? TopicId, int QuestionCount)`.
- `Quizzes/QuizResponse(Guid Id, Guid ExamId, Guid? TopicId, string Mode, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, int CorrectCount, int IncorrectCount, IReadOnlyList<QuizQuestionResponse> Questions)`.
- `Quizzes/QuizQuestionResponse(Guid QuestionId, int Order, string Statement, IReadOnlyList<QuizOptionResponse> Options, AnswerFeedback? Feedback)`.
- `Quizzes/QuizOptionResponse(Guid Id, int Order, string Text)` — **sin** `IsCorrect`.
- `Quizzes/AnswerFeedback(Guid SelectedOptionId, bool IsCorrect, Guid CorrectOptionId, string Explanation, string LegalReference)`.
- `Quizzes/SubmitAnswerRequest(Guid QuestionId, Guid OptionId)`.
- `Quizzes/AnswerFeedbackResponse(Guid QuestionId, AnswerFeedback Feedback, bool QuizFinished, int CorrectCount, int IncorrectCount, int AnsweredCount, int QuestionCount)`.
- `Quizzes/QuizSummaryResponse(Guid Id, Guid ExamId, Guid? TopicId, string? TopicTitle, string Mode, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, int QuestionCount, int CorrectCount, int IncorrectCount)`.
- `Mode` viaja como texto (`"ByTopic"` / `"Random"`), igual que se guarda en la base de datos.

La API no expone `Key`, `IsActive` ni `FirebaseUid`, y `Explanation`/`LegalReference`/`IsCorrect` solo aparecen dentro de `AnswerFeedback` de preguntas ya respondidas.

### Errores (`src/OpoStudy.Api/Errors/`)
- `DomainErrors` (estático): diccionario `code → (StatusCode, mensaje en español)`. Ningún mensaje incluye el código interno. Mínimo:

  | Código | Estado | Mensaje |
  |---|---|---|
  | `quiz.finished` | 409 | El test ya está terminado. |
  | `quiz.already_answered` | 409 | Ya has respondido a esta pregunta. |
  | `quiz.foreign_question` | 422 | La pregunta no forma parte de este test. |
  | `quiz.foreign_option` | 422 | La opción elegida no corresponde a la pregunta. |
  | `quiz.no_questions_available` | 422 | No hay preguntas disponibles para este test. |
  | `quiz.invalid_question_count` | 422 | Un test debe tener entre 1 y 100 preguntas. |
  | cualquier otro código | 422 | No se ha podido completar la operación. |
- `DomainRuleExceptionHandler : IExceptionHandler` (registrado con `AddExceptionHandler<>` antes de `UseExceptionHandler`, que ya existe) escribe un ProblemDetails con `status`, `title` = mensaje de `DomainErrors`, y la extensión `code` con el código estable (para que la app pueda reaccionar). **No** usa `exception.Message`.
- El mismo handler (u otro) traduce `DbUpdateConcurrencyException` a `409` con código `quiz.concurrent_update` y mensaje "El test se ha modificado desde otra petición. Vuelve a cargarlo.".
- Si ninguna de las entidades en conflicto (`exception.Entries`) es un `QuizAttempt`, el `409` usa el código `concurrency.conflict` y el mensaje "Los datos se han modificado desde otra petición. Vuelve a intentarlo.".
- Los errores que genera el propio endpoint (404, 422 de `quiz.foreign_question` y `quiz.no_questions_available`) usan el mismo helper de `DomainErrors` para que formato y mensajes coincidan. 404: "La oposición no existe.", "El tema no existe." y "El test no existe." (extensión `code`: `exam.not_found`, `topic.not_found`, `quiz.not_found`).
- Un `BadHttpRequestException` de Minimal APIs (p. ej. body vacío o JSON mal formado) sale como `400` (o el 4xx que traiga la excepción) con código `request.invalid` y mensaje "La petición no es válida.", en lugar de 500.
- Las excepciones no controladas siguen saliendo como 500 ProblemDetails genérico (sin detalles fuera de Development).

### Datos
Sin cambios de esquema ni migraciones. Índices existentes suficientes: `questions(topic_id)`, `quiz_attempts(user_id, started_at)`, PK de `quiz_answers`.

### App
Nada (#19 consume estos endpoints).

## Seguridad
- Autorización por recurso: todo acceso a un `QuizAttempt` filtra por `UserId` del usuario del token **en la consulta**; el de otro usuario da 404 (no 403), para no revelar que existe.
- Las respuestas correctas, la aclaración y la referencia legal nunca salen antes de responder la pregunta.
- Entrada validada (ids no vacíos, rangos de `QuestionCount`, `skip` y `take`); consultas parametrizadas con EF Core. El aleatorio usa `random()` de Postgres, no entrada del usuario.
- Los ProblemDetails no exponen mensajes de excepción, trazas ni códigos dentro del texto.
- Ningún secreto nuevo. Rate limiting por usuario heredado del grupo `/api/v1`.

## Criterios de aceptación
- [x] Un usuario no puede leer ni responder los tests de otro (404).
- [x] La respuesta al crear un test no incluye `isCorrect`, la aclaración ni la referencia legal.
- [x] Responder devuelve si se ha acertado, la opción correcta, la aclaración y la referencia legal; al responder la última, `quizFinished` es `true`.
- [x] Responder dos veces la misma pregunta → 409; opción ajena → 422; pregunta ajena → 422; test terminado → 409. Todos con ProblemDetails, mensaje en español sin el código interno y extensión `code`.
- [x] Respuestas concurrentes al mismo test nunca dan 500: las que pierden la carrera dan 409.
- [x] El historial usa proyecciones y está paginado.
- [x] Tests de integración de cada endpoint; build sin warnings.

## Plan de tests
- **Unitarios:** ninguno nuevo en Domain. Si `DomainErrors` tiene lógica (fallback), un test sencillo en el proyecto de integración o de la API basta.
- **Integración** (`tests/OpoStudy.Api.IntegrationTests`), sembrando contenido con el `OpoStudyDbContext` del factory (helper `ContentSeeder` reutilizable: una oposición activa con 2 temas y unas pocas preguntas de 3-4 opciones, una oposición inactiva, una pregunta inactiva). Cada test usa uids y datos propios (slugs/keys únicos) para no interferir con los demás:
  - `ExamsEndpointsTests`: sin token → 401; lista solo activas; temas ordenados con `questionCount` que no cuenta inactivas; oposición inexistente o inactiva → 404 ProblemDetails.
  - `QuizzesEndpointsTests`:
    - Crear por tema → 201, `Location`, nº de preguntas = min(pedidas, disponibles), todas del tema, sin inactivas; el JSON no contiene `isCorrect`, `explanation` ni `legalReference` (comprobar sobre el texto bruto) y `feedback` es `null`.
    - Crear aleatorio → preguntas de varios temas de la oposición.
    - `QuestionCount` 0 o 101, `ExamId` vacío → 400 ValidationProblem; tema de otra oposición → 404; oposición inactiva → 404; tema sin preguntas activas → 422 `quiz.no_questions_available`.
    - Responder bien y mal → `isCorrect`, `correctOptionId`, `explanation`, `legalReference` correctos; la última respuesta → `quizFinished = true` y recuentos correctos.
    - Repetir respuesta → 409 `quiz.already_answered`; opción de otra pregunta → 422; pregunta fuera del test → 422; responder en test terminado → 409. Comprobar que `title` no contiene el código.
    - Usuario B: `GET` y `POST answers` sobre el test de A → 404.
    - `GET /quizzes/{id}` tras responder una → esa pregunta con `feedback`, el resto `null`.
    - Historial: dos tests del usuario en orden descendente con recuentos correctos; no incluye tests de otro usuario; `take=0`/`take=51`/`skip=-1` → 400.
    - Concurrencia: test de 10 preguntas, 10 respuestas en paralelo (una por pregunta) → todos los estados ∈ {200, 409}, al menos un 200, y el nº de preguntas respondidas en BD = nº de 200.
- **Manual:** AppHost arriba (con el contenido importado), en Scalar con un token: listar oposiciones y temas, crear un test del tema 1, responder una pregunta y ver la aclaración.
