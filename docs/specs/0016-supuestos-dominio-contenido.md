# 0016 — Supuestos prácticos: dominio, contenido y lectura

- **Issue:** #51
- **Milestone:** v0.3.0
- **Estado:** lista

> Primera spec de la v0.3.0 (supuestos corregidos con LLM). Orden del milestone: 0016 dominio y contenido → 0017 supuestos reales → 0018 envío y cola de corrección (API + Worker, corrector falso) → 0019 proveedor Gemini detrás de `ILlmCorrector` → 0020 app: supuestos y envío → 0021 app: resultado e historial.
> Términos del glosario del ADR 0001: `PracticalCase`, `PracticalCasePart`, `GradingCriterion`, `ModelAnswer`, `Grading`.

## Objetivo
Los supuestos prácticos se guardan como contenido versionado en `content/` (como las preguntas, spec 0003) y se importan en PostgreSQL con el importador existente. Cada supuesto tiene apartados, y cada apartado una **rúbrica** (criterios con puntos) y una **respuesta modelo**: son la base con la que el LLM corregirá en 0018/0019. La API expone los supuestos para leerlos, sin la rúbrica ni la respuesta modelo.

## Alcance
**Incluye:**
- Entidades `PracticalCase` y `PracticalCasePart` (con `GradingCriterion` como valor) en `OpoStudy.Domain/PracticalCases/`.
- Configuración EF Core y migración `AddPracticalCases` (skill `new-ef-migration`).
- JSON Schema `content/schema/practical-case.schema.json` y ampliación de `ContentReader` y `ContentImporter`.
- `GET /exams/{examId}/practical-cases` y `GET /practical-cases/{id}` (skill `new-endpoint`), DTOs en `OpoStudy.Contracts/PracticalCases/`.
- Un supuesto en `content/exams/malaga-programador-c1/practical-cases/` para que el AppHost y `RepositoryContentTests` tengan contenido real.

**No incluye:**
- Envío de respuestas, cola, cuota y Worker (0018). `GradingWorker` no se toca.
- `ILlmCorrector` y cualquier proveedor LLM (0019).
- App: ni pantallas, ni cliente Refit, ni `ApiJsonContext` (0020).
- Supuestos en el contenido offline (`/offline-content`): corregir exige servidor.
- Redactar los supuestos definitivos ni revisar el formato del ejercicio práctico en la convocatoria (0017).
- Vincular supuestos con temas, dificultad o etiquetas.

## Diseño

### Contenido
Un fichero por supuesto en `content/exams/<exam-slug>/practical-cases/<nombre>.json`. El nombre del fichero es libre (convención `0001-gestion-subvenciones.json`).

```json
{
  "$schema": "../../../schema/practical-case.schema.json",
  "key": "malaga-programador-c1/practical-cases/0001",
  "title": "Gestión de solicitudes de subvenciones",
  "statement": "La Diputación quiere una aplicación para…",
  "estimatedMinutes": 45,
  "parts": [
    {
      "statement": "Diseña el modelo de datos relacional…",
      "modelAnswer": "Tablas: solicitudes(id, …)…",
      "criteria": [
        { "description": "Identifica las entidades principales y sus claves primarias", "points": 2 },
        { "description": "Las relaciones y claves ajenas son correctas", "points": 2 }
      ]
    }
  ]
}
```
- Mismas reglas que spec 0003: camelCase, propiedades desconocidas = error, `pattern: "\\S"` en los textos, `key` con el patrón `^[a-z0-9-]+(/[a-z0-9-]+)*$` y estable para siempre (convención `<exam>/practical-cases/<nnnn>`).
- Los textos son **texto plano**; los saltos de línea se conservan (puede haber código o SQL). Cómo se muestran se decide en 0020.
- Los apartados se identifican por su **posición** (`order` 0..n-1). Los criterios pueden cambiar libremente: la corrección (0018) guardará una copia de los criterios con los que se corrigió.

### Dominio (`OpoStudy.Domain/PracticalCases/`)
- **`PracticalCase`** (agregado): `Id` (v7), `ExamId`, `Key`, `Title`, `Statement`, `EstimatedMinutes`, `IsActive`, `Parts` (ordenadas por `Order`), `MaxPoints` = suma de los apartados.
  - `Create(examId, key, title, statement, estimatedMinutes, IReadOnlyList<PracticalCasePartData> parts)`.
  - `Update(examId, title, statement, estimatedMinutes, parts)`: valida todo antes de cambiar nada (como `Question.Update`). **El número de apartados no puede cambiar** → `practical_case.part_count_changed` (las respuestas de 0018 apuntarán a los apartados): se crea otro supuesto con `key` nueva.
  - `Activate()` / `Deactivate()`. Nunca se borra.
