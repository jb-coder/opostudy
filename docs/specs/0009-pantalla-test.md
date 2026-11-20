# 0009 — Selección de oposición y pantalla de test con aclaración

- **Issue:** #19
- **Milestone:** v0.1.0-alpha
- **Estado:** implementada

## Objetivo
Flujo principal de la app: desde el inicio, elegir oposición y tema (o test aleatorio), hacer un test pregunta a pregunta viendo al momento si se ha acertado, la aclaración y la referencia legal, y terminar con un resumen de aciertos y fallos. Todo sale de la base de datos a través de los endpoints de #16 (spec 0006): ninguna llamada al LLM.

## Alcance
**Incluye:**
- Cliente Refit de los endpoints de oposiciones, temas y tests (los DTO ya existen en `OpoStudy.Contracts`).
- Pantalla de oposiciones, pantalla de temas (con test aleatorio y número de preguntas), pantalla de test y resumen final.
- Botón "Hacer un test" en el inicio.
- ViewModels en `OpoStudy.Mobile.Core` con tests unitarios.

**No incluye:**
- Historial de tests ni retomar un test desde una lista (`GET /quizzes` sin usar). Si el usuario sale a mitad, las respuestas ya enviadas quedan guardadas en la API, pero la app no ofrece retomarlo.
- Confirmación al salir de un test a medias, temporizador, marcar preguntas o repaso de falladas (v0.2).
- Recordar la oposición elegida (`User.ChooseExam`).
- Cambios en la API, el dominio o la base de datos.

## Diseño
### Dominio
Sin cambios.

### API
Sin cambios. Se consumen (spec 0006):

| Método | Ruta | Uso en la app |
|---|---|---|
| GET | `/api/v1/exams` | Lista de oposiciones |
| GET | `/api/v1/exams/{examId}/topics` | Temas con `QuestionCount` |
| POST | `/api/v1/quizzes` | Crear test (`CreateQuizRequest`) → `QuizResponse` |
| GET | `/api/v1/quizzes/{quizId}` | Cargar el test (y resincronizar tras un 409) |
| POST | `/api/v1/quizzes/{quizId}/answers` | Responder (`SubmitAnswerRequest`) → `AnswerFeedbackResponse` |

### Datos
Sin cambios.

### App
**Cliente (`OpoStudy.Mobile.Core/Api/`)**
- `IOpoStudyApi`: añadir `GetExamsAsync`, `GetTopicsAsync(Guid examId)`, `CreateQuizAsync(CreateQuizRequest)`, `GetQuizAsync(Guid quizId)`, `SubmitAnswerAsync(Guid quizId, SubmitAnswerRequest)`, todos con `CancellationToken cancellationToken = default` (mismo estilo que los existentes).
- `ApiJsonContext`: añadir `[JsonSerializable]` de `IReadOnlyList<ExamResponse>`, `IReadOnlyList<TopicResponse>`, `CreateQuizRequest`, `QuizResponse`, `SubmitAnswerRequest` y `AnswerFeedbackResponse` (los tipos anidados los resuelve el generador; compruébalo con un test de deserialización si hay dudas).
- Los errores se muestran con `ApiErrorMessages.From(ex)` (ya lee `title`/`detail` del ProblemDetails en español).

**Navegación (`Routes`, `QueryKeys`, `AppShell.xaml.cs`)**
- Rutas nuevas, registradas con `Routing.RegisterRoute` y apiladas sobre `home`: `Routes.Exams = "exams"`, `Routes.Topics = "topics"`, `Routes.Quiz = "quiz"`.
- `QueryKeys`: `ExamId`, `ExamName`, `QuizId`, `QuizTitle`. Los ViewModels que los reciben implementan `IQueryReceiver` (como `RegisterViewModel` con el email).
- Volver: `navigation.GoToAsync("..")`; ir al inicio: `Routes.Root(Routes.Home)`. Añade `Routes.Back = ".."` para no repetir el literal.
- Flujo: `home` → `exams` → `topics` → `quiz`. Desde el resumen, "Hacer otro test" vuelve a `topics` (`..`) y "Volver al inicio" a `//home`.
- Registrar páginas y ViewModels como `Transient` en `MauiProgram`.

**`HomeViewModel` / `HomePage`**
- Nueva tarjeta al principio (antes de "Estado del servidor"): título "Practicar", texto "Haz un test y repasa la aclaración de cada pregunta." y botón principal "Hacer un test" → `StartQuizCommand` → `GoToAsync(Routes.Exams)`.

**`ExamsViewModel` / `ExamsPage`** — título "Elige oposición"
- `OnAppearingAsync` carga `GetExamsAsync` la primera vez; `RefreshCommand` + `RefreshView`. Estados del patrón 3 del sistema de diseño (cargando "Cargando oposiciones…", error + "Reintentar", vacío "No hay oposiciones disponibles todavía.").
- Cada oposición es una tarjeta tocable (`CardBorder` + `TapGestureRecognizer`, alto mínimo 48, `SemanticProperties.Description` = nombre) con `Name` (`SubtitleLabel`) y `"{Organization} · Grupo {Group}"` (`CaptionLabel`).
- `SelectExamCommand(ExamResponse)` → `GoToAsync(Routes.Topics, {ExamId, ExamName})`.

