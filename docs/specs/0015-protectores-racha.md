# 0015 — Protectores de racha pagados con XP

- **Issue:** #43
- **Milestone:** v0.2.0
- **Estado:** implementada

## Objetivo
Que un imprevisto no haga perder semanas de racha. El usuario compra protectores de racha con XP y, si un día no cumple el objetivo, se gasta uno automáticamente y la racha sigue viva. Es el primer uso del XP como moneda. Se apoya en la racha de la 0010 y en el XP de la 0011.

## Decisiones
- **Precio: 200 XP. Máximo: 2 protectores guardados.** Con objetivo 10 se ganan unos 130-180 XP al día, así que un protector cuesta algo más de un día de estudio y cubre como mucho un fin de semana.
- **El XP ganado nunca baja; lo que baja es el XP disponible.** `User.Xp` sigue siendo el XP total ganado, que define el nivel (la regla de la 0011 se mantiene: el nivel nunca baja). Se añade `XpSpent`, y `SpendableXp = Xp − XpSpent` es lo que se puede gastar.
- **Un protector cubre un día entero sin cumplir el objetivo.** El día protegido mantiene la racha, pero no la alarga: con racha 5, si el martes se protege y el miércoles se cumple, la racha pasa a 6. `Longest` no cambia por un día protegido. No da XP ni bonus.
- **Consumo perezoso al «liquidar» la racha**, sin tareas programadas: antes de leer o modificar la racha, el servidor mira los días cerrados (hasta ayer, en la zona del usuario) que han quedado sin cumplir desde el último día que mantuvo la racha:
  - Si hay protectores suficientes para **todos** esos días, se gastan y los días quedan protegidos.
  - Si no hay suficientes, la racha se pierde y **no se gasta ninguno**: se guardan para la siguiente racha.
  - Sin racha viva no se gasta nada.
- **Comprar no repara el pasado.** La compra liquida la racha antes de cobrar: si ya estaba perdida, sigue perdida.
- **Leer la racha puede escribir.** `GET /me/streak` liquida y guarda. Es idempotente: tras la primera llamada del día no cambia nada más.
- **Concurrencia optimista también en `users`** (`xmin`), porque comprar, gastar protectores y ganar XP modifican el usuario sin pasar siempre por la `DailyActivity` de hoy. Un conflicto devuelve 409, como hasta ahora.

## Alcance
**Incluye:**
- Dominio: protectores y días protegidos en `Streak`, XP gastado y compra en `User`, día protegido en `DailyActivity`.
- Liquidación de la racha en `ProgressRecorder`, antes de responder, cambiar el objetivo, leer la racha y comprar.
- Endpoint de compra y ampliación de `StreakResponse`, `DayActivityResponse` y `LevelResponse`.
- App: protectores y botón de compra en la tarjeta «Objetivo diario», días protegidos en los 7 últimos días, aviso cuando un protector salva la racha y XP disponible en la tarjeta «Experiencia».

**No incluye:**
- Reparar una racha ya perdida, comprar varios protectores a la vez, regalar protectores al subir de nivel ni otros usos del XP (tienda, temas).
- Notificaciones (0013). Esa spec podrá avisar de que se ha gastado un protector.
- Respuestas offline (0014): la liquidación usa la hora del servidor, como el resto de la racha.

## Diseño
### Dominio
Glosario (ADR 0001): `StreakFreeze` (protector de racha) y `SpendableXp` (XP disponible). Ya añadidos.

- Constantes: `XpRules.StreakFreezePrice = 200` y `Limits.MaxStreakFreezes = 2`.
- `Streak` (tipo complejo de la 0010) gana:
  - `int Freezes`: protectores guardados (de 0 a `MaxStreakFreezes`).
  - `DateOnly? ProtectedThrough`: último día cubierto por un protector.
  - `KeptThrough` (privado): el mayor de `LastMetOn` y `ProtectedThrough`, es decir, el último día que mantuvo la racha.
  - `RegisterGoalMet(day)`:
    - Un `day <= KeptThrough` se ignora.
    - Si `LastLength > 0` y `day == KeptThrough + 1`, la racha suma 1; si no, vuelve a 1.
    - Lo demás sigue igual que en la 0010 (`LastMetOn = day`, `Longest`).
  - `CurrentAt(today)`: `LastLength` si `KeptThrough >= today − 1`; si no, 0.
  - `void AddFreeze()`: con `Freezes == MaxStreakFreezes` lanza `DomainRuleException("streak.freezes_full")`.
  - `IReadOnlyList<DateOnly> Settle(DateOnly today)`:
    - Si `LastLength == 0` o `LastMetOn` es nulo, no hace nada y devuelve una lista vacía.
    - Calcula los días perdidos: de `KeptThrough + 1` a `today − 1`, ambos incluidos. Si no hay ninguno, no hace nada.
    - Si `perdidos <= Freezes`: resta los protectores, fija `ProtectedThrough = today − 1` y devuelve esos días.
    - Si no: `LastLength = 0` (racha perdida y liquidada) y devuelve una lista vacía, sin tocar `Freezes`.
