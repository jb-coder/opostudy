# 0033 — Simulacro de examen con tiempo y penalización

- **Issue:** #91
- **Milestone:** v0.5.0
- **Estado:** lista

## Objetivo
Simular los ejercicios tipo test de la oposición tal como se examinan: un número fijo de preguntas de su bloque, tiempo límite, preguntas que se pueden dejar en blanco o cambiar hasta entregar, y una nota en la que cada fallo resta. Así se entrena el ritmo y la decisión de arriesgar o dejar en blanco, que es donde se decide la nota. El formato (preguntas, minutos, puntos, penalización y bloques) sale de los datos de la oposición, no del código.

## Alcance
**Incluye:**
- Ejercicios de la oposición en `exam.json` (`exercises`), con su JSON Schema, el importador y la tabla `exam_exercises`. Datos de Programador C1: ejercicio 1 (55 preguntas del bloque I, 50 min, sobre 20) y ejercicio 2 (75 preguntas del bloque II, 60 min, sobre 25), los dos con penalización de la mitad de un acierto.
- Simulacro = `QuizAttempt` con `Mode = MockExam`, que guarda una copia de las reglas del ejercicio y la nota. Sus respuestas cuentan como las de cualquier test: XP, objetivo diario, racha, estadísticas y falladas para el repaso (las preguntas en blanco no cuentan).
- Endpoints para listar ejercicios, crear un simulacro, entregarlo de una vez y volver a leer el resultado.
- Nota mínima para aprobar configurable por el usuario al empezar cada simulacro (por defecto, la mitad de la nota máxima). La app recuerda el último valor usado en cada ejercicio.
- App: lista de ejercicios, pantalla del simulacro con temporizador accesible y entrega automática, y pantalla de resultado con la nota, apto/no apto, el desglose y la corrección de cada pregunta.

**No incluye:**
- Retomar un simulacro tras cerrar la app o salir de la pantalla: queda sin entregar y no cuenta (no se borra).
- Simulacros sin conexión: crear y entregar exigen red. El modo offline (spec 0014) no cambia.
- Historial de simulacros con su nota en la app, y estadísticas específicas de simulacros.
- Reparto de preguntas por tema dentro del bloque: se eligen al azar entre todas las del bloque.
- Que el servidor impida entregar fuera de plazo: el temporizador es una ayuda de entrenamiento, no un control antitrampas (ver *Seguridad*).

## Diseño
### Glosario
Ya añadido en el ADR 0001: Simulacro → `MockExam`, Ejercicio (de la oposición) → `ExamExercise`, Nota → `Score`, Nota para aprobar → `PassScore`, Penalización por fallo → `PenaltyRatio`, En blanco → `Blank`, Entrega → `Submission`.

### Contenido (`content/`)
`exam.json` admite un array opcional `exercises`:
```json
"exercises": [
  { "number": 1, "title": "Primer ejercicio", "questionCount": 55, "durationMinutes": 50, "maxScore": 20, "penaltyRatio": 0.5,
    "sections": ["Bloque I. Materias comunes"] },
  { "number": 2, "title": "Segundo ejercicio", "questionCount": 75, "durationMinutes": 60, "maxScore": 25, "penaltyRatio": 0.5,
    "sections": ["Bloque II-A. Materias específicas", "Bloque II-B. Materias específicas", "Bloque II-C. Materias específicas"] }
]
```
- `content/schema/exam.schema.json`: `number` entero ≥ 1; `title` 1-300; `questionCount` 1-100 (`Limits.MaxQuestionsPerQuiz`); `durationMinutes` 1-240; `maxScore` > 0 y ≤ 100 (`multipleOf` 0.01); `penaltyRatio` 0-1 (`multipleOf` 0.01); `sections` array no vacío de textos únicos; `additionalProperties: false`.
- El importador (`src/OpoStudy.Infrastructure/Content/`: `FileModels`, `ContentReader`, `ContentImporter`) valida además que los `number` no se repiten y que cada sección existe tal cual en algún `topics[].section` de la oposición; si no, error de contenido como los demás. Hace upsert por `(exam_id, number)` y borra los ejercicios que ya no estén en el fichero (nada los referencia: el simulacro guarda su copia de las reglas).
- Copia las secciones exactas de `content/exams/malaga-programador-c1/exam.json` (hay tres del bloque II).
- Actualiza la documentación del formato en la spec 0003 o donde se documente `exam.json` (una línea y el ejemplo).

