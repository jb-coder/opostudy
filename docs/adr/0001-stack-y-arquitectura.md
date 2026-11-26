# ADR 0001 — Stack y arquitectura inicial

- **Estado:** aceptado
- **Fecha:** 2026-11-18

## Contexto
OpoStudy es una app móvil para repasar oposiciones. Arranca con una sola: Programador Informático C1 de la Diputación de Málaga. Es un proyecto personal y didáctico con un requisito clave: **el coste mínimo**, es decir, un VPS y poco más. Solo pasará a producción si el producto lo merece. El repositorio es **público**.

## Decisiones

### Arquitectura
- **Monolito modular**: una API (ASP.NET Core 10, Minimal APIs organizadas por features), un Worker y PostgreSQL 17. No hay microservicios ni brokers de mensajes.
- **Capas**:
  - `Domain` no depende de nada.
  - `Infrastructure` contiene EF Core y los proveedores externos.
  - `Contracts` guarda los DTOs que comparten la API y la app.
  - Los tests de arquitectura (NetArchTest) hacen cumplir estas reglas.
- **.NET Aspire** orquesta el entorno local y aporta OpenTelemetry y los health checks (`ServiceDefaults`).

### Datos y contenido
- **PostgreSQL + EF Core** (migraciones *code-first*).
- **Contenido como datos versionados**: preguntas, opciones, aclaraciones y supuestos viven en JSON dentro de `content/`. Un importador los carga en la base de datos de forma idempotente.
- **Las aclaraciones de los tests se redactan de antemano y se sirven desde la base de datos.** No se usa ningún LLM para los tests.

### IA
- Se usa **solo para corregir supuestos prácticos**.
- El proveedor se configura detrás de `ILlmCorrector`. Al principio es Gemini (free tier); Claude u otros quedan como alternativa, y cambiar es solo cuestión de configuración.
- Las API keys viven únicamente en el backend.
- Las correcciones son asíncronas: una tabla hace de cola y el Worker la procesa con `FOR UPDATE SKIP LOCKED`.
- Cada usuario tiene una cuota mensual.

### Autenticación
- **Firebase Authentication** (Google, Facebook, correo), plan gratuito. La API solo valida el JWT de Firebase.

### App móvil
- **.NET MAUI**, Android primero e iOS en la última fase.
- MVVM con CommunityToolkit.Mvvm.
- Caché offline con SQLite.
- Las notificaciones de racha son locales.

### Infraestructura y coste
- **Versiones 0.x**:
  - VPS low-cost con Docker Compose y Caddy.
  - La API solo es accesible por **Tailscale**: no se expone a Internet.
  - Backups con `pg_dump` y restic cifrado a Cloudflare R2 (free tier).
- **Producción (opcional, desde 1.0.0)**: dominio con Cloudflare, Firebase App Check, LLM de pago, anuncios y suscripción.

### Repositorio público y secretos
- Nunca se versionan secretos:
  - En local: `dotnet user-secrets`.
  - En CI: GitHub Actions secrets.
  - En el VPS: Docker secrets.
- Hay varias defensas:
  - `.gitignore` estricto.
  - Hook `pre-commit` con gitleaks.
  - gitleaks en CI.
  - GitHub Secret Scanning y Push Protection.
- El harness local de agentes (`.claude/`, `CLAUDE.md`) no se publica.

### Flujo de trabajo
- **Gitflow** (`main`, `develop`, `feature/*`, `release/*`, `hotfix/*`).
- Todo cambio entra por PR, con CI obligatorio y revisión de un agente local antes del merge.
- **Milestones de GitHub = releases.** El APK se publica en GitHub Releases.
- **Versionado SemVer sin sufijos de fase**: `vMAJOR.MINOR.PATCH`.
  - Las versiones `0.x` ya indican que el proyecto está en desarrollo y puede cambiar sin aviso, así que no se usa `-alpha` ni `-beta`. En GitHub Releases se marcan como *pre-release*.
  - Cada milestone sube el MINOR (`v0.1.0`, `v0.2.0`…). Los hotfix suben el PATCH (`v0.1.1`).
  - `v1.0.0` es la primera versión estable: el rediseño accesible de la app (WCAG 2.2 AA en todas las pantallas, según `docs/diseno/sistema-diseno.md`).
  - `v1.1.0` es la publicación en tienda (Google Play).
  - Si hace falta probar un APK antes de cerrar una versión, se etiqueta como candidata: `v0.2.0-rc.1`.

### Roadmap
Milestones de GitHub (título y descripción tal como figuran allí):

