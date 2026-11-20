# 0012 — Repaso de preguntas falladas y mejoras del test

- **Issue:** #39
- **Milestone:** v0.2.0
- **Estado:** lista

## Objetivo
Que fallar una pregunta sirva para aprender: el usuario puede hacer tests solo con las preguntas que tiene falladas, de toda la oposición o de un tema, hasta acertarlas. Además, la app deja de perder tests a medias por error: pide confirmación al salir de un test empezado y ofrece continuarlo desde el inicio.

## Decisiones
- **Una pregunta está «fallada» mientras su última respuesta sea incorrecta.** Cuenta la respuesta más reciente del usuario a esa pregunta en cualquier test (por `AnsweredAt`; a igualdad, la del `QuizAttemptId` mayor, que es un UUID v7). Si la acierta, sale del repaso; si la vuelve a fallar en cualquier test, vuelve a entrar.
- **Se calcula con las `QuizAnswer` existentes, sin tabla nueva ni migración.** Las preguntas desactivadas (`IsActive = false`) no se cuentan ni se repasan.
- **El repaso es un modo de test más, `QuizMode.Review`**, con tema opcional: con tema repasa las falladas de ese tema; sin tema, las de toda la oposición. Las preguntas se eligen al azar entre las falladas, hasta `QuestionCount`.
- **El repaso cuenta igual que cualquier test** para el objetivo diario, la racha y el XP (reglas de las specs 0010 y 0011, vía `ProgressRecorder`). No hay XP extra.
- **Confirmación al salir solo si el test está empezado y sin terminar** (al menos una respuesta y sin `FinishedAt`).
- **Continuar un test a medias desde el inicio.** Así la confirmación no miente («podrás continuarlo»). Solo se ofrece el test más reciente del usuario, si está sin terminar y tiene al menos una respuesta.

## Alcance
**Incluye:**
- Dominio: modo `Review` en `QuizAttempt`.
- API: `CreateQuizRequest.MistakesOnly` y endpoint con el número de falladas por tema.
- App: interruptor «Solo preguntas falladas» en la pantalla de temas, resumen del repaso con las falladas que quedan, confirmación al salir del test y tarjeta «Test a medias» en el inicio.

**No incluye:**
- Marcar preguntas para repasarlas después y temporizador: issues aparte (v0.2.0 o posterior).
- Historial completo de tests ni estadísticas por tema (v0.4).
- Repaso espaciado (repetir aciertos al cabo de días) ni XP extra por acertar una fallada.
- Repaso offline (0014).

## Diseño
### Dominio
Glosario (ADR 0001): `Mistake` (pregunta fallada) y `Review` (repaso). Ya añadidos.

- `QuizMode` gana `Review`. Se guarda como texto (`HasConversion<string>`, máx. 20): no hay cambio de modelo.
- `QuizAttempt.Create`: con `Review`, `topicId` es opcional (las reglas de `ByTopic` y `Random` no cambian). El resto de invariantes (entre 1 y `Limits.MaxQuestionsPerQuiz` preguntas, sin repetir) se mantienen.
- `QuizAttempt.Answer` no cambia: la respuesta de un repaso queda como `QuizAnswer` y es la que decide si la pregunta sigue fallada.

