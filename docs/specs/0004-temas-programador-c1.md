# 0004 — Primeros temas de Programador/a C1 (Diputación de Málaga)

- **Issue:** #14
- **Milestone:** v0.1.0-alpha
- **Estado:** lista

> El texto del contenido está en español. Los slugs, las `key` y cualquier código, en inglés o sin tildes, según la spec 0003.

## Objetivo
Cargar el temario oficial completo de la plaza de Programador/a (C1) de la Diputación Provincial de Málaga y las primeras preguntas de test de dos materias del Bloque I: la Constitución y la Ley 39/2015. Una IA redacta las preguntas, se comprueban contra el texto legal vigente y una persona las revisa antes del merge.

## Fuente oficial
- **Temario:** Anexo VI («Programador/a (C1)») del acuerdo de la Junta de Gobierno de 4 de diciembre de 2024, punto 2.2.16 (2ª convocatoria, OEP 2021-2023). Publicado en el **BOP de Málaga nº 235, de 5 de diciembre de 2024**, págs. 81-83, CVE 20241205-05107-2024 (verificable en www.bopmalaga.es/cve.php).
- **Bases generales:** BOP de Málaga nº 234, de 11 de diciembre de 2023.
- **Modificaciones:** BOP nº 147, de 1 de agosto de 2025, y refundición en el BOP nº 51, de 16 de marzo de 2026 (extracto en el BOJA nº 99, de 26 de mayo de 2026). Solo cambian los requisitos de titulación del Anexo VI; el temario no cambia.
- **Convocatoria:** BOE nº 251, de 9 de octubre de 2026 (BOE-A-2026-21009). 3 plazas: 2 de turno libre y 1 reservada a personas con discapacidad. Escala de Administración Especial, subescala Técnica, clase Auxiliar.
- **Textos legales de referencia (BOE, legislación consolidada):**
  - Constitución Española: BOE-A-1978-31229. Incluye las reformas de 1992 (art. 13.2), 2011 (art. 135) y 2024 (art. 49).
  - Ley 39/2015, de 1 de octubre, del Procedimiento Administrativo Común de las Administraciones Públicas: BOE-A-2015-10565.

## Alcance
**Incluye:**
- `exam.json` con los 40 temas oficiales (tabla de abajo).
- `topics/constitucion.json`: 20 preguntas del tema 1.
- `topics/lpac-interesados-actividad.json`, `topics/lpac-acto-administrativo.json` y `topics/lpac-fases-procedimiento.json`: 10 preguntas cada uno (temas 2, 3 y 4).
- En total, 50 preguntas, cada una comprobada contra el BOE consolidado.

**No incluye:**
- Preguntas del resto de temas (issues futuras; solo se añade el fichero del tema).
- Cambios de código, de esquema o de modelo.
- Supuestos prácticos.

## Diseño

### Temas en `exam.json`
- `number` es correlativo en todo el temario (1-40), porque la BD exige `(exam_id, number)` único y el BOP numera cada bloque desde 1. Así no hay que renumerar nunca (ver la limitación conocida de la spec 0003).
- `section` indica el bloque del BOP. `title` resume el enunciado oficial (máx. 300 caracteres) sin inventar nada.
- `title` empieza con «Tema N.» usando la numeración **del BOP**, para que el opositor lo reconozca aunque `number` sea global.

