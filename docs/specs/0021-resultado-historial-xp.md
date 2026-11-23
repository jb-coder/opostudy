# 0021 — Supuestos: resultado detallado, historial de correcciones y XP

- **Issue:** #61
- **Milestone:** v0.3.0
- **Estado:** lista

> Sexta y última spec de la v0.3.0. Parte de la 0018 (cola `gradings`, `GradingProcessor` en el Worker), la 0019 (Gemini) y la 0020 (app: lista, editor, envío y pantalla `grading` con polling). También se apoya en la 0010/0011/0015 (`DailyActivity`, racha, `XpRules`, `ProgressRecorder`).
> Sin términos nuevos en el glosario del ADR 0001.

## Objetivo
Cerrar el ciclo del supuesto: al terminar la corrección, el usuario ve la nota por apartado y por criterio, los comentarios del corrector y la respuesta modelo; puede consultar sus correcciones anteriores; y el esfuerzo cuenta en el hábito: un supuesto corregido da XP según la nota y cumple el objetivo del día (con su bonus y la racha).

## Alcance
**Incluye:**
- **Worker**: al pasar una corrección a `Completed`, dar XP proporcional a la nota y marcar como cumplido el objetivo del día del envío, en la misma transacción.
- Refactor: la parte de escritura de `ProgressRecorder` (liquidar la racha, buscar o crear la actividad del día) pasa a `OpoStudy.Infrastructure` para que la usen la API y el Worker.
- Dominio: `XpRules.ForPracticalCase`, `DailyActivity.MeetGoal` y `Grading.RecordProgress`.
- Datos: columnas `xp_earned` y `met_daily_goal` en `gradings` (migración `AddGradingProgress`).
- API: `GradingResponse` añade `XpEarned` y `MetDailyGoal`.
- App: la pantalla `grading` muestra el resultado completo cuando está `Completed`.
- App: pantalla nueva **Mis correcciones** (`gradings`), con el historial paginado y acceso desde la lista de supuestos.
- App: acción «Repetir supuesto» desde el resultado.

**No incluye:**
- XP u objetivo por correcciones `Failed`.
- Notificaciones push al terminar una corrección.
- Estadísticas de supuestos (media, evolución): v0.4.0.
- Filtrar el historial por oposición o por estado.
- Cambiar la cuota, la cola o los correctores.

## Diseño
### Dominio
- **`XpRules`**:
  - Nueva constante `PracticalCaseMaxXp = 100`, que equivale a 10 aciertos.
  - Nuevo `ForPracticalCase(decimal score, int maxPoints)` = `(int)Math.Round(PracticalCaseMaxXp * score / maxPoints, MidpointRounding.AwayFromZero)`, acotado a `[0, 100]`.
  - `maxPoints <= 0` o `score < 0` → `ArgumentOutOfRangeException`.
  - Ejemplos: 18,5 / 25 → 74; 25 / 25 → 100; 0 → 0.
- **`DailyActivity.MeetGoal(DateTimeOffset now)`**:
  - Marca el día como cumplido (`GoalMetAt = now` en UTC) sin cambiar `AnsweredCount`.
  - Devuelve `true` solo si el día no estaba ya cumplido.
  - Un día protegido por un protector también se puede cumplir (igual que con respuestas tardías).
- **`Grading.RecordProgress(int xpEarned, bool metDailyGoal)`**:
  - Solo con `Status == Completed` y solo una vez; si no, `InvalidOperationException`.
  - Guarda `XpEarned` (`>= 0`; incluye el bonus del objetivo) y `MetDailyGoal`.
  - Si la corrección está `Completed`, `XpEarned` queda `null` hasta que se registra el progreso. `MetDailyGoal` vale `false` por defecto.
- **Reglas** (las aplica el Worker al completar):
  - **Día**: el del **envío** (`SubmittedAt`), en la zona horaria del usuario. Si la corrección termina después de medianoche, cuenta para el día en que se envió, con el mismo criterio que las respuestas sincronizadas tarde (spec 0014).
  - **Racha**: se liquida antes (`SettleStreak`) con ese día, como en `RecordAnswerAsync`.
  - **Objetivo**: cualquier corrección `Completed` cumple el objetivo de ese día, sea cual sea la nota.
    - Si lo cumple ahora: `Streak.RegisterGoalMet(day)` y bonus `XpRules.ForGoalMet(Math.Max(1, Streak.CurrentAt(day)))`.
    - Si el día ya estaba cumplido: no hay bonus.
    - Se acepta que enviar una respuesta mínima cumpla el objetivo, porque gasta una de las 10 correcciones del mes.
  - **XP** = `ForPracticalCase(Score, MaxPoints)` + bonus.
    - Si es > 0: `user.GainXp` y `activity.AddXp` (`AddXp` no admite 0: no se llama con 0).
    - Se guarda en `Grading.RecordProgress(xp, metNow)`.

