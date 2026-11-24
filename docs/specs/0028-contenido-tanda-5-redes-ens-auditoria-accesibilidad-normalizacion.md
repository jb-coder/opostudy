# 0028 — Contenido, tanda 5: redes (SARA y NEREA), ENS, auditoría, accesibilidad y normalización

- **Issue:** #71
- **Milestone:** v0.4.0
- **Estado:** lista

> Quinta de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad y proceso en la spec 0004, fuentes técnicas en la spec 0022. Modelo: spec 0027.

## Objetivo
Dar 50 preguntas de test, redactadas y verificadas contra el texto de la fuente citada, a los temas 11, 12, 14, 17 y 18 (bloque II-A), que hoy no tienen ninguna.

## Alcance
**Incluye:**
- `topics/redes-sara-nerea.json`: 10 preguntas del tema 11.
- `topics/ens.json`: 10 preguntas del tema 12.
- `topics/auditoria-informatica.json`: 10 preguntas del tema 14.
- `topics/accesibilidad-usabilidad.json`: 10 preguntas del tema 17.
- `topics/normalizacion.json`: 10 preguntas del tema 18.

**No incluye:**
- Preguntas de otros temas. Linux y nube (9), protección de datos (15 y 39), software (16), HTML y CSS (21) y Métrica (26) se pueden nombrar, pero no son el objeto de la pregunta.
- Ley 11/2023 y RD 193/2023 (accesibilidad de productos, servicios, bienes y comercio): no son la accesibilidad web del sector público.
- EN 301 549: es una norma de pago de otra entidad; solo se cita lo que dicen el BOE y el DOUE.
- RDL 12/2018 y RD 43/2021 (NIS), más allá de la remisión del RD 311/2022.
- Propuestas no aprobadas: COM(2026) 780 (nuevo reglamento de normalización) y la revisión del Reglamento (UE) 2019/881.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 11** (3 del bloque). Redes corporativas. Infraestructuras comunes: Red SARA y Red NEREA.
- **Tema 12** (4). Esquema Nacional de Seguridad (ENS).
- **Tema 14** (6). Auditoría informática: concepto, contenidos, prácticas operativas, peritajes, marco legal y herramientas.
- **Tema 17** (9). Accesibilidad y usabilidad. W3C. Diseño universal. Diseño web adaptativo.
- **Tema 18** (10). Organizaciones de normalización. Pruebas de conformidad y certificación.

### Red NEREA: entra
La define la Agencia Digital de Andalucía (Junta) en `https://www.juntadeandalucia.es/organismos/ada/areas/red-corporativa/nerea.html` («La red NEREA», 200 el 24-11-2026). Da para 2 preguntas: qué es y cómo se relaciona con la RCJA y la Red SARA.
- No se usan las subpáginas de topología, servicios y conexión (404), ni el portal `rednerea.juntadeandalucia.es` (no responde), ni lo que de ellos muestran los buscadores.
- No se preguntan cifras (áreas de conexión, porcentaje de municipios), fechas, organismos gestores ni la lista de servicios.

