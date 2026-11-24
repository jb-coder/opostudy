# 0027 — Contenido, tanda 4: SIA y DIR3, firma electrónica, Cl@ve, factura electrónica y Facturae

- **Issue:** #70
- **Milestone:** v0.4.0
- **Estado:** lista

> Cuarta de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad y proceso en la spec 0004, fuentes técnicas en la spec 0022. Modelo: spec 0026.

## Objetivo
Dar 50 preguntas de test, redactadas y verificadas contra el texto de la fuente citada, a los temas 30 y 35 a 38 (administración electrónica del bloque II-C), que hoy no tienen ninguna.

## Alcance
**Incluye:**
- `topics/sia-dir3.json`: 10 preguntas del tema 30.
- `topics/firma-electronica.json`: 10 preguntas del tema 35.
- `topics/clave.json`: 10 preguntas del tema 36.
- `topics/factura-electronica.json`: 10 preguntas del tema 37.
- `topics/facturae.json`: 10 preguntas del tema 38.

**No incluye:**
- Preguntas de otros temas. Notifica, Carpeta Ciudadana, sede, registro, SIR, expediente, INSIDE y Archive (temas 31-34), ENS (13) y protección de datos (39) se pueden nombrar, pero no son el objeto de la pregunta.
- Firma no criptográfica (Resoluciones de 14-07-2017 y 23-02-2022) e identidad con tecnología de registro distribuido (Resolución de 06-07-2023): específicas de la AGE.
- Actos de ejecución de eIDAS (ver «Vigencia»), salvo para descartar lo derogado.
- Factura entre empresas (RD 238/2026, Orden HAC/1028/2026) y Veri\*factu (RD 1007/2023): no son la Ley 25/2013. Única excepción: los formatos del art. 7.1 del RD 238/2026, en el tema 38.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 30** (22 del bloque). Sistema de Información Administrativa (SIA). Directorio Común (DIR3). Servicios web e integraciones.
- **Tema 35** (27). Firma electrónica. Plataforma @firma. Autoridades de certificación. Reglamento eIDAS.
- **Tema 36** (28). Gestión de identidades. Cl@ve.
- **Tema 37** (29). Ley 25/2013 de impulso de la factura electrónica. Punto General de Entrada de Facturas Electrónicas. Integración.
- **Tema 38** (30). El formato de factura electrónica Facturae.

### Fuentes normativas (identificadores y vigencia comprobados el 24-11-2026)
| Norma | Identificador | Abreviatura en `legalReference` |
|---|---|---|
| Reglamento (UE) 910/2014 (eIDAS) | `DOUE-L-2014-81822` | `Reglamento (UE) 910/2014 (eIDAS), art. 25.2` |
| — modificado por el Reglamento (UE) 2024/1183 | `DOUE-L-2024-80608` | (la misma; se cita siempre el 910/2014) |
| Ley 6/2020, servicios electrónicos de confianza | `BOE-A-2020-14046` | `Ley 6/2020, art. 4.2` |
| Ley 39/2015 | `BOE-A-2015-10565` | `Ley 39/2015, art. 10.5` |
| Ley 40/2015 | `BOE-A-2015-10566` | `Ley 40/2015, art. 42` |
| RD 203/2021 | `BOE-A-2021-5032` | `RD 203/2021, art. 16.2` |
| RD 4/2010 (ENI) | `BOE-A-2010-1331` | `RD 4/2010 (ENI), art. 9.1.a)`; `RD 4/2010 (ENI), disposición adicional primera, apartado 4.d)` |
| NTI de Política de Firma y Sello Electrónicos y de Certificados, Resolución de 27-10-2016 | `BOE-A-2016-10146` | `NTI de Política de Firma (Resolución de 27-10-2016), apartado II.1` |
| Orden PRE/1838/2014 (Cl@ve) | `BOE-A-2014-10264` | `Orden PRE/1838/2014, apartado tercero.2` |
| Prescripciones técnicas de Cl@ve, Resolución de 14-12-2015 | `BOE-A-2015-14215` | `Resolución de 14-12-2015 (prescripciones técnicas de Cl@ve), apartado IV.1` |
| Ley 25/2013 | `BOE-A-2013-13722` | `Ley 25/2013, art. 6.7` |
| Orden HAP/1074/2014 (PGEFe) | `BOE-A-2014-6662` | `Orden HAP/1074/2014, art. 5 bis.1` |
| Orden HAP/492/2014 (registro contable de facturas) | `BOE-A-2014-3373` | `Orden HAP/492/2014, disposición adicional tercera` |
| Condiciones técnicas del PGEFe, Resolución de 10-10-2014 | `BOE-A-2014-10660` | `Resolución de 10-10-2014 (condiciones técnicas del PGEFe), anexo I.a)` |
| RD 1619/2012, obligaciones de facturación | `BOE-A-2012-14696` | `RD 1619/2012, art. 10.1.a)` |
| RD 238/2026, factura entre empresarios y profesionales | `BOE-A-2026-7295` | `RD 238/2026, art. 7.1` |

