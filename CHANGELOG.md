# Changelog

Todos los cambios relevantes de este proyecto se documentan en este fichero.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto usa [Versionado Semántico](https://semver.org/lang/es/).

## [Unreleased]

## [0.3.0] - 2026-11-23

Supuestos prácticos: contenido real por bloque, redacción en la app y corrección automática con Gemini, con resultado detallado, historial y XP.

### Añadido

- Supuestos prácticos: entidades, formato JSON con su esquema, importación y endpoints de lectura (#51).
- Bloque opcional en los supuestos y seis supuestos de 25 puntos, dos por bloque II-A, II-B y II-C (#53).
- Envío de respuestas y cola de corrección en el Worker (#55).
- Corrección con Gemini detrás de `ILlmCorrector` (#57).
- App: lista de supuestos, editor con borrador y envío a corrección (#59).
- App: resultado detallado e historial de correcciones; XP y objetivo diario al completar una corrección (#61).

### Corregido

- Correcciones atascadas y validaciones del envío (#55).
- Apartado nulo en la salida de Gemini (#57).
- Polling de la corrección ante errores 4xx y cuota agotada, y orden del borrador tras la revisión (#59).
- Reintento ante la creación simultánea de la actividad del día al completar una corrección (#61).

## [0.2.0] - 2026-11-23

Hábito de estudio: objetivo diario, rachas, XP y niveles, repaso de falladas, recordatorios y modo offline.

### Añadido

- Objetivo diario y rachas, con zona horaria del usuario (#37).
- XP y niveles, con tarjeta de nivel en inicio y en el test (#38).
- Repaso de preguntas falladas, confirmación al salir del test y tarjeta de test a medias (#39).
- Recordatorio diario con notificaciones locales (#40).
- Modo offline: contenido en caché, cola de sincronización de tests y banner sin conexión (#41).
- Protectores de racha pagados con XP (#43).

### Corregido

- Recarga de la racha al volver a la pantalla de inicio (#37).
- XP del test oculto en el resumen si no se ha ganado nada (#38).
- Confirmación al salir con la flecha de la barra en la pantalla de test (#39).
- Ventana de 15 minutos para la alarma del recordatorio y cancelación al caducar la sesión (#40).
- 409 si dos sincronizaciones crean el mismo test a la vez y recarga del inicio al recuperar la conexión (#41).

## [0.1.0] - 2026-11-20

Primera versión: test de la oposición de Programador/a C1 (Diputación de Málaga) con aclaración de cada respuesta.

### Añadido

- Modelo de dominio y persistencia con EF Core (#22).
- Formato JSON del contenido e importador idempotente (#23).
- Temario de Programador/a C1 y primeras 50 preguntas (#24).
- Autenticación con Firebase en la API (#25).
- Endpoints de oposiciones, temas y tests con aclaración (#26).
- Base de la app y sistema de diseño (#27).
- Login en la app con Google y correo (#28).
- Selección de oposición y pantalla de test con aclaración (#29).

### Corregido

- Ajustes de la revisión de la pantalla de test (#32).
- Mensaje cuando el login con Google falla por un error de Google (#33).

### Mantenimiento

- Setup del repositorio, solución y CI (#2).
- Una sola aprobación por release, tests de Scalar y Dependabot sin majors de Microsoft (#10).
- Versionado SemVer sin sufijo `-alpha` (#34).
- Actualización de dependencias: GitHub Actions (`actions/checkout`, `actions/setup-dotnet`, `actions/setup-java`, `github/codeql-action`) y paquetes de Microsoft y OpenTelemetry (#3, #4, #5, #6, #8, #11).

[Unreleased]: https://github.com/jb-coder/opostudy/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/jb-coder/opostudy/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/jb-coder/opostudy/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/jb-coder/opostudy/releases/tag/v0.1.0