### Infraestructura y Worker
- **`ProgressTracker`** (nuevo, `public sealed`, scoped, en `OpoStudy.Infrastructure/Progress/`):
  - No depende de `OpoStudy.Contracts`, porque Infrastructure no lo referencia.
  - Contiene, movidos tal cual desde `ProgressRecorder`:
    - `SettleStreakAsync(User, DateOnly, DateTimeOffset, CancellationToken)`.
    - `GetOrStartActivityAsync(User, DateOnly, CancellationToken)`: es `FindActivityAsync` más la creación con `DailyActivity.Start`, y mira también en `Local`.
  - Contiene también `RecordPracticalCaseAsync(User user, decimal score, int maxPoints, DateTimeOffset submittedAt, CancellationToken)` → `PracticalCaseProgress(int XpEarned, bool MetDailyGoal)`, que aplica las reglas de arriba.
  - Como `ProgressRecorder`, solo cambia entidades seguidas por el contexto: guarda quien lo llama.
- **`ProgressRecorder`** (API) delega en `ProgressTracker` lo que se ha movido y sigue haciendo el mapeo a Contracts. No cambia su comportamiento: los tests existentes siguen en verde sin tocarlos.
- **Registro en DI**: una extensión de Infrastructure que llamen la API y el Worker (p. ej. dentro de `AddGrading` y en `Program.cs` de la API, o un `AddProgressTracking()` común; lo que encaje con el registro actual del `DbContext`).
- **`GradingProcessor`**:
  - Tras `Complete` con éxito, carga el `User` (con seguimiento), llama a `RecordPracticalCaseAsync(user, grading.Score!.Value, grading.MaxPoints, grading.SubmittedAt, ct)` y a `grading.RecordProgress(...)`.
  - Lo guarda todo en el **mismo** `SaveChangesAsync` que el `Completed`.
  - Con `Failed` (incluido `invalid_result`) no se toca el progreso.
- **Concurrencia**:
  - `users`, `daily_activities` y `gradings` usan `xmin`. Una respuesta de test simultánea del mismo usuario provoca `DbUpdateConcurrencyException` al guardar.
  - Al guardar el resultado, se reintenta hasta **3 veces** sin volver a llamar al corrector:
    1. `ChangeTracker.Clear()`.
    2. Se recarga la corrección. Si ya no está `Processing` o su `StartedAt` cambió (otro Worker la reclamó tras caducar el lease), se abandona con log `Warning`.
    3. Se vuelve a aplicar el `GradingOutcome` guardado en memoria y el progreso.
  - Si se agotan los 3 intentos, se registra un `Error` y la corrección queda en `Processing`. El lease caducará y se reclamará, lo que costará otra llamada al LLM; se acepta por ser muy raro.
- **Usuario borrado** durante la corrección: la cascada ya borró la corrección. Al no encontrar al usuario o la corrección se registra un log y no se lanza nada.
- **Logs**: el log `Processed` añade `XpEarned` y `MetDailyGoal`. Nunca se registran respuestas ni comentarios.

### API
Sin endpoints nuevos. Se amplía un DTO añadiendo los campos **al final** del record:
```csharp
public sealed record GradingResponse(
    Guid Id, Guid PracticalCaseId, string CaseTitle, string Status, int Attempts,
    DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt, int MaxPoints, decimal? Score,
    string? Feedback, string? FailureCode, IReadOnlyList<GradingAnswerResponse> Answers,
    int? XpEarned, bool MetDailyGoal);
```
- `XpEarned` es `null` salvo con `Completed`.
- `GET /gradings` (`GradingSummaryResponse`) no cambia: el historial usa `Status`, `Score`, `MaxPoints`, `Section`, `CaseTitle` y `SubmittedAt`.
- Paginación del historial: la de la 0018 (`skip`/`take`, mismas reglas que `GET /quizzes`). La app pide `take=20`.

### Datos
Migración **`AddGradingProgress`** (skill `new-ef-migration`):
- `gradings.xp_earned` int null.
- `gradings.met_daily_goal` boolean not null default `false`.

Las correcciones `Completed` anteriores quedan con `xp_earned = null`: no se les da XP con efecto retroactivo y la app no muestra la línea de XP.

### App
Skill `maui-ui`. ViewModels en `Mobile.Core/ViewModels/` y vistas en `Mobile/Views/`, `Transient`. Ruta nueva: `Gradings = "gradings"`. Textos en `PracticalCaseTexts`; números y fechas en `es-ES`, con decimales como `18,5`.

#### 1. Resultado (`GradingViewModel` y `GradingPage`, ampliadas)
Con `Completed` se sustituye «El detalle por apartado llegará en una próxima versión.» (se borra `CompletedDetailLater`) por el resultado completo.