**Novedades frente a la issue.** A las normas previstas se suman, porque regulan el dato mejor que el PAe: RD 203/2021, ENI, Ley 40/2015, Resolución de 14-12-2015, Orden HAP/492/2014, Resolución de 10-10-2014, RD 1619/2012 y RD 238/2026.

**Vigencia:**
- **eIDAS.** No está en la API consolidada: se usa el XML del DOUE publicado por el BOE (`https://www.boe.es/buscar/xml.php?id=DOUE-L-2014-81822`), el del 2024/1183 y las correcciones `DOUE-L-2016-82007` (art. 36.c)), `DOUE-L-2017-80776` (título y arts. 3.19 y 42.1.c)) y `DOUE-L-2025-80570` (art. 47). Para montar la versión vigente se contrasta con el consolidado de la Oficina de Publicaciones (`CELEX 02014R0910-20241018`, español, sin valor jurídico).
  - Lo modifican solo la Directiva (UE) 2022/2555, que suprime el art. 19, y el Reglamento 2024/1183. Comprobado en el BOE y en el punto SPARQL de la Oficina de Publicaciones.
  - Los actos de ejecución de 2025 y 2026 no cambian el articulado y no se preguntan.
- **Ley 6/2020:** modificada por la Ley 11/2023 (arts. 19 bis y 20). El resto es el texto original.
- **Ley 39/2015, arts. 9 y 10:** vigentes desde 30-06-2022 (Ley 11/2022).
- **RD 203/2021, arts. 27.1 y 28.1:** vigentes desde 02-04-2025 (RD 255/2025).
- **Orden PRE/1838/2014, Resolución de 14-12-2015 y NTI de 2016:** sin modificaciones.
- **Ley 25/2013:** arts. 4, 6, 9 y 12.3 cambiados en 2015 (Ley Orgánica 6/2015).
- **Orden HAP/1074/2014 y Orden HAP/492/2014:** modificadas por la Orden HAP/1650/2015. De la primera está derogado el apartado 6 de la disposición adicional primera.
- **RD 1619/2012, arts. 8 a 10:** vigentes desde 20-04-2026 (RD 238/2026). El art. 10.1.a) ya dice «certificado electrónico cualificado».
- **RD 238/2026:** vigente desde 20-04-2026, con aplicación efectiva diferida (disposición final cuarta). No se pregunta por plazos ni por fechas de aplicación.

### Contradicciones y textos superados: quedan fuera
| Texto | Motivo |
|---|---|
| Ley 6/2020, art. 9.3.c) (cese con 2 meses de antelación) | El art. 24.2.a) de eIDAS exige avisar al supervisor con 3 meses desde 2024 |
| Ley 6/2020, art. 13 (notificación de incidentes) | Remite al art. 19.2 de eIDAS, suprimido |
| RD 203/2021, arts. 28.2-3 y 29.4, y disposición adicional novena (autorización previa de sistemas no basados en certificado) | La Ley 39/2015 exige comunicación desde 2022 (arts. 9.2.c) y 10.2.c)) |
| NTI de Política de Firma, apartado III.4 (formatos de contenido) | Remite a la Decisión (UE) 2015/1506, derogada por el Reglamento (UE) 2026/248 |
| eIDAS, art. 19 | Suprimido |
| Remisiones de las órdenes y resoluciones de 2014-2015 a las Leyes 30/1992, 11/2007 y 59/2003, al RD 1671/2009, al RD 3/2010 y a la LO 15/1999 | Normas derogadas. Se usa el contenido propio del precepto, nunca la remisión |
| Cita del ENI, art. 9, que hacen la ficha, el documento de visión y el manual de atributos de DIR3 | Redacción anterior al RD 203/2021. Se cita el ENI vigente |
| Modalidades de Cl@ve | La Orden de 2014 y la Resolución de 2015 dicen «Cl@ve ocasional (PIN)»; el portal `clave.gob.es` solo ofrece Cl@ve Móvil y Permanente. La pregunta dice siempre «Según la Orden PRE/1838/2014…» y no pregunta cuáles hay ahora |
| Versión de Facturae | La Ley 25/2013 (disposición adicional segunda) dice 3.2; la web, 3.2.x. Cada pregunta dice de qué fuente sale |
| Ficha de Integr@ | Habla de @firma v5; la ficha de @firma, de la 6.4 |
| Política de firma de Facturae v3.1 | Se apoya en la Ley 59/2003. Solo se usan los perfiles y la estructura de la firma, no los certificados ni los algoritmos |

