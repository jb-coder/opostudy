# 0026 — Contenido, tanda 3: notificaciones, sede, registro y expediente electrónico

- **Issue:** #69
- **Milestone:** v0.4.0
- **Estado:** lista

> Tercera de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad y proceso en la spec 0004, fuentes técnicas en la spec 0022. Modelo: specs 0023 y 0024.
> Es la primera tanda que usa fuentes técnicas (PAe), además del BOE.

## Objetivo
Dar 40 preguntas de test, redactadas y verificadas contra el texto de la fuente citada, a los temas 31 a 34 (administración electrónica del bloque II-C), que hoy no tienen ninguna.

## Alcance
**Incluye:**
- `topics/notificaciones-notifica.json`: 10 preguntas del tema 31.
- `topics/pag-carpeta-sede.json`: 10 preguntas del tema 32.
- `topics/registro-electronico.json`: 10 preguntas del tema 33.
- `topics/expediente-electronico.json`: 10 preguntas del tema 34.

**No incluye:**
- Preguntas de otros temas (tandas 1, 2 y 4 a 8).
- Firma electrónica, sellos y certificados (tema 35; incluye los arts. 18 a 20 del ENI y la NTI de Política de Firma), Cl@ve e identificación (tema 36), SIA y DIR3 (tema 30) y ENS (tema 13). Se pueden nombrar, pero no son el objeto de la pregunta.
- Orden HAP/566/2013 (Registro Electrónico Común): derogada por la Orden PCM/1382/2021. Solo puede aparecer en una `explanation` como antecedente.
- Orden HAP/1949/2014 (PAG de la AGE): vigente, pero desarrolla la Ley 11/2007, derogada, y está superada en lo esencial por el RD 203/2021. No se usa.
- Las NTI que no tratan el documento, el expediente, el copiado, la digitalización, la gestión documental o los asientos registrales.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 31** (23 del bloque). Notificaciones y comunicaciones en las Leyes 39/2015 y 40/2015. Notifica. Servicios web e integraciones.
- **Tema 32** (24 del bloque). Punto de Acceso General electrónico. Carpeta Ciudadana. Sede electrónica.
- **Tema 33** (25 del bloque). Registro de entrada/salida. SICRES 4.0. Registro Electrónico Común. SIR, GEISER y ORVE.
- **Tema 34** (26 del bloque). Archivo, documento y expediente electrónico. INSIDE y Archive. Esquema Nacional de Interoperabilidad y Normas Técnicas.

### Fuentes normativas (identificadores y vigencia comprobados en la API del BOE el 24-11-2026)
| Norma | Identificador | Abreviatura en `legalReference` |
|---|---|---|
| Ley 39/2015 | `BOE-A-2015-10565` | `Ley 39/2015, art. 43.1` |
| Ley 40/2015 | `BOE-A-2015-10566` | `Ley 40/2015, art. 38.1` |
| RD 203/2021, Reglamento de actuación y funcionamiento del sector público por medios electrónicos (también su anexo de definiciones) | `BOE-A-2021-5032` | `RD 203/2021, art. 42.1` |
| RD 4/2010, Esquema Nacional de Interoperabilidad | `BOE-A-2010-1331` | `RD 4/2010 (ENI), art. 21.1` |
| NTI de Modelo de Datos para el Intercambio de asientos entre las entidades registrales (SICRES 4.0), Resolución de 22-07-2021 | `BOE-A-2021-13749` | `NTI SICRES 4.0 (Resolución de 22-07-2021), apartado V` |
| Orden PCM/1382/2021, Registro Electrónico General de la AGE | `BOE-A-2021-20477` | `Orden PCM/1382/2021, art. 1.3` |
| NTI de Documento Electrónico, Resolución de 19-07-2011 | `BOE-A-2011-13169` | `NTI de Documento Electrónico (Resolución de 19-07-2011), apartado III` |
| NTI de Expediente Electrónico, Resolución de 19-07-2011 | `BOE-A-2011-13170` | `NTI de Expediente Electrónico (Resolución de 19-07-2011), apartado III.1` |
| NTI de Digitalización de Documentos, Resolución de 19-07-2011 | `BOE-A-2011-13168` | `NTI de Digitalización (Resolución de 19-07-2011), apartado IV` |
| NTI de Procedimientos de copiado auténtico y conversión, Resolución de 19-07-2011 | `BOE-A-2011-13172` | `NTI de Copiado Auténtico (Resolución de 19-07-2011), apartado III` |
| NTI de Política de gestión de documentos electrónicos, Resolución de 28-06-2012 | `BOE-A-2012-10048` | `NTI de Política de Gestión de Documentos (Resolución de 28-06-2012), apartado VI` |

