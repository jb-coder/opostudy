# 0031 — Contenido, tanda 8: MVC y casos de uso, métodos ágiles y Métrica, Gantt y PERT, control de versiones, pruebas y despliegue

- **Issue:** #74
- **Milestone:** v0.4.0
- **Estado:** lista

> Octava y última tanda de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad y proceso en la spec 0004, fuentes técnicas en la spec 0022. Modelo: spec 0030.

## Objetivo
Dar 50 preguntas de test, redactadas y verificadas contra el texto de la fuente citada, a los temas 23, 26, 27, 28 y 29 (bloque II-B), que hoy no tienen ninguna. Con ellas, los 40 temas tienen preguntas.

## Alcance
**Incluye:**
- `topics/arquitectura-mvc.json`: 10 preguntas del tema 23.
- `topics/metodologias-agiles.json`: 10 preguntas del tema 26.
- `topics/gantt-pert.json`: 10 preguntas del tema 27.
- `topics/control-versiones.json`: 10 preguntas del tema 28.
- `topics/pruebas-integracion-despliegue.json`: 10 preguntas del tema 29.

**No incluye:**
- Preguntas de otros temas. Linux (9), E-R y normalización (10), BPMN y modelado de procesos (13), auditoría (14), normas ISO y certificación (18), Java, Servlet y beans de Spring (19), .NET, C# y duración de los servicios (20), servidores web y HTTP (25) se pueden nombrar, pero no son el objeto de la pregunta.
- **Windows DNA** y, en su lugar, arquitecturas de n capas (decisión 1).
- Varianza, desviación típica y probabilidad de cumplir el plazo en PERT (decisión 4).
- XP, Lean, SAFe, DSDM, PMBOK, PRINCE2, PM² e ISO 21502. Subversion y Mercurial como herramientas (solo como ejemplos de Pro Git). GitHub, GitLab, Docker, Kubernetes, Incus, LXD, Gradle, TestNG, Cucumber, AWX y Terraform.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 23** (15 del bloque II). Arquitectura de programación DNA. Modelo-Vista-Controlador (MVC). Casos de uso.
- **Tema 26** (18). Metodologías ágiles. Dirección y gestión de proyectos TI. Métrica.
- **Tema 27** (19). Metodologías predictivas para la gestión de proyectos: GANTT, PERT.
- **Tema 28** (20). Herramientas de control de versiones. Estrategias. Repositorios locales y distribuidos. Soluciones de código abierto.
- **Tema 29** (21). Pruebas con JUnit y Selenium. Integración con Nexus, Jenkins y SonarQube. Despliegue con Ansible y LXC.

### Decisiones que se aprueban con esta spec
1. **Windows DNA queda fuera.** Microsoft Learn no conserva documentación que la defina: ni su buscador ni una búsqueda web limitada a `learn.microsoft.com` (25-11-2026) devuelven ninguna página. Solo quedan notas de prensa de 1997 a 1999, que no son documentación. El tema 23 se reparte entre MVC (6) y casos de uso (4). Tampoco se sustituye por la arquitectura de n capas, que no es DNA.
2. **Se admiten expresamente estas fuentes,** que la spec 0022 no nombra:
   - **The Scrum Guide, noviembre de 2020** (scrumguides.org). Es la vigente: el historial oficial no recoge ninguna posterior. Se usa la traducción oficial al español (España), y la inglesa manda en caso de duda.
   - **Manifiesto por el Desarrollo Ágil de Software** y sus doce principios (agilemanifesto.org, traducción oficial al español). Es la fuente de «metodologías ágiles» en general.
   - **The Kanban Guide, mayo de 2025** (kanbanguides.org). Única metodología ágil distinta de Scrum con una guía pública y versionada de sus autores. Se pregunta solo lo que es igual en la de diciembre de 2020.
   - **OMG UML 2.5.1** (diciembre de 2017, vigente). Ver la decisión 3.
   - **NASA Schedule Management Handbook** (NASA/SP-2010-3403, revisión 1, 30-01-2020, sin fecha de caducidad). Ver la decisión 4.
   - **DOE, Earned Value Management Tutorial, módulo 3: Project Scheduling** (energy.gov), solo para la estimación de tres valores. Ver la decisión 4.
   - **Sonatype Nexus Repository** (help.sonatype.com) y **Spring Framework** (ya admitida en la spec 0030), solo para el controlador frontal.
