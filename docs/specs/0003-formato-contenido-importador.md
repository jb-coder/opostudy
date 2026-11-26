# 0003 — Formato JSON del contenido e importador

- **Issue:** #13
- **Milestone:** v0.1.0
- **Estado:** lista

> El código va en inglés según el glosario del ADR 0001. El **texto del contenido** (enunciados, aclaraciones…) está en español.

## Objetivo
El temario y las preguntas se guardan como JSON versionado en el repo, se revisan por PR y se cargan en PostgreSQL con un importador **idempotente**. Si el contenido es inválido, el CI falla con un mensaje que indica el fichero y el campo.

## Alcance
**Incluye:**
- Formato y JSON Schema del contenido en `content/`.
- Lectura y validación del contenido, e importación (upsert) en `OpoStudy.Infrastructure/Content`.
- Consola `OpoStudy.ContentImporter`, que el AppHost lanza en local.
- Tests: lectura y validación, importación contra Postgres real, y validación de todo `content/` del repo.
- Contenido inicial: solo `exam.json` de Programador Informático C1 (Diputación de Málaga), sin temas. Los temas y las preguntas llegan en #14.

**No incluye:**
- Redactar preguntas (#14).
- Un endpoint de administración para importar: se usa la consola.

## Diseño

### Estructura de `content/`
```
content/
  schema/
    exam.schema.json        # JSON Schema (draft 2020-12)
    topic.schema.json
  exams/
    <exam-slug>/
      exam.json             # datos de la oposición y lista de temas
      topics/
        <topic-slug>.json   # preguntas de un tema
```

**`exam.json`**
```json
{
  "$schema": "../../schema/exam.schema.json",
  "slug": "malaga-programador-c1",
  "name": "Programador Informático",
  "organization": "Diputación Provincial de Málaga",
  "group": "C1",
  "topics": [
    { "slug": "constitucion", "number": 1, "title": "La Constitución Española de 1978", "section": "Materias comunes" }
  ],
  "exercises": [
    { "number": 1, "title": "Primer ejercicio", "questionCount": 55, "durationMinutes": 50, "maxScore": 20, "penaltyRatio": 0.5,
      "sections": ["Materias comunes"] }
  ]
}
```
`exercises` es opcional (spec 0033): define el formato de los simulacros de examen. Cada `section` debe existir tal cual en algún tema; los `number` no se repiten. El importador hace upsert por número y borra los ejercicios que desaparecen del fichero.

**`topics/<topic-slug>.json`**
```json
{
  "$schema": "../../../schema/topic.schema.json",
  "topic": "constitucion",
  "questions": [
    {
      "key": "malaga-programador-c1/constitucion/0001",
      "statement": "¿…?",
      "options": [
        { "text": "…", "isCorrect": true },
        { "text": "…", "isCorrect": false }
      ],
      "explanation": "…",
      "legalReference": "CE, art. 1.1"
    }
  ]
}
```
- Propiedades en camelCase; cualquier propiedad desconocida es un error.
- `key` es el identificador estable de la pregunta en todo el contenido. Patrón: `^[a-z0-9-]+(/[a-z0-9-]+)*$`. Por convención, `<exam>/<topic>/<nnnn>`.
- `key` no se cambia nunca. Para cambiar el número de opciones de una pregunta, se le da una `key` nueva y la antigua desaparece del fichero (el importador la desactiva).

### Validación (`ContentReader`)
`ContentReader.Read(string contentRoot)` devuelve `ContentReadResult(IReadOnlyList<ExamContent> Exams, IReadOnlyList<ContentError> Errors)`. Cada `ContentError(string File, string Path, string Message)` lleva la ruta relativa del fichero, la ruta JSON (p. ej. `$.questions[3].options`) y un mensaje en español. **Recoge todos los errores, no se detiene en el primero.**

**Errores por fichero:**
- JSON mal formado (con línea).
- Propiedades obligatorias que faltan o desconocidas: `System.Text.Json` con `required` y `UnmappedMemberHandling.Disallow`.
- Reglas del dominio: se construyen las entidades (`Exam.Create`, `Topic.Create`, `Question.Create`) y cada `DomainRuleException` se convierte en un error con su `Code` y la ruta del elemento.

**Errores entre ficheros:**
- `exam.json` existe y su `slug` coincide con el nombre de la carpeta.
- No hay `slug` ni `number` de tema repetidos dentro de una oposición.
- Cada `topics/<x>.json` tiene `"topic": "x"` y ese tema figura en `exam.json`. Un tema sin fichero es válido y queda sin preguntas.
- Las `key` son únicas en todo el contenido.
- No hay ficheros fuera de esta estructura, salvo `schema/`.

### Importación (`ContentImporter`)
`ContentImporter.ImportAsync(IReadOnlyList<ExamContent> exams, bool dryRun, CancellationToken ct)` devuelve un `ImportReport` con el número de altas, actualizaciones, desactivaciones y reactivaciones por tipo (oposición, tema y pregunta).

Reglas generales:
- Recibe `OpoStudyDbContext` por inyección de dependencias y trabaja en **una sola transacción**: o se aplica todo o no se aplica nada.
- Usa la estrategia de ejecución, porque Aspire activa los reintentos.

Upsert por tipo:
- **Oposición:** busca por `slug`. Si existe, `Update`; si no, `Create`.
- **Tema:** busca por `(examId, slug)`. Si existe, `Update`; si no, `Create`. Los temas de la base de datos que ya no están en el contenido se dejan como están (`Topic` no tiene `IsActive`) y se anota un aviso en el informe.
- **Pregunta:** busca por `key`.
  - Si existe, `Update`; si estaba desactivada, además se reactiva.
  - Si no existe, `Create`.
  - Si la `key` ya existe pero cambia el número de opciones, el dominio lanza `question.option_count_changed`: se aborta la importación y se indica la `key`.
- **Preguntas que ya no están:** las de las oposiciones importadas que no aparecen en el contenido se **desactivan**. Nunca se borran.

Otras reglas:
- Una ejecución que no cambia nada no modifica ninguna fila: comparar antes de llamar a `Update`, o confiar en el change tracker de EF.
- `dryRun = true` hace todo y al final revierte la transacción.
- Limitación conocida: el índice único `(exam_id, number)` se comprueba fila a fila al guardar, así que los siguientes cambios pueden fallar en la importación (se aborta sin cambios parciales) aunque el contenido sea válido: intercambiar el `number` de dos temas, desplazar números (p. ej. el tema 2 pasa a 3 y el 3 a 4), reutilizar el número de un tema retirado del contenido (el tema sigue en la BD con su número) y cambiar el slug de un tema conservando su número (para la BD es un tema nuevo con un número ya ocupado). El lector no puede detectarlo sin consultar la BD. Workaround: hacerlo en dos importaciones (la primera con números provisionales). No se implementa renumeración en dos pasos.

### Consola `OpoStudy.ContentImporter`
- Host genérico con `AddServiceDefaults()` y `AddNpgsqlDbContext<OpoStudyDbContext>("opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions)`.
- Uso:
  ```
  dotnet run --project src/OpoStudy.ContentImporter -- --content <ruta> [--dry-run] [--validate-only] [--verbose]
  ```
  `--validate-only` no necesita base de datos.
- Códigos de salida:
  - `0`: OK.
  - `1`: contenido inválido; se imprimen todos los errores como `fichero: ruta: mensaje`.
  - `2`: la importación falló.
- No aplica migraciones, porque eso lo hace la Api en Development.
- **AppHost:** la consola se añade como recurso `content-importer` con `.WithReference(db).WaitFor(api)` y los argumentos `--content <ruta absoluta a content/ del repo>`. Así, al arrancar el AppHost, la base de datos local siempre tiene el contenido. Al ser idempotente, se puede ejecutar en cada arranque.

### API, datos y app
Sin cambios de modelo ni migraciones.

## Seguridad
- El contenido es público (legislación). No contiene datos personales ni secretos, y gitleaks lo revisa en CI como el resto del repo.
- La cadena de conexión de la consola la proporciona Aspire en local, o user-secrets y variables de entorno. Nunca va en ficheros versionados.
- El tamaño de los textos está limitado por el dominio (`Limits`). Un fichero de más de 5 MB se rechaza.

## Criterios de aceptación
- [ ] Importar dos veces el mismo contenido no duplica nada. La segunda importación no cuenta altas ni actualizaciones.
- [ ] Un JSON inválido hace fallar el CI (test sobre `content/`) con el fichero, la ruta JSON y el motivo.
- [ ] Una pregunta que se quita del contenido queda desactivada, y si vuelve, se reactiva.
- [ ] Un error a mitad de la importación no deja cambios parciales.
- [ ] Los JSON Schema validan los ficheros del repo y dan autocompletado en el editor.
- [ ] El AppHost importa el contenido al arrancar.

## Plan de tests
- **`tests/OpoStudy.Infrastructure.Tests/Content/ContentReaderTests`.** Cada test escribe su contenido en un directorio temporal. Casos:
  - contenido válido;
  - JSON mal formado;
  - propiedad desconocida;
  - pregunta sin opción correcta o con dos;
  - tema que no está en `exam.json`;
  - `slug` de la oposición distinto del de la carpeta;
  - `key` repetida entre ficheros;
  - varios errores a la vez (se informan todos).
- **`ContentImporterTests`** (Postgres con Testcontainers). Casos:
  - primera importación;
  - segunda importación sin cambios;
  - pregunta modificada;
  - pregunta eliminada que queda desactivada y luego se reactiva;
  - cambio en el número de opciones, que aborta sin cambios parciales;
  - `dryRun`, que no persiste nada.
- **`RepositoryContentTests`:**
  - `ContentReader` sobre el `content/` del repo no da errores. La raíz se localiza subiendo directorios hasta `OpoStudy.slnx`.
  - Cada fichero valida contra su JSON Schema con `JsonSchema.Net`, una dependencia que solo usa este proyecto de tests.
- **Manual:** arrancar el AppHost y comprobar que existe la oposición en `exams`.

## Decisiones de implementación
- `ContentReader.Read` devuelve `Exams` vacío si hay algún error: el contenido solo es importable si es válido por completo.
- La ruta JSON de un error de dominio apunta a la propiedad concreta cuando se conoce (p. ej. `$.questions[1].options`); si no, al elemento.
- Los ficheros de tema se leen en orden alfabético; la `key` repetida se informa en el segundo fichero.
- Un fichero fuera de la estructura (cualquier cosa que no sea `schema/**`, `exams/<slug>/exam.json` o `exams/<slug>/topics/*.json`) es un error.
- El importador convierte cualquier `DomainRuleException` al actualizar una pregunta en `ContentImportException` (con la `key`); la consola la muestra con código de salida 2.
- La consola no pasa sus argumentos al host (`Args = []`): la cadena de conexión viene del entorno (Aspire) o de user-secrets.
- Una oposición desactivada que vuelve a aparecer en el contenido se reactiva (cuenta como reactivación de oposición).
- Los slugs (oposición, tema), el campo `topic` y las `key` no admiten espacios al principio ni al final: el lector lo informa como error (igual que el patrón del esquema) y detecta duplicados sobre el valor recortado, así lector, importador y dominio manejan el mismo valor.
- Los enlaces simbólicos (reparse points) en `content/`, ficheros o directorios, son un error y no se siguen.
- Los textos de los esquemas llevan `pattern: "\\S"` para rechazar cadenas formadas solo por espacios.
- `maxLength` del esquema cuenta code points y `Limits` del dominio cuenta unidades UTF-16: solo difieren con emojis u otros caracteres suplementarios. La fuente de verdad es el dominio, que el lector aplica siempre.
- Consola: `ContentReader.Read` va dentro del manejo de errores; al fallar se imprime la cadena de `InnerException` (una línea por nivel) y, con `--verbose`, el stack trace. Ctrl+C cancela mediante un `CancellationToken` (la transacción se revierte) y sale con código 2.
- Los ficheros de `content/` no pertenecen a ningún proyecto, así que no se copian a ningún `bin`.