### Fuentes normativas (identificadores y vigencia comprobados el 24-11-2026)
| Norma | Identificador | Abreviatura en `legalReference` |
|---|---|---|
| RD 311/2022 (ENS) | `BOE-A-2022-7191` | `RD 311/2022 (ENS), art. 28.2`; `RD 311/2022 (ENS), anexo II, mp.com.4` |
| ITS de Auditoría, Resolución de 27-03-2018 | `BOE-A-2018-4573` | `ITS de Auditoría (Resolución de 27-03-2018), apartado VI.3` |
| ITS de Conformidad, Resolución de 13-10-2016 | `BOE-A-2016-10109` | `ITS de Conformidad (Resolución de 13-10-2016), apartado VI.1` |
| Ley 40/2015 | `BOE-A-2015-10566` | `Ley 40/2015, art. 156.2` |
| RD 4/2010 (ENI) | `BOE-A-2010-1331` | `RD 4/2010 (ENI), art. 13.1` |
| NTI de Requisitos de conexión, Resolución de 19-07-2011 | `BOE-A-2011-13173` | `NTI de Requisitos de conexión (Resolución de 19-07-2011), apartado II.1` |
| RD 203/2021 | `BOE-A-2021-5032` | `RD 203/2021, art. 2.c)` |
| RGPD | `DOUE-L-2016-80807` (correcciones `DOUE-L-2018-80845` y `DOUE-L-2021-80264`) | `RGPD, art. 32.1.d)` |
| Ley 1/2000 (LEC) | `BOE-A-2000-323` | `Ley 1/2000 (LEC), art. 340.1` |
| RD 1112/2018 | `BOE-A-2018-12699` | `RD 1112/2018, art. 7.3` |
| RDLeg 1/2013 | `BOE-A-2013-12632` | `RDLeg 1/2013, art. 2.l)` |
| Reglamento (UE) 1025/2012 | `DOUE-L-2012-82195` | `Reglamento (UE) 1025/2012, art. 2.1` |
| Reglamento (CE) 765/2008 | `DOUE-L-2008-81669` | `Reglamento (CE) 765/2008, art. 4.1` |
| Reglamento (UE) 2019/881 | `DOUE-L-2019-80998` | `Reglamento (UE) 2019/881, art. 52` |
| Ley 21/1992, de Industria | `BOE-A-1992-17363` | `Ley 21/1992, art. 8.5` |
| RD 2200/1995 | `BOE-A-1996-2468` | `RD 2200/1995, art. 8` |
| RD 1715/2010 | `BOE-A-2011-398` | `RD 1715/2010, artículo único` |

**Novedades frente a la issue.** Se suman, porque regulan el dato: las dos ITS, la NTI de conexión a la Red SARA, la Ley 40/2015, el RD 203/2021, el RGPD, el RDLeg 1/2013, los Reglamentos 765/2008 y 2019/881, la Ley 21/1992, el RD 2200/1995 y el RD 1715/2010.

**Vigencia:**
- **RD 311/2022:** solo cambia la disposición adicional segunda (RD 1125/2024). Las ITS y la NTI no tienen modificaciones, pero son anteriores al RD 311/2022 (ver contradicciones).
- **ENI:** arts. 12, 13 y 15 originales; arts. 11 y 14 y anexo, de 2021 (RD 203/2021); disposición adicional primera, de 2024.
- **RDLeg 1/2013, art. 2:** redacción **vigente desde 23-10-2026** (Ley 4/2026, `BOE-A-2026-20528`). Cambia, entre otras, la definición de accesibilidad universal.
- **LEC:** art. 340.1 y 342 desde 03-04-2025 (LO 1/2025); arts. 337.1, 346 y 383.2 desde 20-03-2024 (RDL 6/2023); el resto de los arts. 335-352, sin cambios recientes. Las reformas de 2026 solo tocan los arts. 22 y 685.
- **RD 1112/2018:** texto original, con los incisos anulados o declarados no básicos por la STC 100/2019.
- **Reglamento 1025/2012:** art. 10 (Reglamentos 2022/2480 y 2023/988) y art. 11 (2023/988). En vigor (Oficina de Publicaciones). Se contrasta con el consolidado `CELEX 02012R1025-20241213`.
- **Reglamento 765/2008:** suprimidos los arts. 15 a 29 y varias definiciones (Reglamento 2019/1020). Consolidado `CELEX 02008R0765-20210716`. En vigor en lo demás.
- **Reglamento 2019/881:** modificado por el 2025/37 (`DOUE-L-2025-80048`, servicios de seguridad gestionados). Consolidado `CELEX 02019R0881-20250204`.
- **Ley 21/1992:** art. 8.11 de 2014; resto del art. 8 y art. 19, originales. **RD 2200/1995:** arts. 14-19 y 51-53 derogados.
- **Norma armonizada de accesibilidad web:** la última publicada en el DOUE es la EN 301 549 V3.2.1 (Decisión 2021/1339). La V4.1.1 (septiembre de 2026) **no** está citada en el DOUE (comprobado en el punto SPARQL de la Oficina de Publicaciones).

