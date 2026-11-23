# 0017 — Supuestos prácticos reales

- **Issue:** #53
- **Milestone:** v0.3.0
- **Estado:** lista

> Segunda spec de la v0.3.0. Parte del modelo de la spec 0016 (`PracticalCase`, `PracticalCasePart`, `GradingCriterion`, `ModelAnswer`). Puede hacerse en paralelo a la 0018.

## Objetivo
Sustituir el supuesto de ejemplo por un banco de supuestos que se parezca al tercer ejercicio real de Programador/a C1: uno por bloque de materias específicas, sobre 25 puntos y combinando formatos. Guardar el bloque de cada supuesto para que la app (0020) los agrupe como en el examen. Ampliar la skill `content-author` para redactar y verificar supuestos con el mismo rigor que las preguntas.

## Formato oficial del ejercicio práctico
Fuente: base octava de las bases generales (OEP 2020-2023), redactada por el acuerdo de Junta de Gobierno de 22-07-2026, punto III.5. Publicada en el **BOP de Málaga nº 145, de 29 de julio de 2026**, CVE 20260729-02972-2026, págs. 23-26, verificable en www.bopmalaga.es/cve.php. El texto del tercer ejercicio coincide con el original del BOP nº 234, de 11-12-2023.

- Subgrupo C1: el tribunal propone **tres supuestos, uno por cada bloque** (II-A, II-B y II-C) de las materias específicas, y el aspirante **resuelve uno**.
- Formato a elección del tribunal: «el análisis de un supuesto, la preparación de un informe; la contestación de preguntas con respuestas breves; test multirrespuesta, etc.».
- El tribunal fija el tiempo al empezar la prueba y anuncia antes si se pueden consultar textos o material.
- Puntuación: **25 puntos**; mínimo **12,5** para superarlo. Si el supuesto es a desarrollar, la nota es la media de las de los miembros del tribunal.
- Los otros dos ejercicios de C1 son tests: el primero, de 55 preguntas en 50 minutos (bloque I, 20 puntos); el segundo, de 75 preguntas en 60 minutos (bloque II, 25 puntos). Los fallos restan la mitad de un acierto.
- No hay supuestos de convocatorias anteriores publicados.

Consecuencia: la corrección con LLM (0018/0019) sirve para los formatos desarrollados (análisis, informe y respuesta breve). Un supuesto tipo test (enunciado común con preguntas de test) queda fuera de la v0.3.0.

## Alcance
**Incluye:**
- Campo opcional `section` en `PracticalCase`: contenido, schema, dominio, migración, importador y DTOs.
- Cinco supuestos nuevos y la revisión del 0001 (ver la tabla): 6 en total, 2 por bloque, todos de **25 puntos**.
- Nueva sección «Supuestos prácticos» en la skill `content-author`, que además actualiza su `description`.
- `docs/specs/0004-temas-programador-c1.md`: citar el BOP nº 145/2026 (base octava, sistema selectivo).

**No incluye:**
- Supuestos tipo test ni simulacros de examen con tiempo.
- Cambios en las reglas de la 0016 (límites, `key`, número de apartados fijo, sin `criteria`/`modelAnswer` en la API).
- App (0020), envío y corrección (0018/0019).
- Vincular supuestos con temas concretos (solo el bloque).

## Diseño

### Dominio
- `PracticalCase` gana `Section` (`string?`). Se valida igual que `Topic.Section`: `Guard.OptionalText(section, Limits.Name, "practical_case.invalid_section")`.
- `Create(examId, key, title, section, statement, estimatedMinutes, parts)` y `Update(examId, title, section, statement, estimatedMinutes, parts)`. Cambiar `section` es una actualización normal.
- Nuevo código `practical_case.invalid_section` (mensaje en español, como `topic.invalid_section`).

