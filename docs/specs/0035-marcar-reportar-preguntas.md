# 0035 — Marcar y reportar preguntas

- **Issue:** #93
- **Milestone:** v0.5.0
- **Estado:** lista

## Objetivo
Durante un test, el usuario puede marcar una pregunta para repasarla después y hacer un test solo con las marcadas. También puede reportar una pregunta que cree errónea (respuesta incorrecta, ley derogada…), con un motivo, para que quede registrada y se revise el contenido. Es el circuito de calidad que se aplazó en las specs 0009 y 0012.

## Decisiones
- **Marcar es un estado por usuario y pregunta**, en la tabla `question_bookmarks`. Marcar y desmarcar son idempotentes (`PUT` / `DELETE`).
- **El test de marcadas es un modo más, `QuizMode.Bookmarks`**, igual que `Review` y `SpacedReview`: tema opcional, cuenta para objetivo diario, racha y XP, y el resumen es el normal. Marcar no se quita solo al acertar: lo desmarca el usuario.
- **Un reporte es un registro aparte**, `question_reports`, con motivo de una lista cerrada y comentario opcional (obligatorio con «Otro»). Solo puede haber **un reporte abierto por usuario y pregunta**: el segundo → 409.
- **Sin rol de administrador.** La revisión se hace con la consulta SQL de *Datos* sobre la BD del VPS; marcar como resuelto también es SQL. El panel de administración queda fuera.
- **Solo con conexión.** Marcar, desmarcar y reportar exigen red: en un test sin conexión los botones no aparecen. No se encola nada para la sincronización.
- **Borrar la cuenta borra marcadas y reportes** (cascade): el comentario es texto libre del usuario.

## Alcance
**Incluye:**
- Dominio: entidades `QuestionBookmark` y `QuestionReport`, enums `QuestionReportReason` y `QuestionReportStatus`, modo `QuizMode.Bookmarks`.
- Datos: tablas `question_bookmarks` y `question_reports` con migración.
- API: marcar, desmarcar, reportar, `IsBookmarked` en las preguntas del test y `CreateQuizRequest.BookmarkedOnly`.
- App: botones «Marcar» y «Reportar» en la pantalla de test e interruptor «Solo marcadas» en la pantalla de temas.

**No incluye:**
- Panel o endpoints de administración para revisar reportes, ni avisar al usuario cuando se resuelve el suyo.
- Lista de preguntas marcadas, contador de marcadas en temas o inicio, y marcar desde el resumen o el simulacro.
- Marcar o reportar sin conexión.
- Desactivar automáticamente una pregunta con muchos reportes.

## Diseño
### Glosario
Añadido al ADR 0001: Pregunta marcada → `QuestionBookmark` (modo `Bookmarks`); Reporte → `QuestionReport`; Motivo del reporte → `QuestionReportReason`; Estado del reporte → `QuestionReportStatus`.

### Dominio
En `OpoStudy.Domain/Questions/`, con `Guard` como el resto de entidades:

**`QuestionBookmark`** (clave compuesta `UserId` + `QuestionId`): `UserId`, `QuestionId`, `CreatedAt` (`DateTimeOffset`). `static QuestionBookmark Create(Guid userId, Guid questionId, DateTimeOffset now)`.

**`QuestionReportReason`**: `WrongAnswer` (la respuesta correcta está mal), `OutdatedLegislation` (ley derogada o modificada), `UnclearStatement` (enunciado u opciones ambiguos), `Typo` (errata), `Other`.

**`QuestionReportStatus`**: `Open`, `Resolved`, `Dismissed`.

**`QuestionReport`**: `Id`, `UserId`, `QuestionId`, `Reason`, `Comment` (`string?`), `Status`, `CreatedAt`.
- `const int MaxCommentLength = 500`.
- `static QuestionReport Create(Guid userId, Guid questionId, QuestionReportReason reason, string? comment, DateTimeOffset now)`: recorta el comentario y lo deja en `null` si queda vacío; longitud > 500 o `Other` sin comentario → `DomainRuleException` (como el resto de reglas). Estado `Open`.

