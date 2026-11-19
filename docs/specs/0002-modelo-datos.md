# 0002 — Modelo de dominio y persistencia con EF Core

- **Issue:** #12
- **Milestone:** v0.1.0-alpha
- **Estado:** implementada

## Objetivo
Crear las entidades del núcleo de tests (oposiciones, temas, preguntas con aclaración, usuarios e intentos de test) con sus reglas en Domain, y guardarlas en PostgreSQL con EF Core 10. Es la base de las issues #13 (importador), #15 (auth) y #16 (endpoints).

## Alcance
**Incluye:**
- Entidades y reglas en `OpoStudy.Domain`, sin dependencias.
- `OpoStudyDbContext`, configuraciones y migración inicial en `OpoStudy.Infrastructure`.
- Registro del DbContext en la Api y el Worker con la integración cliente de Aspire, y migración automática en Development.
- Tests unitarios de dominio, tests de persistencia con Testcontainers y adaptación de los tests de la Api.

**No incluye:**
- Endpoints (#16), auth (#15) ni el importador (#13).
- Búsqueda full-text (`tsvector`), gamificación ni rachas (v0.2), ni supuestos (v0.3).
- Datos semilla: el contenido llegará con el importador.

## Diseño

### Dominio (`src/OpoStudy.Domain/`, carpetas por agregado)
Reglas comunes:
- Ids `Guid` generados en el dominio con `Guid.CreateVersion7()`.
- Propiedades con `private set` y colecciones expuestas como `IReadOnlyList<T>` sobre un campo privado.
- Constructor privado sin parámetros para EF y fábricas estáticas `Crear(...)` que validan.
- Una regla incumplida lanza `ReglaDominioException(string codigo, string mensaje)`. El `codigo` es estable (p. ej. `pregunta.sin_opcion_correcta`) y en #16 se traducirá a ProblemDetails.
- El tiempo llega como parámetro `DateTimeOffset ahora`: el dominio nunca llama a `DateTime.Now`. Fuera del dominio se usa `TimeProvider`.

| Entidad | Campos | Reglas |
|---|---|---|
| `Oposicion` | `Id`, `Slug`, `Nombre`, `Organismo`, `Grupo` (p. ej. "C1"), `Activa` | El slug va en kebab-case y no puede estar vacío |
| `Tema` | `Id`, `OposicionId`, `Slug`, `Numero`, `Titulo`, `Bloque?` | `Numero` > 0 |
| `Pregunta` | `Id`, `TemaId`, `Clave` (id estable que viene del contenido), `Enunciado`, `Aclaracion`, `ReferenciaLegal`, `Activa`, `Opciones` | Ningún texto vacío; de 2 a 6 opciones; **exactamente una correcta**; `Orden` de las opciones único. Las preguntas no se borran, se desactivan (`Activa = false`) para conservar el historial. Un método `Actualizar(...)` revalida todo y empareja las opciones por orden (lo usará el importador). No puede cambiar el número de opciones (`pregunta.cambio_numero_opciones`), porque las respuestas ya dadas apuntan a ellas: en ese caso se crea otra pregunta y se desactiva esta. |
| `Opcion` | `Id`, `PreguntaId`, `Orden`, `Texto`, `EsCorrecta` | Solo se crea a través de `Pregunta` |
| `Usuario` | `Id`, `FirebaseUid`, `FechaAlta`, `OposicionActivaId?` | `FirebaseUid` no puede estar vacío; `ElegirOposicion(id)` |
| `IntentoTest` | `Id`, `UsuarioId`, `OposicionId`, `TemaId?` (null = aleatorio), `Modo` (`PorTema` \| `Aleatorio`), `FechaInicio`, `FechaFin?`, `Respuestas` | `Crear(usuarioId, oposicionId, temaId, modo, preguntas, ahora)` necesita entre 1 y 100 preguntas distintas y crea una `RespuestaUsuario` vacía por pregunta, en orden. `Responder(pregunta, opcionId, ahora)` → `ResultadoRespuesta(EsCorrecta, OpcionCorrectaId)`. Responder falla si la pregunta no está en el intento, si ya está respondida, si la opción no es de esa pregunta o si el intento ya está finalizado. Cuando se responde la última, fija `FechaFin`. Expone `Aciertos`, `Fallos` y `EstaFinalizado`. |
| `RespuestaUsuario` | `IntentoTestId`, `PreguntaId`, `Orden`, `OpcionElegidaId?`, `EsCorrecta?`, `RespondidaEn?` | Solo se modifica a través de `IntentoTest` |

### API
No hay endpoints nuevos. Sí cambia `Program.cs`:
- `builder.AddNpgsqlDbContext<OpoStudyDbContext>("opostudy", configureDbContextOptions: o => o.UseSnakeCaseNamingConvention())` (integración cliente de Aspire, que añade health check, reintentos y trazas).
- En Development: `await app.AplicarMigracionesAsync()` (extensión en Infrastructure). En el VPS se usará un *migration bundle* (v0.5).
- El Worker registra el mismo DbContext pero **no** migra.

### Datos
- Paquetes, todos en `Directory.Packages.props`:
  - `Npgsql.EntityFrameworkCore.PostgreSQL` y `EFCore.NamingConventions` (Infrastructure).
  - `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` (Api y Worker), en la versión alineada con Aspire 17.
  - `Microsoft.EntityFrameworkCore.Design` (solo la Api, `PrivateAssets=all`).
  - `dotnet-ef` como herramienta local en `dotnet-tools.json` (`dotnet tool restore`).
  - `CentralPackageTransitivePinningEnabled` fija EF Core (`Microsoft.EntityFrameworkCore` y `.Relational`), para que Npgsql y Design no traigan versiones distintas.
- Configuraciones con `IEntityTypeConfiguration<T>` en `Infrastructure/Persistencia/Configuraciones/`. Tablas y columnas en snake_case.
- `Pregunta.Opciones` e `IntentoTest.Respuestas` se cargan siempre con la entidad (`AutoInclude`).
- Las migraciones generadas se marcan como `generated_code` en `.editorconfig`, para que los analizadores no las revisen.
- Longitudes máximas: `Slug` 100, `Nombre`/`Titulo` 300, `Enunciado` 2000, `Opcion.Texto` 1000, `Aclaracion` 4000, `ReferenciaLegal` 300, `Clave` 150, `FirebaseUid` 128. `Modo` se guarda como texto.
- Índices únicos:
  - `oposiciones(slug)`
  - `temas(oposicion_id, slug)`
  - `temas(oposicion_id, numero)`
  - `preguntas(clave)`
  - `opciones(pregunta_id, orden)`
  - `usuarios(firebase_uid)`
  - `respuestas_usuario(intento_test_id, orden)`
- Índices simples: `preguntas(tema_id)` e `intentos_test(usuario_id, fecha_inicio)`.
- Claves y borrados:
  - PK compuesta de `respuestas_usuario`: `(intento_test_id, pregunta_id)`.
  - Al borrar un usuario se borran sus intentos y respuestas en cascada (lo necesita `DELETE /me` en #15).
  - Las FKs hacia `preguntas`, `opciones`, `temas` y `oposiciones` desde los intentos son `Restrict`.
- Migración inicial: `dotnet ef migrations add Inicial --project src/OpoStudy.Infrastructure --startup-project src/OpoStudy.Api --output-dir Persistencia/Migraciones`.

### App
Sin cambios.

## Seguridad
- No hace falta ningún secreto nuevo. En local, Aspire genera la contraseña de Postgres en los user-secrets del AppHost; en los tests la pone Testcontainers.
- `FirebaseUid` es un identificador personal, así que no se escribe en logs.
- Las consultas siempre van parametrizadas (EF Core): nada de `FromSqlRaw` con concatenaciones.
- El borrado en cascada de `Usuario` deja lista la base del RGPD (borrado de cuenta).

## Criterios de aceptación
- [ ] Domain sigue sin dependencias externas (tests de arquitectura en verde, incluida la regla de que no dependa de EF Core).
- [ ] `dotnet run --project src/OpoStudy.AppHost` crea las tablas en Postgres y `/health` responde Healthy con el check de la base de datos.
- [ ] El modelo no tiene cambios pendientes de migrar (`HasPendingModelChanges()` es `false`, comprobado en un test).
- [ ] Las invariantes de `Pregunta` e `IntentoTest` tienen tests unitarios.
- [ ] Los tests de persistencia corren contra Postgres real (Testcontainers), en local y en CI.
- [ ] Build sin warnings y CI en verde.

## Plan de tests
- **Unitarios:** nuevo proyecto `tests/OpoStudy.Domain.Tests`.
  - `Pregunta`: ninguna correcta, dos correctas, menos de 2 o más de 6 opciones, textos vacíos.
  - `IntentoTest`: responder una pregunta ajena, responder dos veces, una opción de otra pregunta, un intento finalizado; aciertos y fallos; `FechaFin` al responder la última.
- **Integración de persistencia:** nuevo proyecto `tests/OpoStudy.Infrastructure.Tests` con `Testcontainers.PostgreSql`.
  - Un contenedor compartido mediante una fixture de colección; la base se migra una vez.
  - Casos: ida y vuelta de un `IntentoTest` con sus respuestas, violación de los índices únicos (`preguntas.clave`, `usuarios.firebase_uid`), borrado en cascada del usuario y `HasPendingModelChanges()`.
- **Api:** los tests actuales pasan a una `ApiFactory` (WebApplicationFactory + contenedor Postgres) que inyecta `ConnectionStrings:opostudy`.
- **Manual:** arrancar el AppHost y comprobar en el dashboard que la Api está sana y que las tablas existen.