**Cabecera:**
- `CaseTitle` y «Enviado {d MMM HH:mm}».
- Nota grande: «{Score} / {MaxPoints}».
- Si `XpEarned` no es nulo: «+{XpEarned} XP».
- Si `MetDailyGoal`: «¡Objetivo del día cumplido!». Se indica con texto e icono, no solo con color.
- `Feedback` general, si lo hay.

**Un `GradingPartItem` por apartado**, en orden de `Order`:
- Título: «Apartado {Order + 1} · {Score} / {MaxPoints} puntos».
- `Feedback` del apartado, siempre visible.
- **Criterios**: una fila por `CriterionScoreResponse` con la descripción, «{Awarded} / {Points}» y un marcador con icono + texto:
  - «Completo» si `Awarded == Points`.
  - «Sin puntos» si `Awarded == 0`.
  - «Parcial» en el resto de casos.
- Tres bloques plegables (`[ObservableProperty] bool`, conmutados con un comando del item), plegados por defecto:
  - «Enunciado» (`Statement`).
  - «Tu respuesta» (`Text`; si está vacía, «(En blanco)»).
  - «Respuesta modelo» (`ModelAnswer`).
- `Criteria` o `ModelAnswer` nulos (no debería pasar con `Completed`): se oculta ese bloque, sin error.

**Vista:**
- `ScrollView` + `BindableLayout`, con la misma excepción justificada de la 0020: como mucho 8 apartados con texto largo.
- Todo el texto que viene del LLM o del usuario se muestra como **texto plano** (`Label`, sin `TextType="Html"`).

**Alerta al terminar** (solo en la transición, como en la 0020): «Has sacado {Score} de {MaxPoints} puntos.» y, si hay XP, « +{XpEarned} XP.».

**Acciones con `Completed`:**
- «Volver» (`Routes.Back`).
- «Repetir supuesto»: navega a `Routes.PracticalCase` con `PracticalCaseId`, apilando la página. Si hay borrador, se recupera como siempre.

Con `Failed`, en curso y polling: sin cambios.

#### 2. Mis correcciones (`GradingsViewModel` : `OfflineAwareViewModel`, `GradingsPage`)
- **Acceso**: enlace «Mis correcciones» (zona táctil ≥ 48 dp) en la cabecera de `PracticalCasesPage`, junto a la cuota. `PracticalCasesViewModel.OpenGradingsCommand` → `Routes.Gradings`.
- **Carga**:
  - En `OnAppearingAsync`: `GetGradingsAsync(0, 20)`. Volver a la pantalla recarga la primera página, para que se vea el estado actualizado de una corrección en curso.
  - `RefreshCommand` hace lo mismo.
  - «Cargar más» (`LoadMoreCommand`) pide `skip = Items.Count` y aparece mientras la última página traiga 20 elementos.
  - Se usa `CollectionView`.
- **Elemento** (`GradingHistoryItem`):
  - `CaseTitle`.
  - `Section`, si lo hay.
  - «{SubmittedAt d MMM yyyy, HH:mm}» en hora local.
  - Estado:
    - `Completed` → «{Score} / {MaxPoints} puntos».
    - `Pending`/`Processing` → «En curso».
    - `Failed` → «No corregida».
  - El estado se indica con texto (e icono), no solo con color.
  - Tocar un elemento → `Routes.Grading` con `GradingId`. Esa pantalla ya resuelve cada estado: resultado, polling o «Recuperar mis respuestas».
- **Estados**:
  - Carga.
  - Vacío: «Aún no has enviado ningún supuesto».
  - Error con «Reintentar».
  - Sin conexión: «Los supuestos prácticos necesitan conexión» con «Reintentar», mismo patrón que `PracticalCasesViewModel`. No se llama a la API si `OfflineState.IsOffline`.
- Un fallo en «Cargar más» muestra el error, pero conserva lo ya cargado.

#### 3. Progreso en Inicio
Sin cambios de código: Inicio ya recarga objetivo, racha y XP al aparecer. Se comprueba a mano que, tras una corrección, Inicio refleja el objetivo cumplido y la XP.