**`QuizMode.Bookmarks`** (nuevo valor, cabe en el texto de 20): en `QuizAttempt.Create` se comporta como `Review` (tema opcional). La sincronización offline sigue aceptando solo `ByTopic`/`Random`.

### API
Rutas bajo `/api/v1`, con usuario autenticado y la política de rate limiting `per-user` del grupo. Feature nueva `Features/Questions/QuestionsEndpoints.cs`, `MapQuestionsEndpoints`, tag `Questions` (skill `new-endpoint`).

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| PUT | `/questions/{questionId}/bookmark` | Usuario | — → `204` · `404 question.not_found` |
| DELETE | `/questions/{questionId}/bookmark` | Usuario | — → `204` |
| POST | `/questions/{questionId}/reports` | Usuario | `CreateQuestionReportRequest` → `204` · `400` · `404 question.not_found` · `409 question.already_reported` |
| POST | `/quizzes` | Usuario | `CreateQuizRequest` (+ `BookmarkedOnly`) → `201 QuizResponse` · `400` · `422 quiz.no_bookmarks` |

**Marcar** (`PUT`): la pregunta debe existir, estar activa y pertenecer a una oposición activa; si no → 404. Si ya está marcada, 204 sin cambios. Dos `PUT` simultáneos pueden chocar en la clave primaria. Para que sigan siendo idempotentes, captura la violación en el endpoint (`DbUpdateException` con `23505` y la restricción `QuestionBookmarksPrimaryKey` → 204), igual que hace `CurrentUserService`.

**Desmarcar** (`DELETE`): `ExecuteDeleteAsync` por `(user.Id, questionId)`; 204 aunque no estuviera marcada o la pregunta no exista.

**Reportar** (`POST`): `CreateQuestionReportRequest(string Reason, string? Comment)` en `Contracts/Questions`.
- `Reason` es el nombre del enum (sin distinguir mayúsculas); otro valor → 400 (`reason`: «Elige un motivo.»). Comentario > 500 → 400 (`comment`: «El comentario no puede superar los 500 caracteres.»); `Other` sin comentario → 400 (`comment`: «Cuéntanos qué está mal.»). Valida en el endpoint antes del dominio para devolver `ValidationProblem`.
- Pregunta inexistente o inactiva → 404 `question.not_found`.
- Ya hay un reporte `Open` del usuario para esa pregunta → 409 `question.already_reported`, «Ya reportaste esta pregunta. La revisaremos pronto.». La carrera la cubre el índice único parcial: mapea su violación (`QuestionReportsOneOpenPerUserIndex`) al mismo código en `DomainRuleExceptionHandler`.
- Respuesta 204: la app no necesita el id.

**Preguntas del test**: `QuizQuestionResponse` gana `bool IsBookmarked` (último parámetro) en todas las respuestas que construyen `QuizResponse` (`POST /quizzes`, `GET /quizzes/{id}` y las que devuelven el test tras responder). Se calcula con una sola consulta de las marcadas del usuario entre los `QuestionId` del test. Los simulacros no lo usan (pueden mandar `false`).

**`POST /quizzes`**:
- `CreateQuizRequest` gana un último parámetro `bool BookmarkedOnly = false`. Con `BookmarkedOnly` y `MistakesOnly` o `DueOnly` → 400 (`bookmarkedOnly`: «Elige solo un tipo de repaso.»). El caso `MistakesOnly` + `DueOnly` no cambia.
- Con `BookmarkedOnly`: mismas validaciones de oposición, tema y número de preguntas. Candidatas: las marcadas del usuario, activas, de esa oposición (o tema), al azar, hasta `QuestionCount`. Modo `Bookmarks`.
- Sin ninguna → `422 quiz.no_bookmarks`, «No tienes preguntas marcadas aquí.».
- `QuizResponse.Mode` y `QuizSummaryResponse.Mode` pueden valer `"Bookmarks"`: actualiza su comentario XML y el de `CreateQuizRequest`.

