# 0011 — XP y niveles

- **Issue:** #38
- **Milestone:** v0.2.0
- **Estado:** implementada

## Objetivo
Dar una recompensa inmediata a cada respuesta y una medida del avance a largo plazo. El usuario gana puntos de experiencia (XP) al responder, con un bonus al cumplir el objetivo diario que crece con la racha, y sube de nivel según el XP acumulado. Lo ve en la pantalla de inicio, en cada respuesta y al terminar un test. Se apoya en el punto único de registro de progreso de la 0010 (`ProgressRecorder`).

## Decisiones
- **XP por respuesta: 10 por acierto y 2 por fallo.** Fallar también suma para premiar la práctica, pero acertar vale cinco veces más.
- **Bonus al cumplir el objetivo diario: 50 XP + 5 XP por día de racha, con tope de +50** (racha de 10 días o más). La racha que cuenta es la que queda tras cumplir hoy: con racha 1 el bonus es 55; con 10 o más, 100. Se concede una sola vez por día, en el mismo momento en que el día pasa a cumplido (al responder o al bajar el objetivo, igual que la racha de la 0010).
- **Curva de niveles creciente: pasar del nivel N al N+1 cuesta 100·N XP.** El XP total para llegar al nivel L es `50·L·(L−1)`: nivel 2 con 100, nivel 3 con 300, nivel 5 con 1.000, nivel 10 con 4.500 y nivel 20 con 19.000. Se empieza en el nivel 1, sin tope ni nombres de nivel. Con objetivo 10 y un 70 % de aciertos se ganan unos 130-180 XP al día: nivel 10 en un mes y nivel 20 en unos cuatro.
- **El nivel no se guarda: se calcula a partir del XP total.** Así cambiar la curva no exige migrar datos.
- **XP total en el usuario y XP del día en `DailyActivity`.** Sin tabla de movimientos: el desglose por día basta para las estadísticas de la v0.4.
- **El XP nunca baja.** No hay penalizaciones ni gasto de XP en esta spec.
- **Sin backfill.** Las respuestas anteriores a este cambio no dan XP.

## Alcance
**Incluye:**
- Dominio: reglas de XP y niveles (`XpRules`), XP total en `User` y XP del día en `DailyActivity`.
- Concesión de XP en `ProgressRecorder` al responder y al cumplir el objetivo, en la misma transacción.
- Endpoint para leer el nivel y ampliación de la respuesta de `SubmitAnswer`.
- App: tarjeta «Experiencia» en la pantalla de inicio, XP ganado en cada respuesta, aviso de subida de nivel y XP total del test en el resumen.

