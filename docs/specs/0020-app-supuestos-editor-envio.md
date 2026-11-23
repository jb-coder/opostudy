# 0020 — App: lista de supuestos, editor y envío a corrección

- **Issue:** #59
- **Milestone:** v0.3.0
- **Estado:** lista

> Quinta spec de la v0.3.0. La API ya existe: supuestos (0016), envío, cola y cuota (0018) y Gemini (0019). Esta spec solo toca la app (`Mobile.Core` y `Mobile`). La 0021 hará la pantalla de resultado con el detalle por apartado, el historial de correcciones y la XP.
> Término nuevo del glosario del ADR 0001: «Borrador (de un supuesto)» → `PracticalCaseDraft`. Se añade en la rama de la feature.

## Objetivo
Que el usuario pueda elegir un supuesto de su oposición, leer el enunciado, redactar una respuesta por apartado sin miedo a perderla (borrador local) y enviarla a corregir viendo su cuota mensual. Tras el envío, la app consulta el estado hasta que la corrección termina y avisa con la nota total. Los supuestos necesitan conexión: no hay modo offline más allá del borrador.

## Alcance
**Incluye:**
- Cliente Refit y `ApiJsonContext` para los 6 endpoints de supuestos y correcciones.
- Pantalla **Supuestos** (`practical-cases`): lista por oposición agrupada por bloque (`Section`), cuota y aviso de corrección en curso.
- Pantalla **Supuesto** (`practical-case`): enunciado, apartados con su editor, contador de caracteres, borrador local y envío.
- Pantalla **Estado de la corrección** (`grading`): consulta periódica (polling) de `GET /gradings/{id}` hasta `Completed` o `Failed`.
- Acceso desde la pantalla de temas (`TopicsPage`) de cada oposición.
- Tratamiento de `409 grading.already_pending`, `422 grading.quota_exceeded`, el resto de 422 y la falta de conexión.

**No incluye:**
- Detalle del resultado (nota por apartado y criterio, comentarios, respuesta modelo): 0021. Aquí solo se muestra la nota total.
- Historial de correcciones y XP: 0021.
- Notificaciones push (FCM) y avisos con la app cerrada: el estado solo se consulta con la pantalla abierta.
- Descargar supuestos para uso offline.
- Cambios en la API, el dominio o la BD.

## Diseño
### Dominio
Sin cambios. Reglas del servidor que la app replica para no gastar peticiones (la validación de verdad sigue en la API):
- Una respuesta por apartado, en el orden de `Order`; vacía si se deja en blanco.
- Máximo **8000 caracteres** por respuesta (`Limits.PartAnswer`, contados como `string.Length`).
- Al menos una respuesta no vacía (`grading.empty`).
- Una sola corrección en curso por usuario y cuota mensual.