- **`PracticalCasePart`**: `Id` (v7), `PracticalCaseId`, `Order`, `Statement`, `ModelAnswer`, `Criteria` (`IReadOnlyList<GradingCriterion>` en el orden del contenido), `MaxPoints` (= suma de `Points`, se guarda en columna para poder sumarlo en SQL).
- **`GradingCriterion`**: `record` o tipo valor con `Description` y `Points`.
- Datos de entrada: `PracticalCasePartData(string Statement, string ModelAnswer, IReadOnlyList<GradingCriterionData> Criteria)` y `GradingCriterionData(string Description, int Points)`.
- **Límites** (añadir a `Limits`): título = `Limits.Name` (300); `CaseStatement` 8000; enunciado de apartado = `Limits.Statement` (2000); `ModelAnswer` 8000; `CriterionDescription` 500; apartados 1..10; criterios por apartado 1..10; puntos por criterio 1..10 (enteros); `EstimatedMinutes` 5..240.
- **Códigos de error** (`DomainRuleException`, mensajes en español): `practical_case.invalid_key`, `practical_case.invalid_title`, `practical_case.invalid_statement`, `practical_case.invalid_estimated_minutes`, `practical_case.invalid_part_count`, `practical_case.part_count_changed`, `practical_case.invalid_part_statement`, `practical_case.invalid_model_answer`, `practical_case.invalid_criterion_count`, `practical_case.invalid_criterion_description`, `practical_case.invalid_criterion_points`.

### Datos
- `practical_cases`: `id` uuid PK, `exam_id` FK → `exams` (índice), `key` varchar(150) **único**, `title` varchar(300), `statement` varchar(8000), `estimated_minutes` int, `is_active` bool.
- `practical_case_parts`: `id` uuid PK, `practical_case_id` FK → `practical_cases` (cascade), `order` int, `statement` varchar(2000), `model_answer` varchar(8000), `max_points` int, `criteria` **jsonb** (colección compleja u owned con `ToJson()`; lo que admita EF Core 10 sin warnings). Único `(practical_case_id, order)`.
- Migración `AddPracticalCases`. Nombres en snake_case según las convenciones existentes (`OpoStudyDbContext.ApplyConventions`).

### Lector e importador (`OpoStudy.Infrastructure/Content`)
- `ContentReader` acepta `exams/<slug>/practical-cases/*.json` (hoy cualquier ruta fuera de la estructura es error) y lo valida con `System.Text.Json` (`required`, `UnmappedMemberHandling.Disallow`) + `PracticalCase.Create`, convirtiendo cada `DomainRuleException` en `ContentError` con la ruta JSON (p. ej. `$.parts[1].criteria[0].points`).
- Errores entre ficheros: `key` de supuesto repetida en todo el contenido (se informa en el segundo fichero, por orden alfabético); mismas reglas de espacios al principio/final, symlinks y tamaño máximo (5 MB) que el resto.
- `ExamContent` gana la lista de supuestos de la oposición.
- `ContentImporter`, misma transacción: upsert de supuestos por `key` (si cambia de carpeta, cambia `ExamId`); los supuestos de las oposiciones importadas que ya no están en el contenido se **desactivan**; los que vuelven se reactivan. `part_count_changed` aborta la importación sin cambios parciales (`ContentImportException` con la `key`, código de salida 2). Una segunda importación sin cambios no modifica filas.
- `ImportReport` cuenta altas, actualizaciones, desactivaciones y reactivaciones de supuestos; la consola las imprime junto a las demás.