**No incluye:**
- Protectores de racha pagados con XP (#43), nombres o insignias por nivel, logros ni clasificaciones.
- XP por supuestos prácticos (v0.3) ni por repasar fallos (0012): cuando lleguen, se registrarán con `ProgressRecorder`.
- Respuestas offline (0014): el XP se concede al llegar la respuesta al servidor, con las mismas reglas.

## Diseño
### Dominio
Glosario (ADR 0001): `Xp` (puntos de experiencia) y `Level` (nivel). Ya añadidos.

- `XpRules` (clase estática en `Domain/Users`):
  - Constantes: `CorrectAnswer = 10`, `IncorrectAnswer = 2`, `GoalMetBonus = 50`, `StreakBonusPerDay = 5`, `MaxStreakBonus = 50`, `LevelStep = 100`.
  - `int ForAnswer(bool isCorrect)`.
  - `int ForGoalMet(int currentStreak)`: `GoalMetBonus + min(StreakBonusPerDay · currentStreak, MaxStreakBonus)`. Con `currentStreak < 1` lanza `ArgumentOutOfRangeException`.
  - `int XpToReach(int level)`: `50 · level · (level − 1)` (`LevelStep / 2`). Con `level < 1` lanza `ArgumentOutOfRangeException`.
  - `int LevelFor(int totalXp)`: el mayor `L` con `XpToReach(L) <= totalXp`. Aritmética entera (bucle o raíz corregida), sin errores de coma flotante en los bordes. Con XP negativo lanza `ArgumentOutOfRangeException`.
- `User`:
  - `int Xp` (por defecto 0).
  - `int Level => XpRules.LevelFor(Xp)`.
  - `void GainXp(int amount)`: `amount <= 0` lanza `ArgumentOutOfRangeException`. Usa `checked` para no desbordar en silencio.
- `DailyActivity`:
  - `int XpEarned` (XP ganado ese día, bonus incluido).
  - `void AddXp(int amount)`: misma validación que `GainXp`.
- `ProgressRecorder` sigue siendo el único punto que registra progreso y el único que concede XP:
  - `RecordAnswerAsync(User user, bool isCorrect, DateTimeOffset now, CancellationToken)`. Tras `RegisterAnswer` y la racha, calcula `gained = ForAnswer(isCorrect)` más `ForGoalMet(user.Streak.CurrentAt(today))` si el día pasa a cumplido. Llama a `user.GainXp(gained)` y `activity.AddXp(gained)`, y compara el nivel antes y después.
  - Devuelve el `DailyProgressResponse` de antes y el nuevo `XpGainResponse`, por ejemplo con un `record` interno `AnswerProgress(DailyProgressResponse Daily, XpGainResponse Xp)`.
  - `ChangeDailyGoalAsync`: si el cambio cumple el día, concede el bonus a `user` y a `activity` igual que al responder.
  - Invariante: todo cambio de XP modifica también la `DailyActivity` de hoy, que tiene concurrencia optimista con `xmin`. Así dos respuestas simultáneas del mismo usuario no pierden XP: una de ellas recibe 409, como en la 0010. No hace falta token de concurrencia en `users`.
- `SubmitAnswer` pasa `result.IsCorrect` a `RecordAnswerAsync`.

### API
Rutas bajo `/api/v1`. Todas exigen usuario autenticado y operan solo sobre el usuario del token.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/me/level` | Usuario | — → `200 LevelResponse` |
| POST | `/quizzes/{id}/answers` | Usuario | `AnswerFeedbackResponse` + `Xp` (`XpGainResponse`) |

Contratos nuevos en `Contracts/Progress` (añadirlos a `ApiJsonContext` de Mobile.Core y `GetLevel` al cliente Refit):
- `LevelResponse(int Level, int TotalXp, int XpIntoLevel, int XpForNextLevel, int XpToday)`:
  - `XpIntoLevel = TotalXp − XpToReach(Level)`.
  - `XpForNextLevel = XpToReach(Level + 1) − XpToReach(Level)`, es decir, `100·Level`.
  - `XpToday` sale de la `DailyActivity` de hoy (0 si no hay fila).
- `XpGainResponse(int Gained, int Bonus, int TotalXp, int Level, bool LeveledUp)`:
  - `Gained` incluye `Bonus`. `Bonus` vale 0 salvo en la respuesta que cumple el objetivo.
  - `LeveledUp` es `true` solo en la respuesta que cambia de nivel.
- `AnswerFeedbackResponse` gana un último parámetro, `XpGainResponse Xp`.

`PUT /me/daily-goal` no cambia de contrato: si el cambio concede el bonus, la app recarga `GET /me/level`.

### Datos
Migración `AddXp` con la skill `new-ef-migration`:
- `users`: `xp int not null default 0`.
- `daily_activities`: `xp_earned int not null default 0`.

### App
Usa la skill `maui-ui` (tema claro y oscuro, capturas en el emulador).
- **Pantalla de inicio**, tarjeta «Experiencia», justo debajo de «Objetivo diario»:
  - Título «Experiencia», «Nivel 4» destacado y una `ProgressBar` con `XpIntoLevel / XpForNextLevel`.
  - «230 / 400 XP para el nivel 5» y «Hoy: +120 XP» (o «Hoy aún no has ganado XP» si `XpToday` es 0).
  - Se carga con `GET /me/level` en el mismo refresco que la racha. Si falla, la tarjeta muestra el error sin bloquear la de «Objetivo diario».
  - Tras «Cambiar objetivo», se recarga también el nivel.
- **Pantalla de test**:
  - En el panel de corrección de cada respuesta, una etiqueta «+10 XP» o «+2 XP» (`Xp.Gained − Xp.Bonus`).
  - El aviso de objetivo cumplido de la 0010 añade el bonus: «🎯 ¡Objetivo diario cumplido! Racha: N días · +55 XP».
  - Si `Xp.LeveledUp`, aviso no bloqueante «⭐ ¡Has subido al nivel N!». Si coincide con el de objetivo, se muestra un único aviso con las dos líneas.
  - El resumen final muestra «+N XP en este test»: la suma de `Xp.Gained` de las respuestas dadas en esa pantalla (si se retoma un test a medias, solo cuenta lo ganado desde que se retomó).
- `HomeViewModel` gana `LevelCard` (con la lógica de textos, como `DailyGoalCard`); `QuizViewModel` gana el XP de la última respuesta, el acumulado del test y el texto del aviso. Los textos van en Mobile.Core para poder testearlos.

## Seguridad
- El usuario sale siempre del claim `sub` (`CurrentUserService`); `GET /me/level` no acepta ids.
- El cliente no envía XP ni nivel: todo se calcula en el servidor a partir de respuestas ya validadas por `QuizAttempt.Answer`, que no permite responder dos veces la misma pregunta. Por eso el XP no se puede inflar repitiendo peticiones.
- Límite conocido: crear tests nuevos permite seguir ganando XP sin tope diario. Es aceptable en una app personal y se revisará si se añaden clasificaciones.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [x] Un usuario nuevo tiene 0 XP, nivel 1, `XpIntoLevel = 0` y `XpForNextLevel = 100`.
- [x] Un acierto suma 10 XP y un fallo, 2, al total y al día. `XpGainResponse.Gained` lo refleja, con `Bonus = 0`.
- [x] La respuesta que cumple el objetivo diario suma, además, `50 + min(5·racha, 50)`: 55 con racha 1, 100 con racha 10 o más. Las siguientes respuestas del día no repiten el bonus.
- [x] Bajar el objetivo hasta cumplir el día concede el bonus una sola vez. Volver a cambiarlo no lo repite.
- [x] `LevelFor` devuelve 1 con 0 y 99 XP, 2 con 100, 2 con 299, 3 con 300, 10 con 4.500 y 9 con 4.499.
- [x] La respuesta que cruza un umbral de nivel devuelve `LeveledUp = true` y el nivel nuevo; la siguiente devuelve `false`.
- [x] `GET /me/level` devuelve el XP de hoy en la zona horaria del usuario: tras medianoche, `XpToday` vuelve a 0 y `TotalXp` se mantiene.
- [ ] Dos respuestas simultáneas del mismo usuario no pierden XP: una se guarda y la otra recibe 409 sin cambiar nada. Sin test automático: lo garantiza el `xmin` de la `DailyActivity` del día, que se modifica junto con el XP.
- [x] Un usuario no ve ni modifica el XP de otro.
- [x] La app muestra la tarjeta «Experiencia» en tema claro y oscuro, el XP de cada respuesta, el aviso de subida de nivel y el total del test en el resumen (verificado en el emulador).

## Plan de tests
- **Unitarios (Domain):**
  - `XpRules`: `ForAnswer`, `ForGoalMet` con racha 1, 9, 10 y 30, y con 0 (excepción); `XpToReach` y `LevelFor` en los bordes de los criterios y con XP alto (nivel 100), comprobando que `LevelFor(XpToReach(L)) == L` y `LevelFor(XpToReach(L) − 1) == L − 1` para L de 2 a 200.
  - `User.GainXp` y `DailyActivity.AddXp`: suman y rechazan 0 o negativos.
- **Integración (API, Testcontainers, `FakeTimeProvider`):**
  - Responder un test de 10 con aciertos y fallos conocidos y comprobar XP total, XP del día, bonus en la décima respuesta y `GET /me/level`.
  - Bonus con racha de 2 días (avanzando el reloj) y concesión al bajar el objetivo.
  - Subida de nivel (sembrando XP al usuario en la BD), cambio de día con `XpToday` a 0, aislamiento entre usuarios y `Model_HasNoPendingChangesToMigrate` en verde.
  - Si se puede reproducir como en la 0010, el 409 por concurrencia sin pérdida de XP.
- **Unitarios (Mobile.Core):**
  - Textos de `LevelCard` (con y sin XP hoy, singulares si aplica).
  - Etiqueta de XP por respuesta, aviso combinado de objetivo y nivel, acumulado del resumen y recarga del nivel tras cambiar el objetivo.
- **Manual (emulador):** tarjeta en tema claro y oscuro, test de 10 preguntas con «+XP» en cada respuesta, aviso de objetivo con bonus, subida al nivel 2 en el primer día y total en el resumen.