3. **Casos de uso: Métrica v3 y UML, cada una con su ángulo.** Métrica v3 es la referencia de la Administración y define el caso de uso, el actor, los escenarios y las relaciones «usa» y «extiende». UML 2.5.1 (§18) se cita para lo que Métrica no tiene: puntos de extensión, dependencia entre el caso base y el incluido, sujeto y actor. El enunciado nombra siempre la fuente, porque usan nombres distintos («usa» frente a «include»).
4. **PERT y camino crítico: Métrica v3 no basta.** Define actividad, suceso, grafo, prelaciones, actividades ficticias, matriz de encadenamientos y cuadro de prelaciones, pero no las holguras ni cómo se calcula el camino crítico. Para eso se cita el manual de la NASA (§5.5.10 y 5.5.11), público, oficial y vigente. La guía equivalente de la GAO (GAO-16-89G) responde 403 a la descarga automática y no se elude.
   - **La duración esperada, (optimista + 4 · más probable + pesimista) / 6, entra con 1 pregunta.** Ni Métrica ni el manual de la NASA la dan. Se cita el tutorial de gestión del valor ganado del Departamento de Energía de EE. UU. (energy.gov), módulo 3, que da la fórmula y un ejemplo. Un artículo de la revista *Documentación Administrativa* del INAP (1990, n.º 223, §7.3.4) da la misma: sirve de contraste y no se cita. La varianza no está en el tutorial: no entra.
5. **Tema 28 sin fuentes nuevas.** Pro Git (2.ª ed.) cubre los sistemas locales, centralizados y distribuidos, los flujos de trabajo y las soluciones de código abierto que nombra; la referencia de Git, el resto.
6. **Versión citada cuando la última es reciente o no es de soporte largo.** Se cita la estable y se pregunta solo lo que es igual en las demás columnas de la tabla, que también se ejecutan.
7. **Sin norma.** Ninguna de las 50 preguntas cita el BOE: Métrica v3 no es una norma y no hay disposición que regule estas materias.

### Fuentes técnicas (versión vigente comprobada el 25-11-2026)
| Fuente | Vigente | Se cita | Formato de `legalReference` |
|---|---|---|---|
| ASP.NET Core MVC | .NET 11 (STS); .NET 10, última LTS | .NET 10; igual en la 11 | `Microsoft Learn (ASP.NET Core 10), «Overview of ASP.NET Core MVC»` |
| Spring MVC | Spring Framework 7.1.0 | 7.1 | `Spring Framework 7.1, «DispatcherServlet»` |
| Métrica v3 | Versión 3 (PDF del PAe) | La misma | `Métrica v3, Técnicas y prácticas, «Casos de Uso»`; `Métrica v3, Gestión de Proyectos, GPI 2` |
| UML | OMG UML 2.5.1 (12-2017) | 2.5.1 | `UML 2.5.1 (OMG), §18.1.3.2` |
| Scrum | The Scrum Guide 2020 | La misma | `Guía de Scrum 2020, «Daily Scrum»` (título de la traducción) |
| Manifiesto ágil | 2001, sin versiones | La misma | `Manifiesto Ágil, principios` |
| Kanban | The Kanban Guide, mayo de 2025 | La misma | `The Kanban Guide (05-2025), «Flow Metrics»` |
| Programación de proyectos | NASA/SP-2010-3403, Rev. 1 | La misma | `NASA Schedule Management Handbook (SP-2010-3403 Rev. 1), §5.5.11` |
| Estimación PERT | DOE, tutorial de valor ganado, módulo 3 (sin versión; PDF descargado el 25-11-2026) | El mismo | `DOE, Earned Value Management Tutorial, módulo 3, «Methods for Determining Activity Duration»` |
| Git, referencia | 2.56.1 (26-10-2026) | Git, sin versión menor | `Git, referencia, git-reset` |
| Pro Git | 2.ª ed. (edición en línea 2.1.450, 25-05-2026) | 2.ª ed. | `Pro Git (2.ª ed.), §3.6` |
| JUnit | 6.1.3 (07-08-2026); la 6.2 es preliminar (M1) | 6.1; igual en la 5.14 | `JUnit 6.1, User Guide, «Test Instance Lifecycle»` |
| Selenium | 4.54.0 (23-11-2026) | Selenium 4 | `Selenium, documentación, «Waiting Strategies»` |
| Jenkins | LTS 2.580.2 (28-10-2026); semanal 2.592 | Jenkins (manual sin versión) | `Jenkins, User Handbook, «Pipeline Syntax»` |
| SonarQube | Server 2026.5 LTA; Community Build 26.11 | Server 2026.5; igual en Community Build | `SonarQube Server 2026.5, «Quality standards and new code»` |
| Nexus | Nexus Repository 3.98 | 3.x (ayuda sin versión) | `Sonatype Nexus Repository 3, «Repository Types»` |
| Ansible | ansible-core 2.22.0 (02-11-2026); la documentación «latest» es Ansible 14, con core 2.21 | Ansible (core 2.21); igual en la 2.22 | `Ansible, documentación (core 2.21), «Handlers: running operations on change»` |
| LXC | 7.0 LTS (30-04-2026); también 6.0 LTS | LXC, sin versión | `LXC, documentación, «Security»`; `lxc(7)` |