## Seguridad
- La autorización por recurso no cambia: `GET /gradings/{id}` solo para el dueño y el historial filtrado por usuario.
- La XP la calcula solo el servidor a partir de la nota del corrector; la app no envía nada nuevo.
- `Feedback`, `ModelAnswer`, criterios y respuestas se pintan como texto plano: el texto generado por el LLM nunca se interpreta como HTML ni como markup.
- Logs sin respuestas, comentarios ni texto del LLM: solo ids, estado, XP y tipo de excepción.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [ ] Al completar una corrección, el usuario gana `round(100 · Score / MaxPoints)` XP; con `Failed` no gana nada.
- [ ] Una corrección completada cumple el objetivo del día del envío (en la zona del usuario), suma el bonus y extiende la racha. Si el día ya estaba cumplido, no hay bonus.
- [ ] Una corrección enviada antes de medianoche y completada después cuenta para el día del envío.
- [ ] `GET /gradings/{id}` devuelve `XpEarned` (con el bonus incluido) y `MetDailyGoal`; las correcciones antiguas devuelven `XpEarned = null`.
- [ ] Un conflicto de concurrencia con una respuesta de test simultánea no pierde XP ni repite la llamada al corrector.
- [ ] Los tests existentes de progreso (`ProgressRecorder`, quizzes, racha) siguen en verde sin cambios.
- [ ] La pantalla de resultado muestra la nota total, el comentario general, la XP, el aviso de objetivo, y por apartado la nota, los comentarios y los criterios (con marcador de texto), con enunciado, respuesta y respuesta modelo plegables.
- [ ] «Repetir supuesto» abre el editor del mismo supuesto.
- [ ] «Mis correcciones», desde la lista de supuestos, muestra el historial (de la más reciente a la más antigua) con estado y nota, carga más de 20 y abre cada corrección en su pantalla.
- [ ] Sin conexión, el historial muestra «necesitan conexión» con «Reintentar».
- [ ] Revisado en el emulador o en el móvil en tema claro y oscuro con escala de fuente 1,3, sin colores fijos ni textos cortados.
- [ ] `dotnet build OpoStudy.Backend.slnf` y `dotnet build src/OpoStudy.Mobile -f net10.0-android` sin warnings.

## Plan de tests
- **Unitarios de dominio:**
  - `XpRules.ForPracticalCase`: 18,5/25 → 74, 25/25 → 100, 0 → 0, redondeo en .5, argumentos inválidos.
  - `DailyActivity.MeetGoal`: devuelve `true` una vez, después `false`, y no cambia `AnsweredCount`; funciona con un día protegido.
  - `Grading.RecordProgress`: falla si no está `Completed` o si se llama dos veces.
- **Integración** (Testcontainers; `GradingProcessor` con el corrector falso):
  - Completa → la XP del usuario y `daily_activities.xp_earned` suben lo esperado; `gradings.xp_earned` y `met_daily_goal` quedan guardados.
  - Primer supuesto del día → objetivo cumplido, bonus y racha +1. Día ya cumplido por respuestas → sin bonus y `MetDailyGoal = false`.
  - Enviado a las 23:59 y completado a las 00:01 (con `FakeTimeProvider` y zona del usuario) → cuenta para el día del envío.
  - Fallo (`invalid_result` o `provider_error`) → ni XP ni actividad.
  - Concurrencia: un interceptor (o una segunda conexión) modifica el `User` antes del primer guardado → se reintenta, la XP final es la suma de ambas operaciones y el corrector se llama una sola vez.
  - `GET /gradings/{id}` incluye `XpEarned` y `MetDailyGoal`.
  - Suite existente de progreso en verde.
- **Unitarios de la app** (`Mobile.Core.Tests`, NSubstitute, Shouldly, `FakeTimeProvider`):
  - `ApiJsonContext`: ida y vuelta de `GradingResponse` con los campos nuevos.
  - `GradingViewModel`:
    - `Completed` construye los items en orden, con textos de nota, marcadores de criterio y bloques plegados por defecto; conmuta cada bloque.
    - Se oculta la XP cuando es nula y el aviso de objetivo cuando es `false`.
    - La alerta incluye la XP.
    - «Repetir supuesto» navega con `PracticalCaseId`.
  - `GradingsViewModel`:
    - Primera página y orden.
    - Textos por estado.
    - «Cargar más» visible solo con 20, pide el `skip` correcto y conserva lo cargado si falla.
    - Vacío.
    - Offline sin llamar a la API; error de conectividad → «necesitan conexión».
    - Navegación con `GradingId`.
  - `PracticalCasesViewModel`: `OpenGradingsCommand`.
  - `PracticalCaseTexts`: formatos nuevos (`18,5 / 25`, «+74 XP», fecha del historial).
- **Manual** (AppHost con `Grading:Provider = Fake`, emulador o móvil con `adb reverse`):
  1. Enviar un supuesto y esperar a que termine: la alerta muestra la nota y la XP, y el resultado aparece con los apartados y los criterios.
  2. Abrir y plegar enunciado, respuesta y respuesta modelo.
  3. Inicio: objetivo cumplido y XP actualizada.
  4. Mis correcciones: aparece la nueva corrección; abrirla y luego volver. Con más de 20 correcciones (sembradas por SQL), «Cargar más».
  5. «Repetir supuesto» abre el editor.
  6. Modo avión en el historial.
  7. Capturas en tema claro y oscuro con `font_scale 1.3` del resultado y del historial.
