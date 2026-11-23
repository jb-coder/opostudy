# 0022 — Fuentes para temas técnicos

- **Issue:** #65
- **Milestone:** v0.4.0
- **Estado:** borrador

## Objetivo
La spec 0004 solo admite preguntas citando un artículo del BOE consolidado. Unos 20 de los 36 temas pendientes (Linux, SQL, Java, .NET, PHP, BPMN, Git, Gantt…) no tienen norma, así que no podrían tener preguntas. Esta spec define qué fuentes técnicas valen, cómo se citan y cómo se verifican, con el mismo rigor que el BOE. Es requisito de las tandas de contenido de la v0.4.0 que cubren temas técnicos.

## Alcance
**Incluye:**
- Nueva regla de fuentes en la spec 0004 (formato de la referencia, criterios de calidad y proceso de autoría).
- El campo `legalReference` pasa a significar «fuente» (referencia legal o técnica). Mismo nombre, mismo tipo y mismo límite (300), así que no hay migración ni cambio de contrato.
- Glosario del ADR 0001: «Referencia legal» → «Fuente (referencia legal o técnica)», con el código `LegalReference` sin cambios.
- La app muestra «Fuente: …» en lugar de «Referencia: …».

**No incluye:**
- Preguntas nuevas (las aportan las specs de tandas).
- Renombrar `LegalReference` en el código, la BD, los DTO o el JSON (el coste no compensa).
- Campo de URL de la fuente: la referencia es texto, como hasta ahora.

## Diseño
### Regla de fuentes (se añade a la spec 0004)
1. **Prioridad del BOE.** Si el tema o el dato está regulado por una norma (ley, real decreto, orden, reglamento UE publicado en el DOUE, ley autonómica publicada en el BOE), se cita la norma, aunque el tema sea técnico (p. ej., accesibilidad → RD 1112/2018; ENS → RD 311/2022; factura → Ley 25/2013).
2. **Fuentes técnicas admitidas**, solo cuando no hay norma, en este orden:
   - **Especificaciones y estándares oficiales:** W3C (WCAG, XML, XML Schema, XPath, SOAP, WSDL), WHATWG (HTML, DOM), ECMA-262, OMG BPMN 2.0.2, OASIS (SAML 2.0, WS-Security), RFC del IETF (p. ej. RFC 9110 HTTP), NIST SP 800-145 (cloud).
   - **Documentación oficial del fabricante o proyecto:** Microsoft Learn (.NET, C#, ASP.NET Core), Oracle/OpenJDK (Java SE, JLS), php.net, PostgreSQL, git-scm (Pro Git, referencia), JUnit, Selenium, Jenkins, SonarQube, Ansible, LXC/linuxcontainers.org, páginas man de Linux.
   - **Administración electrónica:** guías del PAe (Métrica v3, Normas Técnicas de Interoperabilidad, SIA, DIR3, SICRES, Facturae) y guías CCN-STIC.
3. **No valen:** blogs, Wikipedia, cursos, apuntes de academias, Stack Overflow, respuestas de una IA ni la memoria. Para conceptos de manual sin especificación (p. ej. PERT/Gantt, modelo E-R), solo si una fuente admitida los define; si no, la pregunta no entra.
4. **Versión.** Se usa la versión vigente al redactar y se nombra en la referencia cuando la fuente está versionada (WCAG 2.2, Java SE 21, .NET 10, PHP 8.x, BPMN 2.0.2). Se evitan los detalles que cambian entre versiones menores (valores por defecto, nombres de flags recientes, APIs marcadas como preview u obsoletas).
5. **Mismo rigor que el BOE.** El texto de la fuente se descarga (HTML o PDF) y se convierte a texto plano. Se trabaja sobre ese texto, nunca de memoria. La segunda pasada de verificación comprueba cada pregunta contra él.

### Formato de `legalReference` (fuentes técnicas)
`<Fuente abreviada con versión>, <sección>`. La sección es la que permite encontrar el dato: número de apartado, criterio, capítulo o título de la página.

| Fuente | Ejemplo |
|---|---|
| W3C WCAG | `WCAG 2.2, criterio 1.4.3` |
| ECMA-262 | `ECMA-262 (2025), §14.7.5` |
| OMG BPMN | `BPMN 2.0.2, §10.6.2` |
| RFC | `RFC 9110, §9.3.1` |
| NIST | `NIST SP 800-145, §2` |
| Microsoft Learn | `Microsoft Learn (.NET 10), «Garbage collection fundamentals»` |
| Java | `JLS (Java SE 21), §8.4.3.1` |
| PHP | `Manual de PHP, «Declaración de tipos»` |
| Git | `Pro Git (2.ª ed.), §3.2` |
| Métrica v3 | `Métrica v3, Procesos principales, ASI 5` |
| CCN-STIC | `CCN-STIC 808, §4.2` |

La `explanation` cita la fuente igual que con el BOE: por qué la correcta lo es (con cita literal entre comillas si ayuda, traducida si la fuente está en inglés) y por qué fallan los distractores principales.

### Criterios de calidad (cambios en la spec 0004)
- «Nada inventado»: cada afirmación está en el texto vigente **de la fuente citada** (BOE o fuente técnica admitida). Si no se puede citar un artículo o una sección, la pregunta no entra.
- Distractores técnicos creíbles: otros métodos, palabras clave, comandos, códigos de estado o elementos reales de la misma tecnología. Nada inventado ni que no compile o no exista.
- Código en el enunciado: corto (≤ 10 líneas), en bloque de texto plano, y el resultado esperado se comprueba ejecutándolo (SQL en el contenedor de Postgres, C# compilado, Java/PHP/JS con su intérprete si está disponible; si no se puede ejecutar, la pregunta no lleva código).
- El resto de criterios de la spec 0004 (4 opciones, una correcta indiscutible, negaciones en MAYÚSCULAS ≤ 1/5, sin duplicados, español correcto) no cambia.

### API y datos
Sin cambios. `LegalReference` sigue siendo obligatorio y de 300 caracteres como máximo.

### App
- `QuizViewModel`: «Referencia: {x}» → «Fuente: {x}» en `LegalReferenceText` (feedback del test) y en `ReferenceText` (repaso de falladas). Mismo cambio en el flujo offline, que usa el mismo ViewModel.
- Solo cambia el texto. No hay cambios de layout.

## Seguridad
Contenido público. Los ficheros descargados (HTML/PDF) son datos no confiables: se guardan en un directorio vacío del scratchpad, nunca en el repo, y se procesan sin ejecutarlos. Sin secretos ni datos personales.

## Criterios de aceptación
- [ ] La spec 0004 incluye la regla de fuentes (prioridad del BOE, fuentes admitidas y excluidas, versión), el formato de referencia técnica con ejemplos y los criterios de calidad ampliados (ver «Diseño»).
- [ ] El glosario del ADR 0001 dice «Fuente (referencia legal o técnica)» para `LegalReference`.
- [ ] La app muestra «Fuente: …» en el feedback del test (online y offline) y en el repaso de falladas.
- [ ] Ningún cambio en el dominio, la BD, los contratos ni el JSON Schema del contenido.
- [ ] Build sin warnings y tests en verde.

## Plan de tests
- **Unitarios:** actualizar las aserciones de `QuizFlowViewModelTests` y `OfflineViewModelTests` que esperan «Referencia: …» para que esperen «Fuente: …». Añadir o actualizar la del repaso de falladas (`ReferenceText`) si no existe.
- **Integración:** ninguno nuevo.
- **Manual:** opcional. La primera tanda con fuentes técnicas (0028: SIA/DIR3, Facturae) servirá de prueba real de la regla.