### Fuentes técnicas (PAe y facturae.gob.es, spec 0022): solo lo que no regula una norma
Todas devuelven 200 sin autenticación el 24-11-2026. Las fichas están en `https://administracionelectronica.gob.es/ctt/<solución>`, los documentos, en `…/descargas`, y Facturae, en `https://www.facturae.gob.es/formato`.

| Solución | Documento | Formato de `legalReference` |
|---|---|---|
| SIA (`sia`) | Ficha; «Guía de contenidos» (diciembre de 2020); «Servicios web de inserción 3.1 - Trámites», v5.0 (09-03-2022); «Servicios web de consulta - Trámites», v1.2 (01-02-2021); «Servicios web de consulta - Catálogos», v3.0 (03-11-2021) | `PAe, SIA, «Servicios web de inserción 3.1 - Trámites» v5.0, §2.1` |
| DIR3 (`dir3`) | Ficha; «Modelo de Codificación y Manual de Atributos de Información», v3.16 (26-08-2021); «Manual de Integración con Directorio Común», v4.27 (13-03-2023); «Guía de Buenas Prácticas», v1.4 (26-08-2020) | `PAe, DIR3, «Manual de Integración con Directorio Común» v4.27, §2.1.2` |
| @firma (`afirma`), TS@ (`tsa`), VALIDe (`valide`), Autofirma (`clienteafirma`) | Fichas; «Manual de Programación de Web Services de @firma 6», rev. 060 (17-07-2019) | `PAe, @firma (ficha del CTT), «Descripción»` |
| Cl@ve (`clave`) | Ficha; «Manual de Integración para Proveedores de Servicios», Cl@ve 2, v2.8.4_07 r14 (13-11-2025) | `PAe, Cl@ve, «Manual de Integración para Proveedores de Servicios» r14, §6.3` |
| FACe (`face`) | Ficha; «Manual de API de Proveedores» y «Manual de API de Organismos», v2.0.0 (19-11-2025) | `PAe, FACe, «Manual de API de Proveedores» v2.0.0, §2.3` |
| Facturae | «Esquema en castellano, versiones 3.2, 3.2.1 y 3.2.2» (06-06-2017); «Historial de versiones», 3.2.2; XSD 3.2.2; «Política de firma», v3.1 | `Facturae, «Esquema, versiones 3.2, 3.2.1 y 3.2.2» (06-06-2017), §1.2` |

Notas:
- **Primero la norma.** Antes de citar el PAe se busca el dato en todas las normas de la tabla, en el ENI (disposición adicional primera, apartado 4, que define SIA y Directorio Común, y anexo) y en el anexo de definiciones del RD 203/2021. Así:
  - Qué son SIA y DIR3 y con qué se conectan los inventarios → ENI, no la ficha.
  - Qué es @firma como plataforma de verificación → RD 203/2021, art. 16, y ENI, art. 20; del PAe, solo el producto (servicios, formas de uso).
  - Roles de centros administrativos de la factura → Orden HAP/1074/2014, anexo I; estados → Resolución de 10-10-2014.
- **Solo «Resumen» y «Descripción» de las fichas**, nunca las noticias.
- **El manual de Cl@ve r15** («Cartera Digital, próxima implantación») no se usa: describe algo no desplegado.
- **El esquema de Facturae en PDF se contrasta con el XSD 3.2.2.** Si difieren, la pregunta no entra.
- **Datos volátiles excluidos:** organismo responsable, cifras, URL, versiones de producto, contactos, tamaños de página o de mensaje, caducidad de tokens y códigos de error.

### Reparto orientativo
Se puede mover 1 pregunta entre apartados, sin salirse del ámbito del tema.