**`TopicsViewModel` / `TopicsPage`** — título = nombre de la oposición
- `ApplyQuery` lee `ExamId` y `ExamName`; carga `GetTopicsAsync(examId)` la primera vez. Mismos estados (cargando "Cargando temas…", error, vacío "Esta oposición aún no tiene temas.").
- Selector de número de preguntas: opciones 10, 20 y 30, por defecto 10 (`QuestionCountOptions` + `SelectedQuestionCount`; en la vista un `Picker` con etiqueta "Preguntas por test").
- Tarjeta "Test aleatorio" ("Preguntas de todos los temas") arriba de la lista → `StartRandomQuizCommand`. Deshabilitada si ningún tema tiene preguntas.
- Lista de temas: `Title` tal cual (ya empieza por «Tema N.» con la numeración del BOP, que no coincide con `Number`, ver spec 0004), `Section` como `CaptionLabel` si existe y `"{QuestionCount} preguntas"` (`"1 pregunta"` en singular). Los temas con `QuestionCount == 0` se muestran atenuados con "Sin preguntas todavía" y no son tocables (en el ViewModel: `TopicItem.IsAvailable`; el comando ignora los no disponibles).
- `StartTopicQuizCommand(TopicItem)` / `StartRandomQuizCommand`: `IsBusy`, `CreateQuizAsync(new(examId, topicId|null, SelectedQuestionCount))` y navega a `Routes.Quiz` con `QuizId` y `QuizTitle` (`Title` del tema o `"Test aleatorio"`). Si el tema tiene menos preguntas que las pedidas, la API devuelve las disponibles: no se avisa. Error (p. ej. 422 `quiz.no_questions_available`) → `ErrorMessage`, sin navegar.

**`QuizViewModel` / `QuizPage`** — título = `QuizTitle`, una pregunta cada vez
- `ApplyQuery` lee `QuizId` y `QuizTitle`; `OnAppearingAsync` carga `GetQuizAsync` la primera vez y se coloca en la **primera pregunta sin `Feedback`** (si todas tienen, muestra directamente el resumen). Así un test con respuestas previas continúa donde iba.
- Estado expuesto (todo en Core, sin tipos de MAUI):
  - `ProgressText` = `"Pregunta {n} de {total}"`, `Progress` (0-1, para un `ProgressBar`), `ScoreText` = `"{aciertos} aciertos · {fallos} fallos"`.
  - `CurrentStatement` y `Options`: `ObservableCollection<QuizOptionItem>`; `QuizOptionItem` (`ObservableObject`) con `Id`, `Letter` ("A", "B"… según `Order`), `Text` y `State` (`enum OptionState { Neutral, Correct, Incorrect, Dimmed }`) más `StateText` ("Correcta", "Tu respuesta: incorrecta", vacío).
  - `IsAnswered`, `IsCorrect`, `FeedbackTitle` ("¡Correcto!" / "Incorrecto"), `Explanation`, `LegalReference`, `NextButtonText` ("Siguiente pregunta" / "Ver resultado" en la última).
  - `IsFinished` y los datos del resumen.
- `AnswerCommand(QuizOptionItem)` (deshabilitado si `IsBusy` o `IsAnswered`): `SubmitAnswerAsync(quizId, new(questionId, optionId))`. Con la respuesta: la opción correcta pasa a `Correct`; si la elegida es otra, esa pasa a `Incorrect`; el resto a `Dimmed`. Actualiza recuentos con los de la respuesta, guarda el `Feedback` en la pregunta local (para el resumen) y muestra la aclaración.
  - 409 (`ApiException` con `StatusCode == Conflict`): recarga el test con `GetQuizAsync` y se coloca de nuevo en la primera pendiente (la pregunta ya respondida muestra su `Feedback`). Otros errores → `ErrorMessage` y la pregunta sigue sin responder (se puede reintentar).
- `NextCommand`: pasa a la siguiente pregunta pendiente; si no queda ninguna (o `QuizFinished` venía a `true`), muestra el resumen.
- Vista de pregunta: `ProgressBar` + `ProgressText` y `ScoreText` (`CaptionLabel`), enunciado en `TitleLabel` dentro de una tarjeta, opciones como tarjetas tocables con la letra y el texto (alto mínimo 48, `SemanticProperties.Description` = `"Opción {Letter}: {Text}"`). Colores por `DataTrigger` sobre `State`: borde y texto `Success` para `Correct`, `Error` para `Incorrect`, opacidad 0.6 para `Dimmed`; **siempre** con `StateText` visible (no solo color). Tras responder, tarjeta de aclaración: `FeedbackTitle` (`SuccessLabel`/`ErrorLabel`), `Explanation` (`BodyLabel`) y `LegalReference` (`CaptionLabel`, precedida de "Referencia: "), y el botón `NextButtonText`. Todo dentro de un `ScrollView` (las aclaraciones pueden ser largas).
- Resumen (`IsFinished`): título "Resultado", `"{aciertos} de {total} correctas"` en `DisplayLabel`, porcentaje redondeado (`"{p} %"`), y la lista de preguntas (`SummaryItems`: número, enunciado, "Acertada"/"Fallada" con el color correspondiente; en las falladas, también la aclaración y la referencia). Botones "Hacer otro test" (`..`) y "Volver al inicio" (`//home`, `SecondaryButton`).