| number | slug | section | title |
|---|---|---|---|
| 1 | `constitucion` | Bloque I. Materias comunes | Tema 1. La Constitución española de 1978: principios generales. Características y estructura. Reforma de la Constitución |
| 2 | `lpac-interesados-actividad` | Bloque I. Materias comunes | Tema 2. Procedimiento Administrativo Común (I): derechos del ciudadano, el interesado (concepto y capacidad de obrar), identificación y firma, y normas generales de actuación de las Administraciones Públicas |
| 3 | `lpac-acto-administrativo` | Bloque I. Materias comunes | Tema 3. Procedimiento Administrativo Común (II): el acto administrativo. Concepto, elementos y clases. Requisitos: motivación y forma. Eficacia. Notificación y publicación |
| 4 | `lpac-fases-procedimiento` | Bloque I. Materias comunes | Tema 4. Procedimiento Administrativo Común (III): fases del procedimiento (iniciación, ordenación, instrucción y finalización). La ejecución |
| 5 | `regimen-local` | Bloque I. Materias comunes | Tema 5. Régimen local español: principios constitucionales y Ley 7/1985. Organización y competencias provinciales y municipales |
| 6 | `personal-entidades-locales` | Bloque I. Materias comunes | Tema 6. Personal al servicio de las entidades locales: función pública local, selección, situaciones administrativas, derechos y deberes |
| 7 | `presupuesto-local` | Bloque I. Materias comunes | Tema 7. El presupuesto de las entidades locales: elaboración, aprobación, ejecución, control y fiscalización |
| 8 | `contratos-sector-publico` | Bloque I. Materias comunes | Tema 8. Ley de Contratos del Sector Público: objeto, ámbito de aplicación y tipos contractuales |
| 9 | `linux-cloud` | Bloque II-A. Materias específicas | Tema 1. El sistema operativo Linux. Computación en la nube: IaaS, PaaS, SaaS. Nubes privadas, públicas e híbridas |
| 10 | `bases-de-datos-sql` | Bloque II-A. Materias específicas | Tema 2. Sistemas gestores de bases de datos relacionales. Modelo entidad-relación. SQL |
| 11 | `redes-sara-nerea` | Bloque II-A. Materias específicas | Tema 3. Redes corporativas. Infraestructuras comunes: Red SARA y Red NEREA |
| 12 | `ens` | Bloque II-A. Materias específicas | Tema 4. Esquema Nacional de Seguridad (ENS) |
| 13 | `bpmn` | Bloque II-A. Materias específicas | Tema 5. Modelado de procesos de negocio. Notación BPMN 2.0 |
| 14 | `auditoria-informatica` | Bloque II-A. Materias específicas | Tema 6. Auditoría informática: concepto, contenidos, prácticas operativas, peritajes, marco legal y herramientas |
| 15 | `proteccion-datos` | Bloque II-A. Materias específicas | Tema 7. La protección de datos personales: marco normativo, principios, derechos, responsables y encargados, autoridades de control. Propiedad intelectual en el entorno digital |
| 16 | `proteccion-software` | Bloque II-A. Materias específicas | Tema 8. La protección jurídica de los programas de ordenador. Comprobación de la legalidad y control del software |
| 17 | `accesibilidad-usabilidad` | Bloque II-A. Materias específicas | Tema 9. Accesibilidad y usabilidad. W3C. Diseño universal. Diseño web adaptativo |
| 18 | `normalizacion` | Bloque II-A. Materias específicas | Tema 10. Organizaciones de normalización. Pruebas de conformidad y certificación |
| 19 | `java` | Bloque II-B. Materias específicas | Tema 11. Lenguaje de programación Java. Arquitectura. Frameworks |
| 20 | `dotnet` | Bloque II-B. Materias específicas | Tema 12. Entorno de desarrollo Microsoft .NET |
| 21 | `html-css-javascript` | Bloque II-B. Materias específicas | Tema 13. Desarrollo web con HTML y CSS. Lenguaje JavaScript |
| 22 | `php` | Bloque II-B. Materias específicas | Tema 14. Programación de páginas y aplicaciones web con PHP |
| 23 | `arquitectura-mvc` | Bloque II-B. Materias específicas | Tema 15. Arquitectura de programación DNA. Modelo-Vista-Controlador (MVC). Casos de uso |
| 24 | `soa-servicios-web` | Bloque II-B. Materias específicas | Tema 16. Arquitectura SOA. Servicios web. Tecnologías XML. WS-Security. SAML |
| 25 | `servidores-web` | Bloque II-B. Materias específicas | Tema 17. Servidores web. Tecnologías para el desarrollo de aplicaciones web. Aplicaciones en entornos PHP y MySQL |
| 26 | `metodologias-agiles` | Bloque II-B. Materias específicas | Tema 18. Metodologías ágiles. Dirección y gestión de proyectos TI. Métrica |
| 27 | `gantt-pert` | Bloque II-B. Materias específicas | Tema 19. Metodologías predictivas para la gestión de proyectos: GANTT, PERT |
| 28 | `control-versiones` | Bloque II-B. Materias específicas | Tema 20. Herramientas de control de versiones. Estrategias. Repositorios locales y distribuidos. Soluciones de código abierto |
| 29 | `pruebas-integracion-despliegue` | Bloque II-B. Materias específicas | Tema 21. Pruebas con JUnit y Selenium. Integración con Nexus, Jenkins y SonarQube. Despliegue con Ansible y LXC |
| 30 | `sia-dir3` | Bloque II-C. Materias específicas | Tema 22. Sistema de Información Administrativa (SIA). Directorio Común (DIR3). Servicios web e integraciones |
| 31 | `notificaciones-notifica` | Bloque II-C. Materias específicas | Tema 23. Notificaciones y comunicaciones en las Leyes 39/2015 y 40/2015. Notifica. Servicios web e integraciones |
| 32 | `pag-carpeta-sede` | Bloque II-C. Materias específicas | Tema 24. Punto de Acceso General electrónico. Carpeta Ciudadana. Sede electrónica |
| 33 | `registro-electronico` | Bloque II-C. Materias específicas | Tema 25. Registro de entrada/salida. SICRES 4.0. Registro Electrónico Común. SIR, GEISER y ORVE |
| 34 | `expediente-electronico` | Bloque II-C. Materias específicas | Tema 26. Archivo, documento y expediente electrónico. INSIDE y Archive. Esquema Nacional de Interoperabilidad y Normas Técnicas |
| 35 | `firma-electronica` | Bloque II-C. Materias específicas | Tema 27. Firma electrónica. Plataforma @firma. Autoridades de certificación. Reglamento eIDAS |
| 36 | `clave` | Bloque II-C. Materias específicas | Tema 28. Gestión de identidades. Cl@ve |
| 37 | `factura-electronica` | Bloque II-C. Materias específicas | Tema 29. Ley 25/2013 de impulso de la factura electrónica. Punto General de Entrada de Facturas Electrónicas. Integración |
| 38 | `facturae` | Bloque II-C. Materias específicas | Tema 30. El formato de factura electrónica Facturae |
| 39 | `transparencia-proteccion-datos` | Bloque II-C. Materias específicas | Tema 31. Transparencia (Ley 19/2013 y Ley 1/2014 de Andalucía) y protección de datos (RGPD y Ley Orgánica 3/2018) |
| 40 | `prevencion-igualdad` | Bloque II-C. Materias específicas | Tema 32. Prevención de riesgos laborales. Igualdad efectiva de mujeres y hombres (LO 3/2007, Ley 12/2007 de Andalucía y Ley 9/2018) |