Notas:
- **Vigencia.** RD 203/2021: los arts. 27.1 y 28.1 cambiaron en 2025 (RD 255/2025) y la disposición transitoria 1 es nula (STS de 30-05-2022); ninguno entra en esta tanda. El ENI tiene 10 bloques vigentes desde 02-04-2021 (arts. 9, 11, 14, 16-18, disposición adicional primera y anexo) y la disposición adicional primera cambió en 2024. El art. 19 está suprimido.
- **Remisiones derogadas.** El ENI y las NTI de 2011-2012 citan la Ley 11/2007 y el RD 1671/2009. Las preguntas usan el contenido técnico (componentes, metadatos, estados, formatos), nunca esas remisiones.
- **Nombres de ministerios** (RD 203/2021, NTI y PAe): cambian con cada reestructuración. No se pregunta por ellos.
- En los `.txt`, las anclas de los artículos no coinciden con su número (`a4-4` = art. 42 del RD 203/2021): se busca `^Artículo N\.`.

### Fuentes técnicas (PAe, spec 0022): solo lo que no regula una norma
Todas devuelven 200 sin autenticación el 24-11-2026. Las fichas están en `https://administracionelectronica.gob.es/ctt/<solución>` y los documentos, en la pestaña «Descargas» de cada ficha.

| Solución | Documento | Formato de `legalReference` |
|---|---|---|
| Notifica | Ficha del CTT; «Integración de Servicios Web para Organismos Emisores», v5.2 (03-09-2026); «Guía rápida de integración» (30-04-2026); «Glosario de términos», v3.1 (04-08-2025) | `PAe, Notifica, «Integración de Servicios Web para Organismos Emisores» v5.2, §4.3.1` |
| Carpeta Ciudadana (`ccd`) | Ficha del CTT | `PAe, Carpeta Ciudadana (ficha del CTT), «Descripción»` |
| SIR | Ficha del CTT; «Guía funcional para las oficinas de asistencia en materia de registro» (noviembre de 2020) | `PAe, SIR, «Guía funcional para las oficinas de asistencia en materia de registro» (2020), §4.1` |
| GEISER, ORVE | Ficha del CTT de cada una | `PAe, ORVE (ficha del CTT), «Resumen»` |
| INSIDE | Ficha del CTT; «Descripción Suite InSide», v005 (19-12-2022) | `PAe, INSIDE, «Descripción Suite InSide» v005, §4.2` |
| Archive | Ficha del CTT; «Manual de usuario Archive», v1.15.0 (05-05-2026); «Archive - Servicios Web», rev. 011 (09-06-2022) | `PAe, Archive, «Manual de usuario Archive» v1.15.0, §N` |

Notas:
- **Solo la sección «Resumen» o «Descripción» de las fichas**, no las noticias. En los documentos, la `legalReference` lleva el número de sección real.
- **Contradicciones entre fuentes.** La ficha de GEISER habla de certificación SICRES 3.0 y la de ORVE, de 4.0 en el resumen y de 3.0 en la descripción. No se pregunta por la versión certificada de ninguna. La Guía funcional del SIR (2020) es anterior a SICRES 4.0 y al RD 203/2021: solo se usa si no los contradice.
- **Datos volátiles excluidos:** organismo responsable, cifras de organismos u oficinas, URL, versiones del producto, contactos, métodos marcados «a extinguir» (`infoEnvioV2`) y límites de tamaño.
- **Registro Electrónico Común:** la ficha `rec` del CTT no es pública. El apartado se cubre con la Orden PCM/1382/2021, que lo sustituye. El enunciado lo llama «Registro Electrónico General de la AGE (REG-AGE)».

### Reparto orientativo
Se puede mover 1 pregunta entre apartados si el texto da más juego, pero sin salirse del ámbito del tema. En la columna «Fuente», lo técnico es PAe.