Notas:
- Las fuentes en inglés se citan traducidas y sin comillas angulares; el término original, entre paréntesis si hace falta. Los títulos de página y de apartado van en su idioma y exactos: se comprueban en el `.txt`.
- **Scrum:** se usan los términos ingleses habituales (Product Owner, Scrum Master, Daily Scrum), los mismos que en el supuesto 0004.
- **Git:** las páginas de referencia son las de la 2.56. Solo se pregunta lo que da el mismo resultado en las tres versiones ejecutadas. Los fragmentos fijan la rama inicial (`git init -b main`) y la identidad: nada depende de la configuración.
- **JUnit:** los nombres de paquete y anotación son los de Jupiter, iguales en la 5 y la 6. El enunciado dice «En JUnit (Jupiter)…».
- **ASP.NET Core:** comparadas las páginas de introducción, enlace de modelos, validación, componentes de vista y estado, el texto servido es el mismo en la 10 y la 11. La comprobación se repite con las páginas citadas.
- **Datos volátiles excluidos:** valores por defecto, límites, versiones, fechas de soporte, puertos, rutas de instalación, nombres de paquetes, ediciones y licencias de pago, opciones de línea de órdenes recientes y cuotas de mercado.

### Contradicciones y textos que quedan fuera
| Texto | Motivo |
|---|---|
| **MVC.** Página «Model-View-Controller» de patterns & practices (2014, previous-versions) | Microsoft la marca como contenido retirado: no se cita (ni modelo activo y pasivo) |
| ASP.NET MVC 5 y anteriores, Web Forms, `routes.MapRoute`, jQuery Validation, vistas «fuertemente tipadas» de versiones antiguas | Otra plataforma u obsoletos. Se pregunta lo común |
| Quién depende de quién: Learn dice que la vista y el controlador dependen del modelo, y otras descripciones de MVC hacen que el modelo notifique a la vista | El enunciado dice «Según la documentación de ASP.NET Core MVC…» |
| Orden de las fuentes del enlace de modelos, validación implícita de tipos no anulables, 400 automático de `[ApiController]`, proveedor de TempData | Valores por defecto o configurables |
| Las tres responsabilidades de MVC y las capas como definición directa | Son el supuesto 0001 |
| **Casos de uso.** «usa» (Métrica) frente a `include` (UML); «diagrama de colaboración» (Métrica) frente a comunicación (UML 2) | Nombres distintos. Nunca son la respuesta ni un distractor entre sí |
| UML §18.1.3.3: «executed at a single location in the included UseCase» | Errata aparente (debería ser el caso que incluye). No se pregunta dónde se ejecuta |
| Generalización entre actores o casos de uso | Métrica no la recoge en «Casos de Uso». Solo con UML, si entra |
| **Métrica v3.** Introducción, «Aportaciones»: enumera las interfaces repitiendo «Gestión de Proyectos» y sin «Gestión de Configuración», frente a la página anterior | Errata. Las interfaces se preguntan por la definición de una, no por la lista |
| Alcance del mantenimiento (correctivo y evolutivo en Métrica; más tipos en otras normas) | Solo con «Según Métrica v3…» |
| Normas que Métrica toma como referencia (ISO 12207, SPICE, ISO 9001:2000), organismos y páginas que cita | Sustituidas o desaparecidos; tema 18 |
| Duración y coste por fórmulas (COCOMO, Staffing Size, tablas de Albrecht y MARK II) | Tablas de valores que no son el dato central. De estimación solo se pregunta para qué desarrollo sirve cada técnica |
| **Scrum.** Reglas de la guía de 2017: tres preguntas del Daily, «Development Team», roles en vez de responsabilidades, auto-organizado | Retiradas en 2020 |
| Tamaño del equipo «por lo general 10 o menos» | Orientativo: no se pregunta como límite |
| Duraciones máximas de los eventos «para un Sprint de un mes» | El enunciado dice la duración del Sprint |
| Plazo de entrega del Manifiesto («entre dos semanas y dos meses») frente a la duración del Sprint | Si entra, por su valor exacto y con «Según los principios del Manifiesto Ágil…» |
| Manifiesto: «equipos auto-organizados» frente a Scrum 2020: «autogestionados» | No se enfrentan en una pregunta |
| **Kanban.** Lo que cambió en 2025: SLE dentro del DoW, control del WIP menos explícito, nombre de las métricas | Solo lo que es igual en 2020 y 2025 |
| **PERT.** Grafo con actividades en los arcos (Métrica) frente a nodos (herramientas CPM) | Los cálculos se dan con un cuadro de prelaciones, válido para los dos |
| DOE: «PERT» como duración por tres valores y «CPM» con un solo valor, frente a «idénticas» «en sus formas esenciales» (Métrica) | No se pregunta en qué se diferencian PERT y CPM. La pregunta de la fórmula da los tres valores y pide la duración esperada |
| Métrica, gráfico y cálculo de fechas: no da tiempos «early» y «last» | Se pregunta duración y holgura, no nombres de tiempos |
| NASA §5.5.10: holgura total, hasta entrar en el camino crítico; §5.5.11: hasta retrasar el fin del proyecto | Se usa la del §5.5.11. Los ejercicios no tienen fechas impuestas ni recursos limitados, así que coinciden |
| Holgura negativa, restricciones de fecha, calendarios, y día 0 o día 1 como inicio | Dependen de la herramienta. Se pregunta en semanas o días, sin fechas |
| NASA: margen, contingencia, reservas (GAO usa los términos de otro modo) | Terminología propia de la NASA |
| Métrica, Gantt: «Cada actividad debe tener recursos asociados» y el hito es «un tipo de actividad» sin recursos | Se pregunta el hito por su definición, no si toda actividad tiene recursos |
| Métrica, patrón de límites: «Diseño Funcional» y «análisis funcional» para lo mismo | Solo se pregunta el reparto entre arranque, pleno rendimiento y finalización |
| Métrica, diagrama de extrapolación: ejes «de la misma escala» y ejemplo con escalas distintas | Se pregunta para qué sirve y qué significa la bisectriz, no la construcción |
| **Git.** Rama inicial, `pull` con historias divergentes, `push.default`, estrategia de fusión y formato de `merge` | Valores por defecto o configuración |
| Pro Git usa `checkout`, `master` y la estrategia «recursive» | Ángulo antiguo: los fragmentos usan `switch` y `restore` si hay equivalente estable y no preguntan la estrategia |
| SHA-1 frente a SHA-256, `reftable`, cambios anunciados para Git 3.0, `git history`, `git replay`, órdenes marcadas como experimentales | Recientes o en transición |
| Recomendaciones «no reescribas lo publicado» en términos absolutos | Se pregunta la consecuencia (identificadores nuevos), no la recomendación |
| **JUnit.** JUnit 4 (`@Before`, `@RunWith`), motor Vintage (obsoleto), novedades de la 6 (`@ClassTemplate`, `@ParameterizedClass`) y ciclo por defecto configurable | Otra versión, obsoletos o recientes. El ciclo se pregunta con `@TestInstance` escrito o con el comportamiento estándar sin configuración |
| JUnit: orden de los métodos por defecto, ejecución paralela, `assertTimeoutPreemptively` | Deliberadamente no obvio, o experimental |
| **Selenium.** Valor por defecto de la espera implícita, Selenium IDE, Selenium RC, `DesiredCapabilities`, BiDi, localizadores relativos, Selenium Manager | Valores por defecto, heredados o recientes |
| **Jenkins.** Orden de todas las condiciones de `post`, sintaxis que depende de complementos (`docker`, `junit`, `withSonarQubeEnv`), nodo integrado, «master» y «slave» | Complementos, o terminología retirada |
| **SonarQube.** Tipos bug, vulnerabilidad y code smell frente a MQR y calidades del software; severidades | Dependen del modo de la instancia |
| Security Hotspots | En retirada en la 2026.5 |
| Condiciones de «Sonar way», 80 %, 3 %, factor de ajuste de 20 líneas, opciones por edición | Valores por defecto o de pago |
| **Nexus.** Repositorios preconfigurados, `staging` y etiquetas (Pro), tipos de almacén, qué antigüedad manda | Por defecto, de pago, o texto confuso en la fuente |
| **Ansible.** Plantillas y condicionales tras el cambio de la 2.19, precedencia completa de variables, `smart`, colecciones y nombres cortos de módulo | Cambian entre versiones o son listas largas. Módulos, con su nombre completo (`ansible.builtin.…`) |
| **LXC.** Introducción (LTS 5.0 y 6.0) y requisitos de núcleo frente a las noticias (7.0 LTS); `lxc(7)` describe cgroup v1, retirado en la 7.0 | Textos sin actualizar. No se preguntan versiones, núcleo, cgroup v1 ni congelación |
| LXC: orden `lxc` (LXD, Incus) frente a `lxc-*` | Otro producto |