Códigos nuevos en `DomainErrors`: `question.not_found` (404, «La pregunta no existe.»), `question.already_reported` (409), `quiz.no_bookmarks` (422). Si `question.not_found` ya existe con otro texto, reutilízalo.

Contratos nuevos en `ApiJsonContext` y cliente Refit: `BookmarkQuestionAsync(Guid)`, `UnbookmarkQuestionAsync(Guid)`, `ReportQuestionAsync(Guid, CreateQuestionReportRequest)`.

### Datos
Skill `new-ef-migration`, migración `AddQuestionBookmarksAndReports`. Nombres de restricciones en `DbConstraintNames`.
- `question_bookmarks`: `user_id` FK → `users` (cascade), `question_id` FK → `questions` (cascade), PK `(user_id, question_id)`, `created_at` timestamptz.
- `question_reports`: `id` uuid PK, `user_id` FK → `users` (cascade), `question_id` FK → `questions` (cascade), `reason` text (enum como texto, máx. 30), `comment` varchar(500) nullable, `status` text (máx. 20), `created_at` timestamptz. Índice único parcial `(user_id, question_id) WHERE status = 'Open'`; índice `(status, created_at)`.

Consulta de revisión (para el VPS; no va en código):
```sql
SELECT q.key, r.reason, count(*) AS reports, array_agg(r.comment) FILTER (WHERE r.comment IS NOT NULL) AS comments, min(r.created_at) AS first_at
FROM question_reports r JOIN questions q ON q.id = r.question_id
WHERE r.status = 'Open'
GROUP BY q.key, r.reason
ORDER BY reports DESC, first_at;
-- Al corregir el JSON y reimportar: UPDATE question_reports SET status = 'Resolved' WHERE status = 'Open' AND question_id = (SELECT id FROM questions WHERE key = '...');
```

### App
Skill `maui-ui` (claro y oscuro, capturas en el emulador).

**`IDialogService`** gana `PromptAsync(string title, string message, string accept, string cancel, string? placeholder, int maxLength)` → `string?` (null si cancela), implementado con `DisplayPromptAsync`.

**`QuizViewModel` / `QuizPage`** — fila de acciones bajo el enunciado, solo si `!IsOffline`:
- **Marcar**: `IsCurrentBookmarked` sale de `IsBookmarked` de la pregunta actual (guárdalo por pregunta para que se mantenga al navegar). `ToggleBookmarkCommand` cambia el estado al momento y llama a `BookmarkQuestionAsync` o `UnbookmarkQuestionAsync`; si falla, revierte y muestra `ErrorMessage` (`ApiErrorMessages.From`). Texto «Marcar» / «Marcada», con `SemanticProperties.Description` «Marcar pregunta para repasar» / «Quitar marca». Deshabilitado mientras hay una llamada en curso.
- **Reportar**: `ReportCommand` → `ChooseAsync("¿Qué le pasa a esta pregunta?", "Cancelar", …)` con «La respuesta correcta está mal», «Ley derogada o modificada», «Enunciado u opciones confusos», «Errata», «Otro». Después `PromptAsync` para el comentario (opcional; con «Otro», si queda vacío no se envía y se vuelve a pedir una vez con el mensaje «Cuéntanos qué está mal.»). Cancelar en cualquier paso no envía nada. Éxito → `AlertAsync("Gracias", "Revisaremos la pregunta.")`; 409 → `AlertAsync` con el mensaje del ProblemDetails; otro error → `ErrorMessage`.
- Los textos y el mapeo motivo ↔ etiqueta van en Mobile.Core (p. ej. `QuestionReportReasons`).

**`TopicsViewModel` / `TopicsPage`** — interruptor «Solo marcadas» junto a «Solo falladas»; activar uno desactiva el otro. Con `BookmarkedOnly`, como con falladas, no se usa el modo offline. Envía `CreateQuizRequest(..., BookmarkedOnly: true)`. El 422 `quiz.no_bookmarks` se muestra como el resto de errores de creación.

