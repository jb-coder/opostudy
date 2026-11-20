# 0010 — Objetivo diario y rachas

- **Issue:** #37
- **Milestone:** v0.2.0
- **Estado:** borrador

## Objetivo
Crear el hábito de estudiar cada día. El usuario tiene un objetivo de preguntas respondidas al día y una racha de días seguidos cumpliéndolo, que ve en la pantalla de inicio y al terminar el objetivo durante un test. Es la base de la v0.2: el XP (0011) y las notificaciones locales (0013) se apoyan en ella.

## Decisiones
- **La unidad es la pregunta respondida, no el test terminado.** Los tests tienen longitudes distintas (10, 20 o 30) y pueden quedar a medias; contar preguntas da progreso visible durante el día («7/10») y no penaliza dejar un test sin acabar. Cuentan todas las respuestas, aciertos y fallos: se premia practicar.
- **Objetivo configurable con valores fijos** en la app: 10, 20, 30 o 50; **10 por defecto**, que es un test estándar al día. El dominio admite de 5 a 100.
- **El día se cuenta en la zona horaria del usuario**, guardada en su perfil (IANA, por defecto `Europe/Madrid`; Canarias va una hora por detrás). La app la envía si cambia. No se acepta por cabecera en cada petición, para que el día de una respuesta no dependa de quién llama.
- **El día de una respuesta se fija al registrarla** y no se recalcula: si el usuario cambia de zona horaria, el historial no cambia.
- **Actividad diaria materializada** (una fila por usuario y día con actividad) en vez de calcularla a partir de `quiz_answers`. Así un cambio de objetivo no reescribe días pasados, el cálculo es O(1) al responder y queda el historial para el calendario y las estadísticas (v0.4).
- **La racha vive en el usuario** (actual, máxima y último día cumplido) y se actualiza solo cuando un día pasa a cumplido. La racha actual se calcula al leer: si el último día cumplido es anterior a ayer, vale 0.
- **Cambiar el objetivo se aplica desde hoy.** Un día ya cumplido sigue cumplido. Si hoy no estaba cumplido y el nuevo objetivo ya se alcanza, hoy pasa a cumplido.
- **Sin backfill.** No hay usuarios reales todavía: las rachas empiezan a contar con este cambio.

## Alcance
**Incluye:**
- Dominio: objetivo diario y zona horaria en `User`, tipo complejo `Streak` y entidad `DailyActivity`.
- Registro de la actividad al responder una pregunta (`SubmitAnswer`), en la misma transacción.
- Endpoints para leer la racha y cambiar objetivo y zona horaria. Ampliación del perfil y de la respuesta de `SubmitAnswer`.
- App: tarjeta «Objetivo diario» en la pantalla de inicio, cambio de objetivo, aviso al cumplirlo durante un test y sincronización de la zona horaria.

**No incluye:**
- XP y niveles (0011), notificaciones (0013), protectores de racha (posible en 0011, pagados con XP) ni calendario mensual o estadísticas (v0.4).
- Respuestas offline (0014). Limitación conocida: el día se calcula con la hora del servidor al recibir la respuesta. La 0014 tendrá que enviar la hora de la respuesta y decidir cómo actualizar rachas con días fuera de orden.

## Diseño
### Dominio
Glosario (ADR 0001): `DailyGoal` (objetivo diario), `DailyActivity` (actividad diaria), `Streak` (racha).

- `Limits`: `MinDailyGoal = 5`, `MaxDailyGoal = 100`, `DefaultDailyGoal = 10`, `TimeZone = 64`, `DefaultTimeZone = "Europe/Madrid"`, `StreakHistoryDays = 7`.
- `User`:
  - `int DailyGoal` (por defecto 10) y `string TimeZone` (por defecto `Europe/Madrid`).
  - `Streak Streak` (tipo complejo, nunca nulo).
  - `SetDailyGoal(int)`: fuera de rango lanza `DomainRuleException("user.invalid_daily_goal")`.
  - `SetTimeZone(string)`: lanza `user.invalid_time_zone` si `TimeZoneInfo.TryFindSystemTimeZoneById` no la encuentra o supera 64 caracteres.
  - `DateOnly TodayAt(DateTimeOffset now)`: fecha local en su zona horaria.
- `Streak` (tipo complejo):
  - `int Longest`, `DateOnly? LastMetOn`, `int LastLength` (longitud de la racha que termina en `LastMetOn`).
  - `RegisterGoalMet(DateOnly day)`: si `day == LastMetOn`, no hace nada; si `day == LastMetOn + 1`, `LastLength++`; si no, `LastLength = 1`. Después `LastMetOn = day` y `Longest = max(Longest, LastLength)`. Un `day` anterior a `LastMetOn` se ignora.
  - `int CurrentAt(DateOnly today)`: `LastLength` si `LastMetOn` es hoy o ayer; si no, 0.