### Dominio
**`Exams/ExamExercise`** (entidad): `Id`, `ExamId`, `Number`, `Title`, `QuestionCount`, `DurationMinutes`, `MaxScore` (`decimal`), `PenaltyRatio` (`decimal`), `Sections` (`IReadOnlyList<string>`). `Create(examId, number, title, questionCount, durationMinutes, maxScore, penaltyRatio, sections)` y `Update(...)` validan con `Guard`/`DomainRuleException` (códigos `exercise.invalid_*`) los mismos rangos que el schema; validan todo antes de asignar. `DefaultPassScore => Math.Round(MaxScore / 2, 2, MidpointRounding.AwayFromZero)`. `ToRules(decimal? passScore)` devuelve el `MockExamRules` (abajo) y valida la nota para aprobar.

**`Quizzes/MockExamRules`** (valor inmutable, copia de las reglas al crear el simulacro): `ExerciseNumber`, `Title`, `QuestionCount`, `Duration` (`TimeSpan`), `MaxScore`, `PenaltyRatio`, `PassScore`. Invariante: `0 < PassScore ≤ MaxScore` y como mucho 2 decimales → si no, `mock_exam.invalid_pass_score`.

**`Quizzes/MockExamScoring`** (estático, puro):
- `Calculate(int correct, int incorrect, int questionCount, decimal maxScore, decimal penaltyRatio)` = `max(0, correct − incorrect × penaltyRatio) × maxScore / questionCount`, redondeado a 2 decimales con `MidpointRounding.AwayFromZero`. En blanco ni suma ni resta.
- Ejemplo (ejercicio 1): 40 aciertos, 10 fallos, 5 en blanco → netas 35 → 35 × 20 / 55 = **12,73**. Con 10 aciertos y 30 fallos → netas −5 → **0**.

**`QuizMode.MockExam`** (nuevo valor) y cambios en **`QuizAttempt`**:
- `MockExamRules? MockExam` y `decimal? Score` (null hasta la entrega). `DeadlineAt => StartedAt + MockExam.Duration` (solo simulacros).
- `Create(...)` rechaza `Mode = MockExam` (`quiz.invalid_mode`): los simulacros solo se crean con `CreateMockExam`. El endpoint de sincronización ya solo admite `ByTopic`/`Random`; no cambia.
- `static CreateMockExam(Guid userId, Guid examId, MockExamRules rules, IReadOnlyList<Guid> questionIds, DateTimeOffset now)`: `TopicId = null`; exige `questionIds.Count == rules.QuestionCount` (`mock_exam.invalid_question_count`) y reutiliza las validaciones de `Create` (sin duplicados, etc.).
- `Answer(...)` en un simulacro → `mock_exam.answer_requires_submission` (no hay corrección pregunta a pregunta).
- `Submit(IReadOnlyCollection<Question> questions, IReadOnlyDictionary<Guid, Guid> selections, DateTimeOffset now)`: `selections` = `questionId → optionId` de las respondidas; las que no aparecen quedan en blanco.
  - No es simulacro → `quiz.not_mock_exam`; ya terminado → `quiz.finished`; pregunta fuera del simulacro → `quiz.foreign_question`; opción que no es de su pregunta → `quiz.foreign_option`. Se valida todo antes de registrar nada.
  - Registra cada respuesta con `QuizAnswer.Record(optionId, isCorrect, now)`, fija `FinishedAt = now`, `LastAnsweredAt = now`, `Score` con `MockExamScoring` y `Revision++`.
  - Devuelve la lista de resultados de las respondidas (`IReadOnlyList<(Guid QuestionId, bool IsCorrect)>` o un record), para registrar el progreso.