### API (`Features/PracticalCases/PracticalCasesEndpoints.cs`, tag `PracticalCases`)
| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/exams/{examId}/practical-cases` | Usuario autenticado | → `200 IReadOnlyList<PracticalCaseSummaryResponse>` (activos, ordenados por `Key`); `404 exam.not_found` si la oposición no existe o está inactiva |
| GET | `/api/v1/practical-cases/{id}` | Usuario autenticado | → `200 PracticalCaseResponse`; `404 practical_case.not_found` si no existe, está inactivo o su oposición está inactiva |

```csharp
public sealed record PracticalCaseSummaryResponse(Guid Id, string Title, int PartCount, int MaxPoints, int EstimatedMinutes);
public sealed record PracticalCaseResponse(Guid Id, Guid ExamId, string Title, string Statement, int EstimatedMinutes, int MaxPoints, IReadOnlyList<PracticalCasePartResponse> Parts);
public sealed record PracticalCasePartResponse(int Order, string Statement, int MaxPoints);
```
- **No se devuelven** ni los criterios ni la respuesta modelo: se mostrarán con la corrección (0021). No es por seguridad (el repo es público), sino para que el usuario responda sin verlos, como en el examen.
- `practical_case.not_found` se añade a `Api/Errors/DomainErrors` (mensaje: «El supuesto práctico no existe.»).
- Consultas `AsNoTracking` con proyección a DTO, como `ExamsEndpoints`.

### App
Sin cambios (0020).

### Supuesto del repo
El implementador redacta **un** supuesto plausible para Programador Informático C1 (p. ej. modelo de datos + consulta SQL + validación/endpoint en C#), con 2-4 apartados, rúbricas cuyos puntos sumen un total redondo (p. ej. 10) y respuestas modelo completas. Sin legislación inventada. En 0017 se revisará contra el formato real del ejercicio práctico.

## Seguridad
- Rutas bajo la política por defecto (JWT de Firebase); el contenido no es de ningún usuario, así que no hay autorización por recurso más allá de estar autenticado (igual que `/exams`).
- Sin datos personales ni secretos. Los textos los limita el dominio y el lector rechaza ficheros de más de 5 MB.
- `{id}` con restricción `:guid`.

## Criterios de aceptación
- [ ] `content/schema/practical-case.schema.json` valida el supuesto del repo y da autocompletado en el editor.
- [ ] Un supuesto inválido (sin criterios, puntos 0, propiedad desconocida, `key` repetida…) hace fallar `RepositoryContentTests` con fichero, ruta JSON y motivo.
- [ ] Importar dos veces no duplica nada y la segunda vez no cuenta altas ni actualizaciones de supuestos.
- [ ] Un supuesto que se quita del contenido queda desactivado y deja de salir en la API; si vuelve, se reactiva.
- [ ] Cambiar el número de apartados de un supuesto existente aborta la importación sin cambios parciales.
- [ ] `GET /exams/{id}/practical-cases` lista los activos con `PartCount`, `MaxPoints` y `EstimatedMinutes` correctos.
- [ ] `GET /practical-cases/{id}` devuelve enunciado y apartados sin criterios ni respuesta modelo (el JSON no contiene `criteria` ni `modelAnswer`).
- [ ] Anónimo → 401; oposición o supuesto inexistente/inactivo → 404 con ProblemDetails y `code`.
- [ ] El AppHost importa el supuesto al arrancar.
- [ ] Glosario del ADR actualizado (`PracticalCasePart`, `GradingCriterion`, `ModelAnswer`): ya escrito, se commitea en la rama de la feature.

## Plan de tests
- **Unitarios (`OpoStudy.Domain.Tests` o el proyecto de tests de dominio existente):** `PracticalCase.Create` válido (calcula `MaxPoints`); cada límite y código de error; `Update` cambia textos y criterios y recalcula `MaxPoints`; `Update` con distinto número de apartados lanza `part_count_changed` y deja el supuesto intacto; `Deactivate`/`Activate`.
- **`ContentReaderTests`:** supuesto válido; propiedad desconocida; `points` fuera de rango con ruta `$.parts[i].criteria[j].points`; `key` repetida entre dos ficheros; fichero en `practical-cases/` de una oposición sin `exam.json` (error de estructura).
- **`ContentImporterTests` (Testcontainers):** alta; segunda importación sin cambios; modificación de textos/criterios; desactivación y reactivación; `part_count_changed` sin cambios parciales; `dryRun`.
- **`RepositoryContentTests`:** el nuevo fichero valida con `ContentReader` y con su JSON Schema.
- **Integración API (`PracticalCasesEndpointsTests`, con `ContentSeeder` ampliado):** listado ordenado y sin inactivos; detalle sin `criteria`/`modelAnswer` en el JSON crudo; 404 de oposición y de supuesto (inexistente e inactivo); 401 anónimo.
- **Manual:** arrancar el AppHost, ver el supuesto en `practical_cases` y probar los dos endpoints en Scalar.

## Decisiones de implementación
- **Criterios en jsonb:** `OwnsMany(...).ToJson("criteria")` sobre un campo privado (`UsePropertyAccessMode(Field)`). `ComplexCollection` de EF Core 10 exige que la propiedad sea `IList<T>`, y el dominio expone `IReadOnlyList<GradingCriterion>`. La longitud de `Description` la valida el dominio (una columna JSON no tiene tamaño por propiedad). La columna queda `nullable` en la migración, pero el dominio siempre guarda al menos un criterio.
- **Rutas JSON de los errores del lector:** `PracticalCase.Create` no sabe en qué apartado falla, así que el lector valida cada elemento por separado con un supuesto auxiliar (el resto, valores válidos de relleno) y así informa de `$.parts[i].statement`, `$.parts[i].criteria[j].points`, etc. Los errores de nivel superior salen en `$.key`, `$.title`, `$.statement`, `$.estimatedMinutes` y `$.parts` (número de apartados).
- **Keys de supuestos:** se comprueban repetidas en un diccionario propio, distinto del de las preguntas (son tablas y claves únicas distintas).
- **`ExamContent`** gana el parámetro posicional `PracticalCases` (sin valor por defecto); `ImportReport` gana `PracticalCases` antes de `Warnings`.
- **Desactivación en el importador:** se desactivan los supuestos activos de las oposiciones importadas cuya `key` no está en ningún fichero del contenido (así un supuesto movido de carpeta cambia de `ExamId` y no se desactiva).
- **Supuesto del repo:** `0001-gestion-subvenciones.json`, 3 apartados (modelo relacional, consulta SQL y endpoint en C#), 20 puntos (8 + 6 + 6).
- **Dominio:** `PracticalCasePart.Criteria` y `MaxPoints` se rellenan en la propia entidad; `PracticalCasePartData`, `GradingCriterionData` y `GradingCriterion` están juntos en `GradingCriterion.cs`.
- **Endpoint de detalle:** `MaxPoints` se calcula en la API como suma de los apartados devueltos; el listado suma `max_points` en SQL.