### API (ya existe, solo se consume)
| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/exams/{examId}/practical-cases` | Usuario | → `IReadOnlyList<PracticalCaseSummaryResponse>` (ordenados por `Key`); `404 exam.not_found` |
| GET | `/api/v1/practical-cases/{id}` | Usuario | → `PracticalCaseResponse`; `404 practical_case.not_found` |
| POST | `/api/v1/practical-cases/{id}/gradings` | Usuario | `SubmitGradingRequest` → `202 GradingResponse`; `404`, `409 grading.already_pending`, `422 grading.quota_exceeded` / `grading.invalid_answer_count` / `grading.invalid_answer` / `grading.empty` |
| GET | `/api/v1/gradings/{id}` | Dueño | → `GradingResponse`; `404 grading.not_found` |
| GET | `/api/v1/gradings?skip=0&take=1` | Usuario | → `IReadOnlyList<GradingSummaryResponse>` (más reciente primero) |
| GET | `/api/v1/me/grading-quota` | Usuario | → `GradingQuotaResponse(Limit, Used, Remaining, ResetsAt)` |

**`IOpoStudyApi`** (`Mobile.Core/Api/`), mismo estilo que los métodos existentes:
```csharp
[Get("/api/v1/exams/{examId}/practical-cases")]
Task<IReadOnlyList<PracticalCaseSummaryResponse>> GetPracticalCasesAsync(Guid examId, CancellationToken cancellationToken = default);
[Get("/api/v1/practical-cases/{id}")]
Task<PracticalCaseResponse> GetPracticalCaseAsync(Guid id, CancellationToken cancellationToken = default);
[Post("/api/v1/practical-cases/{id}/gradings")]
Task<GradingResponse> SubmitGradingAsync(Guid id, [Body] SubmitGradingRequest request, CancellationToken cancellationToken = default);
[Get("/api/v1/gradings/{id}")]
Task<GradingResponse> GetGradingAsync(Guid id, CancellationToken cancellationToken = default);
[Get("/api/v1/gradings")]
Task<IReadOnlyList<GradingSummaryResponse>> GetGradingsAsync(int skip, int take, CancellationToken cancellationToken = default);
[Get("/api/v1/me/grading-quota")]
Task<GradingQuotaResponse> GetGradingQuotaAsync(CancellationToken cancellationToken = default);
```
**`ApiJsonContext`**: `[JsonSerializable]` de `IReadOnlyList<PracticalCaseSummaryResponse>`, `PracticalCaseResponse`, `SubmitGradingRequest`, `GradingResponse`, `IReadOnlyList<GradingSummaryResponse>` y `GradingQuotaResponse`. Los tipos anidados (`PracticalCasePartResponse`, `GradingAnswerResponse`, `CriterionScoreResponse`) los genera el contexto al serializar los padres; se comprueba con el test de ida y vuelta.

### Datos (solo en el dispositivo)
**Borrador** (`PracticalCaseDraft`), en `Mobile.Core/PracticalCases/`:
- `sealed record PracticalCaseDraft(IReadOnlyList<string> Answers, DateTimeOffset SavedAt)`.
- `IPracticalCaseDraftStore`: `PracticalCaseDraft? Get(Guid practicalCaseId)`, `void Save(Guid practicalCaseId, PracticalCaseDraft draft)`, `void Delete(Guid practicalCaseId)`.
- Implementación `PreferencesPracticalCaseDraftStore` sobre `IPreferencesStore` (ya existe; en la app es `Preferences`, privado de la app). Se serializa con un `PracticalCaseDraftJsonContext` (source generator, como `ReminderJsonContext`).
- Clave: `practical-case-draft:{uid}:{practicalCaseId}`, con el uid de `IAuthService` (sin sesión → no se guarda ni se lee). Así un segundo usuario del dispositivo no ve borradores ajenos.
- JSON corrupto o con un número de respuestas distinto del número de apartados actual (el contenido cambió) → se descarta el borrador y se borra la clave. Sin excepción hacia arriba.
- Tamaño máximo: 8 apartados × 8000 caracteres; cabe sin problema en `Preferences`.
- No se borran al cerrar sesión: quedan ligados a su uid y vuelven si ese usuario entra otra vez. Al **borrar la cuenta** tampoco hay forma de enumerarlos; se acepta (son datos del propio usuario en su dispositivo).

### App
Mismo patrón que `TopicsPage` + `TopicsViewModel` (skill `maui-ui`). ViewModels en `Mobile.Core/ViewModels/`, vistas en `Mobile/Views/`, todos `Transient`. Rutas nuevas en `Routes` y `Routing.RegisterRoute`: `PracticalCases = "practical-cases"`, `PracticalCase = "practical-case"`, `Grading = "grading"`. `QueryKeys` nuevos: `PracticalCaseId = "practicalCaseId"`, `GradingId = "gradingId"` (se reutilizan `ExamId` y `ExamName`).

**Textos y formato** en una clase estática `PracticalCaseTexts` (Core) para poder probarlos: estados, cuota, contador y mensajes de fallo. Fechas y números con `es-ES` (`25`, `18,5`).

#### 1. Acceso desde Temas
- `TopicsPage`: tarjeta «Supuestos prácticos» (icono + texto, zona táctil ≥ 48 dp) debajo de las tarjetas de test.
- `TopicsViewModel.OpenPracticalCasesCommand` → `Routes.PracticalCases` con `ExamId` y `ExamName`. Siempre visible: la lista se encarga del estado vacío.

#### 2. `PracticalCasesViewModel` : `OfflineAwareViewModel`, `IQueryReceiver`
- Recibe `ExamId` y `ExamName`. En `OnAppearingAsync` (y con `RefreshCommand`) lanza **en paralelo**: `GetPracticalCasesAsync`, `GetGradingQuotaAsync` y `GetGradingsAsync(0, 1)`.
- **Lista agrupada**: `ObservableCollection<PracticalCaseGroup>`, donde `PracticalCaseGroup : List<PracticalCaseItem>` con `Title`. Agrupa por `Section`, conservando el orden de la API (primera aparición de cada bloque). `Section` nulo → grupo «Otros supuestos», al final. `CollectionView` con `IsGrouped="True"` y `GroupHeaderTemplate`.
- **Elemento**: título, «{PartCount} apartados · {MaxPoints} puntos · ~{EstimatedMinutes} min» y una marca «Borrador» si existe borrador local (`IPracticalCaseDraftStore.Get` no nulo). Tocar → `Routes.PracticalCase` con `PracticalCaseId`.
- **Cuota** (cabecera): «Te quedan {Remaining} de {Limit} correcciones este mes» y, debajo, «Se renuevan el {ResetsAt en hora local, "d 'de' MMMM"}». Con `Remaining == 0`: «Has agotado tus {Limit} correcciones de este mes» con estilo de aviso (texto, no solo color). Si la cuota falla y la lista no, se oculta la cuota (no bloquea).
- **Corrección en curso**: si la más reciente de `GetGradingsAsync(0, 1)` está `Pending` o `Processing`, tarjeta «Corrección en curso: {CaseTitle}» que navega a `Routes.Grading` con `GradingId`. Si esa llamada falla, se omite la tarjeta.
- **Estados**: carga (`IsBusy`), vacío («Aún no hay supuestos para esta oposición»), error con «Reintentar».
- **Sin conexión**: si `OfflineState.IsOffline` al entrar, o la carga falla con `ConnectivityErrors.IsConnectivityError`, no se llama/reintenta y se muestra el estado «Los supuestos prácticos necesitan conexión» con «Reintentar» (y el banner offline de la base). No se informa `OfflineState` de nada nuevo: es solo lectura.

#### 3. `PracticalCaseViewModel` : `OfflineAwareViewModel`, `IQueryReceiver`
- Recibe `PracticalCaseId`. En `OnAppearingAsync` carga en paralelo `GetPracticalCaseAsync` y `GetGradingQuotaAsync` (solo la primera vez; volver a la pantalla no recarga ni pisa lo escrito). Un fallo de cuota no bloquea la pantalla.
- Muestra título, «~{EstimatedMinutes} min · {MaxPoints} puntos», el enunciado (`Statement`, texto largo seleccionable) y un `PracticalCasePartItem` por apartado en orden de `Order`.
- **`PracticalCasePartItem`** (`ObservableObject`): `Order`, `Title` («Apartado {Order + 1} · {MaxPoints} puntos»), `Statement`, `[ObservableProperty] Answer` y derivados:
  - `CharacterCountText`: «{Answer.Length} / 8000».
  - `IsNearLimit`: `Length >= 7200` (90 %) → el contador cambia a estilo de aviso y añade « · Te quedan {8000 − Length}».
  - El `Editor` tiene `MaxLength="8000"`, así que no se puede superar (también al pegar). La constante es `PracticalCaseLimits.AnswerMaxLength = 8000` en Core.
- **Borrador**:
  - Al cargar, si hay borrador válido se rellenan las respuestas y se muestra «Borrador recuperado ({SavedAt en hora local, "d MMM HH:mm"})».
  - Cada cambio de `Answer` reprograma un guardado a los **1,5 s** (debounce con `TimeProvider` inyectado, cancelando el anterior). También se guarda en `OnDisappearing`. Si todas las respuestas están vacías, se borra en lugar de guardar.
  - Texto discreto «Guardado en el dispositivo» tras cada guardado.
  - Funciona sin conexión: si se pierde la red tras cargar, se puede seguir escribiendo.
- **Envío** (`SubmitCommand`, barra fija abajo con la cuota resumida «{Remaining}/{Limit} este mes»):
  - `CanExecute`: no `IsBusy`, al menos una respuesta con contenido no blanco y (`Quota` desconocida o `Remaining > 0`).
  - Confirmación con `IDialogService.ConfirmAsync`: «¿Enviar a corregir?» / «Usarás 1 de tus {Remaining} correcciones restantes de este mes. Los apartados en blanco puntúan 0.» / «Enviar» / «Seguir escribiendo». Si hay apartados en blanco, el mensaje indica cuántos.
  - Envía `SubmitGradingRequest(Answers)` con **todas** las respuestas en orden (las vacías como `""`), sin recortar el texto.
  - **202** → borra el borrador y navega a `Routes.Grading` con `GradingId`, **sustituyendo** la página del supuesto en la pila (`"../" + Routes.Grading`), para que «Atrás» vuelva a la lista.
  - **409 `grading.already_pending`** (`ApiErrorMessages.ProblemCode`) → conserva el borrador, llama a `GetGradingsAsync(0, 1)` y, si la última está en curso, `ConfirmAsync` «Ya tienes una corrección en curso» / «Espera a que termine para enviar otra.» / «Ver estado» / «Cerrar» → `Routes.Grading`. Si no se puede averiguar, solo el mensaje del servidor en `ErrorMessage`.
  - **422 `grading.quota_exceeded`** → `ErrorMessage` con el `detail` del servidor y recarga la cuota (deja el botón deshabilitado).
  - **Otros 422** y **404** → `ErrorMessage = ApiErrorMessages.From(ex)`; con 404 además se deshabilita el envío.
  - **Sin conexión** → «No hay conexión. Tu respuesta está guardada en el dispositivo; envíala cuando vuelvas a tener red.» El borrador se guarda antes de enviar, así que nunca se pierde.
- **Carga fallida**: sin conexión → estado «Los supuestos prácticos necesitan conexión» con «Reintentar»; otro error → mensaje y «Reintentar».
- **Vista**: `ScrollView` con `VerticalStackLayout` + `BindableLayout` para los apartados. Excepción justificada a la regla de `CollectionView`: son pocos elementos (un supuesto tiene ≤ 8 apartados) y cada uno contiene un `Editor` que crece con el texto (`AutoSize="TextChanges"`), algo que el reciclado de `CollectionView` gestiona mal. Se deja un comentario en el XAML explicándolo.
- Se añade al sistema de diseño un estilo de `Editor` (`AnswerEditorStyle`: fondo de superficie, borde, `MinimumHeightRequest` de ~6 líneas, tipografía de cuerpo) y un estilo de texto de aviso si no existe; se documentan en `docs/diseno/sistema-diseno.md`.

#### 4. `GradingViewModel` : `ViewModelBase`, `IQueryReceiver`
- Recibe `GradingId`. Muestra `CaseTitle`, «Enviado {SubmittedAt hora local}» y el estado:
  | Estado | Texto | Indicador |
  |---|---|---|
  | `Pending`, `Attempts == 0` | «En cola. Empezará en unos segundos.» | `ActivityIndicator` |
  | `Processing` | «Corrigiendo tu supuesto… Suele tardar menos de un minuto.» | `ActivityIndicator` |
  | `Pending`, `Attempts > 0` | «El corrector no respondía. Lo reintentaremos en unos minutos.» | `ActivityIndicator` |
  | `Completed` | «¡Corrección lista!» + «{Score} / {MaxPoints} puntos» + «El detalle por apartado llegará en una próxima versión.» | icono de éxito |
  | `Failed` | según `FailureCode` (abajo) + «Esta corrección no cuenta en tu cuota.» | icono de error |
  - `FailureCode` `grading.provider_unavailable` → «El corrector no está disponible ahora mismo.»; cualquier otro → «No se ha podido corregir tu supuesto.»
  - Estado desconocido → se trata como en curso (sigue consultando).
- **Polling** mientras la página está visible:
  - Primera consulta al aparecer; después espera **3 s** y luego cada **5 s** durante los primeros 3 minutos; a partir de ahí cada **15 s**. Sin límite total: mientras la pantalla esté abierta.
  - Para con `Completed`, `Failed`, `404` (mensaje «La corrección no existe.» del servidor) o al desaparecer la página (`OnDisappearing` cancela el `CancellationTokenSource`). Al volver a aparecer, reanuda si no había terminado.
  - Errores transitorios (sin conexión, timeout, 5xx): no paran el bucle; muestran «Sin conexión. Seguimos intentándolo…» y la siguiente espera es de 15 s. Al recuperar, se limpia el aviso.
  - Las esperas usan `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` con el `TimeProvider` inyectado (en los tests, `FakeTimeProvider`).
  - Un único bucle activo: si `OnAppearingAsync` se llama dos veces, no se lanzan dos.
- **Aviso al terminar**: cuando la transición a `Completed`/`Failed` ocurre con la pantalla abierta, además del cambio visual se llama a `IDialogService.AlertAsync` («Corrección lista» / «Has sacado {Score} de {MaxPoints} puntos.»; o «No se ha podido corregir» con el texto del fallo). Si la primera consulta ya viene terminada, no hay alerta, solo el estado.
- **Acciones**:
  - `Completed` → «Volver a los supuestos» (`Routes.Back`).
  - `Failed` → «Recuperar mis respuestas»: guarda `GradingResponse.Answers` (ordenadas por `Order`, campo `Text`) como borrador de `PracticalCaseId` y navega a `"../" + Routes.PracticalCase` con `PracticalCaseId`. Así no hay que reescribir nada.
  - En curso → «Volver» (`Routes.Back`); el texto aclara «Puedes salir: la corrección sigue en el servidor. Mira el estado desde la lista de supuestos.»
- Semántica: el texto de estado con `SemanticProperties.Description` y sin depender del color.

### Modo offline
- Los supuestos **no** forman parte del contenido offline (`offline-content`) ni de `OfflineSynchronizer`; no se descargan ni se encolan envíos.
- Sin conexión: la lista y el detalle muestran «Los supuestos prácticos necesitan conexión»; el editor ya abierto sigue funcionando con el borrador; el envío falla con mensaje y se conserva lo escrito; el polling espera y reintenta.
- La detección es la de siempre: `OfflineState.IsOffline` para no lanzar llamadas que van a fallar y `ConnectivityErrors.IsConnectivityError` para clasificar la excepción. No se añaden estados nuevos a `OfflineState`.

## Seguridad
- Toda la autorización por recurso ya la hace la API (dueño en `GET /gradings/{id}`); la app solo usa ids que le ha dado la API.
- La app nunca recibe la rúbrica ni la respuesta modelo antes de `Completed`, y en esta spec no las muestra.
- Borradores en `Preferences` (almacenamiento privado de la app), separados por uid. No se escriben respuestas en logs: los `LoggerMessage` nuevos solo registran ids, estado y tipo de excepción.
- No hacen falta secretos nuevos.

## Criterios de aceptación
- [ ] Desde Temas se llega a Supuestos, que lista los supuestos agrupados por bloque (sin bloque → «Otros supuestos» al final) con apartados, puntos y minutos.
- [ ] La cuota se ve en la lista y en el supuesto; con `Remaining == 0` el envío está deshabilitado y el texto lo explica.
- [ ] El supuesto muestra enunciado y apartados; cada editor tiene contador «n / 8000», aviso desde 7200 y no deja superar 8000.
- [ ] Lo escrito se guarda solo (≤ 1,5 s) y al salir; al volver a entrar (incluso tras cerrar la app) se recupera; tras un 202 el borrador desaparece.
- [ ] Un borrador de otro usuario del mismo dispositivo no aparece.
- [ ] Enviar pide confirmación, manda una respuesta por apartado en orden (vacías como `""`) y lleva a la pantalla de estado; «Atrás» desde ella vuelve a la lista.
- [ ] Con todo en blanco, el botón de enviar está deshabilitado.
- [ ] `409 grading.already_pending` ofrece ver la corrección en curso; `422 grading.quota_exceeded` muestra el mensaje y recarga la cuota; otros 422 muestran el `detail` del servidor; en ningún caso se pierde el borrador.
- [ ] La pantalla de estado consulta hasta `Completed` o `Failed`, avisa al terminar con la nota total o el motivo del fallo, y deja de consultar al salir de la pantalla.
- [ ] Con `Failed`, «Recuperar mis respuestas» vuelve al editor con el texto enviado.
- [ ] La lista muestra «Corrección en curso» cuando la última corrección está `Pending`/`Processing` y lleva a su estado.
- [ ] Sin conexión: lista y detalle muestran el estado «necesitan conexión» con «Reintentar»; el editor abierto sigue funcionando y el envío falla sin perder nada.
- [ ] Tema claro y oscuro y escala de fuente 1,3 revisados en el emulador (skill `maui-ui`), sin colores fijos ni textos cortados.
- [ ] `dotnet build OpoStudy.Backend.slnf` y `dotnet build src/OpoStudy.Mobile -f net10.0-android` sin warnings.

## Plan de tests
- **Unitarios** (`tests/OpoStudy.Mobile.Core.Tests`, NSubstitute + Shouldly, `FakeTimeProvider`):
  - `ApiJsonContext`: ida y vuelta de `PracticalCaseResponse`, `GradingResponse` (con `Criteria` nulo y con valores) y `GradingQuotaResponse`.
  - `PreferencesPracticalCaseDraftStore`: guarda y lee; clave por uid (otro uid no lo ve); sin sesión no guarda; JSON corrupto → `null` y borra la clave.
  - `PracticalCasesViewModel`: agrupación y orden de grupos (nulo al final); cuota normal y agotada; fallo solo de cuota no bloquea; tarjeta de corrección en curso sí/no; offline al entrar sin llamar a la API; error de conectividad → estado «necesitan conexión»; marca de borrador; navegación con `ExamId`.
  - `PracticalCaseViewModel`: borrador recuperado, descartado si no cuadra el nº de apartados; debounce (avanzar 1,5 s guarda una vez, cambios seguidos solo guardan el último); guarda en `OnDisappearing`; `CanExecute` (todo en blanco, cuota 0, `IsBusy`); confirmación cancelada no envía; request con vacías en orden; 202 borra borrador y navega a `../grading`; 409 con y sin corrección en curso; 422 quota recarga cuota; otro 422 muestra `detail`; error de conectividad conserva borrador; contador y `IsNearLimit`.
  - `GradingViewModel`: `Pending` → `Processing` → `Completed` con los intervalos 3 s / 5 s / 15 s; alerta solo en la transición; ya terminado al entrar sin alerta; `Failed` con cada `FailureCode`; error transitorio no para y espera 15 s; 404 para; `OnDisappearing` cancela y `OnAppearingAsync` reanuda sin duplicar el bucle; «Recuperar mis respuestas» guarda el borrador ordenado y navega.
  - `PracticalCaseTexts`: formatos de cuota, fechas y nota en `es-ES` (`18,5`).
- **Integración**: no aplica (no hay cambios en la API).
- **Manual** (AppHost + emulador, `Grading:Provider = Fake`):
  1. Temas → Supuestos → abrir uno, escribir, cerrar la app, volver: borrador recuperado.
  2. Enviar: estado En cola → Corrigiendo → alerta con la nota (el corrector falso tarda segundos).
  3. Enviar otro mientras el primero está en curso (o forzar con dos envíos rápidos): 409 y «Ver estado».
  4. Bajar `Grading:MonthlyQuota` a 1 en el AppHost y comprobar la cuota agotada.
  5. Modo avión: lista, detalle, envío y polling.
  6. Capturas claro/oscuro y `font_scale 1.3` de las tres pantallas.
