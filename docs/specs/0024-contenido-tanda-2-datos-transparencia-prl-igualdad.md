# 0024 — Contenido, tanda 2: protección de datos, software, transparencia, PRL e igualdad

- **Issue:** #68
- **Milestone:** v0.4.0
- **Estado:** lista

> Segunda de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad, proceso y regla de fuentes en la spec 0004 (ampliada por la 0022). Procedimiento: skill `content-author`. Modelo: spec 0023.

## Objetivo
Dar 40 preguntas de test, redactadas y verificadas contra el texto de la norma, a los temas 15, 16, 39 y 40, que hoy no tienen ninguna. Son los temas legales de los bloques II-A y II-C.

## Alcance
**Incluye:**
- `topics/proteccion-datos.json`: 10 preguntas del tema 15.
- `topics/proteccion-software.json`: 10 preguntas del tema 16.
- `topics/transparencia-proteccion-datos.json`: 10 preguntas del tema 39.
- `topics/prevencion-igualdad.json`: 10 preguntas del tema 40.
- Skill `content-author`: añadir a la sección 1.4 los identificadores nuevos (TRLPI, Ley 1/2014 y Ley 12/2007 de Andalucía, Ley 31/1995, LO 3/2007) y el procedimiento para el RGPD (texto del DOUE en el BOE y sus correcciones de errores).

**No incluye:**
- Preguntas de otros temas (tandas 1 y 3 a 8).
- Gestión de activos de software (SAM, ISO/IEC 19770) en el tema 16: las normas ISO no son fuente admitida (spec 0022). La parte «comprobación de la legalidad y control del software» se cubre con los arts. 99-104 del TRLPI.
- Protección penal del software y de los datos (Código Penal): no está entre las fuentes de la issue.
- Reglamentos de desarrollo (RD 39/1997 de servicios de prevención, RD 901/2020 de planes de igualdad): el enunciado del tema solo cita las leyes.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 15.** La protección de datos personales: marco normativo, principios, derechos, responsables y encargados, autoridades de control. Propiedad intelectual en el entorno digital.
- **Tema 16.** La protección jurídica de los programas de ordenador. Comprobación de la legalidad y control del software.
- **Tema 39.** Transparencia (Ley 19/2013 y Ley 1/2014 de Andalucía) y protección de datos (RGPD y Ley Orgánica 3/2018).
- **Tema 40.** Prevención de riesgos laborales. Igualdad efectiva de mujeres y hombres (LO 3/2007, Ley 12/2007 de Andalucía y Ley 9/2018).

### Fuentes (identificadores comprobados en la API de datos abiertos del BOE el 24-11-2026)
| Norma | Identificador | Texto | Abreviatura en `legalReference` |
|---|---|---|---|
| Reglamento (UE) 2016/679, RGPD | `DOUE-L-2016-80807` | DOUE original, **no consolidado** | `RGPD, art. 5.1.a)` |
| Corrección de errores del RGPD (DOUE L 127, 23-05-2018) | `DOUE-L-2018-80845` | DOUE | (no se cita; corrige el RGPD) |
| Corrección de errores del RGPD (DOUE L 74, 04-03-2021) | `DOUE-L-2021-80264` | DOUE | (no se cita; corrige el RGPD) |
| LO 3/2018, LOPDGDD | `BOE-A-2018-16673` | consolidado | `LO 3/2018, art. 7.1` |
| RDLeg 1/1996, texto refundido de la Ley de Propiedad Intelectual (TRLPI) | `BOE-A-1996-8930` | consolidado | `TRLPI, art. 96.1` |
| Ley 19/2013, de transparencia | `BOE-A-2013-12887` | consolidado | `Ley 19/2013, art. 20.1` |
| Ley 1/2014, de Transparencia Pública de Andalucía | `BOE-A-2014-7534` | consolidado | `Ley 1/2014 de Andalucía, art. 32` |
| Ley 31/1995, de Prevención de Riesgos Laborales | `BOE-A-1995-24292` | consolidado | `Ley 31/1995, art. 35.2` |
| LO 3/2007, para la igualdad efectiva de mujeres y hombres | `BOE-A-2007-6115` | consolidado | `LO 3/2007, art. 7.1` |
| Ley 12/2007, para la promoción de la igualdad de género en Andalucía | `BOE-A-2008-2492` | consolidado | `Ley 12/2007 de Andalucía, art. 32.1` |

