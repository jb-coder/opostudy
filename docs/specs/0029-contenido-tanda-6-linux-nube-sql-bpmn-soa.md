# 0029 — Contenido, tanda 6: Linux y nube, bases de datos y SQL, BPMN y SOA

- **Issue:** #72
- **Milestone:** v0.4.0
- **Estado:** lista

> Sexta de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad y proceso en la spec 0004, fuentes técnicas en la spec 0022. Modelo: spec 0028.

## Objetivo
Dar 40 preguntas de test, redactadas y verificadas contra el texto de la fuente citada, a los temas 9, 10 y 13 (bloque II-A) y 24 (bloque II-B), que hoy no tienen ninguna.

## Alcance
**Incluye:**
- `topics/linux-cloud.json`: 10 preguntas del tema 9.
- `topics/bases-de-datos-sql.json`: 10 preguntas del tema 10.
- `topics/bpmn.json`: 10 preguntas del tema 13.
- `topics/soa-servicios-web.json`: 10 preguntas del tema 24.

**No incluye:**
- Preguntas de otros temas. Redes (11), ENS (12), Java (19), .NET (20), HTML (21), MVC y casos de uso (23), PHP y MySQL (25), Métrica (26), LXC (29) y firma (35) se pueden nombrar, pero no son el objeto de la pregunta.
- Contenedores y virtualización: son del tema 29 (LXC).
- Firma XML (XMLDSig, XAdES) y los servicios web de SIA, DIR3, Notifica y Cl@ve: son de los temas 30, 31, 35 y 36.
- UDDI, BPEL y REST: ninguna fuente de la tabla los define.
- BPMN DI (diagrama), el formato XML de intercambio y la correspondencia con WS-BPEL.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 9** (1 del bloque II-A). El sistema operativo Linux. Computación en la nube: IaaS, PaaS, SaaS. Nubes privadas, públicas e híbridas.
- **Tema 10** (2). Sistemas gestores de bases de datos relacionales. Modelo entidad-relación. SQL.
- **Tema 13** (5). Modelado de procesos de negocio. Notación BPMN 2.0.
- **Tema 24** (16, bloque II-B). Arquitectura SOA. Servicios web. Tecnologías XML. WS-Security. SAML.

### Decisiones que pide la issue
- **Modelo entidad-relación: entra.** Lo define Métrica v3, «Técnicas y prácticas» (guía del PAe, admitida por la spec 0022), en «Modelo Entidad/Relación Extendido», «Normalización» y «Reglas de Obtención del Modelo Físico a partir del Lógico».
- **SOA: entra.** La define el OASIS Reference Model for Service Oriented Architecture 1.0 (estándar OASIS). La lista de OASIS de la spec 0022 es de ejemplos; con esta spec se admite expresamente.
- **Servicios web: entran,** por la NTI de Protocolos de intermediación de datos (norma) y por SOAP 1.2 y WSDL 2.0. Las notas del W3C «Web Services Architecture» y «Web Services Glossary» (2004) no se usan: no son recomendaciones.
- **Proceso de negocio: entra,** con BPMN 2.0.2 y con Métrica v3, «Modelado de Procesos de la Organización».

### Fuentes normativas (identificadores y vigencia comprobados el 25-11-2026)
| Norma | Identificador | Abreviatura en `legalReference` |
|---|---|---|
| NTI de Protocolos de intermediación de datos, Resolución de 28-06-2012 | `BOE-A-2012-10049` | `NTI de Protocolos de intermediación de datos (Resolución de 28-06-2012), apartado III.5.3` |

- **Vigencia:** en vigor, sin modificaciones. De ella solo se usa III.5, apartados 2 y 3: el resto remite al RD 3/2010 y a la LO 15/1999, nombra órganos desaparecidos o son datos de la plataforma.
- **Búsqueda de norma (prioridad del BOE, spec 0022):**
  - *Computación en la nube.* El RDL 12/2018, art. 3.s) (`BOE-A-2018-12257`), define «servicio de computación en nube». No se pregunta la definición general (ver contradicciones). El RD 311/2022, op.nub.1, nombra SaaS, PaaS e IaaS sin definirlos y es del tema 12. Los modelos de servicio y de despliegue y las características esenciales no los define ninguna norma: NIST.
  - *Estándares XML y de servicios web.* La NTI de Catálogo de estándares (`BOE-A-2012-13501`, con corrección de errores `BOE-A-2013-455`) solo los enumera, con versión y estado: no define nada. No se cita.
  - *Linux, SQL, E-R y BPMN.* Sin norma.