### Reparto de Métrica v3 entre temas
| Apartado de Métrica v3 | Tema |
|---|---|
| Técnicas: «Modelo Entidad/Relación Extendido», «Normalización» | 10 (publicado) |
| Técnicas: «Modelado de Procesos de la Organización» | 13 (publicado) |
| Técnicas: «Casos de Uso» | 23 |
| Introducción, Participantes, procesos principales (objetivo de PSI, EVS, ASI, DSI, CSI, IAS y MSI), interfaces de apoyo, Gestión de Proyectos (GPI, GPS, GPF), técnicas de estimación, prácticas «Pruebas» y sesiones de trabajo (JAD, JRP) | 26 |
| Técnicas: «Planificación» (PERT, Gantt, asignación de recursos, patrón de límites, histograma, WBS, diagrama de extrapolación) | 27 |
| Interfaz de Gestión de Configuración | 26, sin preguntas en el 28 |
| Interfaces de Seguridad y de Aseguramiento de la Calidad; diagramas de clases, componentes, secuencia y DFD | Ninguno |

El 28 y el 29 no citan Métrica. En el 26, GPI 2 (Planificación) se pregunta sin PERT, Gantt ni WBS.

### Reparto orientativo
Se puede mover 1 pregunta entre apartados, sin salirse del ámbito del tema. «(f)»: con fragmento ejecutado. «(c)»: con cálculo por script.