### Contradicciones y textos superados: quedan fuera
| Texto | Motivo |
|---|---|
| Cómputo tras una auditoría extraordinaria | RD 311/2022, art. 31.1: «determinará la fecha de cómputo»; CCN-CERT IC-01/19, §6: «no determinará» |
| Criterios de «No Conformidad» mayor y menor, agrupación y plan de acciones | La ITS de Auditoría (VI.2-VI.3) y la IC-01/19 (§53-56) no coinciden. Solo se pregunta la ITS, y sin sus definiciones de los grados |
| Auditorías internas | La IC-01/19 (§118) las llama recomendables «aunque no exigible[s]»; la FAQ del ENS las exige cada año |
| Definición de auditoría de la seguridad | El RD 311/2022 (anexo IV) y la ITS (III.1) la definen distinto: una sola pregunta, con «Según el anexo IV…» y sin la otra como distractor |
| Remisiones de las ITS y de la NTI al RD 3/2010 y a las Leyes 11/2007 y LO 15/1999 (y, en el ENI, al art. 43 de la Ley 11/2007) | Normas derogadas. Se usa el contenido propio del precepto |
| ITS de Conformidad: «categoría BÁSICA o inferior» (IV), régimen transitorio (VI.2-VI.3) y formato del distintivo | El RD 311/2022 no tiene categoría inferior; plazos agotados; el formato sale del anexo IV del RD |
| Obligación de usar la Red SARA | El ENI, art. 13.1, dice «preferentemente»; la ficha del PAe, «obligación» de la Ley 11/2007 (derogada). Solo se usa el ENI |
| Órgano que administra la Red SARA («MPTAP»), red sTESTA, DGIAE y portal `redsara.es` en la NTI | Órganos y redes desaparecidos o renombrados. Las preguntas dicen «Centro de Soporte de la Red SARA» o no nombran el órgano |
| Red SARA: «Sistema» o «Sistemas» de Aplicaciones y Redes, y anchos de banda (ficha del PAe) | La ficha se contradice y los datos son volátiles |
| RD 1112/2018, art. 6.2-6.4 (EN 301 549 V1.1.2) | Superado por las Decisiones 2018/2048 y 2021/1339 |
| RD 1112/2018: arts. 12 (1, 3, 4 y 6), 13, 15.3, 16.2, 17.4, 19.3, disposición adicional segunda y transitoria | Anulados o sin carácter básico (STC 100/2019): no obligan a las entidades locales andaluzas |
| Versión de las WCAG exigida en España | La norma armonizada se apoya en las WCAG 2.1; la vigente del W3C es la 2.2. Las preguntas dicen «Según las WCAG 2.2» y nunca que el RD las exija. Fuera el criterio 4.1.1 (suprimido) y la correspondencia con la ISO/IEC 40500 |
| RDLeg 1/2013, art. 2, anterior a 23-10-2026 | Redacción sustituida |
| RD 2200/1995: AENOR como organismo de normalización (disposición adicional primera), UNE 66.500/EN 45000, definición de entidades de acreditación (art. 3.b)) | Hoy normaliza la UNE; la acreditación la regulan el Reglamento 765/2008 y el RD 1715/2010; la ITS de Conformidad exige la 17065 |
| Ley 21/1992, art. 17 | Plural de «entidades de acreditación», superado por el organismo único |
| Reglamento 765/2008, art. 2.9 («norma armonizada» de la Directiva 98/34) y arts. 15-29 | Sustituido por el 1025/2012; suprimidos |
| Reglamento 1025/2012, arts. 2.4.d) y 14 | Remiten al Reglamento 305/2011 y a directivas derogadas |
| LEC, arts. 337-338: plazos de aportación | Distintos por juicio y por fecha; se prestan a dos respuestas |