### Alcance de cada tema con preguntas
Las preguntas se ciñen al enunciado del BOP:

| Tema | Artículos | Reparto orientativo |
|---|---|---|
| `constitucion` (20) | Preámbulo, estructura (títulos, disposiciones), Título Preliminar (arts. 1-9) y Título X (arts. 166-169) | 3 de estructura y características, 12 del Título Preliminar, 5 de reforma |
| `lpac-interesados-actividad` (10) | Ley 39/2015, arts. 3-12 (capacidad, interesado, representación, identificación y firma) y 13-28 (derechos de las personas, lengua, registros, términos y plazos…) | Al menos 2 de identificación y firma y 2 de derechos (art. 13) |
| `lpac-acto-administrativo` (10) | Ley 39/2015, arts. 34-46 (requisitos, motivación, forma, eficacia, notificación y publicación) | Al menos 3 de notificación (arts. 40-44) |
| `lpac-fases-procedimiento` (10) | Ley 39/2015, arts. 53-105 (derechos del interesado en el procedimiento, iniciación, ordenación, instrucción, finalización y ejecución) | Repartidas entre las cuatro fases y al menos 1 de ejecución |

Los conceptos puramente doctrinales («elementos» y «clases» de actos) solo se preguntan si se pueden anclar a un artículo de la ley.