- Derivados: `BlankCount` (preguntas sin responder de un simulacro terminado; 0 si no ha terminado), `Passed => Score >= MockExam.PassScore`, `IsSubmittedLate => FinishedAt > DeadlineAt + Limits.MockExamSubmissionGrace`.
- `Limits`: `MockExamSubmissionGrace = TimeSpan.FromMinutes(2)`, `MaxMockExamMinutes = 240`, `MaxMockExamScore = 100`.
- `IsFinished` sigue siendo `FinishedAt is not null`; en un simulacro, terminado no implica todas respondidas. Revisa que nada en `Stats`/`Progress` dé por hecho lo contrario.

### API
Feature nueva `src/OpoStudy.Api/Features/MockExams/MockExamsEndpoints.cs` (`MapMockExamsEndpoints`, tag `MockExams`), en el grupo `/api/v1`. Mismo patrón que `QuizzesEndpoints` (usuario con `CurrentUserService`, `.WithName/.WithTags/.WithSummary` en español, `.ProducesProblem`, `TimeProvider`). Usa la skill `new-endpoint`.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/exams/{examId:guid}/exercises` | Usuario | — → `200 IReadOnlyList<ExamExerciseResponse>` · `404` |
| POST | `/api/v1/mock-exams` | Usuario | `CreateMockExamRequest` → `201 MockExamResponse` + `Location` · `400` · `404` · `422` |
| POST | `/api/v1/mock-exams/{mockExamId:guid}/submission` | Usuario (dueño) | `SubmitMockExamRequest` → `200 SubmitMockExamResponse` · `400` · `404` · `409` · `422` |
| GET | `/api/v1/mock-exams/{mockExamId:guid}/result` | Usuario (dueño) | — → `200 MockExamResultResponse` · `404` · `409` |

**Detalle**
- `GET .../exercises`: oposición activa (si no, 404 `exam.not_found`). Ejercicios por `Number`, con `AvailableQuestionCount` = preguntas activas de los temas de la oposición cuya `Section` está en `Sections` (subconsulta `Count` en la proyección).
- `POST /mock-exams`:
  1. Valida: `ExamId` no vacío, `ExerciseNumber ≥ 1`; `PassScore`, si viene, > 0 → `400 ValidationProblem`.
  2. Oposición activa y ejercicio con ese número → si no, 404 (`exam.not_found` / `exercise.not_found`, "El ejercicio no existe.").
  3. `exercise.ToRules(request.PassScore)` (null → `DefaultPassScore`); `PassScore > MaxScore` → 422 `mock_exam.invalid_pass_score`.
  4. Elige al azar `QuestionCount` ids de preguntas activas de esas secciones (`OrderBy(_ => EF.Functions.Random()).Take(n)`, solo ids). Si hay menos → 422 `mock_exam.not_enough_questions` (un simulacro con menos preguntas no es un simulacro).
  5. `QuizAttempt.CreateMockExam(...)`, guarda y devuelve `201` con `Location: /api/v1/mock-exams/{id}/result` y el `MockExamResponse` (sin respuestas correctas).
- `POST .../submission`:
  1. Valida: `Answers` no nulo, como mucho `Limits.MaxQuestionsPerQuiz` elementos, ids no vacíos, sin `QuestionId` repetidos → `400`.
  2. Intento del usuario **y** `Mode == MockExam` en la consulta → si no, 404 `mock_exam.not_found` ("El simulacro no existe.").
  3. Carga en una consulta las preguntas del simulacro con sus opciones. `attempt.Submit(...)`; las reglas del dominio se traducen con `DomainErrors`.
  4. Por cada respuesta registrada, `progress.RecordAnswerAsync(user, isCorrect, now, ct)` (igual que la sincronización) y suma la XP.
  5. `SaveChangesAsync` (`DbUpdateConcurrencyException` → 409 como siempre) y devuelve `SubmitMockExamResponse(result, xpGained)`.
  - No rechaza entregas tardías: las guarda y el resultado lleva `SubmittedLate = true`.
- `GET .../result`: mismo filtro de dueño y modo. Sin entregar → 409 `mock_exam.not_submitted`. Si no, el resultado (sirve para recuperarlo si se perdió la respuesta de la entrega).

**Errores nuevos en `DomainErrors`**

| Código | Estado | Mensaje |
|---|---|---|
| `mock_exam.not_found` | 404 | El simulacro no existe. |
| `exercise.not_found` | 404 | El ejercicio no existe. |
| `mock_exam.not_enough_questions` | 422 | Todavía no hay preguntas suficientes para este simulacro. |
| `mock_exam.invalid_pass_score` | 422 | La nota para aprobar debe estar entre 0 y la nota máxima. |
| `mock_exam.answer_requires_submission` | 422 | En un simulacro las respuestas se envían todas juntas al entregarlo. |
| `mock_exam.not_submitted` | 409 | El simulacro todavía no se ha entregado. |
| `quiz.not_mock_exam` | 422 | Este test no es un simulacro. |
| `quiz.invalid_mode` | 422 | No se ha podido completar la operación. |

`POST /quizzes/{id}/answers` sobre un simulacro da 422 `mock_exam.answer_requires_submission` (lo traduce el handler existente). `GET /quizzes/{id}` sigue funcionando para simulacros (`Mode = "MockExam"`). El historial `GET /quizzes` los excluye: su único uso es la tarjeta «continuar» del inicio, y un simulacro no se puede retomar como test.

### Contracts (`src/OpoStudy.Contracts/MockExams/`)
- `ExamExerciseResponse(int Number, string Title, int QuestionCount, int DurationMinutes, decimal MaxScore, decimal PenaltyRatio, decimal DefaultPassScore, int AvailableQuestionCount)`.
- `CreateMockExamRequest(Guid ExamId, int ExerciseNumber, decimal? PassScore)`.
- `MockExamResponse(Guid Id, Guid ExamId, int ExerciseNumber, string Title, DateTimeOffset StartedAt, DateTimeOffset DeadlineAt, int DurationMinutes, decimal MaxScore, decimal PenaltyRatio, decimal PassScore, IReadOnlyList<MockExamQuestionResponse> Questions)`.
- `MockExamQuestionResponse(Guid QuestionId, int Order, string Statement, IReadOnlyList<QuizOptionResponse> Options)` — reutiliza `QuizOptionResponse` (sin `IsCorrect`).
- `SubmitMockExamRequest(IReadOnlyList<MockExamAnswer> Answers)` y `MockExamAnswer(Guid QuestionId, Guid OptionId)`; las preguntas en blanco no se envían.
- `MockExamResultResponse(Guid Id, int ExerciseNumber, string Title, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, bool SubmittedLate, int QuestionCount, int CorrectCount, int IncorrectCount, int BlankCount, decimal PenaltyRatio, decimal Score, decimal MaxScore, decimal PassScore, bool Passed, IReadOnlyList<MockExamResultItem> Items)`.
- `MockExamResultItem(Guid QuestionId, int Order, string Statement, IReadOnlyList<QuizOptionResponse> Options, Guid? SelectedOptionId, Guid CorrectOptionId, string Outcome, string Explanation, string LegalReference)`; `Outcome` = `"Correct" | "Incorrect" | "Blank"`.
- `SubmitMockExamResponse(MockExamResultResponse Result, int XpGained)`.

### Datos
Usa la skill `new-ef-migration` (migración `AddMockExams`).
- Tabla `exam_exercises`: `id` PK, `exam_id` FK → `exams` (cascade), `number`, `title` varchar(300), `question_count`, `duration_minutes`, `max_score` numeric(5,2), `penalty_ratio` numeric(3,2), `sections` `text[]`. Único `(exam_id, number)`.
- `quiz_attempts`: columnas nullable `mock_exam_exercise_number`, `mock_exam_title` varchar(300), `mock_exam_question_count`, `mock_exam_duration` (interval), `mock_exam_max_score` numeric(5,2), `mock_exam_penalty_ratio` numeric(3,2), `mock_exam_pass_score` numeric(5,2) y `score` numeric(5,2). `MockExamRules` como tipo complejo opcional (`ComplexProperty`, EF Core 10) o `OwnsOne`, con el prefijo `mock_exam_`. `Mode` ya es texto de 20: `MockExam` cabe.
- Sin cambios en `quiz_answers`: en blanco = fila con `selected_option_id` nulo en un intento terminado.

### App
Usa la skill `maui-ui` (vistas, sistema de diseño y capturas en claro y oscuro).

**Cliente:** `IOpoStudyApi` añade `GetExercisesAsync(Guid examId)`, `CreateMockExamAsync(CreateMockExamRequest)`, `SubmitMockExamAsync(Guid id, SubmitMockExamRequest)` y `GetMockExamResultAsync(Guid id)`; sus tipos, en `ApiJsonContext`.

**Navegación:** `Routes.MockExams = "mock-exams"` y `Routes.MockExam = "mock-exam"`; `QueryKeys.ExerciseNumber` y `QueryKeys.PassScore` (se reutilizan `ExamId` y `ExamName`). Flujo: `topics` → `mock-exams` → `mock-exam`. Páginas y ViewModels `Transient`.

**`TopicsPage`:** tarjeta "Simulacro de examen" ("Con tiempo y penalización, como el examen real") encima de "Test aleatorio" → `StartMockExamCommand` → `mock-exams` con `ExamId` y `ExamName`. Siempre visible (la lista de ejercicios gestiona el estado vacío).

**`MockExamsViewModel` / `MockExamsPage`** — título "Simulacro de examen". Hereda de `OfflineAwareViewModel`: sin conexión, mensaje "Necesitas conexión para hacer un simulacro." y nada se puede empezar.
- Carga `GetExercisesAsync`; estados del sistema de diseño (cargando, error + reintentar, vacío "Esta oposición aún no tiene simulacros.").
- Una tarjeta por ejercicio: `Title`; `"{QuestionCount} preguntas · {DurationMinutes} minutos · sobre {MaxScore} puntos"`; penalización en texto (`0.5` → "Cada fallo resta la mitad de un acierto", `0` → "Los fallos no restan", resto → "Cada fallo resta {ratio} de un acierto"); campo "Nota para aprobar" (`Entry` numérico, admite coma decimal, con `SemanticProperties.Description`) precargado con el último valor guardado en `IPreferencesStore` (`mock-exam.pass-score.{examId}.{number}`) o `DefaultPassScore`; validación > 0 y ≤ `MaxScore` con mensaje junto al campo; botón "Empezar".
- Si `AvailableQuestionCount < QuestionCount`, la tarjeta muestra "Aún no hay preguntas suficientes ({Available} de {QuestionCount})" y el botón queda deshabilitado.
- "Empezar": confirmación (`IDialogService.ConfirmAsync`) "Tendrás {n} minutos para {q} preguntas. Al acabar el tiempo se entrega solo." ("Empezar" / "Cancelar"); guarda la nota en preferencias y navega a `mock-exam` con `ExamId`, `ExerciseNumber` y `PassScore`.

**`MockExamViewModel` / `MockExamPage`** — título = `Title` del ejercicio. Estados: `IsLoading`, `IsRunning`, `IsSubmitting`, `IsFinished`, `ErrorMessage`.
- Al aparecer por primera vez crea el simulacro (`CreateMockExamAsync`); solo una vez aunque la página vuelva a aparecer. Error al crear → mensaje y botón "Volver".
- **Temporizador** (en Core, con `TimeProvider` inyectado para los tests): al recibir la respuesta fija `localDeadline = timeProvider.GetUtcNow() + DurationMinutes` (no depende del reloj del servidor ni se pausa en segundo plano). Un `ITimer` de `timeProvider.CreateTimer` cada segundo actualiza `RemainingText` (`"mm:ss"`) y `RemainingDescription` ("Quedan 23 minutos" / "Queda menos de un minuto") para el lector de pantalla, sin anunciar cada segundo. Anuncios con `IScreenReaderAnnouncer` al quedar 10, 5 y 1 minuto, y "Tiempo agotado. Entregando el simulacro." `IsTimeLow` (≤ 5 min) cambia el estilo a `Warning` **y** muestra el texto "Quedan menos de 5 minutos". El ViewModel libera el timer al terminar (`IDisposable` o al desaparecer la página).
- **Preguntas:** una cada vez, con "Anterior" / "Siguiente", `ProgressText` = `"Pregunta {n} de {total} · {respondidas} respondidas"` y un índice (cuadrícula de números tocables, 48 dp, con estado "respondida"/"en blanco" en texto para el lector) para saltar. Las opciones se seleccionan y se pueden cambiar; "Dejar en blanco" quita la selección. No se muestra nada de corrección hasta entregar.
- **Entregar:** botón "Entregar" → confirmación "¿Entregar el simulacro? Tienes {b} preguntas en blanco." → envía las seleccionadas. Al llegar el tiempo a 0: bloquea las respuestas y entrega sin confirmar.
- Error al entregar → `ErrorMessage` y "Reintentar entrega" (las respuestas se conservan y siguen bloqueadas si el tiempo acabó). Un 409 (ya entregado: se perdió la respuesta anterior) → `GetMockExamResultAsync` y se muestra el resultado.
- **Salir a medias:** `Shell.BackButtonBehavior` y el botón atrás de Android (`OnBackButtonPressed` del code-behind, que solo delega en el comando) → `ExitCommand`: confirmación "¿Salir del simulacro? No se guardará y no contará." ("Salir" / "Seguir"). Sin confirmación una vez terminado.
- **Resultado** (`IsFinished`): título "Resultado"; nota `"{Score} / {MaxScore}"` en `DisplayLabel` con formato es-ES (`12,73 / 20`); "Apto" (`SuccessLabel`) o "No apto" (`ErrorLabel`) junto a "Nota para aprobar: {PassScore}"; desglose "{c} aciertos · {i} fallos · {b} en blanco" y la cuenta "Netas: {c} − {i} × {PenaltyRatio} = {netas}"; "Tiempo: {usado} de {duración}"; "Entregado fuera de tiempo" si `SubmittedLate`; "+{XpGained} XP". Lista de preguntas con "Acertada" / "Fallada" / "En blanco" (texto, no solo color), tu respuesta, la correcta y, en falladas y en blanco, la aclaración y "Fuente: {LegalReference}". Botones "Hacer otro simulacro" (`..`) y "Volver al inicio" (`//home`, `SecondaryButton`).
- Logging como en el resto (`[LoggerMessage]`, solo tipo de excepción).
- `QuizTitles` (si alguna pantalla muestra el modo de un test): `"MockExam"` → "Simulacro".