| Milestone | Contenido |
|---|---|
| `v0.5.0` | Entrenador de examen: simulacro con tiempo y penalización, repaso espaciado, marcar y reportar preguntas, ajustes y endurecer la release |
| `v0.6.0` | Contenido 2.ª ronda: más preguntas por tema, más supuestos y supuestos tipo test |
| `v0.7.0` | Despliegue en VPS (Tailscale, Compose, backups, observabilidad) |
| `v1.0.0` | Rediseño visual de la app MAUI y accesibilidad WCAG 2.2 AA |
| `v1.1.0` | Producción (opcional): dominio, App Check, anuncios, premium, publicación en Play Store |
| `v1.2.0` | iOS |

### Idioma del código
- **El código se escribe en inglés**:
  - Identificadores, namespaces y comentarios.
  - Nombres de tests (`Method_Scenario_Result`).
  - Tablas y columnas, rutas de la API, nombres y tags de los endpoints, y códigos de error.
- **En español** queda lo que leen las personas: la interfaz de la app y los mensajes al usuario (también los de las excepciones que llegan a ProblemDetails), las specs, los ADR, el README, las issues, los PRs y los commits.
- **Glosario del dominio.** Un término nuevo se añade aquí antes de usarlo.

  | Español | Código |
  |---|---|
  | Oposición | `Exam` |
  | Tema | `Topic` |
  | Pregunta | `Question` |
  | Opción | `AnswerOption` |
  | Aclaración | `Explanation` |
  | Fuente (referencia legal o técnica) | `LegalReference` |
  | Usuario | `User` |
  | Test realizado (intento) | `QuizAttempt` (`Quiz`, para no confundirlo con los tests de código) |
  | Respuesta del usuario | `QuizAnswer` |
  | Racha | `Streak` |
  | Objetivo diario | `DailyGoal` |
  | Actividad diaria | `DailyActivity` |
  | Puntos de experiencia (XP) | `Xp` |
  | Nivel | `Level` |
  | XP disponible (para gastar) | `SpendableXp` |
  | Protector de racha | `StreakFreeze` |
  | Recordatorio diario | `DailyReminder` |
  | Pregunta fallada (pendiente de repaso) | `Mistake` |
  | Repaso (de falladas) | `Review` |
  | Contenido offline (paquete descargado de una oposición) | `OfflineContent` |
  | Respuesta pendiente (de sincronizar) | `PendingAnswer` |
  | Sincronización (de un test hecho sin conexión) | `QuizSync` |
  | Supuesto práctico | `PracticalCase` |
  | Apartado (de un supuesto) | `PracticalCasePart` |
  | Criterio de corrección (de la rúbrica de un apartado) | `GradingCriterion` |
  | Respuesta modelo (de un apartado) | `ModelAnswer` |
  | Borrador (de un supuesto) | `PracticalCaseDraft` |
  | Corrección (de un supuesto) | `Grading` |
  | Estado de una corrección | `GradingStatus` |
  | Respuesta a un apartado (dentro de una corrección) | `GradingAnswer` |
  | Puntuación de un criterio | `CriterionScore` |
  | Cuota mensual de correcciones | `GradingQuota` |
  | Corrector (proveedor LLM o falso) | `LlmCorrector` (`ILlmCorrector`) |
  | Estadísticas | `Stats` |
  | Porcentaje de aciertos | `Accuracy` |
  | Simulacro (de examen) | `MockExam` (un `QuizAttempt` con `Mode = MockExam`) |
  | Ejercicio (de la oposición, formato del simulacro) | `ExamExercise` |
  | Nota | `Score` |
  | Nota para aprobar | `PassScore` |
  | Penalización por fallo (fracción de un acierto) | `PenaltyRatio` |
  | En blanco | `Blank` |
  | Entrega (de un simulacro) | `Submission` |
  | Repaso espaciado | `SpacedReview` |
  | Programación de repaso (de una pregunta) | `ReviewSchedule` |
  | Caja (de Leitner) | `Box` |
  | Fecha de repaso | `DueOn` |

### Calidad
- **Tests**:
  - xUnit v3 (Microsoft Testing Platform), Shouldly y NSubstitute.
  - Testcontainers para los tests de integración contra PostgreSQL real.
  - NetArchTest para las reglas de arquitectura.
- **Build**:
  - `TreatWarningsAsErrors` y analizadores `latest-recommended`.
  - NuGet Audit bloquea los paquetes con vulnerabilidades.

## Consecuencias
- El coste fijo es casi nulo, a cambio de no tener alta disponibilidad, algo asumible en uso personal.
- Pasar a producción exige revisar la exposición de la API, `/health` incluido, la privacidad del proveedor de LLM y el RGPD.
- La compilación de iOS queda aplazada. El proyecto MAUI ya está preparado para añadir `net10.0-ios`.