### Contenido
- `practical-case.schema.json`: propiedad opcional `"section": { "type": "string", "minLength": 1, "maxLength": 300, "pattern": "\\S" }`, igual que en `exam.schema.json`.
- `ContentReader` la lee (ruta de error `$.section`) y `ContentImporter` la incluye en el upsert. Si solo cambia `section`, cuenta como una actualización.
- Valores: exactamente el `section` de los temas en `exam.json` (`Bloque II-A. Materias específicas`, `Bloque II-B. …`, `Bloque II-C. …`). Así la app puede relacionarlos sin tablas nuevas.

### Datos
- Columna `section` varchar(300) nullable en `practical_cases`. Migración `AddPracticalCaseSection` (skill `new-ef-migration`).

### API
Sin rutas nuevas. Los DTOs ganan `string? Section`:
```csharp
public sealed record PracticalCaseSummaryResponse(Guid Id, string Title, string? Section, int PartCount, int MaxPoints, int EstimatedMinutes);
public sealed record PracticalCaseResponse(Guid Id, Guid ExamId, string Title, string? Section, string Statement, int EstimatedMinutes, int MaxPoints, IReadOnlyList<PracticalCasePartResponse> Parts);
```
El listado sigue ordenado por `Key`. Las `key` 0001-0006 no siguen el orden de los bloques; la app agrupará por `Section` en la 0020.

### Supuestos
| Fichero (`content/exams/malaga-programador-c1/practical-cases/`) | Bloque | Temas del BOP | Contenido |
|---|---|---|---|
| `0001-gestion-subvenciones.json` (revisión, misma `key`) | II-B | 12, 15 | Reorientado a .NET. Mantiene **3 apartados** (es obligatorio): 1) capas y entidades (MVC, EF Core); 2) consulta LINQ/EF Core en lugar de SQL puro; 3) endpoint con validación y ProblemDetails. Pasa de 20 a 25 puntos. |
| `0002-…` | II-A | 2 | Modelo E-R de un caso de la Diputación, paso a tablas, consultas SQL (JOIN, GROUP BY/HAVING, subconsulta) e índices o transacciones razonados. |
| `0003-…` | II-A | 4, 7 | Informe: categorización ENS de un sistema (dimensiones y nivel), medidas aplicables, y base jurídica y derechos RGPD/LOPDGDD del tratamiento. |
| `0004-…` | II-B | 18, 20, 21 | Equipo que adopta Scrum: estrategia de ramas Git, pruebas JUnit, pipeline Jenkins con SonarQube y Nexus, y respuestas breves sobre artefactos ágiles. |
| `0005-…` | II-C | 23, 25, 26 | Integración de un registro municipal con SIR y Notifica: flujo de un asiento, expediente y documento electrónico (ENI) y plazos de las notificaciones según la Ley 39/2015. |
| `0006-…` | II-C | 27, 29, 30 | Facturación electrónica de un proveedor: obligación (Ley 25/2013), FACe, estructura Facturae y validación de firma con @firma. |

Reglas comunes:
- Formato del contenido: enunciado general (`statement`) con el contexto y de **2 a 4 apartados** que combinen formatos: análisis o diseño, informe breve y preguntas de respuesta breve.
- Puntos: la suma de todos los criterios es **25**. Criterios con puntos enteros de 1 a 10 (normalmente de 1 a 5), dentro de los límites de la 0016.
- `estimatedMinutes`: entre 60 y 90, según la extensión. El tiempo real lo fija el tribunal.
- Nombres y datos ficticios pero verosímiles (la Diputación, ayuntamientos de la provincia). Ningún dato personal real.
- Los slugs de los ficheros los elige quien implementa (kebab-case, en español). Las `key` son `malaga-programador-c1/practical-cases/000N`.

### Skill `content-author`
Añadir una sección «Supuestos prácticos» a `.claude/skills/content-author/SKILL.md` (la de preguntas se queda como está) y ampliar la `description` a «preguntas de test y supuestos prácticos». Reglas:
1. **Ámbito:** un bloque por supuesto y temas del enunciado oficial del BOP de ese bloque.
2. **Rúbrica pensada para corregir solo con la respuesta:**
   - Cada criterio comprueba **una** cosa observable («Define la clave ajena de solicitudes a ayuntamientos»), no valoraciones vagas («buena presentación»).
   - Los criterios no se solapan.
   - La suma es 25.