**`QuizTitles`** gana `"Bookmarks"` → «Preguntas marcadas».

## Seguridad
- El usuario sale siempre del token (`CurrentUserService`); marcadas y reportes se leen y escriben solo con su `UserId`. No hay ids de usuario en ruta ni cuerpo.
- El `questionId` de la ruta solo se acepta si la pregunta está activa: no se puede sondear contenido desactivado (404 igual que si no existe).
- `BookmarkedOnly` solo restringe las candidatas; la app sigue sin recibir la opción correcta antes de responder.
- Comentario limitado a 500 caracteres, guardado como texto plano (no se renderiza en ningún sitio). Rate limiting `per-user` existente contra el spam de reportes.
- Borrar la cuenta borra marcadas y reportes (cascade). Sin secretos nuevos.

## Criterios de aceptación
- [ ] `PUT`/`DELETE /questions/{id}/bookmark` marcan y desmarcan de forma idempotente; marcar una pregunta inexistente o inactiva → 404.
- [ ] Las preguntas del test llevan `IsBookmarked` correcto para el usuario y no dependen de otros usuarios.
- [ ] `POST /quizzes` con `BookmarkedOnly` crea un test `Bookmarks` solo con marcadas activas de la oposición o tema, sin superar `QuestionCount`; sin marcadas → 422 `quiz.no_bookmarks`; combinado con otro repaso → 400.
- [ ] `POST /questions/{id}/reports` registra el reporte abierto con motivo y comentario; motivo inválido, comentario largo u «Otro» sin comentario → 400; segundo reporte abierto → 409 `question.already_reported`.
- [ ] Borrar la cuenta borra marcadas y reportes; `Model_HasNoPendingChangesToMigrate` en verde.
- [ ] En la app se marca, desmarca y reporta desde el test, y «Solo marcadas» abre un test con ellas (verificado en el emulador, claro y oscuro). Build sin warnings (backend y `net10.0-android`).

## Plan de tests
- **Unitarios Domain:** `QuestionReport.Create`: recorta y anula comentario vacío, > 500 lanza, `Other` sin comentario lanza, estado `Open`; `QuestionBookmark.Create` con ids vacíos lanza; `QuizAttempt.Create` con `Bookmarks` con y sin tema.
- **Integración:**
  - Marcar dos veces → 204 y una fila; desmarcar sin marca → 204; marcar inexistente o inactiva → 404.
  - `IsBookmarked` en `POST /quizzes` y `GET /quizzes/{id}`; marca de otro usuario no cuenta.
  - `BookmarkedOnly`: por oposición y por tema, pregunta desactivada excluida, `QuestionCount`, 422 sin marcadas, 400 con `MistakesOnly` y con `DueOnly`, `Mode = "Bookmarks"` en `QuizResponse` y `GET /quizzes`.
  - Reportes: 204 y fila `Open`; los tres 400; 404; 409 con uno abierto; tras pasarlo a `Resolved` por BD se puede volver a reportar; otro usuario puede reportar la misma pregunta.
  - Borrar la cuenta borra marcadas y reportes.
- **Unitarios Mobile.Core:** `QuizViewModel`: toggle optimista y revertido al fallar; estado por pregunta al avanzar; acciones ocultas offline; reportar envía el motivo y el comentario, cancelar no envía, «Otro» vacío no envía, 409 muestra el mensaje. `TopicsViewModel`: interruptores excluyentes y `BookmarkedOnly` en la petición. `QuizTitles` con `"Bookmarks"`. Serialización de `CreateQuestionReportRequest` en `ApiJsonContext`.
- **Manual (AppHost + emulador):** marcar dos preguntas, hacer el test de marcadas, reportar una y verla con la consulta SQL; capturas en claro y oscuro de la fila de acciones.
