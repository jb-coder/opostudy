# 0013 — Recordatorio diario con notificaciones locales

- **Issue:** #40
- **Milestone:** v0.2.0
- **Estado:** lista

## Objetivo
Mantener el hábito con un aviso diario a la hora que elija el usuario si aún no ha cumplido el objetivo del día. La notificación es **local** (ADR 0001): sin servidor, sin Firebase Cloud Messaging y sin coste. Se apoya en el objetivo diario y la racha de la 0010.

## Decisiones
- **Solo app, sin cambios en la API.** La alarma se programa en Android y decide al dispararse, sin red, con una **instantánea local** del progreso del día que la app guarda cada vez que recibe datos de la racha. Llamar a la API desde segundo plano exigiría un token válido y más permisos; para un único dispositivo, la instantánea basta.
- **Implementación nativa de Android** (`AlarmManager` + `BroadcastReceiver` + `NotificationManager`), sin plugins de terceros: es poco código, no añade dependencias y Android es la única plataforma.
- **Alarma con ventana de 15 minutos** (`SetWindow`, `RTC_WAKEUP`). No necesita el permiso `SCHEDULE_EXACT_ALARM` (restringido desde Android 12/14). Se descartó `SetAndAllowWhileIdle` porque en Android 12+ el sistema le da una ventana de hasta una hora (verificado en el emulador). A cambio, con el móvil en reposo profundo (Doze) el aviso puede esperar a la siguiente ventana de mantenimiento. Es de un solo disparo: al saltar, se reprograma para el día siguiente.
- **Desactivado por defecto.** El usuario lo activa desde la pantalla de inicio; así el permiso de notificaciones de Android 13+ se pide en el momento en que tiene sentido y no al abrir la app.
- **Hora a elegir entre valores fijos**: 18:00, 19:00, 20:00, 21:00 y 22:00; **20:00 por defecto**. Mismo patrón que el objetivo diario (hoja de acciones), sin selector de hora.
- **El día es el día local del dispositivo** (`TimeProvider`). La app ya sincroniza su zona horaria con el servidor (0010), así que coincide con el día de la racha.
- **Al cerrar sesión se cancela la alarma y se borra la instantánea**, para que no avise con datos de otra cuenta. La preferencia (activado y hora) es del dispositivo y se conserva: queda suspendida (`reminder.suspended`) hasta volver a iniciar sesión y entonces se reprograma si estaba activada.

## Alcance
**Incluye:**
- Preferencias del recordatorio (activado, hora) e instantánea del progreso del día, guardadas con `Preferences`.
- Lógica en Mobile.Core: textos de la notificación, cálculo de la próxima hora de disparo y actualización de la instantánea.
- Implementación Android: canal de notificaciones, alarma, receptor que decide y muestra, y reprogramación tras reiniciar el móvil, actualizar la app o cambiar la hora o la zona horaria.
- Permiso `POST_NOTIFICATIONS` en Android 13+.
- Tarjeta «Recordatorio» en la pantalla de inicio.

**No incluye:**
- Notificaciones push desde el servidor (FCM) ni avisos de que se ha gastado un protector (la app ya lo muestra en la tarjeta, 0015).
- Varios recordatorios al día, hora libre con selector o días de la semana.
- Sincronizar la preferencia entre dispositivos (no hay columna en `users`).
- iOS u otras plataformas: la interfaz existe en Core, pero solo hay implementación de Android. En otras plataformas la tarjeta no se muestra.
- Pantalla de ajustes: la tarjeta vive en la pantalla de inicio hasta que exista una.

## Diseño
### Dominio
Sin cambios en `OpoStudy.Domain`. Glosario (ADR 0001): `DailyReminder` (recordatorio diario). Ya añadido. Los tipos de la app usan el prefijo `Reminder` por brevedad.

### API
Sin cambios. Se reutilizan `GET /me/streak` (`StreakResponse`) y el `DailyProgress` de `POST /quizzes/{id}/answers`.