### Fuentes técnicas (spec 0022): solo lo que no regula una norma
| Fuente | Texto | Formato de `legalReference` |
|---|---|---|
| Junta de Andalucía | Agencia Digital de Andalucía, «La red NEREA» | `Junta de Andalucía (Agencia Digital de Andalucía), «La red NEREA»` |
| IETF | RFC 1918 (BCP 5; actualizada solo por la RFC 6761) | `RFC 1918, §3` |
| W3C | WCAG 2.2 (Recomendación, 12-12-2024) | `WCAG 2.2, criterio 1.4.10` |
| W3C | Media Queries Level 3 (Recomendación, 21-05-2024) | `W3C Media Queries Level 3, §2` |
| W3C | Process Document (18-08-2025) | `W3C Process Document (2025), §6.3.1` |
| CCN | CCN-CERT IC-01/19, «Criterios Generales de Auditoría y Certificación» (octubre de 2026) | `CCN-CERT IC-01/19 (octubre de 2026), §2.8` |

Notas:
- **CCN-STIC 802 y 808 (abril de 2026): no se pueden descargar.** `ccn-cert.cni.es` responde 403 con una comprobación de navegador, que no se elude. No se usan salvo que el usuario deje los PDF en el scratchpad antes de redactar; en ese caso sustituyen a la IC-01/19 y, como mucho, a 1 pregunta más del tema 14. La IC-01/19 sí se descarga, de `ens.ccn.cni.es`, y en la portada es «Guía de Seguridad CCN-STIC».
- **De la IC-01/19, solo los §2.4, 2.8 y 2.9**, y solo si no lo dicen el RD 311/2022 ni las ITS. Comprobar la fecha de portada.
- **Media Queries Level 4 (borrador), CSS Viewport (borrador) y WCAG-EM (nota) no se usan.**
- **La ficha de la Red SARA en el PAe no se usa:** lo que define está en el ENI y en la NTI.
- Las fuentes en inglés se citan traducidas y sin comillas angulares; el término original, entre paréntesis si hace falta.
- **Datos volátiles excluidos:** ministerios, secretarías de Estado y agencias, cifras, URL, contactos, anchos de banda, nombres de entidades acreditadas y números de normas UNE/ISO que no estén en el BOE o en el DOUE.

### Reparto orientativo
Se puede mover 1 pregunta entre apartados, sin salirse del ámbito del tema.