| Tema | Apartado | Fuente | Nº |
|---|---|---|---|
| 30 | Inventarios de información administrativa y definición de SIA y Directorio Común | ENI, art. 9 y disposición adicional primera, apartado 4.a) y d) | 3 |
| 30 | SIA: contenido (procedimiento, servicio, código) y publicación de procedimientos | PAe, SIA («Guía de contenidos»); Ley 39/2015, art. 21.4 | 1 |
| 30 | SIA: servicios web (operaciones de inserción, autenticación, consulta, catálogos) | PAe, SIA | 2 |
| 30 | DIR3: codificación y modelo de información | PAe, DIR3 (Manual de Atributos) | 2 |
| 30 | DIR3: integración (consumo, provisión, perfiles, servicios) | PAe, DIR3 (Manual de Integración) | 2 |
| 35 | eIDAS: definiciones, firma, sello, sello de tiempo y efectos jurídicos | eIDAS, arts. 3 (salvo puntos 1-5 y 42), 25-26, 35-36, 41 y 46 | 3 |
| 35 | eIDAS: prestadores, listas de confianza y servicios nuevos de 2024 | eIDAS, arts. 13, 17-24, 32-34, 45 y 45 bis-45 terdecies | 1 |
| 35 | Autoridades de certificación: certificados, obligaciones y responsabilidad | Ley 6/2020, arts. 3-12 (salvo 9.3.c)) y 16 | 2 |
| 35 | Firma en las Administraciones: sistemas admitidos, sello, CSV, política de firma, plataformas de validación | Ley 39/2015, art. 10; Ley 40/2015, arts. 42, 43 y 45; RD 203/2021, arts. 16-24; ENI, arts. 18 y 20; NTI de Política de Firma, apartados I-IV (salvo III.4) | 2 |
| 35 | Plataforma @firma y su suite | PAe, @firma, TS@, VALIDe, Autofirma | 2 |
| 36 | Identificación de interesados y representantes | Ley 39/2015, art. 9 (salvo 9.2.c)); RD 203/2021, arts. 26, 27, 28.1 y 30-35 | 2 |
| 36 | Identidad electrónica en eIDAS (niveles de seguridad, reconocimiento mutuo, cartera europea) y nodo eIDAS español | eIDAS, arts. 3 (puntos 1-5 y 42), 5 bis-12 ter; RD 203/2021, disposición adicional tercera | 2 |
| 36 | Cl@ve: creación, ámbito, modalidades y adhesión | Orden PRE/1838/2014 | 2 |
| 36 | Cl@ve: niveles de registro, firma centralizada, punto de acceso y responsables | Resolución de 14-12-2015 | 2 |
| 36 | Cl@ve: integración (SAML, proveedores de servicio y de identidad, pasarela, LoA) | PAe, Cl@ve | 2 |
| 37 | Ley 25/2013: objeto, obligados, PGEFe, archivo, registro contable, control | Ley 25/2013, arts. 1-4 y 6-13 | 4 |
| 37 | PGEFe: presentación, seguimiento, adhesión, directorio, FACe | Orden HAP/1074/2014, arts. 3, 4, 6-11 y disposición adicional primera | 2 |
| 37 | Registro contable de facturas e integración con el PGEFe | Orden HAP/492/2014 y anexo II | 1 |
| 37 | Estados de tramitación de la factura | Resolución de 10-10-2014 | 1 |
| 37 | FACe: integración (API, autenticación, grupos de operaciones) | PAe, FACe | 2 |
| 38 | Formato y firma exigidos por la norma; formatos admitidos entre empresas | Ley 25/2013, art. 5 y disposición adicional segunda; RD 1619/2012, art. 10; RD 238/2026, art. 7.1 | 2 |
| 38 | Campos obligatorios, extensiones, anexos y definiciones | Orden HAP/1074/2014, arts. 5, 5 bis y 5 ter, y anexos I y III | 2 |
| 38 | Estructura: cabecera, partes, facturas, extensiones y sus valores | Facturae, esquema | 4 |
| 38 | Novedades de la versión 3.2.2 | Facturae, historial | 1 |
| 38 | Firma de Facturae: perfiles | Facturae, política de firma v3.1 | 1 |

Total previsto: 31 preguntas con norma y 19 con fuente técnica (30: 3 y 7; 35: 8 y 2; 36: 8 y 2; 37: 8 y 2; 38: 4 y 6). Tiene más fuente técnica que la tanda 3 porque ni los servicios web ni el esquema Facturae están en ninguna norma.