Notas sobre las fuentes:
- **RGPD.** La API de legislación consolidada no lo tiene. Se descarga el XML del DOUE desde el BOE: `https://www.boe.es/buscar/xml.php?id=DOUE-L-2016-80807` (igual para las dos correcciones). Los artículos van en `<p class="articulo">Artículo N. Título.</p>` y el texto en `<p class="parrafo">`, así que `boe2txt.py` no sirve: se convierte con un script propio (quitar etiquetas y marcar cada artículo como `[RGPD aN]`). El análisis del BOE (actualizado el 08-10-2026) solo lista las dos correcciones de errores, ninguna modificación.
- **Correcciones de errores del RGPD.** Afectan, entre otros, a los arts. 3.2, 9.1, 9.2.b), 12.3, 13.1.f), 13.2.e)-f), 14.1.f), 14.5.d), 15.1.c), 18.1.a), 23.2.f), 28.1, 28.3.b) y e), 36.1, 37.1.c), 41.3 y 5, 42.7, 43.3 y 6, 57.1.p), 58.2.a)-b), 64, 65.1.a), 69.2, 70.1, 83.4.c), 88.1 y 89.2. Si una pregunta usa uno de esos apartados, se redacta con el texto corregido («debe decir») y se dice en la `explanation`. La lista completa sale del XML de cada corrección.
- **Ley 9/2018 de Andalucía** (`BOE-A-2018-15239`) es una ley modificativa: reforma la Ley 12/2007 y le añade, entre otros, los arts. 9 bis, 11 bis, 26 bis y el título V. No tiene texto consolidado propio, así que las preguntas citan la **Ley 12/2007 de Andalucía** (texto consolidado, que ya incluye esos cambios). Si el bloque está «vigente desde 20181016», la `explanation` puede decir «en la redacción dada por la Ley 9/2018».

### Reparto orientativo por artículos
Se puede mover 1 pregunta entre apartados si el texto da más juego, pero sin salirse del ámbito del tema.

| Tema | Apartado | Artículos | Nº |
|---|---|---|---|
| 15 | Marco normativo, objeto y definiciones | RGPD, arts. 1-4; LO 3/2018, arts. 1-3 | 1 |
| 15 | Principios y licitud (incluye consentimiento de menores) | RGPD, arts. 5-9; LO 3/2018, arts. 4-10 | 2 |
| 15 | Derechos del interesado (ejercicio, acceso, rectificación, supresión, limitación, portabilidad, oposición) | RGPD, arts. 12 y 15-22; LO 3/2018, arts. 12-18 | 2 |
| 15 | Responsables y encargados (incluye registro de actividades y delegado de protección de datos) | RGPD, arts. 24-30 y 37-39; LO 3/2018, arts. 28-36 | 2 |
| 15 | Autoridades de control (AEPD y autonómicas) | RGPD, arts. 51-58; LO 3/2018, arts. 44-62 | 1 |
| 15 | Propiedad intelectual en el entorno digital (puesta a disposición, copia privada, salvaguarda en el entorno digital, medidas tecnológicas) | TRLPI, arts. 17-20, 31, 158 ter y 196-198 | 2 |
| 16 | Régimen jurídico y objeto de la protección | TRLPI, arts. 10.1.i), 95 y 96 | 2 |
| 16 | Titularidad y duración | TRLPI, arts. 97 y 98 | 2 |
| 16 | Derechos de explotación y sus límites (usuario legítimo, copia de seguridad, interoperabilidad) | TRLPI, arts. 99 y 100 | 3 |
| 16 | Comprobación de la legalidad: versiones sucesivas, infracciones, medidas de protección y salvaguardia | TRLPI, arts. 101-104 | 3 |
| 39 | Ley 19/2013: ámbito, publicidad activa, derecho de acceso, límites, procedimiento y reclamación ante el CTBG | Ley 19/2013, arts. 2-24 | 4 |
| 39 | Ley 1/2014 de Andalucía: principios, publicidad activa de las entidades locales, acceso, plazos y Consejo de Transparencia y Protección de Datos de Andalucía | Ley 1/2014 de Andalucía, arts. 6-7, 21, 24-33 y 43-48 | 3 |
| 39 | Protección de datos en el sector público (misión de interés público, DPD obligatorio, acceso a documentos oficiales, régimen sancionador de las AAPP, identificación en notificaciones) | RGPD, arts. 6.1.e), 37.1.a) y 86; LO 3/2018, art. 77 y disposiciones adicionales | 3 |
| 40 | Prevención de riesgos laborales (definiciones, derecho a la protección, evaluación, información y formación, riesgo grave e inminente, vigilancia de la salud, obligaciones del trabajador, delegados y comité de seguridad y salud) | Ley 31/1995, arts. 3-4, 14-22, 29 y 33-39 | 4 |
| 40 | LO 3/2007 (definiciones, acoso, acciones positivas, planes de igualdad, criterios de actuación de las AAPP) | LO 3/2007, arts. 3-11, 45-46 y 51-53 | 3 |
| 40 | Ley 12/2007 de Andalucía (definiciones, transversalidad, impacto de género, lenguaje no sexista, representación equilibrada, planes de igualdad y acoso en la Administración, unidades de igualdad) | Ley 12/2007 de Andalucía, arts. 3-11, 32-33 y 60 | 3 |