### Datos
Sin cambios en la BD. Claves de `Preferences` (dispositivo):
- `reminder.enabled` (bool, `false`) y `reminder.time` (string `HH:mm`, `"20:00"`).
- `reminder.snapshot` (JSON de `ReminderSnapshot`, ver abajo) o ausente.
- `reminder.suspended` (bool, `false`): true tras cerrar sesión, hasta volver a iniciarla.
- `reminder.lastFiredOn` (fecha `yyyy-MM-dd`) o ausente: último día en que saltó la alarma.

### App
Usa la skill `maui-ui` (tema claro y oscuro, capturas en el emulador).

**Mobile.Core** (`Reminders/`, todo testeable sin Android):
- `record ReminderSnapshot(DateOnly Date, int AnsweredToday, int DailyGoal, bool GoalMetToday, int CurrentStreak)`. Se serializa con `ApiJsonContext` u otro `JsonSerializerContext` de Core (source generator, sin reflexión).
- `ReminderSettings(bool Enabled, TimeOnly Time)` y `ReminderSettings.AllowedTimes` con las 5 horas.
- `IReminderStore`: `GetSettings`/`SaveSettings`, `GetSnapshot`/`SaveSnapshot`/`ClearSnapshot`. Implementación `PreferencesReminderStore` en Core sobre una abstracción de `IPreferences` (MAUI Essentials) para poder sustituirla en tests.
- `IReminderScheduler` (plataforma): `Task<bool> RequestPermissionAsync()`, `void Schedule(DateTimeOffset at)`, `void Cancel()`, `bool IsSupported`. Implementación nula (`IsSupported = false`) para otras plataformas.
- `static class ReminderSchedule`:
  - `Window` (15 min) y `DateTimeOffset NextTrigger(DateTimeOffset now, TimeOnly time, TimeZoneInfo zone, DateOnly? lastFiredOn)`: hoy a esa hora si hoy no ha saltado ya y `now < hora + Window` (si la hora ya pasó, Android la dispara enseguida; así reprogramar dentro de la ventana no pierde el aviso del día); si no, mañana. Correcto en los cambios de horario de verano (si la hora no existe ese día, se usa la siguiente válida).
- `static class ReminderMessages`:
  - `ReminderMessage? For(ReminderSnapshot? snapshot, DateOnly today)` → título y texto, o `null` si no hay que avisar:
    - Instantánea de hoy con `GoalMetToday` → `null` (no se avisa).
    - Instantánea de hoy sin cumplir, racha > 0 → «🔥 Tu racha de 5 días está en riesgo» / «Responde 3 preguntas más para mantenerla.».
    - Instantánea de hoy sin cumplir, sin racha → «🎯 Objetivo diario» / «Te faltan 3 preguntas para cumplir tu objetivo de hoy.».
    - Instantánea de ayer con `GoalMetToday` y racha > 0 → «🔥 Tu racha de 5 días está en riesgo» / «Responde 10 preguntas hoy para mantenerla.» (objetivo completo).
    - Cualquier otro caso (sin instantánea, o más antigua) → «🎯 Objetivo diario» / «Aún no has repasado hoy. ¡Unas preguntas y listo!».
    - Singular y plural: «1 pregunta» / «N preguntas», «1 día» / «N días».
- `ReminderService` (orquesta, inyectable en ViewModels):
  - `UpdateFromStreak(StreakResponse)`: guarda la instantánea (`Date = Today`, `AnsweredToday`, `DailyGoal`, `GoalMetToday`, `CurrentStreak`).
  - `UpdateFromProgress(DailyProgressResponse)`: guarda la instantánea con la fecha local de hoy. `GoalMetToday = GoalMetNow || AnsweredToday >= DailyGoal || (instantánea previa de hoy cumplida)`.
  - `Task<bool> EnableAsync(TimeOnly time)`: pide permiso; si se deniega, no guarda nada y devuelve `false`. Si se concede, guarda y programa `NextTrigger`.
  - `Disable()`: guarda `Enabled = false` y cancela.
  - `ChangeTime(TimeOnly)`: guarda y reprograma si está activado.
  - `EnsureScheduled()`: si está activado y no suspendido, programa `NextTrigger` (idempotente).
  - `OnSignedOut()`: cancela, borra la instantánea y marca suspendido (no toca `Enabled` ni `Time`).
  - `OnSignedIn()`: quita la suspensión y llama a `EnsureScheduled()`. Se llama al cargar la pantalla de inicio.
  - `ReminderMessage? OnAlarmFired(DateTimeOffset now)`: `null` si está suspendido o desactivado (sin reprogramar); si no, guarda `lastFiredOn = hoy`, reprograma con `EnsureScheduled()` y devuelve `ReminderMessages.For(instantánea, hoy)`.