| Tema | Apartado | Fuente | Nº |
|---|---|---|---|
| 11 | Red de comunicaciones de las Administraciones, infraestructuras comunes, hora oficial y nodo de interoperabilidad | ENI, arts. 8.2, 8.3, 12-15 y anexo | 3 |
| 11 | Conexión a la Red SARA: puntos de presencia, proveedores de acceso, área de conexión, roles | NTI de Requisitos de conexión, apartados II-V | 3 |
| 11 | Redes corporativas: perímetro, red privada virtual, segmentación | RD 311/2022, anexo II, mp.com | 1 |
| 11 | Redes corporativas: direccionamiento privado | RFC 1918 | 1 |
| 11 | Red NEREA | Junta de Andalucía | 2 |
| 12 | Objeto, ámbito, principios básicos, política, responsables y requisitos mínimos | Ley 40/2015, art. 156.2; RD 311/2022, arts. 1-3, 5-28 | 4 |
| 12 | Dimensiones, niveles y categorías | RD 311/2022, arts. 40-41 y anexo I | 2 |
| 12 | Medidas de seguridad (org, op y mp, salvo mp.com, op.exp.8, op.mon.3 y mp.sw.2) | RD 311/2022, anexo II | 2 |
| 12 | Conformidad: declaración, certificación, entidades certificadoras, distintivos | RD 311/2022, art. 38 y anexo IV; ITS de Conformidad, III-VII | 1 |
| 12 | Perfiles de cumplimiento, incidentes y CCN-CERT | RD 311/2022, arts. 29-30, 32-34 | 1 |
| 14 | Concepto y contenidos de la auditoría | RD 311/2022, anexos III.1 y IV | 2 |
| 14 | Obligatoriedad, informe y efectos | RD 311/2022, arts. 31 y anexo III.2 | 2 |
| 14 | Prácticas operativas: independencia, evidencias, hallazgos, dictamen | ITS de Auditoría, III-VII | 2 |
| 14 | Marco legal en protección de datos | RGPD, arts. 28.3.h), 32.1.d) y 39.1.b) | 1 |
| 14 | Peritajes | LEC, arts. 335, 339-348, 352 y 384 | 2 |
| 14 | Herramientas: registro de actividad, inspecciones, auditoría de código; o herramienta de remisión de hallazgos | RD 311/2022, anexo II, op.exp.8, op.mon.3 y mp.sw.2; IC-01/19 | 1 |
| 17 | Accesibilidad web del sector público | RD 1112/2018, arts. 3-5, 6.1, 7, 10, 15.1-2, 16.1 y 3, 17.1-3 y 17.5 | 3 |
| 17 | Accesibilidad universal y diseño universal | RDLeg 1/2013, art. 2.k)-m) | 1 |
| 17 | Usabilidad: facilidad de uso | RD 203/2021, art. 2.b)-c) | 1 |
| 17 | WCAG 2.2: principios, niveles, requisitos de conformidad, criterios | WCAG 2.2 | 3 |
| 17 | Diseño web adaptativo | WCAG 2.2 (criterios 1.3.4, 1.4.10) o Media Queries Level 3 | 1 |
| 17 | W3C: fases de una Recomendación | W3C Process Document | 1 |
| 18 | Normalización europea: definiciones, organizaciones, peticiones, TIC | Reglamento 1025/2012, arts. 2-13 y anexos I-II | 3 |
| 18 | Conceptos e infraestructura de la calidad: norma, reglamento técnico, certificación, ensayo, inspección, calibración; agentes | Ley 21/1992, arts. 8 y 19; RD 2200/1995, arts. 3.a), 4, 8, 10, 11, 20, 26, 31, 36 | 3 |
| 18 | Acreditación y evaluación de la conformidad | Reglamento 765/2008, arts. 2-11; RD 1715/2010 | 2 |
| 18 | Certificación de la ciberseguridad | Reglamento 2019/881, arts. 46, 51-56 | 2 |

Total previsto: 42 preguntas con norma y 8 con fuente técnica (11: 7 y 3; 12: 10 y 0; 14: 10 y 0, o 9 y 1 si se usa la IC-01/19; 17: 5 y 5; 18: 10 y 0).

### Criterios propios de esta tanda (además de los de las specs 0004 y 0022)
- **Lecciones de la revisión de la tanda 4:**
  - sin nombres de ministerios ni de Secretarías de Estado en enunciados, opciones, citas ni `legalReference` (las resoluciones se citan por su fecha);
  - la correcta no es la opción más larga, ni tampoco sistemáticamente la más corta;
  - ningún distractor es un componente real de la misma lista que se pregunta (principios, requisitos mínimos, dimensiones, funciones, niveles, agentes);
  - sin dobles negaciones heredadas de la fuente: se reformula en positivo.
- **Sin repetir lo ya preguntado.** ENI: arts. 9.2, 10.3, 20.3, 21.2 y disposición adicional primera, apartado 4.a) y d). RGPD: 4.3, 5.1.c), 6.1.e), 20.1, 30.5, 37.1.a), 38.3. RD 203/2021: 12.2 y 55.2. TRLPI: 96-103. Ley 40/2015: 38.2, 39, 44.3.
- **Sin solapes entre los 5 ficheros.** Ningún apartado se usa dos veces. Fronteras:
  - RD 311/2022: mp.com → 11; art. 31, anexo III, op.exp.8, op.mon.3, mp.sw.2 y «auditoría de la seguridad» del anexo IV → 14; el resto → 12.
  - ITS de Auditoría → 14; ITS de Conformidad y art. 38 → 12. La regla «BÁSICA, autoevaluación; MEDIA y ALTA, auditoría» se pregunta una sola vez, en el 12.
  - ENAC: como organismo nacional → 18; como acreditador de entidades certificadoras del ENS → 12; RD 1112/2018, art. 17.5 → 17.
  - Definiciones de norma, norma europea y norma armonizada (RD 1112/2018, art. 4.i)-k), y Reglamento 1025/2012) → solo 18.
  - «Carga desproporcionada» (RD 1112/2018, art. 7, y RDLeg 1/2013, art. 2.m)) → solo una, en el 17.