- `User` gana:
  - `int XpSpent` (por defecto 0) y `int SpendableXp => Xp − XpSpent`.
  - `void BuyStreakFreeze()`: comprueba primero que `Streak.Freezes < MaxStreakFreezes` (`streak.freezes_full`) y después que `SpendableXp >= StreakFreezePrice` (`xp.insufficient`). Luego suma el precio a `XpSpent` (con `checked`) y llama a `Streak.AddFreeze()`.
  - `Level` no cambia: se sigue calculando con `Xp`.
- `DailyActivity` gana:
  - `DateTimeOffset? FreezeUsedAt` y `bool IsProtected => FreezeUsedAt is not null`.
  - `void Protect(DateTimeOffset now)`: lanza `InvalidOperationException` si el día está cumplido o ya protegido.
- `ProgressRecorder`:
  - Nuevo `Task<int> SettleStreakAsync(User user, DateOnly today, DateTimeOffset now, CancellationToken)`. Llama a `user.Streak.Settle(today)` y, por cada día devuelto, carga su `DailyActivity` o la crea con `Start(userId, date, user.DailyGoal)` y llama a `Protect(now)`. Devuelve el número de días protegidos.
  - Se llama al principio de `RecordAnswerAsync`, `ChangeDailyGoalAsync` y `GetStreakAsync`, y en la compra, antes de cualquier otro cambio.
  - Nuevo `BuyStreakFreezeAsync(User user, DateTimeOffset now, CancellationToken)`: liquida y después llama a `user.BuyStreakFreeze()`.
  - `GetStreakAsync` recibe los días protegidos por la liquidación para devolverlos en `FreezesUsedNow`.
  - Los cambios los guarda el endpoint, como hasta ahora. `GET /me/streak` pasa a llamar a `SaveChangesAsync`, que no hace nada si no hay cambios.

### API
Rutas bajo `/api/v1`. Todas exigen usuario autenticado y operan solo sobre el usuario del token.

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| POST | `/me/streak-freezes` | Usuario | — → `200 StreakResponse` · `422 streak.freezes_full` · `422 xp.insufficient` · `409` |
| GET | `/me/streak` | Usuario | — → `200 StreakResponse` (ampliado; liquida la racha) |
| GET | `/me/level` | Usuario | — → `200 LevelResponse` (ampliado) |

Cambios de contratos en `Contracts/Progress`. Los parámetros nuevos van al final. Si aparecen DTO nuevos, se añaden a `ApiJsonContext`, y `BuyStreakFreeze` se añade al cliente Refit.
- `StreakResponse` gana:
  - `int StreakFreezes`, `int MaxStreakFreezes` y `int StreakFreezePrice`.
  - `int FreezesUsedNow`: los días protegidos por esta misma petición. Solo vale más de 0 en la petición que gasta protectores.
- `DayActivityResponse` gana `bool Protected`.
- `LevelResponse` gana `int SpendableXp`.

Errores: `streak.freezes_full` y `xp.insufficient` van como reglas de dominio (422 con ProblemDetails y `code`) por `DomainErrors`, con mensajes en español: «Ya tienes el máximo de 2 protectores.» y «Necesitas 200 XP disponibles para comprar un protector.».

### Datos
Migración `AddStreakFreezes` con la skill `new-ef-migration`:
- `users`:
  - Columnas nuevas: `xp_spent int not null default 0`, `streak_freezes int not null default 0` y `streak_protected_through date null` (en el `ComplexProperty` de `Streak`).
  - Concurrencia con `xmin`, igual que `daily_activities`: `Property<uint>("Version").IsRowVersion()`, sin columna real.
- `daily_activities`: `freeze_used_at timestamptz null`.
- Los usuarios existentes empiezan con 0 protectores y `xp_spent = 0`.

### App
Usa la skill `maui-ui` (tema claro y oscuro, capturas en el emulador).
- **Tarjeta «Objetivo diario»**:
  - Línea «🛡️ Protectores: 1 de 2».
  - Botón «Comprar protector · 200 XP». Pide confirmación con `IDialogService`: «¿Comprar un protector de racha por 200 XP? Se gastará solo si un día no cumples el objetivo.».
  - Con el máximo, el botón se desactiva y muestra «Tienes el máximo de protectores». Con XP insuficiente, también se desactiva y muestra «Necesitas 200 XP disponibles», usando `SpendableXp` de `GET /me/level`.
  - Al comprar, refresca la tarjeta con la respuesta y recarga `GET /me/level`. Si responde 422 o 409, muestra el mensaje del ProblemDetails.
  - En los 7 últimos días, los días protegidos tienen un tercer estado visual (por ejemplo, 🛡️ o un relleno de otro color del sistema de diseño), distinto de cumplido y de no cumplido. Se usa el mismo color en claro y oscuro con contraste suficiente.
  - Si `FreezesUsedNow > 0`, aviso no bloqueante en la tarjeta: «🛡️ Un protector ha salvado tu racha» o «🛡️ 2 protectores han salvado tu racha».