- `HomeViewModel`:
  - Llama a `UpdateFromStreak` tras `GET /me/streak`, cambiar el objetivo y comprar un protector, y a `OnSignedIn` al refrescar.
  - Llama a `OnSignedOut` al cerrar sesión (aunque `SignOutAsync` falle no debe quedar programado).
  - Tarjeta «Recordatorio» (propiedades y comandos): `ReminderEnabled` (switch), `ReminderTimeText` («Todos los días a las 20:00 si no has cumplido el objetivo»), `ToggleReminderCommand` y `ChangeReminderTimeCommand` (hoja de acciones de `IDialogService` con las 5 horas). Si el permiso se deniega, el switch vuelve a apagado y se muestra «Activa las notificaciones de OpoStudy en los ajustes de Android para recibir el recordatorio.». El comando no admite ejecuciones concurrentes: `IsReminderToggleEnabled` desactiva el switch mientras corre y, al terminar, el switch refleja siempre el estado guardado. `IsReminderSupported` oculta la tarjeta fuera de Android.
- `QuizViewModel`: llama a `UpdateFromProgress` con cada `DailyProgress` recibido.

**OpoStudy.Mobile (Platforms/Android)**:
- `AndroidManifest.xml`: `POST_NOTIFICATIONS` y `RECEIVE_BOOT_COMPLETED`.
- Canal `daily_reminder` («Recordatorio diario», importancia por defecto), creado al arrancar en `MainApplication.OnCreate` (idempotente).
- `AndroidReminderScheduler : IReminderScheduler`:
  - `RequestPermissionAsync`: en API ≥ 33, `Permissions.RequestAsync<Permissions.PostNotifications>()`; por debajo, `true`.
  - `Schedule`: `AlarmManager.SetWindow(RTC_WAKEUP, at, 15 min, ...)` con un `PendingIntent` de broadcast fijo (`FLAG_UPDATE_CURRENT | FLAG_IMMUTABLE`), así reprogramar sustituye a la alarma anterior.
  - `Cancel`: cancela ese `PendingIntent`.
- `ReminderReceiver : BroadcastReceiver` (alarma): llama a `ReminderService.OnAlarmFired` y, si el mensaje no es `null`, muestra la notificación (icono de la app, `AutoCancel`, toque → abre `MainActivity`). No hace red ni operaciones largas. Si `NotificationManagerCompat.AreNotificationsEnabled()` es falso, no muestra nada.
- `ReminderRescheduleReceiver`: `BOOT_COMPLETED`, `MY_PACKAGE_REPLACED`, `TIME_SET` y `TIMEZONE_CHANGED` → `EnsureScheduled` (con `TIMEZONE_CHANGED` y `TIME_SET` limpia antes la zona horaria cacheada con `TimeZoneInfo.ClearCachedData()`). Con la sesión cerrada no programa nada.
- Los receptores obtienen los servicios con `IPlatformApplication.Current.Services` (el proceso arranca `MainApplication` antes del receptor). Se registran con atributos `[BroadcastReceiver(Exported = ...)]` e `[IntentFilter]`: el de alarma no exportado; el de reprogramación exportado solo para esas acciones del sistema.
- Registro en `MauiProgram`: `IReminderScheduler` → Android o nulo según plataforma, `IReminderStore`, `ReminderService`, `IPreferences` (`Preferences.Default`).

