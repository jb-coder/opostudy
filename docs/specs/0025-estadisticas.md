# 0025 — Estadísticas de tests, supuestos y actividad

- **Issue:** #75
- **Milestone:** v0.4.0
- **Estado:** lista

> Recoge lo aplazado en la 0010 (calendario mensual), la 0011 (XP por día), la 0012 (estadísticas por tema) y la 0021 (media y evolución de los supuestos).

## Objetivo
Que el usuario vea cómo va: cuánto acierta, qué temas tiene más flojos, qué días ha estudiado y cómo evolucionan sus supuestos. Desde ahí puede practicar directamente un tema flojo.

## Decisiones
- **Sin migración.** Todo se calcula al consultar, con lo que ya se guarda, y los índices actuales bastan:

  | Dato | Origen | Índice que lo cubre |
  |---|---|---|
  | Respuestas y aciertos | `quiz_answers` (`is_correct`, `answered_at`) + `quiz_attempts` (`user_id`, `exam_id`, `finished_at`) + `questions.topic_id` | `ix_quiz_attempts_user_id_started_at` (prefijo `user_id`) y PK de `quiz_answers` |
  | Actividad y XP por día | `daily_activities` | PK (`user_id`, `date`) |
  | Notas de supuestos | `gradings` (`status`, `score`, `max_points`) + `practical_cases.exam_id` | `ix_gradings_user_id_submitted_at` |

- **El porcentaje de aciertos cuenta todas las respuestas**, también las repetidas y las de repasos (`Review`), de tests terminados o a medias. Lo pendiente de repasar ya lo cubre `GET /exams/{examId}/mistakes` con la última respuesta.
- **El tema es el de la pregunta**, no el del test: así cuentan también los tests aleatorios y los repasos.
- **El contenido desactivado sigue contando** en el histórico (preguntas y supuestos).
- **La API devuelve recuentos y la app formatea el porcentaje de los tests.** La nota de los supuestos se calcula en el servidor, porque cada corrección guarda su propio `MaxPoints`.
- **La actividad no depende de la oposición**: `daily_activities` es por usuario y día.
- **Solo en línea.** No se guardan estadísticas en el dispositivo (coherente con la 0014).

## Alcance
**Incluye:**
- API: `GET /me/stats` y `GET /me/activity`.
- Contratos: carpeta `Stats` y `XpEarned` en `DayActivityResponse`.
- App: pantalla **Estadísticas** desde Inicio: indicadores, temas, calendario y supuestos.
- Glosario (ADR 0001): «Estadísticas» → `Stats`, «Porcentaje de aciertos» → `Accuracy`.

**No incluye:**
- Comparativa con otros usuarios ni exportar datos.
- Detalle de cada test antiguo.
- Estadísticas por criterio de la rúbrica.
- Tiempo de estudio, estadísticas sin conexión y filtros por fecha en `GET /me/stats`.

## Diseño
### Dominio
Sin cambios en las entidades. `Limits` gana `MaxActivityRangeDays = 92` y `RecentScoresCount = 10`.