| Tema | Apartado | Fuente | Nº |
|---|---|---|---|
| 23 | MVC: dependencias entre componentes, ViewModel, dónde va la lógica | ASP.NET Core | 2 |
| 23 | Controladores y acciones: enrutamiento, enlace de modelos, validación (1 f) | ASP.NET Core | 2 |
| 23 | Vistas: parciales, componentes de vista, diseño o paso de datos (f) | ASP.NET Core | 1 |
| 23 | Controlador frontal | Spring Framework 7.1 | 1 |
| 23 | Casos de uso: definición, actor, escenarios, «usa» y «extiende» | Métrica v3, Técnicas | 2 |
| 23 | Casos de uso: puntos de extensión, inclusión, sujeto, actor | UML 2.5.1, §18 | 2 |
| 26 | Manifiesto y principios | Manifiesto Ágil | 1 |
| 26 | Scrum: teoría y pilares, responsabilidades del Scrum Master y los Developers, Daily Scrum, Sprint Planning, cancelación del Sprint | Guía de Scrum 2020 | 3 |
| 26 | Kanban: WIP, métricas de flujo | The Kanban Guide | 1 |
| 26 | Métrica: estructura, procesos y participantes | Métrica v3, Introducción, Participantes y procesos | 2 |
| 26 | Métrica: Gestión de Proyectos (inicio, seguimiento, incidencias y cambios, cierre) o estimación | Métrica v3, Gestión de Proyectos | 2 |
| 26 | Métrica: pruebas (tipos, caja blanca y negra, estrategias de integración) | Métrica v3, Técnicas, «Pruebas»; CSI; IAS | 1 |
| 27 | PERT: actividad, suceso, prelaciones, ficticias, matriz y cuadro (c) | Métrica v3, Técnicas, PERT | 2 |
| 27 | Duración esperada con tres valores (c) | DOE, módulo 3 | 1 |
| 27 | Camino crítico, duración, holgura total y libre (c) | NASA, §5.5.10 y 5.5.11 | 3 |
| 27 | Relaciones entre actividades (fin-comienzo, comienzo-comienzo…) | NASA, §5.5 | 1 |
| 27 | Gantt: notación, hitos, encadenamientos | Métrica v3, Técnicas, Gantt | 1 |
| 27 | Asignación de recursos: histograma, patrón de límites (c) | Métrica v3, Técnicas, Gantt | 1 |
| 27 | WBS o diagrama de extrapolación | Métrica v3, Técnicas | 1 |
| 28 | Sistemas locales, centralizados y distribuidos; soluciones de código abierto | Pro Git, §1.1-1.3 | 1 |
| 28 | Repositorio local: áreas, estados, objetos y referencias (1 f) | Pro Git; referencia | 2 |
| 28 | Ramas: avance rápido, fusión a tres bandas, rebase, conflictos (f) | Pro Git, cap. 3 | 2 |
| 28 | Deshacer: `reset`, `revert`, `restore`, `reflog` (f) | Pro Git; referencia | 2 |
| 28 | Remotos: ramas de seguimiento, `fetch` y `pull`, `push` rechazado (f) | Pro Git; referencia | 1 |
| 28 | Estrategias: flujos distribuidos (centralizado, gestor de integración, dictador y tenientes) | Pro Git, §5.1 | 1 |
| 28 | Herramientas: `stash`, `cherry-pick`, `bisect`, etiquetas, `.gitignore` o hooks (f) | Pro Git; referencia | 1 |
| 29 | JUnit: ciclo de vida, aserciones agrupadas, suposiciones, anidadas, desactivación (f) | JUnit 6.1 | 2 |
| 29 | Selenium: esperas, localizadores, Page Object, Grid (1 f) | Selenium | 2 |
| 29 | Nexus: tipos de repositorio, componentes, políticas | Nexus Repository 3 | 1 |
| 29 | Jenkins: canalización declarativa, `post`, `agent`, `parallel`, bibliotecas compartidas (f) | Jenkins | 2 |
| 29 | SonarQube: código nuevo, perfiles de calidad, métricas | SonarQube Server 2026.5 | 1 |
| 29 | Ansible: inventario, idempotencia, handlers, roles (f) | Ansible | 1 |
| 29 | LXC: contenedores con y sin privilegios, qué es y qué no es | LXC | 1 |