- **Tarjeta «Experiencia»**: si `SpendableXp < TotalXp`, añade la línea «Disponible: 340 XP». El nivel y la barra siguen usando el XP total.
- La lógica de textos y del estado del botón va en Mobile.Core (`DailyGoalCard`, `LevelCard`, `HomeViewModel` con `BuyStreakFreezeCommand`) para poder testearla.
- Limitación aceptada: si la liquidación ocurre al responder (la app estaba abierta en un test desde el día anterior), no hay aviso. El día protegido se ve igualmente en la tarjeta.

## Seguridad
- El usuario sale siempre del claim `sub` (`CurrentUserService`); ninguna ruta acepta ids ni cantidades.
- El precio y el máximo los fija el servidor. El cliente no envía XP, precio ni número de protectores.
- Las compras simultáneas, o una compra junto a una respuesta, no pueden gastar más XP del disponible ni duplicar protectores: el `xmin` de `users` hace que una de ellas reciba 409 sin cambiar nada.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [x] Un usuario nuevo tiene 0 protectores, `XpSpent = 0` y `SpendableXp = Xp`.
- [x] Con 200 XP disponibles, `POST /me/streak-freezes` suma un protector, resta 200 de `SpendableXp` y no cambia ni `TotalXp` ni `Level`.
- [x] Con 199 XP disponibles devuelve 422 `xp.insufficient`. Con 2 protectores devuelve 422 `streak.freezes_full`, aunque haya XP. En ambos casos no cambia nada.
- [x] Racha 5 cumplida el lunes, 1 protector y sin actividad el martes: el miércoles, `GET /me/streak` devuelve `CurrentStreak = 5`, `StreakFreezes = 0`, `FreezesUsedNow = 1` y el martes con `Protected = true`. Una segunda llamada devuelve `FreezesUsedNow = 0`.
- [x] Siguiendo con ese caso, cumplir el objetivo el miércoles deja la racha en 6 y da el bonus de racha 6.
- [x] Dos días sin cumplir con 2 protectores: se gastan los dos y la racha sigue. Con 1 protector: la racha queda a 0 y el protector se conserva.
- [x] Un día con respuestas pero sin cumplir el objetivo también se protege, y conserva su `AnsweredCount`.
- [x] Comprar un protector cuando la racha ya está perdida no la recupera.
- [x] Sin racha (usuario nuevo o racha perdida), pasar días sin estudiar no gasta protectores.
- [x] El primer gasto puede ocurrir en cualquier punto de entrada: responder, cambiar el objetivo o leer la racha.
- [x] Un usuario no ve ni compra protectores de otro.
- [x] La app muestra los protectores, compra con confirmación, desactiva el botón con su motivo, marca los días protegidos y muestra el aviso, en tema claro y oscuro (verificado en el emulador).

## Plan de tests
- **Unitarios (Domain):**
  - `Streak.Settle`: sin racha; racha viva sin hueco (hoy o ayer cumplido); hueco de 1 y 2 días con protectores suficientes; hueco mayor que los protectores (racha a 0, protectores intactos); llamada repetida el mismo día; hueco tras un día protegido previo.
  - `RegisterGoalMet` y `CurrentAt` con `ProtectedThrough`. `Longest` no cambia por días protegidos.
  - `AddFreeze` con el máximo. `User.BuyStreakFreeze`: éxito, máximo, XP insuficiente (borde 199/200) y que `Level` no cambia.
  - `DailyActivity.Protect`: día vacío, día parcial y excepción si está cumplido.
- **Integración (API, Testcontainers, `FakeTimeProvider`):**
  - Comprar (sembrando XP en la BD), los dos 422 y los contratos ampliados de `GET /me/streak` y `GET /me/level`.
  - Escenarios de los criterios avanzando el reloj: gasto de 1 y 2 protectores, racha perdida sin gasto, `FreezesUsedNow` solo en la primera lectura, filas `daily_activities` con `freeze_used_at` y continuación de la racha al cumplir.
  - Gasto disparado por `SubmitAnswer` y por `PUT /me/daily-goal`. Aislamiento entre usuarios.
  - `Model_HasNoPendingChangesToMigrate` en verde.
  - Si se puede reproducir como en la 0010, el 409 en dos compras simultáneas.
- **Unitarios (Mobile.Core):**
  - Textos de protectores y motivos del botón desactivado.
  - Aviso en singular y en plural.
  - Línea «Disponible» solo si se ha gastado XP.
  - Compra: confirmación cancelada (no llama), éxito (refresca racha y nivel) y error 422 (muestra el mensaje).
- **Manual (emulador):**
  - Comprar un protector en tema claro y oscuro y ver el botón desactivado con el máximo.
  - Simular un día perdido editando la BD local (por ejemplo, moviendo `streak_last_met_on` dos días atrás) y comprobar el aviso, el día protegido y la racha intacta.
