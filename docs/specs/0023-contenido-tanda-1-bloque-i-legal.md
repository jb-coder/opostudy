# 0023 — Contenido, tanda 1: temas legales pendientes del Bloque I

- **Issue:** #67
- **Milestone:** v0.4.0
- **Estado:** lista

> Primera de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad, proceso y regla de fuentes en la spec 0004 (ampliada por la 0022). Procedimiento: skill `content-author`.

## Objetivo
Completar el Bloque I (materias comunes) con 40 preguntas de test de los temas 5 a 8, que hoy no tienen ninguna, redactadas y verificadas contra el texto consolidado del BOE. Con esta tanda, todo el Bloque I tiene preguntas.

## Alcance
**Incluye:**
- `topics/regimen-local.json`: 10 preguntas del tema 5.
- `topics/personal-entidades-locales.json`: 10 preguntas del tema 6.
- `topics/presupuesto-local.json`: 10 preguntas del tema 7.
- `topics/contratos-sector-publico.json`: 10 preguntas del tema 8.
- Añadir a la lista de identificadores de la skill `content-author` (sección 1.4) los del TRLRHL, el RD 500/1990, el RD 424/2017 y el TRRL.

**No incluye:**
- Preguntas de otros temas (tandas 2 a 8).
- Normativa autonómica: la LAULA (Ley 5/2010 de Andalucía) queda fuera porque el enunciado del tema 5 solo cita la CE y la Ley 7/1985.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 5.** Régimen local español: principios constitucionales y Ley 7/1985. Organización y competencias provinciales y municipales.
- **Tema 6.** Personal al servicio de las entidades locales: función pública local, selección, situaciones administrativas, derechos y deberes.
- **Tema 7.** El presupuesto de las entidades locales: elaboración, aprobación, ejecución, control y fiscalización.
- **Tema 8.** Ley de Contratos del Sector Público: objeto, ámbito de aplicación y tipos contractuales.

### Fuentes (BOE, legislación consolidada; identificadores comprobados en boe.es)
| Norma | Identificador | Abreviatura en `legalReference` |
|---|---|---|
| Constitución Española | `BOE-A-1978-31229` | `CE, art. 140` |
| Ley 7/1985, Reguladora de las Bases del Régimen Local | `BOE-A-1985-5392` | `Ley 7/1985, art. 26.1.a)` |
| RDLeg 781/1986, texto refundido de Régimen Local (TRRL) | `BOE-A-1986-9865` | `RDLeg 781/1986, art. 134` |
| RDLeg 5/2015, TREBEP | `BOE-A-2015-11719` | `TREBEP, art. 61.6` |
| RDLeg 2/2004, TRLRHL | `BOE-A-2004-4214` | `TRLRHL, art. 169.1` |
| RD 500/1990, presupuestos de las entidades locales | `BOE-A-1990-9664` | `RD 500/1990, art. 54` |
| RD 424/2017, control interno en el sector público local | `BOE-A-2017-5192` | `RD 424/2017, art. 3` |
| Ley 9/2017, de Contratos del Sector Público | `BOE-A-2017-12902` | `Ley 9/2017, art. 17` |

### Reparto orientativo por artículos
Se puede mover 1 pregunta entre apartados si el texto da más juego, pero sin salirse del ámbito del tema.