### Fuentes técnicas (spec 0022; versión vigente comprobada el 25-11-2026)
| Fuente | Texto | Formato de `legalReference` |
|---|---|---|
| Linux | Páginas man del proyecto Linux man-pages 6.19 (08-02-2026), secciones 2, 5 y 7, en man7.org | `Linux man-pages 6.19, hier(7)` |
| Linux | Páginas man de GNU coreutils 9.11 (`chmod(1)`, `chown(1)`, `ln(1)`) | `GNU coreutils 9.11, chmod(1)` |
| NIST | SP 800-145 (septiembre de 2011; final, sin versión posterior) | `NIST SP 800-145, §2` |
| PostgreSQL | Documentación de PostgreSQL 19 (versión actual desde el 19-11-2026) | `PostgreSQL 19, §5.5.5`; `PostgreSQL 19, «CREATE VIEW»` |
| PAe | Métrica v3, «Técnicas y prácticas» | `Métrica v3, Técnicas y prácticas, «Normalización»` |
| OMG | BPMN 2.0.2 (formal/2013-12-09; la OMG no ha publicado otra posterior) | `BPMN 2.0.2, §10.6.4` |
| OASIS | SOA Reference Model 1.0 (estándar, 12-10-2006) | `OASIS SOA-RM 1.0, §3.1` |
| W3C | XML 1.0, quinta edición (26-11-2008) | `XML 1.0 (5.ª ed.), §2.1` |
| W3C | Namespaces in XML 1.0, tercera edición (08-12-2009) | `Namespaces in XML 1.0 (3.ª ed.), §6.2` |
| W3C | XML Schema (XSD) 1.1, partes 1 y 2 (05-04-2012) | `XML Schema 1.1, parte 2, §3.3` |
| W3C | XPath 3.1 (21-03-2017) y XSLT 3.0 (08-06-2017) | `XPath 3.1, §3.3.2.1`; `XSLT 3.0, §1.1` |
| W3C | SOAP 1.2, parte 1, segunda edición (27-04-2007) | `SOAP 1.2, parte 1, §5.1` |
| W3C | WSDL 2.0, parte 1 (26-06-2007) | `WSDL 2.0, parte 1, §2.2` |
| OASIS | WS-Security: SOAP Message Security 1.1.1 (18-05-2012) y UsernameToken Profile 1.1.1 | `WS-Security 1.1.1 (SOAP Message Security), §10` |
| OASIS | SAML 2.0: Core, Bindings, Profiles y Glossary (15-03-2005), con las Approved Errata 05 (01-05-2012) | `SAML 2.0 (Core), §2.7`; `SAML 2.0 (Bindings), §3.5` |

Notas:
- **Man pages:** man7.org reúne páginas de varios proyectos. Solo se usan las de Linux man-pages y GNU coreutils. No se usan `crontab(5)` (cronie), `systemd(1)`, `bash(1)`, `ps(1)` ni `shadow(5)` de shadow-utils, ni las de sysfs: cambian entre distribuciones o versiones.
- **PostgreSQL 19** tiene seis días al redactar. Solo se pregunta lo que es igual en la 17, la 18 y la 19: los fragmentos se ejecutan en contenedores `postgres:17` y `postgres:19` con el mismo resultado. Fuera lo nuevo de la 18 y la 19 (restricciones `NOT ENFORCED`, columnas generadas virtuales, claves temporales, `GROUP BY ALL`, grafos, `RETURNING old/new`).
- **XSD y XPath:** se citan las versiones vigentes (XSD 1.1, XPath 3.1) y solo se pregunta lo que es igual en XSD 1.0 y en XPath 1.0, que son las que ejecutan los validadores disponibles.
- **SAML:** cada pregunta de Core, Bindings o Profiles se contrasta con las Errata 05. La «Technical Overview» (borrador de comité) no se usa.
- **Métrica v3** se usa solo en los cuatro apartados citados. Todo lo demás es del tema 26 o del 23 y 27.
- **WS-Security 1.1.1:** 1.1 y 1.1.1 no cambian el fondo, pero la referencia nombra la 1.1.1.
- Las fuentes en inglés se citan traducidas y sin comillas angulares; el término original, entre paréntesis si hace falta.
- **Datos volátiles excluidos:** versiones del núcleo y de los productos, fechas, valores por defecto, opciones recientes, nombres de ministerios, distribuciones, proveedores y productos de nube, URL.