Total previsto: 50 preguntas con fuente técnica y ninguna con norma. Todas se listan en el PR.

### Criterios propios de esta tanda (además de los de las specs 0004 y 0022)
- **Lecciones de las revisiones de las tandas 4 a 7:**
  - sin nombres de ministerios ni de Secretarías de Estado en ningún campo (los PDF de Métrica los llevan en el pie);
  - la correcta no es la más larga ni la más corta, y las 4 opciones comparten estructura y palabras iniciales; si las opciones son valores, se equilibran dos a dos;
  - ningún distractor es un componente real de la lista que se pregunta, salvo que se pregunte la definición de un solo elemento;
  - sin dobles negaciones heredadas de la fuente; sin absolutos («solo», «siempre», «nunca») que estén únicamente en los distractores;
  - sin distractores numéricos del mismo apartado; los umbrales y plazos se preguntan por su valor exacto;
  - sin preguntas gemelas; si dos comparten apartado, como mucho una es de definición y la otra un caso (en Scrum, Kanban, Manifiesto y `lxc(7)`, sobre todo);
  - un enunciado no nombra la respuesta de otra pregunta y un caso no parafrasea la opción de otra;
  - las `explanation` no parafrasean mal otros apartados ni desvelan otra pregunta, y sus cuentas se recalculan;
  - se citan todas las páginas de las que depende la respuesta;
  - en los fragmentos, todo está definido, sin rutas propias de una imagen, y se buscan los valores por defecto que harían correcto un distractor.
- **Enunciado con la fuente o la tecnología al principio:** «Según la documentación de ASP.NET Core MVC…», «En Spring MVC…», «Según Métrica v3…», «Según UML 2.5.1…», «Según la Guía de Scrum de 2020…», «Según el Manifiesto Ágil…», «Según The Kanban Guide…», «Según el método del camino crítico (CPM)…», «En Git…», «En JUnit (Jupiter)…», «En Selenium WebDriver…», «En un Jenkinsfile declarativo…», «En SonarQube Server…», «En Nexus Repository…», «En Ansible…», «En LXC…».
- **Distractores reales:** otras órdenes, anotaciones, directivas o términos de la misma tecnología. Nada inventado. Se comprueba que no son correctos en otro apartado, versión ni fuente de la tabla (Scrum y Kanban, Métrica y UML, Métrica y NASA); los que son órdenes o salida también se ejecutan.
- **Útiles para un C1 de programador:** el dato central. Sin opciones rebuscadas de línea de órdenes.
- **Sin solapes con el supuesto 0004** (el principal). No son la respuesta, el objeto ni el ejemplo:
  - Scrum: los tres artefactos y sus compromisos, quién gestiona el Product Backlog, duración del Sprint, Definition of Done, diferencia entre Sprint Review y Sprint Retrospective;
  - Git: Gitflow y desarrollo basado en troncal, ramas `feature`, `release` y `hotfix`, protección de ramas y Pull Request, `merge --no-ff`, etiqueta anotada con `tag -a`, `push -u`, `switch -c`, `branch -d`;
  - JUnit: `assertThrows`, `@ParameterizedTest` con `@CsvSource` o `@ValueSource`, nombres de las pruebas, casos límite (`@Test` y `assertEquals` aparecen en los fragmentos, sin ser el objeto);
  - Jenkins: Jenkinsfile versionado y multirrama, paso `junit` en `post { always }`, `withSonarQubeEnv`, `waitForQualityGate` y webhook, `when { branch }`, `withCredentials`, `timeout`, `disableConcurrentBuilds`, `agent { label }`;
  - SonarQube: detener la canalización por el quality gate; Nexus: `mvn deploy`, credenciales y repositorio de versiones o de snapshots según la versión.
  - Ni su escenario (Portal de Ayuntamientos, plazos de una subvención) ni sus clases.