## Seguridad
- Autorización por recurso: entrega y resultado filtran por `UserId` del token y por `Mode == MockExam` en la consulta; el de otro usuario → 404.
- Las respuestas correctas, aclaraciones y fuentes no salen hasta entregar (`MockExamResponse` no las lleva; comprobar sobre el JSON bruto).
- Entrada validada (ids, duplicados, tamaño de `Answers`, `PassScore`); EF Core parametrizado; el aleatorio usa `random()` de Postgres.
- El tiempo lo controla la app; el servidor guarda la entrega aunque llegue tarde y lo marca (`SubmittedLate`). Es una herramienta personal de entrenamiento: hacer trampas solo engaña al propio usuario, y rechazar la entrega perdería respuestas válidas por un fallo de red.
- Ningún secreto nuevo. Rate limiting heredado del grupo `/api/v1`.

## Criterios de aceptación
- [ ] `exam.json` de Programador C1 define los dos ejercicios; el importador los carga, valida secciones inexistentes y números repetidos, y es idempotente.
- [ ] Se puede hacer un simulacro de cada ejercicio con su número de preguntas, solo de su bloque, y su tiempo.
- [ ] La nota aplica la penalización, las preguntas en blanco no puntúan ni restan, y la nota mínima es 0 (tests de `MockExamScoring` con los ejemplos de esta spec).
- [ ] La nota para aprobar es la mitad del máximo por defecto, el usuario la puede cambiar antes de empezar y la app la recuerda por ejercicio; el resultado dice "Apto" o "No apto".
- [ ] Al acabar el tiempo se bloquean las respuestas, se entrega solo y se muestra el resultado; el temporizador se entiende con lector de pantalla y el aviso de poco tiempo no depende solo del color.
- [ ] Crear un simulacro no revela respuestas; responder pregunta a pregunta un simulacro → 422; entregar dos veces → 409; el simulacro de otro usuario → 404.
- [ ] Las preguntas falladas del simulacro aparecen en el repaso y las respuestas suman XP y objetivo diario; las en blanco no.
- [ ] Revisión visual con capturas en claro y oscuro (ejercicios, pregunta con temporizador, aviso de poco tiempo, resultado). Build sin warnings (backend y `net10.0-android`).