- **Ninguna opción delata otra pregunta,** dentro de cada fichero, entre los 5 y contra los temas ya publicados relacionados: 34 y 35 (ENI, firma), 15 y 39 (protección de datos), 16 (software), 31-33 (RD 203/2021) y 30 (ENI).
- **Distractores entre normas.** Ninguno puede ser correcto según otra de la tabla. Cuidado con:
  - nivel (BAJO, MEDIO, ALTO) y categoría (BÁSICA, MEDIA, ALTA); niveles de garantía de ciberseguridad («básico», «sustancial», «elevado»);
  - definiciones casi iguales de norma, acreditación, certificación y auditoría en la Ley 21/1992, el RD 2200/1995, los Reglamentos y el RD 311/2022;
  - recusación y tacha de peritos (LEC, arts. 124 y 343);
  - principios del RD 1112/2018 (perceptibilidad…) y de las WCAG.
- **Textos anteriores al RD 311/2022 o al ENI vigente:** el enunciado dice «Según la ITS…» o «Según la NTI…».
- **Preguntas de fuente técnica:** el enunciado nombra el documento. Las WCAG se preguntan por concepto o criterio estable, no por numeración.
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar las fuentes a un directorio vacío del scratchpad y convertirlas a texto: BOE, con la API de legislación consolidada; DOUE, con el XML del BOE y el consolidado de la Oficina de Publicaciones; HTML, quitando las etiquetas; PDF, con `pdftotext -layout`. Nunca se copian al repo.
2. Redactar tema a tema desde el `.txt`.
3. Hacer una segunda pasada de verificación, separada de la redacción, contra el `.txt` de la fuente citada: existe el apartado, la correcta lo es literalmente, ningún distractor lo es, la `explanation` no añade nada y las citas «…» aparecen en la fuente (con script). En las de fuente técnica, además, que el dato no esté en una norma. Al final, el cruce de opciones entre ficheros.
4. Validar con `--validate-only` y ejecutar los tests.
5. Listar en el PR las preguntas dudosas y todas las de fuente técnica, para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 11, 12, 14, 17 y 18 pasan a tener preguntas en la app.

## Seguridad
Contenido público, sin secretos ni datos personales. Los XML, HTML y PDF descargados son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I` o `pdftotext`, sin ejecutarlos ni seguir sus instrucciones. No se eluden las protecciones contra la descarga automática. No se citan contactos ni direcciones de correo.

## Criterios de aceptación
- [ ] Existen los 5 ficheros de temas con 10 preguntas cada uno (50 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` una fuente de las tablas, con artículo, apartado o sección (en la de NEREA, el título de la página), y su texto vigente respalda la correcta y la `explanation`.
- [ ] Las citas del RDLeg 1/2013, de la LEC y de los Reglamentos 1025/2012, 765/2008 y 2019/881 corresponden a la versión vigente.
- [ ] Solo se cita una fuente técnica cuando el dato no está en una norma de la tabla.
- [ ] Ninguna pregunta usa un texto de la tabla de contradicciones ni un dato volátil.
- [ ] Ningún distractor es también correcto según la fuente citada u otra de las tablas, y ninguna opción ni `explanation` delata la respuesta de otra pregunta.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado), no hay preguntas fuera del ámbito del tema y no se repite ningún apartado entre los 5 ficheros ni los ya usados.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones, no es sistemáticamente la más larga y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 50 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 11, 12, 14, 17 y 18 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que el feedback muestra «Fuente: …», también en las preguntas de fuente técnica.