### Formato de cada pregunta (según la spec 0003)
- `key`: `malaga-programador-c1/<topic-slug>/NNNN`, correlativa desde `0001` en cada tema.
- `statement`: enunciado autónomo, sin prefijos `a)`/`1.`. Formulación como en el examen real: «Según el artículo X de…, …» o «Señale la respuesta correcta…». Las preguntas en negativo («no es…», «es incorrecta…») llevan la negación en MAYÚSCULAS (NO, INCORRECTA) y son como mucho 1 de cada 5.
- `options`: **exactamente 4**, una sola correcta. La posición de la correcta varía (repartida aproximadamente igual entre las 4 posiciones en cada fichero). Sin «Todas las anteriores» ni «Ninguna de las anteriores», porque la app puede barajar las opciones.
- `explanation` (2-6 frases):
  1. Por qué la opción correcta lo es, citando el artículo y, si ayuda, reproduciendo entre comillas el fragmento literal del BOE consolidado.
  2. Por qué fallan los distractores principales (p. ej., «El plazo de 10 días es el de subsanación del art. 68.1, no el de…»).
- `legalReference`: norma abreviada y artículo con apartado. Formatos: `CE, art. 167.1`, `CE, arts. 166-169`, `Ley 39/2015, art. 21.3.a)`.

### Criterios de calidad
- **Nada inventado:** cada afirmación de la pregunta, de la respuesta correcta y de la aclaración está en el texto consolidado vigente del BOE. Si no se puede citar un artículo, la pregunta no entra.
- **Texto vigente:** se comprueba contra la última versión consolidada (p. ej., el art. 49 CE tras la reforma de 2024). No se usan redacciones derogadas ni plazos antiguos de la Ley 30/1992.
- **Distractores creíbles:** cifras, plazos, mayorías u órganos reales de la misma norma u otra cercana (otro apartado, otro plazo, otra mayoría). Nada absurdo, nada que se descarte por la forma (longitud, gramática o «siempre/nunca» solo en las falsas).
- **Una sola correcta indiscutible:** ningún distractor puede ser también correcto según otro artículo o una lectura razonable.
- **Sin duplicados:** dos preguntas no evalúan el mismo dato con distinta redacción.
- **Español correcto:** tildes, mayúsculas institucionales como en el BOE («Cortes Generales», «Administraciones Públicas»).

### Proceso de autoría
1. Se descargan los textos consolidados del BOE (API de datos abiertos, `legislacion-consolidada/id/<BOE-A-…>/texto`), se convierten a texto plano y se trabaja sobre ellos, no de memoria.
2. Una IA redacta las preguntas siguiendo esta spec.
3. Una segunda pasada, independiente, comprueba cada pregunta contra el texto consolidado: la cita, la opción correcta, que ningún distractor sea correcto y la aclaración. Las que no pasan se corrigen o se descartan.
4. `--validate-only` y los tests de `content/` pasan.
5. Revisión humana de todas las preguntas antes del merge.

### API, datos y app
Sin cambios. El AppHost importa el contenido nuevo al arrancar (spec 0003).

## Seguridad
Contenido público (legislación y temario oficial). Sin datos personales ni secretos.

## Criterios de aceptación
- [ ] `exam.json` contiene los 40 temas de la tabla, con `number` del 1 al 40.
- [ ] Hay 50 preguntas: 20 de `constitucion` y 10 en cada uno de los tres temas de la Ley 39/2015.
- [ ] Todas tienen 4 opciones con una sola correcta y `legalReference` con artículo.
- [ ] Cada aclaración cita el artículo, y su contenido coincide con el BOE consolidado vigente.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` termina con código 0.
- [ ] `RepositoryContentTests` pasa (lector y JSON Schema).
- [ ] Revisión manual del contenido antes del merge.

## Plan de tests
- **Automáticos:** `RepositoryContentTests` ya valida todo `content/`. No hacen falta tests nuevos.
- **Manual:**
  - `--validate-only`.
  - Comprobación cruzada de cada pregunta contra el BOE (paso 3 del proceso).
  - Revisión humana.
  - Opcional: arrancar el AppHost y comprobar que hay 40 temas y 50 preguntas en la BD.