## Plan de tests
- **Unitarios Domain** (`tests/OpoStudy.Domain.Tests`): `MockExamScoring` (ejemplo 12,73; netas negativas → 0; todo en blanco → 0; todo bien → máximo; redondeo de punto medio); `ExamExercise` (rangos inválidos, `DefaultPassScore`, `ToRules` con nota > máximo); `QuizAttempt.CreateMockExam` (número de preguntas distinto → error; `Create` con `MockExam` → error); `Submit` (registra solo las seleccionadas, `BlankCount`, `Score`, `Passed`, `FinishedAt`; pregunta u opción ajena → error sin registrar nada; doble entrega → `quiz.finished`; no simulacro → `quiz.not_mock_exam`; `IsSubmittedLate` dentro y fuera del margen); `Answer` en simulacro → error.
- **Infraestructura/importador** (donde estén hoy sus tests): ejercicios importados, sección inexistente y número repetido → error, reimportación idempotente y borrado de un ejercicio quitado del fichero.
- **Integración** (`tests/OpoStudy.Api.IntegrationTests`, `MockExamsEndpointsTests`, ampliando `ContentSeeder` con secciones y ejercicios pequeños, p. ej. 3 preguntas): sin token → 401; listar ejercicios con `availableQuestionCount`; crear → 201, `Location`, nº de preguntas exacto, todas de las secciones del ejercicio y activas, JSON sin `isCorrect`/`explanation`/`legalReference`; `passScore` por defecto y personalizado; `passScore` > máximo → 422; preguntas insuficientes → 422; ejercicio inexistente → 404; entregar con una bien, una mal y una en blanco → nota, recuentos, `outcome` por pregunta y `xpGained`; entregar dos veces → 409; `GET result` antes (409) y después (200); usuario B → 404; `POST /quizzes/{id}/answers` sobre simulacro → 422; opción ajena o pregunta repetida en la entrega → 422/400; la fallada aparece en `GET` del repaso (`Review`); entrega tras `DeadlineAt` + margen (con `FakeTimeProvider` si el factory lo permite) → 200 con `submittedLate = true`.
- **Unitarios app** (`tests/OpoStudy.Mobile.Core.Tests`, NSubstitute + `FakeTimeProvider`): `MockExamsViewModel` (textos de penalización, nota por defecto y recordada, validación de la nota, ejercicio sin preguntas suficientes deshabilitado, navegación con los parámetros, sin conexión); `MockExamViewModel` (crea una sola vez; seleccionar, cambiar y dejar en blanco; la entrega envía solo las respondidas; `RemainingText` y anuncios a 10/5/1 min; `IsTimeLow`; a 0 bloquea y entrega sin confirmar; error de entrega conserva respuestas y permite reintentar; 409 → carga el resultado; salir pide confirmación solo mientras está en curso; resultado con formato es-ES).
- **Manual:** AppHost + contenido importado, emulador: simulacro del ejercicio 1 entregado a mano dejando alguna en blanco; simulacro con la app en segundo plano unos minutos (el tiempo sigue corriendo); dejar que se agote el tiempo (para probarlo rápido, ejercicio temporal de 1 minuto en un `exam.json` local, sin versionar); TalkBack con el temporizador; capturas en claro y oscuro.