### SQL estándar y PostgreSQL
La norma ISO/IEC 9075 es de pago: no se cita. La conformidad se mira en el apartado «Compatibility» de la página o en el texto de la documentación.
- Si la documentación dice que PostgreSQL se aparta del estándar, lo amplía o lo deja «definido por la implementación», o no dice nada sobre el estándar, el enunciado empieza por «En PostgreSQL…». Por ejemplo: nulos en `UNIQUE`, niveles de aislamiento, índices, vistas materializadas, `TRUNCATE`, `LIMIT`, `DISTINCT ON`, `ON CONFLICT`, `RETURNING`.
- Si la documentación dice que es conforme, se puede preguntar sin esa coletilla.
- Siempre con la sintaxis del estándar cuando existan las dos (`FETCH FIRST` antes que `LIMIT`).
- Ningún distractor puede ser verdadero en el SQL estándar ni en otro gestor, si el enunciado no dice «En PostgreSQL…».

### Contradicciones y textos que quedan fuera
| Texto | Motivo |
|---|---|
| Definición general de computación en la nube | El RDL 12/2018, art. 3.s), y el NIST SP 800-145 la definen distinto; además, la transposición de la Directiva NIS 2, pendiente, sustituirá al RDL |
| Nube comunitaria | El tema solo nombra privadas, públicas e híbridas. Vale como distractor, no como objeto |
| Ubicación y propiedad de la nube privada («dentro o fuera de las instalaciones») | Se presta a dos respuestas; se pregunta por el uso exclusivo |
| `hier(7)`: `/bin`, `/sbin` y `/lib` frente a `/usr`, `/var/run`, `/etc` y `/usr/etc`, directorios «optional» | Cambian entre distribuciones o la página da dos ubicaciones |
| `umask(2)` y ACL por defecto del directorio; valor «típico» de la máscara | La ACL anula la máscara y el valor es de la distribución. El enunciado da la máscara y dice que no hay ACL |
| Contraseña cifrada en `/etc/passwd` o en `/etc/shadow`; contraseña vacía | `passwd(5)` admite las dos y depende de la configuración |
| Bit setuid o sticky en ficheros no ejecutables o en scripts; numeración de señales | Depende del sistema o de la arquitectura. Las señales se preguntan por nombre |
| Métrica v3: las entidades «Tienen que tener existencia propia» y, a la vez, las débiles dependen de otra | Contradicción interna. No se pregunta la regla |
| Métrica v3: relación 1:1 («una y sólo una») frente a cardinalidad (0,1), y clave propagada «en las dos direcciones» | Contradicción interna |
| Métrica v3: procedimiento de paso a 1FN (atributos que «no dependen funcionalmente de la clave principal») | No encaja con su definición de grupo repetitivo. Solo se preguntan las definiciones |
| Métrica v3: relaciones M:N en «Reglas de Obtención del Modelo Físico» | El apartado no las trata y están en el supuesto 0002 |
| Métrica v3: notación gráfica | Hay otras notaciones (crow's foot, UML): la pregunta diría «en la notación de Métrica» y no aporta |
| PostgreSQL, Repeatable Read y lecturas fantasma; Read Uncommitted | PostgreSQL es más estricto que el estándar. Si entra, «En PostgreSQL…» y sin la tabla del estándar como distractor |
| PostgreSQL, `GREATEST`/`LEAST` con nulos, alias de subconsulta, restricciones `CHECK` de columna, `GROUP BY` con dependencia funcional | La documentación dice que se aparta del estándar |
| BPMN: marcador de instancias múltiples | Los §10.3.3 y 10.3.5 dicen tres líneas verticales siempre; el §10.3.8 distingue paralelo (verticales) y secuencial (horizontales); la tabla 7.2 se contradice |
| BPMN: número y lista de tipos de eventos de inicio | El §10.5.2 dice «siete» y llama «Parallel» al múltiple paralelo, y los subprocesos de evento tienen otra lista |
| BPMN: número de tipos de subproceso y de tareas, y marcadores («cinco», «tres») | Varían según el apartado |
| BPMN: ejecución y monitorización | El §7.2 las deja fuera; el §10.11 trata la monitorización, y la remisión a la cláusula 14 es errónea (la semántica está en la 13) |
| BPMN: marca «X» de la puerta exclusiva | Es opcional: no sirve para distinguir |
| BPMN: cuatro categorías de elementos (BPMN 1.x) frente a cinco | Se pregunta «Según BPMN 2.0.2» y sin «Data» como respuesta ni como distractor |
| BPMN 1.2: proceso «abstracto» y tarea «None» | Renombrados (público y abstracta): no se usan como distractor |
| XML 1.1 y reglas de caracteres de nombres | La quinta edición de XML 1.0 las cambió |
| XSD: diferencias entre 1.0 y 1.1 (`xs:all`, `xs:assert`, comodines, tipos nuevos) y XPath 1.0 y 3.1 (conjuntos de nodos y secuencias, tipos, comparación, `[1]` en ejes inversos) | Dos respuestas según la versión |
| WSDL 1.1 (`portType`, `port`, `definitions`) y SOAP 1.1 (`faultcode`, `SOAPAction`, `actor`) | La NTI de Catálogo admite WSDL 1.1 y SOAP 1.1; el W3C recomienda WSDL 2.0 y SOAP 1.2. El enunciado dice la versión, no se afirma cuál exige la Administración y los términos de la otra no son distractor |
| WS-Security: SHA-1 en la contraseña resumida del UsernameToken y demás algoritmos | Algoritmos desaconsejados. Se pregunta el mecanismo, no el algoritmo |
| WS-Security: `actor` (SOAP 1.1) y `role` (SOAP 1.2) | Dos nombres para lo mismo |
| SAML 2.0: textos corregidos por las Errata 05 (RelayState en Redirect, persistente, varias declaraciones, `NotOnOrAfter`, firma) | Se usa el texto corregido o se evita. RelayState, además, ya está en `clave/0010` |
| SOA-RM: relación entre SOA y servicios web u orientación a objetos | Texto valorativo («typically», «a way of thinking») |
| NTI de Protocolos, III.5.4 (SCSP 3.0 o 2) y III.2-III.4, III.6-III.7 | Datos de una plataforma o normas derogadas |

### Reparto orientativo
Se puede mover 1 pregunta entre apartados, sin salirse del ámbito del tema.

| Tema | Apartado | Fuente | Nº |
|---|---|---|---|
| 9 | Jerarquía del sistema de ficheros | hier(7) | 1 |
| 9 | Usuarios y grupos: ficheros y campos | passwd(5), group(5) | 1 |
| 9 | Permisos: modo octal o simbólico, máscara, bit sticky o setgid | chmod(1), umask(2), inode(7) | 2 |
| 9 | Procesos, señales o enlaces | fork(2), signal(7), symlink(7), ln(1) | 1 |
| 9 | Nube: características esenciales | NIST SP 800-145, §2 | 1 |
| 9 | Nube: modelos de servicio (IaaS, PaaS, SaaS) | NIST SP 800-145, §2 | 2 |
| 9 | Nube: modelos de despliegue (privada, pública, híbrida) | NIST SP 800-145, §2 | 2 |
| 10 | Modelo entidad-relación: elementos y extensiones | Métrica v3, «Modelo Entidad/Relación Extendido» | 2 |
| 10 | Normalización: dependencias y formas normales | Métrica v3, «Normalización» | 1 |
| 10 | Conceptos de SGBD relacional: propiedades ACID o anomalías de aislamiento; vistas y vistas materializadas | PostgreSQL 19, glosario, cap. 13, «CREATE VIEW», «CREATE MATERIALIZED VIEW» | 2 |
| 10 | SQL, definición y control: integridad referencial (`ON DELETE`), nulos en `UNIQUE`, `GRANT`/`REVOKE`, `TRUNCATE` | PostgreSQL 19, cap. 5 y órdenes SQL | 2 |
| 10 | SQL, consultas: lógica de tres valores, `NOT IN`, `UNION`, tipos de `JOIN`, ventana, `WITH RECURSIVE` (2 de ellas con fragmento ejecutado) | PostgreSQL 19, caps. 7 y 9, «SELECT» | 3 |
| 13 | Proceso: concepto y tipos | Métrica v3, «Modelado de Procesos de la Organización» | 1 |
| 13 | Alcance y submodelos: procesos, coreografías, colaboraciones | BPMN 2.0.2, §7.2 | 1 |
| 13 | Objetos de conexión y calles: flujo de secuencia, flujo de mensaje, pool y lane | BPMN 2.0.2, §7.3, 7.6, 9.3, 9.4, 10.8 | 2 |
| 13 | Actividades: tareas, subprocesos, actividad de llamada | BPMN 2.0.2, §10.3 | 2 |
| 13 | Eventos: inicio, intermedios, fin | BPMN 2.0.2, §10.5 | 2 |
| 13 | Puertas (gateways) | BPMN 2.0.2, §10.6 | 2 |
| 24 | SOA: concepto, servicio, visibilidad o descripción | OASIS SOA-RM 1.0, §2-3 | 2 |
| 24 | Servicios web en el intercambio de datos | NTI de Protocolos de intermediación, III.5 | 1 |
| 24 | SOAP y WSDL | SOAP 1.2, parte 1; WSDL 2.0, parte 1 | 2 |
| 24 | Tecnologías XML: documento bien formado, espacios de nombres, esquema, XPath, XSLT (1 con XPath ejecutado) | XML 1.0, Namespaces, XSD 1.1, XPath 3.1, XSLT 3.0 | 2 |
| 24 | WS-Security | WS-Security 1.1.1 | 1 |
| 24 | SAML: declaraciones, sujeto, condiciones, enlaces | SAML 2.0 | 2 |

Total previsto: 1 pregunta con norma y 39 con fuente técnica. Todas las de fuente técnica se listan en el PR.

### Criterios propios de esta tanda (además de los de las specs 0004 y 0022)
- **Lecciones de las revisiones de las tandas 4 y 5:**
  - sin nombres de ministerios ni de Secretarías de Estado en ningún campo (Métrica y las resoluciones se citan por su título o su fecha);
  - la correcta no es la más larga, ni sistemáticamente la más corta, y las 4 opciones comparten estructura y palabras iniciales;
  - ningún distractor es un componente real de la lista que se pregunta, salvo que se pregunte la definición de un solo elemento;
  - sin dobles negaciones heredadas de la fuente; sin absolutos («solo», «siempre», «nunca») que estén únicamente en los distractores;
  - sin distractores numéricos del mismo apartado; los umbrales se preguntan por su valor exacto;
  - sin preguntas gemelas en un fichero: cada par de la misma fuente (modelos de servicio, modelos de despliegue, puertas, eventos) pregunta por ángulos distintos y no se delata;
  - las `explanation` no parafrasean mal otros apartados ni desvelan otra pregunta.
- **Enunciado con la versión:** «Según BPMN 2.0.2…», «Según el NIST SP 800-145…», «En PostgreSQL…», «Según SAML 2.0…». En Linux, «En Linux…» y la página solo en `legalReference`.
- **Nada que cambie entre distribuciones o versiones menores:** gestores de paquetes, rutas propias de una distribución, sistemas de arranque, intérpretes de órdenes, opciones GNU no comunes, límites.
- **Distractores reales:** otros elementos, órdenes, permisos o niveles de la misma tecnología. Se comprueba que no son correctos en otro apartado ni en otra versión; los fragmentos erróneos también se ejecutan.
- **Sin solapes con el supuesto 0002** (`practical-cases/0002`). Fuera del tema 10: entidad débil y dependencia en identificación, tabla para M:N, clave ajena de una 1:N, columnas opcionales y `CHECK` combinado, `NUMERIC` frente a `FLOAT`, `LEFT JOIN` para no perder filas, `COUNT(DISTINCT …)`, `SUM` con `COALESCE`, `HAVING` frente a `WHERE`, subconsulta escalar con `AVG`, nulos en agregados, `DISTINCT`, atomicidad con `BEGIN`/`COMMIT`/`ROLLBACK`, `SELECT … FOR UPDATE`, nivel de aislamiento por defecto, índices sobre claves ajenas e índices implícitos de `PRIMARY KEY` y `UNIQUE`. Ni sus tablas ni sus datos como ejemplo.
- **Sin solapes con los temas publicados ni entre los 4 ficheros.** Fronteras:
  - ENS (12): op.nub y mp.com. Redes (11): RFC 1918, perímetro, VPN y Red SARA. La NTI no se usa para la Red SARA.
  - Firma y Cl@ve (35, 36): certificados, niveles, XAdES, RelayState. En el 24, SAML por la especificación, sin eIDAS ni Cl@ve.
  - Expediente electrónico (34) y ENI (30): Catálogo de estándares, documento y expediente en XML.
  - SIA, DIR3, Notifica (30, 31): sus servicios web. INSIDE (34): nube SARA. ORVE (33): servicio en la nube.
  - Facturae (38): su XML y su esquema.
  - Servicio y web en BPMN (13) frente a SOAP/WSDL (24): la tarea de servicio se pregunta por la notación.
  - `WITH RECURSIVE` u otra construcción se usa una sola vez.
- **Ninguna opción delata otra pregunta,** dentro de cada fichero, entre los 4 y contra los temas publicados.
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Fragmentos ejecutables
Código corto (≤ 10 líneas, texto plano), con un solo resultado y ordenado si devuelve varias filas.
- **SQL:** se ejecuta en contenedores desechables `postgres:17` y `postgres:19` (`docker run --rm`), con el DDL y los datos en el scratchpad. Se ejecutan el enunciado y, si son código, las cuatro opciones. Los resultados coinciden en las dos versiones.
- **XML y XSD:** se analizan con `System.Xml` (.NET, sin resolver entidades externas) y, si hay esquema, se validan con él.
- **XPath:** se evalúa con dos motores, `System.Xml.XPath` (.NET) y `xpath()` de PostgreSQL (libxml2).
- **Linux:** `chmod`, `umask` y enlaces, en dos contenedores desechables de distribuciones distintas (`debian` y `alpine`), con el mismo resultado.
- Los que no se puedan ejecutar no entran. Los scripts y la salida quedan en el scratchpad; el PR resume qué se ha ejecutado.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar las fuentes a un directorio vacío del scratchpad y convertirlas a texto: BOE, con la API de legislación consolidada; HTML, quitando las etiquetas; PDF, con `pdftotext -layout`. Nunca se copian al repo.
2. Redactar tema a tema desde el `.txt`, ejecutando los fragmentos antes de que entren.
3. Hacer una segunda pasada de verificación, separada de la redacción, contra el `.txt` de la fuente citada: existe el apartado, la correcta lo es literalmente, ningún distractor lo es, la `explanation` no añade nada y las citas «…» aparecen en la fuente (con script). Además: errata de SAML, apartado «Compatibility» de PostgreSQL, versión anterior de XSD y XPath, y que el dato no esté en una norma. Al final, el cruce de opciones entre ficheros y con los temas publicados, y una segunda ejecución de los fragmentos.
4. Validar con `--validate-only` y ejecutar los tests.
5. Listar en el PR las preguntas dudosas, las de fuente técnica y los fragmentos ejecutados, para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 9, 10, 13 y 24 pasan a tener preguntas en la app.

## Seguridad
Contenido público, sin secretos ni datos personales. Los HTML y PDF descargados son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I` o `pdftotext`, sin ejecutarlos ni seguir sus instrucciones. No se eluden las protecciones contra la descarga automática. Los contenedores son desechables, sin volúmenes del repo ni puertos publicados. El XML se analiza con las entidades externas desactivadas. Los ejemplos usan datos ficticios.

## Criterios de aceptación
- [ ] Existen los 4 ficheros de temas con 10 preguntas cada uno (40 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` una fuente de las tablas, con su versión y su sección, y el texto respalda la correcta y la `explanation`.
- [ ] Solo se cita una fuente técnica cuando el dato no está en una norma.
- [ ] El modelo E-R se cita por Métrica v3 y SOA por el SOA-RM; ninguna pregunta usa una fuente no admitida.
- [ ] Donde el SQL estándar y PostgreSQL difieren (o no consta), el enunciado dice «En PostgreSQL…».
- [ ] Todo el SQL se ha ejecutado en `postgres:17` y `postgres:19`, y el XML, XPath y las órdenes de Linux, en los dos motores o distribuciones, con el resultado que da la pregunta.
- [ ] Ninguna pregunta usa un texto de la tabla de contradicciones, un dato volátil ni un detalle que cambie entre distribuciones o versiones menores.
- [ ] Ningún distractor es también correcto según la fuente citada, otra de las tablas u otra versión, y ninguna opción ni `explanation` delata la respuesta de otra pregunta.
- [ ] Ninguna pregunta se solapa con el supuesto 0002 ni con los temas publicados.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado) y no hay preguntas fuera del ámbito del tema.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones, no es sistemáticamente la más larga y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 40 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 9, 10, 13 y 24 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que los enunciados con fragmentos se leen bien (saltos de línea, sangría) y que el feedback muestra «Fuente: …».