### Criterios propios de esta tanda (además de los de la spec 0004)
- **Temas 15 y 39 sin solapes.** Ningún apartado de artículo se usa en los dos temas. El 15 trata la protección de datos en general; el 39, la transparencia y la protección de datos en el sector público.
- **Temas 15 y 16 sin solapes.** El tema 15 no usa los arts. 95-104 del TRLPI.
- **RGPD:** solo el articulado (no los considerandos). Antes de usar un apartado se comprueba en la lista de correcciones de errores (ver «Fuentes»).
- **Cifras que cambian por reforma** (plazos de acceso, umbrales de plantilla de los planes de igualdad y de los delegados de prevención, edad del consentimiento, duración de los derechos): solo el valor del texto consolidado vigente. Si el bloque tiene una fecha de vigencia reciente o hay duda, no se pregunta por la cifra.
- **Cuando dos normas regulan lo mismo** (Ley 19/2013 y Ley 1/2014; LO 3/2007 y Ley 12/2007; RGPD y LO 3/2018), se cita la que contiene el dato literal, y los distractores no pueden ser el valor correcto según la otra norma.
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar cada norma con la skill `content-author` (sección 1) a un directorio vacío del scratchpad y convertirla a texto; el RGPD y sus correcciones, como se indica en «Fuentes». Nunca se copia al repo.
2. Redactar tema a tema desde el `.txt`.
3. Hacer una segunda pasada de verificación, separada de la redacción, con las 4 comprobaciones de la skill (sección 3) y, en las del RGPD, contra las correcciones de errores.
4. Validar con `--validate-only` y lanzar `test-runner`.
5. Listar en el PR las preguntas dudosas para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 15, 16, 39 y 40 pasan a tener preguntas en la app.

## Seguridad
Contenido público, sin secretos ni datos personales. Los XML del BOE y del DOUE son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I` sin ejecutarlos.

## Criterios de aceptación
- [ ] Existen los 4 ficheros de temas con 10 preguntas cada uno (40 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` un artículo de una norma de la tabla de fuentes, y su texto vigente (en el RGPD, con las correcciones de errores) respalda la correcta y la `explanation`.
- [ ] Ningún distractor es también correcto según la norma citada u otra de la tabla.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado), no hay preguntas fuera del ámbito del tema y no hay apartados repetidos entre los temas 15 y 39.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] La skill `content-author` lista los identificadores nuevos y el procedimiento del RGPD.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 40 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 15, 16, 39 y 40 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que el feedback muestra «Fuente: …».