## Seguridad
- La notificación no contiene datos sensibles: solo racha y número de preguntas. Se muestra en la pantalla de bloqueo con visibilidad privada por defecto del sistema.
- `PendingIntent` siempre `FLAG_IMMUTABLE`. El receptor de la alarma no está exportado; el de reprogramación solo responde a acciones del sistema y no lee datos del `Intent`.
- La instantánea se borra al cerrar sesión. No guarda tokens ni el id del usuario.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [ ] Con el recordatorio desactivado (por defecto) no se programa ninguna alarma ni se pide permiso.
- [ ] Al activarlo en Android 13+ se pide `POST_NOTIFICATIONS`; si se deniega, el switch queda apagado y se muestra el mensaje de ajustes.
- [ ] Activado a las 20:00 sin cumplir el objetivo, a esa hora (con hasta 15 min de retraso) llega una notificación con el texto según el estado; al tocarla se abre la app.
- [ ] Si el objetivo de hoy ya está cumplido (según la instantánea), no llega notificación ese día, y el día siguiente sí, si no se cumple.
- [ ] Cambiar la hora reprograma la alarma; desactivar la cancela.
- [ ] Tras reiniciar el móvil, el recordatorio sigue programado.
- [ ] Al cerrar sesión no llegan más recordatorios hasta volver a iniciar sesión; al volver, se reprograma si estaba activado.
- [ ] Los textos cubren racha en riesgo, preguntas que faltan, sin racha y sin datos de hoy, con singular y plural.
- [ ] La tarjeta «Recordatorio» se ve bien en tema claro y oscuro (verificado en el emulador).
- [ ] Sin cambios en la API ni en la BD; la app compila con `TreatWarningsAsErrors`.

## Plan de tests
- **Unitarios (Mobile.Core.Tests):**
  - `ReminderMessages.For`: cada caso de la lista (hoy cumplido → `null`; hoy con y sin racha; ayer cumplido con racha; ayer sin cumplir; sin instantánea; instantánea antigua) y singular/plural.
  - `ReminderSchedule.NextTrigger`: antes y después de la hora, justo a la hora (→ hoy), dentro de la ventana sin disparo hoy (→ hoy), ya disparado hoy (→ mañana), fuera de la ventana (→ mañana), y los dos cambios de hora de `Europe/Madrid` (marzo y octubre).
  - `ReminderService` con fakes de `IReminderScheduler` y `IPreferences`: activar con permiso concedido (programa) y denegado (no guarda ni programa); desactivar (cancela); cambiar hora activado y desactivado; `EnsureScheduled`; `OnSignedOut` (cancela, borra y suspende; después `EnsureScheduled` y `OnAlarmFired` no programan); `OnSignedIn` (reprograma si está activado); `OnAlarmFired` (suspendido, desactivado, objetivo cumplido y con mensaje: guarda `lastFiredOn` y reprograma); `UpdateFromProgress` conserva `GoalMetToday` del mismo día y lo reinicia si cambia el día.
  - `HomeViewModel`: switch y hora (comandos, textos, mensaje al denegar), instantánea guardada tras cargar la racha, `OnSignedIn` al cargar, switch desactivado mientras corre el comando y `OnSignedOut` al cerrar sesión. `QuizViewModel`: instantánea tras responder.
- **Integración:** no aplica (sin cambios de backend).
- **Manual (emulador, Android 13+):**
  - Activar, conceder permiso y comprobar la alarma con `adb shell dumpsys alarm | grep -i opostudy`.
  - Forzar el disparo con una hora cercana (en depuración se puede programar a +1 min con un comando temporal, que no se versiona) en tres estados: objetivo cumplido (sin aviso), racha en riesgo y sin racha.
  - Denegar el permiso y ver el mensaje. Reiniciar el emulador y ver que la alarma sigue. Cerrar sesión y ver que se cancela.
  - Capturas de la tarjeta en tema claro y oscuro.