- **Sin solapes con el supuesto 0001:** responsabilidades de Modelo, Vista y Controlador como definición, arquitectura en capas, DTO, API REST con controladores, EF Core, `ProblemDetails`, `WebApplicationFactory`, subvenciones. El 23 pregunta por aplicaciones MVC con vistas.
- **Fronteras con los temas publicados:**
  - 9: permisos, procesos y órdenes de Linux; nube y virtualización. 12: seguridad y ENS (en LXC, sin medidas de seguridad de norma).
  - 10: E-R y normalización. 13: procesos, BPMN y SADT. 14: auditoría. 18: ISO, certificación y conformidad.
  - 19: excepciones, `static` y ciclo de vida del Servlet y de los beans (en Spring MVC, solo el papel de `DispatcherServlet`; en JUnit, la pregunta no se responde con saber Java). 20: duración de servicios y middleware. 21: HTML y DOM (en Selenium, el localizador, no la especificidad CSS). 25: HTTP, cookies, sesiones y proxy inverso (en Nexus, el repositorio proxy).
- **Reparto de lo común entre los 5 ficheros:**

| Materia | Va en | No va en |
|---|---|---|
| Tipos de prueba (unitaria, integración, sistema, regresión), caja blanca y negra | 26 (Métrica) | 29 |
| Pruebas con una herramienta | 29 | 26 |
| PERT, Gantt, WBS, recursos | 27 | 26 |
| Seguimiento, incidencias, cambios y cierre del proyecto | 26 | 27 |
| Casos de uso | 23 | 26 |
| Gestión de la configuración (Métrica) | 26 | 28 y 29 |
| Git | 28 | 29 (en Jenkins y SonarQube, sin órdenes de Git) |
| Ramas como código nuevo o de referencia | 29 (SonarQube) | 28 |
| Validación del modelo y de formularios | 23 | 29 |
| Espera y sincronización de la página | 29 (Selenium) | 23 |
| Repositorio: de código (Git), de artefactos (Nexus), de calidad | 28 y 29, sin enunciados que se confundan | 26 |
| Límite de trabajo en curso | 26 (Kanban) | 27 (máximo de recursos del patrón de límites) |
| Hito | 27 | 26 |

- **Ninguna opción delata otra pregunta,** dentro de cada fichero, entre los 5 y contra los temas publicados y los supuestos.
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Fragmentos ejecutables
Código corto (≤ 10 líneas, texto plano), con un solo resultado y determinista. **Se ejecuta todo el código y todas las órdenes, las del enunciado y las de las opciones** (también las erróneas, para comprobar que fallan o dan otro resultado). Lo que no se pueda ejecutar no entra como fragmento. Las imágenes ya están descargadas (25-11-2026), antes de lanzar los subagentes.

| Materia | Contenedores desechables (`docker run --rm`) | Comprobación |
|---|---|---|
| JUnit | `maven:3-eclipse-temurin-25` (JDK 25.0.5) y `maven:3-eclipse-temurin-27` | `mvn -q test` con JUnit 6.1.3 y con 5.14.4; misma salida |
| Selenium | `selenium/standalone-chromium:4` y `selenium/standalone-firefox:4` (4.54), con cliente Java en la imagen de Maven, en una red desechable | Mismo resultado en los dos navegadores, con páginas `data:` propias |
| Git | `debian:stable` (2.47.3), `alpine/git` (2.54.1) y `alpine:3` con el paquete de edge (2.56.1) | Mismo resultado en los tres, sobre repositorios creados en el contenedor; los «remotos» son repositorios `--bare` locales |
| Ansible | `python:3.14-slim` con ansible-core 2.21.6 y 2.22.0 | `ansible-playbook` con conexión local; mismo `PLAY RECAP` |
| Jenkinsfile | `jenkins/jenkins:lts-jdk25` (2.580.2) con `workflow-aggregator`. Probado: arranca sin asistente y `pipeline-model-converter/validate` acepta un fichero válido y rechaza uno erróneo | Validador y, si se pregunta el comportamiento (`post`, `parallel`), ejecución de un trabajo |
| ASP.NET Core MVC | `mcr.microsoft.com/dotnet/sdk:10.0` y `:11.0` | Aplicación `dotnet new mvc`; petición desde dentro del contenedor; misma respuesta |
| PERT y recursos | Python de la imagen `python:3.14-slim` | Script con pasada hacia delante y hacia atrás, a partir del enunciado publicado: duración, camino crítico (único) y holguras; también qué error lleva a cada distractor |