### Criterios propios de esta tanda (además de los de las specs 0004 y 0022)
- **Sin repetir lo ya preguntado.**
  - Ley 39/2015: 3.c), 6.6, 9.2.c), 11.2, 12.2, 13.c) y d), 14.2, 16.3, 16.5, 21.3, 26.1, 31.2.b), 35-45 y 70.4.
  - Ley 40/2015: 38.2, 39 y 44.3.
  - RD 203/2021: 7.3, 8.1, 10.2, 12.2, 39.4, 40.1, 42.3, 44.4, 55.2, 58, 59 y la definición de PAG.
  - ENI: 10.3 y 21.2. NTI de las tandas anteriores.
  - Los temas LPAC ya preguntan por el funcionario habilitado (art. 12.2): no se pregunta por el RD 203/2021, art. 30, en lo que coincide.
- **Sin solapes entre los 5 ficheros.** Ningún apartado se usa dos veces. Reparto de fronteras:
  - eIDAS, capítulo II y cartera → 36; capítulo III en adelante → 35.
  - Ley 39/2015, art. 9 → 36; art. 10 → 35.
  - Ley 25/2013, art. 5 y disposición adicional segunda → 38; el resto → 37.
  - Orden HAP/1074/2014, arts. 5 a 5 ter y anexos → 38; el resto → 37.
  - Códigos DIR3 en la factura (Orden HAP/492/2014, disposición adicional tercera, y anexo I de la HAP/1074/2014) → una sola pregunta, en el 37 o en el 38.
- **Ninguna opción delata otra pregunta.** Se comprueba dentro de cada fichero, entre los 5 y contra los temas 31-34 (opciones y `explanation`). Atención a las definiciones del ENI y de eIDAS usadas como distractores.
- **Distractores entre normas.** eIDAS, Ley 6/2020, Ley 39/2015, Ley 40/2015 y RD 203/2021 se repiten entre sí: ningún distractor puede ser correcto según otra de ellas. Cuidado con «avanzada» y «cualificada», firma y sello, y «reconocido» (órdenes de 2014) y «cualificado» (hoy).
- **Textos de 2014-2016 y eIDAS.** Si el enunciado usa «certificado reconocido», dice que es la expresión de la norma citada.
- **Preguntas de fuente técnica:** para qué sirve cada cosa, la operación o el concepto que la define. Distractores reales (otra operación, otro elemento del esquema, otra solución). El enunciado dice de qué documento sale.
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar las fuentes a un directorio vacío del scratchpad y convertirlas a texto: BOE, con la API de legislación consolidada; eIDAS, con el XML del DOUE; HTML, quitando las etiquetas; PDF, con `pdftotext -layout`. Nunca se copian al repo.
2. Redactar tema a tema desde el `.txt`.
3. Hacer una segunda pasada de verificación, separada de la redacción, contra el `.txt` de la fuente citada: existe el apartado, la correcta lo es literalmente, ningún distractor lo es, la `explanation` no añade nada y las citas «…» aparecen en la fuente (con script). En las de fuente técnica, además, que el dato no esté en una norma. Al final, el cruce de opciones entre ficheros.
4. Validar con `--validate-only` y ejecutar los tests.
5. Listar en el PR las preguntas dudosas y todas las de fuente técnica, para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 30 y 35 a 38 pasan a tener preguntas en la app.

## Seguridad
Contenido público, sin secretos ni datos personales. Los XML, HTML y PDF descargados son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I` o `pdftotext`, sin ejecutarlos. Los ZIP y los kits de integración no se descargan. No se citan contactos ni direcciones de correo.

## Criterios de aceptación
- [ ] Existen los 5 ficheros de temas con 10 preguntas cada uno (50 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` una fuente de las tablas, con artículo, apartado o sección, y su texto vigente respalda la correcta y la `explanation`.
- [ ] Las citas de eIDAS corresponden a la versión vigente (con el Reglamento 2024/1183 y las correcciones de errores).
- [ ] Solo se cita una fuente técnica cuando el dato no está en una norma de la tabla.
- [ ] Ninguna pregunta usa un texto de la tabla de contradicciones ni un dato volátil.
- [ ] Ningún distractor es también correcto según la fuente citada u otra de las tablas, y ninguna opción ni `explanation` delata la respuesta de otra pregunta.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado), no hay preguntas fuera del ámbito del tema y no se repite ningún apartado entre los 5 ficheros ni los ya usados.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 50 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 30 y 35 a 38 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que el feedback muestra «Fuente: …», también en las preguntas de fuente técnica.