### API
Rutas bajo `/api/v1`. Todas exigen usuario autenticado y operan solo sobre el usuario del token.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/exams/{examId}/mistakes` | Usuario | — → `200 MistakeSummaryResponse`; `404 exam.not_found` |
| POST | `/quizzes` | Usuario | `CreateQuizRequest` (+ `MistakesOnly`) → `201 QuizResponse`; `422 quiz.no_mistakes` |

**Consulta de falladas** (en `Features/Quizzes`, p. ej. `MistakeQueries.PendingMistakes(OpoStudyDbContext db, Guid userId)` que devuelve un `IQueryable` con `QuestionId` y `TopicId`):
- Respuestas respondidas (`IsCorrect != null`) de los `QuizAttempt` del usuario (`db.QuizAttempts.Where(a => a.UserId == userId).SelectMany(a => a.Answers)` o `db.Set<QuizAnswer>()` con join).
- Se queda con las que no tienen otra respuesta posterior del mismo usuario a la misma pregunta (`NOT EXISTS` con el desempate de las decisiones) y con `IsCorrect == false`.
- Une con `Questions` activas para obtener el `TopicId`.
- Tiene que traducirse a SQL entero (nada de evaluación en cliente). Los índices actuales bastan: `quiz_answers` ya tiene índice por `question_id` (FK) y `quiz_attempts` por `(user_id, started_at)`.

**`GET /exams/{examId}/mistakes`** (en `ExamsEndpoints`, junto a `/topics`):
- Si la oposición no existe o no está activa, mismo 404 que `GET /exams/{examId}/topics`.
- Contratos nuevos en `Contracts/Quizzes`:
  - `MistakeSummaryResponse(int Total, IReadOnlyList<TopicMistakeCountResponse> Topics)`.
  - `TopicMistakeCountResponse(Guid TopicId, int Count)`: solo los temas con `Count > 0`, ordenados por `Topic.Number`.

**`POST /quizzes`**:
- `CreateQuizRequest` gana un último parámetro `bool MistakesOnly = false` (compatible con las peticiones actuales).
- Con `MistakesOnly`:
  - Mismas validaciones de oposición, tema y número de preguntas.
  - Candidatas: las falladas del usuario de esa oposición (o de ese tema, si llega `TopicId`), al azar y hasta `QuestionCount`. Modo `Review`, con `TopicId` tal cual llegue.
  - Si no hay ninguna: `422` con código nuevo `quiz.no_mistakes` y mensaje «No tienes preguntas falladas pendientes de repasar aquí.» (en `DomainErrors`).
- `QuizResponse.Mode` y `QuizSummaryResponse.Mode` pueden valer `"Review"`: actualiza su comentario XML.
- `SubmitAnswer` no cambia.

Añade `MistakeSummaryResponse` e `IReadOnlyList<QuizSummaryResponse>` a `ApiJsonContext` (si no está) y al cliente Refit:
- `GetMistakesAsync(Guid examId)`.
- `GetQuizzesAsync(int skip, int take)`, sobre el `GET /quizzes` ya existente.

### Datos
Sin cambios ni migración. `Model_HasNoPendingChangesToMigrate` debe seguir en verde.

### App
Usa la skill `maui-ui` (tema claro y oscuro, capturas en el emulador).

**Pantalla de temas (`TopicsViewModel` / `TopicsPage`)**
- Carga `GetMistakesAsync(examId)` en paralelo con los temas. Los temas se cargan solo la primera vez; las falladas, **cada vez que aparece la página**, porque cambian al volver de un test.
- Interruptor «Solo preguntas falladas» (`MistakesOnly`, `Switch` con etiqueta, alto mínimo 48), encima de «Test aleatorio»:
  - Con `Total == 0`: deshabilitado, con el texto «Aún no tienes preguntas falladas.».
  - Si falla la carga de falladas: deshabilitado, con el texto «No se han podido cargar tus preguntas falladas.». No bloquea la lista de temas.
  - Si al recargar `Total` pasa a 0, se apaga solo.
- Con el interruptor activado:
  - La tarjeta «Test aleatorio» pasa a «Repasar todas las falladas», con «{Total} preguntas falladas» («1 pregunta fallada» en singular).
  - Cada tema muestra «{n} falladas» («1 fallada»). Los temas sin falladas se atenúan con «Sin falladas» y no son tocables (`TopicItem.IsAvailable` depende del modo).
  - Crear un test envía `MistakesOnly = true`. El título es «Repaso: {Title del tema}» o «Repaso de falladas».
  - Un 422 `quiz.no_mistakes` (falladas desfasadas) muestra el mensaje y recarga las falladas.
- Con el interruptor desactivado, todo funciona como en la 0009.

**Pantalla de test (`QuizViewModel` / `QuizPage`)**
- **Confirmación al salir:**
  - `BackCommand` en el ViewModel, enlazado al botón atrás de la barra (`Shell.BackButtonBehavior`) y al botón atrás de Android (`OnBackButtonPressed` en `QuizPage`: ejecuta el comando y devuelve `true`).
  - Si hay al menos una pregunta respondida y el test no ha terminado, pide confirmación con `IDialogService.ConfirmAsync`: título «¿Salir del test?», mensaje «Tus respuestas se guardan y podrás continuarlo desde el inicio.», botones «Salir» y «Seguir con el test».
  - Si confirma o no hace falta preguntar, `GoToAsync(Routes.Back)`.
  - Los botones del resumen no piden confirmación.
- **Resumen de un repaso** (`Mode == "Review"`):
  - Al terminar, carga `GetMistakesAsync(examId)` y muestra cuántas falladas quedan en su ámbito (el `Count` del tema si el test tiene `TopicId`; si no, `Total`):
    - «Te quedan {n} preguntas falladas» (singular «Te queda 1 pregunta fallada»).
    - O «¡Has repasado todas tus preguntas falladas!» si no queda ninguna.
  - Si quedan, el botón «Hacer otro test» se titula «Seguir repasando» (misma acción: `..`, y la pantalla de temas recarga las falladas con el interruptor aún activado).
  - Si la carga falla, se omite la línea sin mostrar error.

**Pantalla de inicio (`HomeViewModel` / `HomePage`)**
- Tarjeta «Test a medias», encima de «Practicar». Se carga con `GetQuizzesAsync(0, 1)` en el mismo refresco que la racha y el nivel, y solo aparece si el test más reciente está sin terminar y tiene al menos una respuesta.
  - Texto principal: el título del test, igual que en la pantalla de temas: `TopicTitle`, «Test aleatorio», o en un repaso «Repaso: {TopicTitle}» / «Repaso de falladas».
  - Debajo, «{respondidas} de {total} respondidas».
  - Botón «Continuar» → `Routes.Quiz` con `QuizId` y `QuizTitle`. `QuizViewModel` ya se coloca en la primera pregunta pendiente.
  - Si la carga falla, la tarjeta no aparece; sin mensaje de error.
- La lógica de títulos y textos va en Mobile.Core para testearla, p. ej. un helper `QuizTitles` compartido por `TopicsViewModel` y `HomeViewModel`.

## Seguridad
- El usuario sale siempre del claim `sub` (`CurrentUserService`). La consulta de falladas filtra por `QuizAttempt.UserId`: nunca usa respuestas de otro usuario.
- `GET /exams/{examId}/mistakes` solo acepta el id de la oposición, y las cuentas son siempre las del usuario del token.
- `MistakesOnly` no da acceso a nada nuevo: solo restringe las preguntas candidatas. La app sigue sin recibir la opción correcta antes de responder.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [ ] Un usuario sin respuestas tiene `Total = 0` y `Topics` vacío en `GET /exams/{examId}/mistakes`.
- [ ] Tras fallar una pregunta, aparece en las falladas de su tema. Si después la acierta en otro test (de repaso o normal), deja de aparecer; si la vuelve a fallar, vuelve.
- [ ] Una pregunta acertada y fallada después cuenta como fallada (manda la última respuesta).
- [ ] Las preguntas sin responder de un test a medias y las preguntas desactivadas no cuentan como falladas.
- [ ] `POST /quizzes` con `MistakesOnly` crea un test `Review` solo con falladas del ámbito pedido (tema u oposición), sin superar `QuestionCount`. Sin falladas devuelve `422 quiz.no_mistakes`.
- [ ] Las respuestas de un repaso suman al objetivo diario, a la racha y al XP como las de cualquier test.
- [ ] Las falladas y los repasos de un usuario no dependen de las respuestas de otro.
- [ ] La pantalla de temas permite repasar las falladas de toda la oposición o de un tema, con sus contadores, y los recarga al volver de un test.
- [ ] Salir de un test empezado y sin terminar pide confirmación (botón de la barra y botón atrás de Android); sin respuestas o terminado, sale directamente.
- [ ] El inicio ofrece continuar el último test a medias, y al continuar se abre en la primera pregunta pendiente.
- [ ] El resumen de un repaso indica cuántas falladas quedan (verificado en el emulador, tema claro y oscuro).

## Plan de tests
- **Unitarios (Domain):**
  - `QuizAttempt.Create` en modo `Review` con y sin tema.
  - `ByTopic` sin tema y `Random` con tema siguen fallando.
- **Integración (API, Testcontainers, `ContentSeeder`, `FakeTimeProvider` para ordenar las respuestas):**
  - `GET /mistakes` en estos casos:
    - Usuario nuevo.
    - Fallo seguido de acierto en otro test.
    - Acierto seguido de fallo.
    - Pregunta sin responder.
    - Pregunta desactivada.
    - Dos temas con cuentas distintas y orden por `Number`.
    - Oposición inexistente: 404.
  - `POST /quizzes` con `MistakesOnly`:
    - Por tema y por oposición, solo con falladas y respetando `QuestionCount`.
    - Sin falladas: 422 `quiz.no_mistakes`.
    - Modo `"Review"` en `QuizResponse` y en `GET /quizzes`.
  - Responder un repaso actualiza las falladas, y el XP y el objetivo diario.
  - Aislamiento entre dos usuarios.
  - `Model_HasNoPendingChangesToMigrate` sigue en verde.
- **Unitarios (Mobile.Core):**
  - `TopicsViewModel`:
    - Interruptor deshabilitado con 0 falladas o con error de carga.
    - Contadores en singular y plural, y disponibilidad de temas según el modo.
    - Petición con `MistakesOnly` y título «Repaso: …».
    - 422 que recarga las falladas.
    - Recarga de falladas en cada aparición, sin recargar los temas.
  - `QuizViewModel`:
    - `BackCommand` con test empezado: confirmación aceptada navega y rechazada no navega.
    - Sin respuestas o terminado: no pregunta.
    - Resumen de repaso con falladas restantes (0, 1, varias, por tema y por oposición) y con error de carga.
  - `HomeViewModel`:
    - Tarjeta «Test a medias» visible solo con test sin terminar y con respuestas.
    - Textos y títulos por modo.
    - Navegación con `QuizId` y `QuizTitle`.
    - Error de carga: sin tarjeta.
  - Serialización: `ApiJsonContext` deserializa un `MistakeSummaryResponse` y una lista de `QuizSummaryResponse`.
- **Manual (emulador, AppHost):**
  - Hacer un test fallando 3 preguntas y ver los contadores en la pantalla de temas.
  - Repasar las falladas acertando 2: el resumen dice «Te queda 1 pregunta fallada» y «Seguir repasando» vuelve a temas con el contador en 1.
  - Salir a mitad con el botón de la barra y con el botón atrás de Android: aparece la confirmación.
  - Continuar desde el inicio.
  - Capturas en tema claro y oscuro de temas con el interruptor, del resumen de repaso, del diálogo y de la tarjeta «Test a medias».