3. **Respuesta modelo:** cubre **todos** los criterios de su apartado. Es una respuesta de opositor bien hecha, no un temario.
4. **Normativa:** sale del texto consolidado del BOE, igual que las preguntas (sección 1). Se cita el artículo en la respuesta modelo, p. ej. «(Ley 39/2015, art. 43.2)», y nunca se inventa.
   - Para lo técnico sin norma (SIR, Notifica, @firma, Facturae), solo se usan hechos de las fuentes oficiales (BOE, PAe/administracionelectronica.gob.es). Lo dudoso se anota para la revisión humana.
5. **Verificación obligatoria y separada de la redacción:**
   - **SQL:** se ejecuta contra PostgreSQL en un contenedor temporal (`docker run --rm postgres:17`), con un script de datos en el scratchpad.
   - **C#:** se compila en un proyecto temporal del scratchpad.
   - **Java y otros lenguajes:** se revisan a mano y se marca qué no se ha ejecutado.
   - Se revisa que cada criterio esté cubierto por la respuesta modelo y que los puntos sumen 25.
6. **Validación:** `--validate-only` del importador y `test-runner`, como en las preguntas. Nunca se hace merge sin la revisión humana de los supuestos.

## Seguridad
Sin cambios respecto a la 0016: rutas autenticadas, contenido sin datos personales ni secretos y textos limitados por el dominio. `section` también la limita el dominio (300).

## Criterios de aceptación
- [ ] Hay 6 supuestos en `practical-cases/`, 2 por bloque (II-A, II-B y II-C), todos con `section` y `MaxPoints` = 25.
- [ ] El 0001 conserva su `key` y sus 3 apartados. Importarlo sobre una BD que tenga la versión anterior lo actualiza sin `part_count_changed`.
- [ ] Toda la normativa citada existe y dice eso en el texto consolidado vigente. Las dudas quedan listadas en el PR para la revisión humana.
- [ ] El SQL de las respuestas modelo se ha ejecutado en PostgreSQL y el C# compila. El PR indica qué código no se ha ejecutado.
- [ ] `section` inválido (vacío o de más de 300 caracteres) → `ContentError` en `$.section` y `practical_case.invalid_section` en el dominio.
- [ ] Un supuesto sin `section` sigue siendo válido.
- [ ] `GET /exams/{id}/practical-cases` y `GET /practical-cases/{id}` devuelven `section`.
- [ ] Cambiar solo `section` cuenta como actualización en `ImportReport`. Una segunda importación sin cambios no cuenta nada.
- [ ] La skill `content-author` tiene la sección de supuestos y su `description` la menciona.
- [ ] La spec 0004 cita el BOP nº 145/2026.
- [ ] El AppHost importa los 6 supuestos al arrancar.

## Plan de tests
- **Unitarios (dominio):** `Create` y `Update` con `section` válida, nula y no válida (vacía o de 301 caracteres → `practical_case.invalid_section`); `Update` que solo cambia `section`.
- **`ContentReaderTests`:** supuesto con `section`; `section` vacío → error en `$.section`.
- **`ContentImporterTests`:** cambio solo de `section` → actualización; segunda importación sin cambios.
- **`RepositoryContentTests`:** los 6 ficheros validan con `ContentReader` y con el JSON Schema. Test nuevo: todo supuesto del repo tiene `section` igual a la `section` de algún tema de su oposición y `MaxPoints` = 25.
- **Integración API (`PracticalCasesEndpointsTests`):** `section` presente en el listado y en el detalle; ampliar `ContentSeeder` si hace falta.
- **Manual:** arrancar el AppHost, comprobar los 6 supuestos en `practical_cases` (con `section`) y abrir un detalle en Scalar.

## Decisiones de implementación
- `section` en blanco: el dominio la normaliza a `null` igual que `Topic.Section` (`Guard.OptionalText`), así que `practical_case.invalid_section` solo salta con más de 300 caracteres. En el contenido, un `section` vacío sigue siendo un error: lo rechazan el JSON Schema y `ContentReader` en `$.section`.