| Tema | Apartado | Fuente | Nº |
|---|---|---|---|
| 31 | Notificaciones en la Ley 39/2015 (condiciones generales, rechazo, aviso, práctica en papel y electrónica) | Ley 39/2015, arts. 40.1 y 3-4, 41.1 y 3-7, 42.1 y 3, 43.1 y 3-4 | 3 |
| 31 | Comunicaciones y notificaciones electrónicas en el RD 203/2021 (doble vía, acuse, aviso, DEHú, sede) | RD 203/2021, arts. 41-45 | 2 |
| 31 | Comunicaciones entre Administraciones y entorno cerrado | Ley 40/2015, arts. 44 y 3.2; RD 203/2021, arts. 56-57 | 1 |
| 31 | Notifica: qué es, vías de puesta a disposición, conceptos | PAe, Notifica | 1 |
| 31 | Notifica: servicios web e integraciones (seguridad, alta de remesas, sincronización, Adviser) | PAe, Notifica (servicios web) | 3 |
| 32 | Punto de Acceso General electrónico | RD 203/2021, art. 7 y anexo de definiciones | 2 |
| 32 | Carpeta Ciudadana | RD 203/2021, arts. 8 y 59 | 2 |
| 32 | Carpeta Ciudadana: servicio | PAe, Carpeta Ciudadana | 1 |
| 32 | Sede electrónica y portal de internet en la Ley 40/2015 | Ley 40/2015, arts. 38 y 39 | 2 |
| 32 | Sede y sede asociada: creación, contenido, servicios, responsabilidad, adhesión | RD 203/2021, arts. 5 y 9-12, y art. 58 | 3 |
| 33 | Registros en la Ley 39/2015 (asientos, recibo, lugares de presentación, cómputo de plazos) | Ley 39/2015, arts. 16.1-4 y 6-8, y art. 31.2 | 2 |
| 33 | Registro en el RD 203/2021 (interoperabilidad, presentación, oficinas, SIR) | RD 203/2021, arts. 37, 39, 40 y 60 | 2 |
| 33 | Registro Electrónico General de la AGE (antes, Registro Electrónico Común) | Orden PCM/1382/2021; RD 203/2021, art. 38 | 1 |
| 33 | SICRES 4.0: modelo de datos, estados del intercambio, funciones | NTI SICRES 4.0, apartados I-VI | 3 |
| 33 | SIR, GEISER y ORVE: qué es cada uno | PAe, SIR, GEISER y ORVE | 2 |
| 34 | Archivo electrónico y documento administrativo electrónico | Ley 39/2015, arts. 17, 26 y 27; Ley 40/2015, art. 46; RD 203/2021, arts. 46-50 y 53-55 | 2 |
| 34 | Expediente electrónico | Ley 39/2015, art. 70; RD 203/2021, arts. 51-52 | 1 |
| 34 | Esquema Nacional de Interoperabilidad (principios, dimensiones, estándares, recuperación y conservación) | RD 4/2010, arts. 1-17, 21-29 y anexo | 2 |
| 34 | NTI: documento, expediente, digitalización, copiado, gestión documental | NTI de la tabla (salvo SICRES) | 3 |
| 34 | INSIDE y Archive | PAe, INSIDE y Archive | 2 |

Total previsto: 31 preguntas con norma y 9 con PAe.

### Criterios propios de esta tanda (además de los de las specs 0004 y 0022)
- **Sin repetir lo que ya preguntan los temas LPAC.** Quedan fuera la Ley 39/2015, arts. 16.5, 40.2, 41.2, 42.2, 43.2, 44 y 45.1.b), que ya tienen pregunta. El plazo de 10 días naturales del rechazo no vuelve a ser la respuesta correcta en ninguna, aunque lo repitan los arts. 44.5 y 45.1 del RD 203/2021.
- **Sin solapes entre los 4 ficheros.** Ningún apartado de artículo se usa dos veces. En particular: la DEHú va en el 31 (no en el 32); las notificaciones de Carpeta Ciudadana van en el 31; el art. 60 del RD 203/2021 (SIR) va solo en el 33; la digitalización va en el 34 (NTI) y no en el 33.
- **Distractores entre normas.** Ley 39/2015, Ley 40/2015 y RD 203/2021 se repiten entre sí: ningún distractor puede ser correcto según otra de ellas.
- **Preguntas de PAe:** se refieren a para qué sirve cada solución, a la operación o al concepto que la define. Distractores reales (otra solución del PAe, otro método del mismo servicio web). Cada una dice en el enunciado de qué fuente sale («Según el manual de integración de Notifica…»).
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar las fuentes a un directorio vacío del scratchpad y convertirlas a texto: BOE, con la API de legislación consolidada; fichas del PAe (HTML), quitando las etiquetas; PDF, con `pdftotext -layout`. Nunca se copian al repo.
2. Redactar tema a tema desde el `.txt`.
3. Hacer una segunda pasada de verificación, separada de la redacción, contra el `.txt` de la fuente citada: existe el apartado, la correcta lo es literalmente, ningún distractor lo es y la `explanation` no añade nada.
4. Validar con `--validate-only` y ejecutar los tests.
5. Listar en el PR las preguntas dudosas y todas las que citan el PAe, para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 31 a 34 pasan a tener preguntas en la app.

## Seguridad
Contenido público, sin secretos ni datos personales. Los XML, HTML y PDF descargados son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I` o `pdftotext`, sin ejecutarlos. No se citan contactos ni direcciones de correo del PAe.

## Criterios de aceptación
- [ ] Existen los 4 ficheros de temas con 10 preguntas cada uno (40 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` una fuente de las tablas, con artículo, apartado o sección, y su texto vigente respalda la correcta y la `explanation`.
- [ ] Solo se cita el PAe cuando el dato no está en una norma de la tabla.
- [ ] Ningún distractor es también correcto según la fuente citada u otra de las tablas.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado), no hay preguntas fuera del ámbito del tema, no se repite ningún apartado entre los 4 ficheros ni los de la Ley 39/2015 ya usados.
- [ ] Ninguna pregunta depende de un dato volátil ni de una contradicción de las listadas.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 40 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 31 a 34 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que el feedback muestra «Fuente: …», también en las preguntas que citan el PAe (referencias más largas).
