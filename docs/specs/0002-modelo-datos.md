# 0002 — Modelo de dominio y persistencia con EF Core

- **Issue:** #12
- **Milestone:** v0.1.0
- **Estado:** implementada

> El código está en inglés y sigue el glosario del ADR 0001 (Oposición → `Exam`, Pregunta → `Question`, test realizado → `QuizAttempt`…).

## Objetivo
Crear las entidades del núcleo de tests en Domain (oposiciones, temas, preguntas con aclaración, usuarios y tests realizados) con sus reglas, y guardarlas en PostgreSQL con EF Core 10. Es la base de las issues #13 (importador), #15 (auth) y #16 (endpoints).

## Alcance
**Incluye:**
- Entidades y reglas en `OpoStudy.Domain`, sin dependencias.
- `OpoStudyDbContext`, configuraciones y migración inicial en `OpoStudy.Infrastructure`.
- Registro del DbContext en la Api y el Worker con la integración cliente de Aspire, y migración automática en Development.
- Tests unitarios de dominio, tests de persistencia con Testcontainers y adaptación de los tests de la Api.

**No incluye:**
- Endpoints (#16), auth (#15) ni importador (#13).
- Búsqueda full-text (`tsvector`), gamificación ni rachas (v0.2), ni supuestos (v0.3).
- Datos semilla: el contenido llegará con el importador.

## Diseño

### Dominio (`src/OpoStudy.Domain/`, una carpeta por agregado: `Exams`, `Questions`, `Users`, `Quizzes`, `Common`)
Reglas comunes:
- Ids `Guid` generados en el dominio con `Guid.CreateVersion7()`.
- Propiedades `private set`; las colecciones se exponen como `IReadOnlyList<T>` sobre un campo privado.
- Constructor privado sin parámetros para EF y fábricas estáticas `Create(...)` que validan.
- `Update(...)` de `Exam`, `Topic` y `Question` es atómico: valida todo antes de asignar nada, así que una validación fallida deja la entidad intacta (textos y opciones).
- Una regla incumplida lanza `DomainRuleException(code, message)`:
  - El `Code` es estable y en inglés (p. ej. `question.no_correct_option`). En #16 se traducirá a ProblemDetails.
  - El mensaje está en español, porque puede llegar al usuario.
- El tiempo entra como parámetro `DateTimeOffset now` y se guarda en UTC: el dominio no llama a `DateTime.Now`, y fuera del dominio se usa `TimeProvider`.

| Entidad | Campos | Reglas |
|---|---|---|
| `Exam` | `Id`, `Slug`, `Name`, `Organization`, `Group` (p. ej. "C1"), `IsActive` | El slug, en kebab-case, no puede estar vacío |
| `Topic` | `Id`, `ExamId`, `Slug`, `Number`, `Title`, `Section?` | `Number` > 0 |
| `Question` | `Id`, `TopicId`, `Key` (identificador estable del contenido), `Statement`, `Explanation`, `LegalReference`, `IsActive`, `Options` | Ningún texto vacío; de 2 a 6 opciones; **exactamente una correcta**. No se borra: se desactiva para conservar el historial. `Update(...)` vuelve a validar todo y empareja las opciones por orden. No puede cambiar el número de opciones (`question.option_count_changed`): en ese caso se crea otra pregunta y se desactiva esta. |
| `AnswerOption` | `Id`, `QuestionId`, `Order`, `Text`, `IsCorrect` | Solo se crea y modifica a través de `Question` |
| `User` | `Id`, `FirebaseUid`, `CreatedAt`, `ActiveExamId?` | `FirebaseUid` no puede estar vacío; `ChooseExam(id)` |
| `QuizAttempt` | `Id`, `UserId`, `ExamId`, `TopicId?` (null en los aleatorios), `Mode` (`ByTopic` \| `Random`), `StartedAt`, `FinishedAt?`, `Answers` | Ver detalle bajo la tabla. |
| `QuizAnswer` | `QuizAttemptId`, `QuestionId`, `Order`, `SelectedOptionId?`, `IsCorrect?`, `AnsweredAt?` | Solo se modifica a través de `QuizAttempt` |

Detalle de `QuizAttempt`:
- `Create(...)` exige entre 1 y 100 preguntas, sin repetir, y crea una `QuizAnswer` vacía por cada una, en orden.
- `Answer(question, optionId, now)` devuelve `AnswerResult(IsCorrect, CorrectOptionId)`. Falla si la pregunta es ajena al test, si ya se respondió, si la opción es de otra pregunta o si el test está finalizado.
- Al responder la última pregunta se fija `FinishedAt`.
- Expone `CorrectCount`, `IncorrectCount` e `IsFinished`.
- `LastAnsweredAt` (UTC) se actualiza en cada `Answer` y es solo un dato informativo.
- `Revision` (`int`, empieza en 0) se incrementa en cada `Answer`. Es lo que garantiza que responder siempre modifica la fila de `quiz_attempts` (y por tanto su `xmin`), aunque `now` coincida con el valor anterior. Sirve de base a la concurrencia (ver *Concurrencia* en Datos).
- `quiz_answers.selected_option_id` no tiene FK compuesta con la pregunta: que la opción pertenezca a la pregunta lo garantiza el dominio (`QuizAttempt.Answer`). Decisión aceptada.

### API
No hay endpoints nuevos. Cambios:
- `builder.AddNpgsqlDbContext<OpoStudyDbContext>("opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions)`: integración cliente de Aspire, que aporta health check, reintentos y trazas. Las convenciones no van en `OnConfiguring` porque Aspire usa un pool de DbContext y el pool no lo admite.
- En Development, `await app.Services.ApplyMigrationsAsync()`. En el VPS se usará un *migration bundle* (v0.5).
- El Worker registra el mismo DbContext pero **no** migra.
- Renombrados para seguir la regla del inglés:
  - `Features/Sistema` pasa a `Features/SystemInfo` y el endpoint `GET /api/v1/version` pasa a llamarse `GetVersion`.
  - `CorreccionesWorker` pasa a `GradingWorker`.

### Datos
- **Paquetes**, todos en `Directory.Packages.props`:
  - `Npgsql.EntityFrameworkCore.PostgreSQL` y `EFCore.NamingConventions`, en Infrastructure.
  - `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, en la Api y el Worker.
  - `Microsoft.EntityFrameworkCore.Design`, solo en la Api (`PrivateAssets=all`).
  - `dotnet-ef` como herramienta local en `dotnet-tools.json`.
  - `CentralPackageTransitivePinningEnabled` fija la versión de EF Core.
- Configuraciones con `IEntityTypeConfiguration<T>` en `Infrastructure/Persistence/Configurations/`. Tablas y columnas en snake_case.
- Tablas: `exams`, `topics`, `questions`, `answer_options`, `users`, `quiz_attempts`, `quiz_answers`.
- `Question.Options` y `QuizAttempt.Answers` se cargan siempre (`AutoInclude`).
- **Orden:** ambas colecciones se exponen ordenadas por `Order`, aunque EF las materialice en otro orden (por PK, un Guid v7). El campo privado sigue siendo el *backing field* de EF y la propiedad devuelve una copia ordenada.
- **Concurrencia:** `quiz_attempts` usa la columna de sistema `xmin` de PostgreSQL como token de concurrencia optimista (propiedad sombra `uint Version` con `IsRowVersion()`, que Npgsql mapea a `xmin`/`xid`). La migración C# la menciona, pero el SQL generado no crea ninguna columna real. Como cada `Answer` incrementa `Revision`, dos respuestas concurrentes al mismo intento hacen que el segundo `SaveChanges` lance `DbUpdateConcurrencyException`. En #16 la API lo traducirá a 409.
- Longitudes máximas (`Common/Limits.cs`):

  | Campo | Máximo |
  |---|---|
  | Slug | 100 |
  | Name / Title | 300 |
  | Group | 20 |
  | Statement | 2000 |
  | Option text | 1000 |
  | Explanation | 4000 |
  | LegalReference | 300 |
  | Key | 150 |
  | FirebaseUid | 128 |

  `Mode` se guarda como texto.
- Índices únicos:
  - `exams(slug)`
  - `topics(exam_id, slug)` y `topics(exam_id, number)`
  - `questions(key)`
  - `answer_options(question_id, order)`
  - `users(firebase_uid)`
  - `quiz_answers(quiz_attempt_id, order)`
- Índices simples: `questions(topic_id)` y `quiz_attempts(user_id, started_at)`.
- PK compuesta de `quiz_answers`: `(quiz_attempt_id, question_id)`.
- Borrado: al borrar un usuario se borran en cascada sus tests y respuestas (`DELETE /me`, #15). Las FKs hacia el contenido (exámenes, temas, preguntas y opciones) son `Restrict`.
- `users.active_exam_id` usa `SetNull` a propósito: borrar una oposición no debe bloquearse por la preferencia de un usuario; el usuario se queda sin oposición activa.
- La migración `InitialCreate` está en `Persistence/Migrations`, marcada como `generated_code` en `.editorconfig`:
  ```
  dotnet ef migrations add <Name> --project src/OpoStudy.Infrastructure --startup-project src/OpoStudy.Api --output-dir Persistence/Migrations
  ```

### App
Sin cambios.

## Pendiente para #16
- Los mensajes de `Guard` son genéricos y exponen el código interno (p. ej. "El campo es obligatorio (exam.invalid_name)"): definir mensajes de usuario.
- El listado de historial debe usar proyecciones en lugar de cargar entidades con `AutoInclude` de todas las respuestas.

## Seguridad
- No hace falta ningún secreto nuevo. En local, Aspire genera la contraseña de Postgres en los user-secrets del AppHost; en los tests la pone Testcontainers.
- `FirebaseUid` es un dato personal y no se escribe en logs.
- Las consultas son siempre parametrizadas (EF Core): nada de `FromSqlRaw` con concatenaciones.
- El borrado en cascada de `User` deja preparada la base para el RGPD (borrado de cuenta).

## Criterios de aceptación
- [x] Domain sigue sin dependencias externas: los tests de arquitectura pasan, incluido el que prohíbe depender de EF Core.
- [x] `dotnet run --project src/OpoStudy.AppHost` crea las tablas y `/health` responde Healthy con el check de la base de datos.
- [x] El modelo no tiene cambios pendientes de migrar: un test comprueba que `HasPendingModelChanges()` es `false`.
- [x] Las reglas de `Question` y `QuizAttempt` tienen tests unitarios.
- [x] Los tests de persistencia corren contra Postgres real (Testcontainers).
- [x] Todo el código está en inglés y sigue el glosario.
- [ ] CI en verde.

## Plan de tests
- **Unitarios** (`tests/OpoStudy.Domain.Tests`):
  - `QuestionTests`: sin correcta, dos correctas, menos de 2 o más de 6 opciones, textos vacíos o demasiado largos, y `Update`.
  - `QuizAttemptTests`: pregunta ajena, respuesta repetida, opción ajena, test finalizado, recuento de aciertos y fallos, y `FinishedAt`.
  - `ExamAndUserTests`.
- **Persistencia** (`tests/OpoStudy.Infrastructure.Tests`):
  - Un único contenedor `postgres:17-alpine` para todo el ensamblado (*assembly fixture*), migrado una sola vez.
  - Casos: guardar y releer un `QuizAttempt`, índices únicos, borrado en cascada y `HasPendingModelChanges()`.
- **Api:** `ApiFactory` (WebApplicationFactory + contenedor Postgres) inyecta `ConnectionStrings:opostudy`.
- **Manual:** arrancar el AppHost y comprobar en el dashboard que la Api está sana y las tablas existen.