| Tema | Apartado | Artículos | Nº |
|---|---|---|---|
| 5 | Principios constitucionales (autonomía, municipio, provincia, suficiencia financiera) | CE, arts. 137, 140-142 | 2 |
| 5 | Disposiciones generales y potestades | Ley 7/1985, arts. 1-4 | 1 |
| 5 | Municipio: organización (órganos necesarios, Alcalde, Pleno, Junta de Gobierno Local) | Ley 7/1985, arts. 19-23 | 2 |
| 5 | Municipio: competencias y servicios mínimos por población | Ley 7/1985, arts. 25-27 | 2 |
| 5 | Provincia: organización y competencias | Ley 7/1985, arts. 31-36 | 3 |
| 6 | Función pública local (clases de personal, habilitación nacional, personal eventual) | Ley 7/1985, arts. 89-92 bis y 104-104 bis; TREBEP, arts. 8-12 | 3 |
| 6 | Selección (principios, requisitos, órganos y sistemas selectivos) | TREBEP, arts. 55-61; Ley 7/1985, arts. 91 y 100 | 3 |
| 6 | Situaciones administrativas | TREBEP, arts. 85-92 | 2 |
| 6 | Derechos y deberes, código de conducta | TREBEP, arts. 14-15 y 52-54 | 2 |
| 7 | Concepto, contenido, elaboración y aprobación (plazos, publicidad, prórroga) | TRLRHL, arts. 162-171 | 3 |
| 7 | Ejecución: créditos, modificaciones y fases del gasto | TRLRHL, arts. 172-189; RD 500/1990, arts. 34-60 | 3 |
| 7 | Liquidación | TRLRHL, arts. 191-193 | 1 |
| 7 | Control y fiscalización (interno: función interventora y control financiero; externo) | TRLRHL, arts. 213-223; RD 424/2017, arts. 3-4 | 3 |
| 8 | Objeto y finalidad | Ley 9/2017, art. 1 | 1 |
| 8 | Ámbito subjetivo (sector público, AAPP, poderes adjudicadores) | Ley 9/2017, arts. 2-3 | 2 |
| 8 | Negocios y contratos excluidos | Ley 9/2017, arts. 4-11 | 2 |
| 8 | Tipos contractuales (obras, concesiones, suministro, servicios, mixtos) | Ley 9/2017, arts. 12-18 | 4 |
| 8 | Contratos administrativos y privados | Ley 9/2017, arts. 24-27 | 1 |

### Criterios propios de esta tanda (además de los de la spec 0004)
- **Importes y umbrales:** los de la LCSP (arts. 19-23) y los del TRLRHL cambian por norma posterior. Se usa solo el valor del texto consolidado vigente y, si hay duda sobre su vigencia, no se pregunta por la cifra.
- **Tramos de población (tema 5):** se cita el apartado exacto (p. ej. `Ley 7/1985, art. 26.1.c)`) y los distractores son los otros tramos reales del mismo artículo.
- **Tema 6:** cuando la Ley 7/1985 y el TREBEP regulan lo mismo, se cita la norma que contiene el dato literal. No se mezclan las dos en una sola pregunta.
- **Tema 7:** no se pregunta por la Instrucción de contabilidad ni por órdenes de estructura presupuestaria (no están en el ámbito).
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `lpac-fases-procedimiento.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar cada norma con la skill `content-author` (sección 1) a un directorio vacío del scratchpad y convertirla a texto. Nunca se copia al repo.
2. Redactar tema a tema desde el `.txt`.
3. Hacer una segunda pasada de verificación, separada de la redacción, con las 4 comprobaciones de la skill (sección 3).
4. Validar con `--validate-only` y lanzar `test-runner`.
5. Listar en el PR las preguntas dudosas para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 5-8 pasan a tener preguntas en la app.

## Seguridad
Contenido público, sin secretos ni datos personales. Los XML del BOE son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I` sin ejecutarlos.

## Criterios de aceptación
- [ ] Existen los 4 ficheros de temas con 10 preguntas cada uno (40 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` un artículo de una norma de la tabla de fuentes, y su texto vigente respalda la correcta y la `explanation`.
- [ ] Ningún distractor es también correcto según la norma citada u otra de la tabla.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado) y no hay preguntas fuera del ámbito del tema.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] La skill `content-author` lista los identificadores nuevos (TRLRHL, RD 500/1990, RD 424/2017 y TRRL).
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 40 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 5-8 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que el feedback muestra «Fuente: …».