- LXC, SonarQube, Nexus, Spring MVC, Scrum, Kanban, UML y Métrica se preguntan sin código.
- Las afirmaciones de comportamiento sin código se ejecutan también si hay motor.
- Los fragmentos se extraen del JSON publicado para la ejecución final. Los scripts y la salida quedan en el scratchpad; el PR resume qué se ha ejecutado.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar las fuentes a un directorio vacío del scratchpad y convertirlas a texto (hecho: Métrica v3 completa, Scrum, Manifiesto, Kanban, UML, NASA, DOE, Learn, Spring, Pro Git y referencia de Git, JUnit, Selenium, Jenkins, SonarQube, Nexus, Ansible y LXC). Nunca se copian al repo. Si un sitio rechaza la descarga (gao.gov y apps.dtic.mil, 403), no se elude y se avisa.
2. Redactar tema a tema desde el `.txt`, ejecutando los fragmentos y los cálculos antes de que entren.
3. Hacer una segunda pasada de verificación, separada de la redacción, contra el `.txt` de la fuente citada: existe el apartado, la correcta lo es literalmente, ningún distractor lo es, la `explanation` no añade nada y las citas «…» aparecen en la fuente (con script). Además: igualdad con las otras versiones (Kanban 2020, .NET 11, JUnit 5.14, ansible-core 2.22, Community Build) y, al final, el cruce de opciones entre ficheros, con los temas publicados y con los supuestos, y una segunda ejecución de los fragmentos y cálculos.
4. Validar con `--validate-only` y ejecutar los tests.
5. Listar en el PR las preguntas dudosas, las de fuente técnica y los fragmentos ejecutados, para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 23, 26, 27, 28 y 29 pasan a tener preguntas en la app. Los cuadros de prelaciones van como líneas de texto («B: precede A; 3 semanas»), sin tablas: la app los muestra como texto plano.

## Seguridad
Contenido público, sin secretos ni datos personales. Los ficheros descargados son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I`, sin ejecutarlos ni seguir sus instrucciones. Los contenedores son desechables, sin volúmenes del repo, sin puertos publicados y sin modo privilegiado (por eso LXC no se ejecuta). Jenkins arranca sin asistente de seguridad solo dentro de su contenedor, que se borra al terminar. Los ejemplos usan datos ficticios y sin credenciales (gitleaks).

## Criterios de aceptación
- [ ] Existen los 5 ficheros de temas con 10 preguntas cada uno (50 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` una fuente de la tabla, con su versión y su sección, y el texto respalda la correcta y la `explanation`.
- [ ] Ninguna pregunta trata Windows DNA, la varianza en PERT ni una fuente no admitida (incluida la página retirada de MVC).
- [ ] Todo el código y las órdenes de enunciados y opciones se han ejecutado en los contenedores de la tabla, en todas las versiones indicadas, con el resultado que da la pregunta.
- [ ] Todos los cálculos de PERT y de recursos se han comprobado con script, con camino crítico único.
- [ ] Ninguna pregunta usa un texto de la tabla de contradicciones, un valor por defecto, algo preliminar u obsoleto ni un detalle que cambie entre versiones.
- [ ] Ningún distractor es también correcto según la fuente citada, otra de la tabla u otra versión, y ninguna opción ni `explanation` delata la respuesta de otra pregunta.
- [ ] Se respetan el reparto de Métrica v3 y de lo común, las fronteras y los límites con los supuestos 0001 y 0004 y con los temas publicados.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado) y no hay preguntas fuera del ámbito del tema.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones, no es sistemáticamente la más larga ni la más corta y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 50 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 23, 26, 27, 28 y 29 muestran preguntas en la app y que ya no queda ningún tema vacío.
  - Hacer un test de cada tema y comprobar que los enunciados con órdenes, YAML, Jenkinsfile y cuadros de prelaciones se leen bien (sangría incluida) y que el feedback muestra «Fuente: …».