- `DailyActivity`:
  - `Guid UserId`, `DateOnly Date`, `int AnsweredCount`, `int Goal` (el objetivo vigente ese día), `DateTimeOffset? GoalMetAt`, `bool IsGoalMet`.
  - `Start(userId, date, goal)`.
  - `bool RegisterAnswer(DateTimeOffset now)`: incrementa el contador y devuelve `true` solo en la respuesta que cumple el objetivo (fija `GoalMetAt`).
  - `bool ChangeGoal(int goal, DateTimeOffset now)`: si ya estaba cumplido, no hace nada y devuelve `false`; si no, actualiza `Goal` y devuelve `true` si con el nuevo objetivo queda cumplido.
- Toda la lógica de «registrar progreso» va en un único punto reutilizable, que la 0011 ampliará con el XP. Por ejemplo, el servicio `ProgressRecorder` en `Api/Features/Progress`, con `RecordAnswerAsync(user, now)`, que carga o crea la `DailyActivity` de hoy, llama a `RegisterAnswer` y, si se cumple, a `user.Streak.RegisterGoalMet(today)`.

### API
Rutas bajo `/api/v1`. Todas exigen usuario autenticado y operan solo sobre el usuario del token.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/me/streak` | Usuario | — → `200 StreakResponse` |
| PUT | `/me/daily-goal` | Usuario | `UpdateDailyGoalRequest(int DailyGoal)` → `200 StreakResponse` · `400` si está fuera de rango |
| PUT | `/me/time-zone` | Usuario | `UpdateTimeZoneRequest(string TimeZone)` → `204` · `400` si no es válida |
| GET | `/me` | Usuario | `UserProfileResponse` + `DailyGoal`, `TimeZone` |
| POST | `/quizzes/{id}/answers` | Usuario | `AnswerFeedbackResponse` + `DailyProgress` |

Contratos nuevos en `Contracts/Progress` (añadirlos a `ApiJsonContext` de Mobile.Core):
- `StreakResponse(int DailyGoal, DateOnly Today, int AnsweredToday, bool GoalMetToday, int CurrentStreak, int LongestStreak, IReadOnlyList<DayActivityResponse> LastDays)`.
  - `LastDays`: los 7 días que terminan hoy, en orden ascendente, incluidos los días sin actividad (`AnsweredCount = 0`, `GoalMet = false`).
- `DayActivityResponse(DateOnly Date, int AnsweredCount, int Goal, bool GoalMet)`: en los días sin fila, `Goal` es el objetivo actual.
- `DailyProgressResponse(int AnsweredToday, int DailyGoal, bool GoalMetNow, int CurrentStreak)`. `GoalMetNow` es `true` solo en la respuesta que cumple el objetivo.
- `UpdateDailyGoalRequest`, `UpdateTimeZoneRequest`.

Errores: la validación de entrada devuelve `ValidationProblem` (400), como `CreateQuiz`. Una carrera al actualizar la `DailyActivity` del día devuelve 409, igual que el conflicto de concurrencia actual.

### Datos
Migración con la skill `new-ef-migration`:
- `users`: `daily_goal int not null default 10`, `time_zone varchar(64) not null default 'Europe/Madrid'`, `streak_longest int not null default 0`, `streak_last_length int not null default 0`, `streak_last_met_on date null` (tipo complejo `Streak` con `ComplexProperty`).
- Tabla nueva `daily_activities`:
  - Columnas: `user_id uuid` (FK a `users`, `ON DELETE CASCADE`, así que borrar la cuenta la borra), `date date`, `answered_count int`, `goal int`, `goal_met_at timestamptz null`.
  - PK `(user_id, date)`. Concurrencia optimista con `xmin`, igual que `quiz_attempts`.

### App
Usa la skill `maui-ui` (tema claro y oscuro, capturas en el emulador).
- **Pantalla de inicio**, tarjeta «Objetivo diario», la primera tras el saludo:
  - Progreso de hoy (`ProgressBar` y «7 de 10 preguntas»; «¡Objetivo de hoy cumplido!» si está cumplido).
  - Racha actual («🔥 5 días», o «Sin racha» si es 0) y máxima («Mejor racha: 12»).
  - Los 7 últimos días como puntos con la inicial del día (L M X J V S D): relleno si cumplido, contorno si no.
  - Si la racha está viva y hoy aún no se ha cumplido: «Responde 3 preguntas más para mantener tu racha».
  - Botón «Cambiar objetivo»: hoja de acciones (`IDialogService`) con 10, 20, 30 y 50, que llama a `PUT /me/daily-goal` y refresca la tarjeta con la respuesta.
- **Zona horaria**: al refrescar la pantalla de inicio, si `GetMe().TimeZone` difiere de `TimeProvider.LocalTimeZone.Id`, la app llama a `PUT /me/time-zone` y luego carga la racha. Si falla, solo se registra en el log; no bloquea la pantalla.
- **Pantalla de test**: si `DailyProgress.GoalMetNow` es `true`, se muestra un aviso no bloqueante: «🎯 ¡Objetivo diario cumplido! Racha: N días».
- `HomeViewModel` gana `StreakResponse` y `ChangeDailyGoalCommand`; `QuizViewModel` gana el mensaje del aviso. La lógica de textos va en Mobile.Core para poder testearla.

## Seguridad
- El usuario sale siempre del claim `sub` (`CurrentUserService`); ninguna ruta acepta un id de usuario.
- La entrada se valida en el endpoint (rango del objetivo; zona horaria existente y de 64 caracteres como máximo) y el dominio lo vuelve a comprobar.
- La zona horaria es un dato poco sensible, pero no se escribe en los logs junto a identificadores.
- No hacen falta secretos nuevos.
- Riesgo de despliegue: la imagen de contenedor necesita `tzdata` para resolver zonas IANA. Hay que comprobarlo en la #21 (v0.5) y anotarlo allí.

## Criterios de aceptación
- [ ] Un usuario nuevo tiene objetivo 10, zona `Europe/Madrid` y racha 0.
- [ ] Cada respuesta incrementa `AnsweredToday`. La respuesta que alcanza el objetivo devuelve `GoalMetNow = true` y suma 1 a la racha. Las siguientes de ese día devuelven `false` y no la vuelven a sumar.
- [ ] Cumplir días consecutivos aumenta la racha. Saltarse un día la deja a 0 en la lectura y la siguiente vez que se cumple empieza en 1. `LongestStreak` conserva el máximo.
- [ ] Si ayer se cumplió y hoy todavía no, `CurrentStreak` mantiene el valor de ayer y `GoalMetToday` es `false`.
- [ ] El día se calcula en la zona del usuario: una respuesta a las 23:30 en Madrid (22:30 UTC en invierno) cuenta para ese día, y una a las 00:30 cuenta para el siguiente.
- [ ] Cambiar el objetivo no altera los días pasados. Si hoy no estaba cumplido y el nuevo objetivo ya se alcanza, hoy pasa a cumplido y suma racha. Un día cumplido no deja de estarlo.
- [ ] `PUT /me/daily-goal` con 4 o 101 devuelve 400. `PUT /me/time-zone` con `Mars/Olympus` devuelve 400.
- [ ] `LastDays` devuelve siempre 7 días en orden ascendente terminando hoy, incluidos los días sin actividad.
- [ ] Borrar la cuenta borra sus `daily_activities`.
- [ ] La app muestra la tarjeta en tema claro y oscuro, cambia el objetivo y muestra el aviso al cumplirlo durante un test (verificado en el emulador).

## Plan de tests
- **Unitarios (Domain):**
  - `Streak`: consecutivo, mismo día, hueco, día anterior ignorado, `Longest` y `CurrentAt` con hoy, ayer y anteayer.
  - `DailyActivity`: transición única a cumplido y `ChangeGoal` (subir, bajar hasta cumplir, ya cumplido).
  - `User`: límites de `SetDailyGoal`, `SetTimeZone` válida e inválida, y `TodayAt` alrededor de medianoche y con cambio de horario de verano.
- **Integración (API, Testcontainers):**
  - Flujo completo con `FakeTimeProvider`: responder hasta cumplir, avanzar un día, cumplir, saltar un día.
  - `GoalMetNow`, `GET /me/streak` con `LastDays` y validaciones 400.
  - Aislamiento entre usuarios, borrado en cascada y el 409 por concurrencia en la actividad del día, si se puede reproducir como el de `quiz_attempts`.
  - `Model_HasNoPendingChangesToMigrate` sigue en verde.
- **Unitarios (Mobile.Core):**
  - Textos de la tarjeta (con y sin racha, cumplido, «faltan N»), puntos de la semana con las iniciales en español.
  - Sincronización de la zona horaria (solo llama si difiere), cambio de objetivo y aviso en `QuizViewModel`.
- **Manual (emulador):** tarjeta en tema claro y oscuro, cambio de objetivo, test de 10 preguntas que cumple el objetivo con su aviso y la racha a 1 al volver a la pantalla de inicio.