**Comunes**
- Patrones del sistema de diseño (`docs/diseno/sistema-diseno.md`): `ViewModelPage`, `x:DataType` en raíz y `DataTemplate`, sin lógica en el code-behind, estilos con clave existentes, `HeadingLevel` en títulos, textos en español.
- Logging como en `HomeViewModel` (`[LoggerMessage]`, solo el tipo de excepción; nunca enunciados, respuestas ni tokens).
- Usa la skill `maui-ui` para las vistas y la revisión con capturas.

## Seguridad
- Todas las llamadas llevan el token con el `AuthHeaderHandler` existente; un 401 lo gestiona el `SessionExpiredHandler` ya implementado.
- La app nunca conoce la respuesta correcta antes de responder (la API no la envía): no hay nada que ocultar en cliente.
- Los ids de test llegan por navegación desde la propia app; la API ya filtra por dueño (404 si no es suyo).
- Ningún secreto nuevo.

## Criterios de aceptación
- [x] Desde el inicio se llega a oposiciones → temas → test, y se completa un test de 10 preguntas en el emulador contra la API local (AppHost).
- [x] Tras responder se ve si se ha acertado, la opción correcta, la aclaración y la referencia legal, sin depender solo del color.
- [x] Al terminar se muestra el resumen con aciertos, fallos y porcentaje, y se puede hacer otro test o volver al inicio.
- [x] La aclaración viene de la base de datos: en las trazas del AppHost solo hay llamadas a `/api/v1/quizzes…` y ninguna al Worker ni a proveedores LLM. (verificado en #31: la app solo llama a `/api/v1/exams…` y `/api/v1/quizzes…`; aún no existe ningún proveedor LLM en el código).
- [x] Un error de red al responder muestra el mensaje y permite reintentar la misma pregunta; un 409 resincroniza el test. (cubierto con tests unitarios de `QuizViewModel`; no probado con modo avión en el emulador).
- [x] Revisión visual con capturas en claro y oscuro de las cuatro vistas (oposiciones, temas, pregunta respondida, resumen) siguiendo el sistema de diseño.
- [x] Tests unitarios de los ViewModels; build sin warnings (backend y `net10.0-android`).

## Plan de tests
- **Unitarios** (`tests/OpoStudy.Mobile.Core.Tests`, NSubstitute para `IOpoStudyApi`/`INavigationService`, Shouldly):
  - `HomeViewModel`: `StartQuiz` navega a `exams`.
  - `ExamsViewModel`: carga la lista; error → `ErrorMessage` (vía `ApiErrorMessages`); lista vacía → estado vacío; seleccionar navega a `topics` con `ExamId` y `ExamName`.
  - `TopicsViewModel`: `ApplyQuery` + carga con el `examId` recibido; texto de preguntas en singular/plural; tema sin preguntas no disponible y no crea test; aleatorio deshabilitado si todos tienen 0; crear test por tema y aleatorio envía el `CreateQuizRequest` correcto (con `SelectedQuestionCount`) y navega a `quiz` con `QuizId` y `QuizTitle`; error al crear → `ErrorMessage` y no navega.
  - `QuizViewModel`: carga y se coloca en la primera pendiente; todas respondidas → resumen; responder bien → `Correct` en la elegida y `Dimmed` el resto; responder mal → `Incorrect` en la elegida y `Correct` en la correcta; recuentos y `ProgressText`; `AnswerCommand` no ejecutable tras responder; `NextButtonText` en la última; `Next` en la última → `IsFinished` con porcentaje correcto; 409 → recarga con `GetQuizAsync`; error de red → `ErrorMessage` y la pregunta sigue sin responder.
  - Serialización: `ApiJsonContext` deserializa un `QuizResponse` y un `AnswerFeedbackResponse` de ejemplo (JSON camelCase como el de la API).
- **Integración:** ninguna nueva (la API no cambia).
- **Manual:** Docker + AppHost con el contenido importado, app en el emulador con sesión iniciada: test de 10 preguntas de un tema (acertando y fallando alguna), test aleatorio, salir a mitad y comprobar que no rompe nada, modo avión al responder → mensaje y reintento. Revisar las trazas del AppHost. Capturas en claro y oscuro (`adb shell cmd uimode night yes|no`).