### API
Feature nueva `Features/Stats` (`StatsEndpoints` + `StatsQueries`), registrada en `Program.cs`. Las consultas usan `AsNoTracking` y proyecciones; nunca cargan `Answers` ni `Criteria` (tienen `AutoInclude`).

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/me/stats?examId=` | Sí | → `StatsResponse` |
| GET | `/api/v1/me/activity?from=&to=` | Sí | → `ActivityResponse` |

**Contratos** (`OpoStudy.Contracts/Stats`)
- `StatsResponse(Guid ExamId, QuizStatsResponse Quizzes, PracticalCaseStatsResponse PracticalCases)`
- `QuizStatsResponse(int AnsweredCount, int CorrectCount, int FinishedQuizCount, int AvailableTopicCount, IReadOnlyList<TopicStatsResponse> Topics)`
  - `FinishedQuizCount`: tests de la oposición con `FinishedAt`, de cualquier modo.
  - `AvailableTopicCount`: temas con al menos una pregunta activa.
  - `Topics`: solo los temas con alguna respuesta. La suma de sus recuentos es igual al total.
- `TopicStatsResponse(Guid TopicId, int Number, string Title, string? Section, int AnsweredCount, int CorrectCount, DateTimeOffset LastAnsweredAt, int AvailableQuestionCount)`
  - Orden, de más débil a más fuerte: `CorrectCount / AnsweredCount` ascendente (división exacta, no entera); a igualdad, más respondidas primero; después, `Number`.
  - `AvailableQuestionCount`: preguntas activas del tema (0 = no se puede practicar).
- `PracticalCaseStatsResponse(int CompletedCount, decimal? AveragePercent, IReadOnlyList<CaseBestScoreResponse> BestScores, IReadOnlyList<CaseScoreResponse> RecentScores)`
  - Solo correcciones `Completed` de supuestos de la oposición.
  - Porcentaje de una corrección: `Score * 100 / MaxPoints`. `AveragePercent`: media de esos porcentajes sin redondear, o `null` si no hay ninguna. Se redondea al final a 1 decimal (`AwayFromZero`), igual que los demás `Percent`.
  - `BestScores`: una por supuesto, ordenadas por `CaseTitle` y después por `PracticalCaseId`.
  - `RecentScores`: las últimas 10 por `SubmittedAt` (desempate por `Id`), devueltas de la más antigua a la más reciente.
- `CaseBestScoreResponse(Guid PracticalCaseId, string CaseTitle, string? Section, int GradingCount, Guid BestGradingId, decimal Score, int MaxPoints, decimal Percent)`: gana el mayor porcentaje exacto; a igualdad, la más reciente.
- `CaseScoreResponse(Guid GradingId, string CaseTitle, DateTimeOffset SubmittedAt, decimal Score, int MaxPoints, decimal Percent)`
- `ActivityResponse(DateOnly Today, int DailyGoal, IReadOnlyList<DayActivityResponse> Days)`
  - `Today`: hoy en la zona horaria del usuario.
  - `Days`: ascendente, solo días con actividad (`AnsweredCount > 0`, `XpEarned > 0`, objetivo cumplido o protegido). Los días sin fila no se rellenan.
- `DayActivityResponse` (en `Progress`) gana `int XpEarned` al final. Cambio aditivo en el JSON: se rellena también en `LastDays` de `GET /me/streak`.

**Comportamiento**
- `from` y `to` son inclusivos e ISO (`yyyy-MM-dd`). `to` puede ser futuro (fin del mes en curso).
- `GET /me/activity` no liquida la racha ni escribe actividad: eso sigue siendo de `GET /me/streak`, que Inicio llama antes.

**Errores** (ProblemDetails)

| Caso | Respuesta |
|---|---|
| Sin token | 401 |
| `examId` ausente o `Guid.Empty` | 400 `ValidationProblem`, clave `examId`: «La oposición es obligatoria.» |
| Oposición inexistente o inactiva | 404 `exam.not_found` |
| `from` o `to` ausentes | 400, clave `from` o `to`: «La fecha es obligatoria.» |
| `from > to` | 400, clave `from`: «La fecha inicial no puede ser posterior a la final.» |
| Más de 92 días (extremos incluidos) | 400, clave `to`: «El periodo no puede superar los 92 días.» |
| `examId` o fechas mal formados | 400 |

El rango se calcula con `DayNumber` (sin `AddDays`), para que `DateOnly.MaxValue` no desborde.

### Datos
Sin tablas, columnas, índices ni migraciones nuevas.

### App
**Cliente y rutas**
- `IOpoStudyApi`: `GetStatsAsync(Guid examId)` y `GetActivityAsync(DateOnly from, DateOnly to)`. Las fechas llevan `[Query(Format = "yyyy-MM-dd")]`: el formato por defecto de Refit no es ISO.
- DTOs nuevos en `ApiJsonContext`. Ruta `Routes.Stats = "stats"`. Página y ViewModel `Transient`.
- Inicio: tarjeta «Estadísticas» debajo de «Practicar», con «Consulta tus aciertos, tus temas más flojos y tu calendario.» y el botón «Ver estadísticas».

**`StatsViewModel` : `OfflineAwareViewModel`**, con los textos y cálculos puros en `StatsTexts` y `ActivityCalendar` (Mobile.Core):
- Al aparecer (también al volver de un test) y en `RefreshCommand`: `GetExamsAsync` si aún no hay oposiciones y, en paralelo, estadísticas y actividad del mes mostrado.
- Se elige la primera oposición. Solo si hay más de una, aparece un `Picker` que recarga las estadísticas.
- El mes inicial sale de `TimeProvider` en la zona del dispositivo (la app ya la sincroniza con el perfil). Después manda `Today`.
- Una respuesta que llega tarde no pisa a una petición posterior (cambio de oposición o de mes).
- Los porcentajes de los tests se redondean al entero (`AwayFromZero`) y usan cultura `es-ES`. Las fechas se muestran en hora local.

**`StatsPage`**: `RefreshView` + `CollectionView` (temas como elementos; el resto, en `Header` y `Footer`), con `OfflineBanner` arriba. De arriba abajo:
1. **Indicadores** (rejilla 2×2, valor grande + etiqueta): «Respondidas», «Aciertos» (`72 %`, o `—` sin respuestas), «Tests completados» y «Temas practicados» (`5 de 12`).
2. **Por tema**: título, `ProgressBar` de aciertos, «72 % · 18 de 25», «Última práctica: 12 nov 2026» y botón «Practicar este tema».
   - Muestra los 5 más flojos. Si hay más, «Ver todos los temas (N)» / «Ver menos».
   - El título se muestra tal cual (ya lleva «Tema N.»).
   - «Practicar este tema»: `CreateQuizAsync` de 10 preguntas del tema y navegación a `Routes.Quiz`. Deshabilitado con `AvailableQuestionCount = 0` o mientras está ocupado.
3. **Actividad**: calendario del mes (semana de lunes a domingo), con «Mes anterior» / «Mes siguiente» (este, deshabilitado en el mes en curso).
   - Estado del día con color **y** marca: objetivo cumplido ✓ (fondo `Success`, texto `OnPrimary`), actividad sin objetivo • (borde `Success`), protegido 🛡️, hoy en negrita con borde `Primary`. Los futuros van atenuados y no se pueden tocar.
   - Leyenda de las marcas debajo. Resumen: «12 días con actividad · 9 objetivos cumplidos · +640 XP». Un día protegido sin respuestas ni XP no suma en los «días con actividad».
   - Tocar un día muestra su detalle: «Martes 12 de noviembre: 14 preguntas de 10 · objetivo cumplido · +160 XP», o «Sin actividad».
   - Celda de 48 dp de alto y ancho = un séptimo de la tarjeta, con `SemanticProperties.Description` igual al detalle.
4. **Supuestos**: «3 correcciones · media 74,5 %».
   - Gráfica de barras verticales (hasta 10, de la más antigua a la más reciente) con XAML: `BoxView` de alto fijo, `ScaleY` = porcentaje y `AnchorY = 1`, en `Primary`. Encima, el porcentaje; debajo, el día y el mes (`d/M`). Cada barra lleva su descripción accesible. La fila se desplaza en horizontal si no cabe.
   - «Mejor nota por supuesto»: título, «18,5 / 25 · 74 %» y «2 intentos». Tocarlo abre `Routes.Grading` con `BestGradingId`.

**Estados**

| Estado | Qué se ve |
|---|---|
| Carga inicial | Indicador con «Cargando estadísticas…». Al recargar, los datos se mantienen |
| Sin datos (0 respuestas y 0 correcciones) | «Aún no hay estadísticas. Haz tu primer test y aquí verás tu progreso.» + botón «Hacer un test» → `Routes.Topics` de la oposición. El calendario se sigue mostrando |
| Solo falta una parte | «Aún no has respondido preguntas.» o «Aún no tienes supuestos corregidos.» en su bloque |
| Sin oposiciones | «No hay oposiciones disponibles.» |
| Sin conexión, sin datos cargados | «Las estadísticas necesitan conexión» + «Reintentar». No se llama a la API si `OfflineState.IsOffline` |
| Sin conexión, con datos cargados | Se conservan y se avisa: «Sin conexión. Se muestran los últimos datos cargados.» |
| Error de `GET /me/stats` | Mensaje de `ApiErrorMessages` + «Reintentar», conservando lo cargado |
| Error de `GET /me/activity` | Solo en la tarjeta del calendario: «No se ha podido cargar la actividad.» + «Reintentar». No afecta al resto, y nunca se pintan los datos de otro mes |
| «Practicar este tema» sin conexión | «No hay conexión. Puedes hacer un test sin conexión desde «Hacer un test».» |

## Seguridad
- Las dos rutas exigen autorización, y el usuario sale siempre del claim `sub` (`CurrentUserService`): ninguna acepta un id de usuario.
- Autorización por recurso: todas las consultas filtran por `UserId` (`quiz_attempts`, `daily_activities`, `gradings`). Los datos de un usuario nunca entran en los de otro.
- El rango de 92 días y las listas acotadas (temas de la oposición, 10 notas) limitan el coste; se aplica además el límite de peticiones por usuario actual.
- No se registran estadísticas ni textos en los logs. No hacen falta secretos nuevos.

## Criterios de aceptación
- [ ] Un usuario nuevo recibe recuentos a 0, `AveragePercent = null` y listas vacías en `GET /me/stats`, y `Days` vacío en `GET /me/activity`.
- [ ] Los recuentos por tema incluyen tests por tema, aleatorios, repasos y tests a medias; las preguntas sin responder no cuentan. La suma por tema coincide con el total.
- [ ] Los temas van de más débil a más fuerte con los desempates indicados.
- [ ] Las respuestas a preguntas desactivadas cuentan, y el tema sin preguntas activas tiene `AvailableQuestionCount = 0`.
- [ ] Solo cuentan correcciones `Completed`; la media, la mejor nota por supuesto y las últimas 10 son correctas, y nada de otra oposición aparece.
- [ ] `GET /me/activity` devuelve `from` y `to` inclusive, la XP y el objetivo de cada día, los días protegidos, y omite las filas sin actividad.
- [ ] Los errores de la tabla devuelven el código y la clave indicados.
- [ ] Los datos de un usuario no dependen de los de otro, y sin token se recibe 401.
- [ ] `has-pending-model-changes` no detecta cambios de modelo.
- [ ] La app muestra los cuatro bloques, recarga al volver de un test y «Practicar este tema» abre el test.
- [ ] Los estados de la tabla se cumplen.
- [ ] Capturas en tema claro y oscuro y con `font_scale 1.3`: nada se corta, y ningún estado depende solo del color.
- [ ] Build sin warnings y tests en verde.

## Plan de tests
- **Unitarios (Mobile.Core):**
  - `ActivityCalendar`: huecos del primer lunes, meses de 28 a 31 días (incluido febrero bisiesto), marcas, futuros, resumen y límites de navegación.
  - `StatsTexts`: porcentajes, redondeo, `—`, singulares y plurales, fechas.
  - `StatsViewModel`: carga, vacío, parcial, sin oposiciones, sin conexión con y sin datos, error de cada petición por separado, cambio de mes y de oposición con respuestas desordenadas, ver todos o menos, practicar tema (éxito, sin conexión, deshabilitado), abrir corrección y navegación.
  - `HomeViewModel`: abre `Routes.Stats`.
- **Integración (`StatsEndpointsTests`):** todos los criterios de API, con dos usuarios y dos oposiciones; rangos de 92 y 93 días, `from = to` y `DateOnly.MaxValue`; `XpEarned` en `GET /me/streak`.
- **Manual (emulador, con `AppHost`):**
  1. Usuario sin datos → vacío → «Hacer un test».
  2. Con datos: los cuatro bloques, ver todos los temas, practicar tema y volver (se recarga).
  3. Calendario: mes anterior y siguiente, detalle de un día.
  4. Modo avión con y sin datos cargados.
  5. Capturas claro, oscuro y `font_scale 1.3`.
