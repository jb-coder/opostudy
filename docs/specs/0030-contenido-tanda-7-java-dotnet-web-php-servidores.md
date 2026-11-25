# 0030 — Contenido, tanda 7: Java, .NET, desarrollo web, PHP y servidores web

- **Issue:** #73
- **Milestone:** v0.4.0
- **Estado:** lista

> Séptima de las 8 tandas de contenido de la v0.4.0 (10 preguntas por tema). Solo contenido: sin cambios de código, de esquema ni de modelo.
> Reglas canónicas: formato en la spec 0003, criterios de calidad y proceso en la spec 0004, fuentes técnicas en la spec 0022. Modelo: spec 0029.

## Objetivo
Dar 50 preguntas de test, redactadas y verificadas contra el texto de la fuente citada, a los temas 19, 20, 21, 22 y 25 (bloque II-B), que hoy no tienen ninguna.

## Alcance
**Incluye:**
- `topics/java.json`: 10 preguntas del tema 19.
- `topics/dotnet.json`: 10 preguntas del tema 20.
- `topics/html-css-javascript.json`: 10 preguntas del tema 21.
- `topics/php.json`: 10 preguntas del tema 22.
- `topics/servidores-web.json`: 10 preguntas del tema 25.

**No incluye:**
- Preguntas de otros temas. SQL y concurrencia de BD (10), ENS y seguridad (12), accesibilidad, WCAG, diseño adaptativo y Media Queries (17), MVC y casos de uso (23), SOA, XML, SOAP y SAML (24), Git (28), JUnit, Selenium, Maven, Nexus, Jenkins, SonarQube y despliegue (29) se pueden nombrar, pero no son el objeto de la pregunta.
- TLS y certificados (12 y 35), contenedores (29), REST (sin especificación admitida) y CORS.
- Productos sin fuente admitida: Hibernate, Spring Boot, Laravel, Symfony, jQuery, React, Node.js, Composer, XAMPP.
- .NET Framework, Visual Studio, MAUI, Blazor, EF Core y la plataforma Windows.
- Cambios en `exam.json`, en el JSON Schema, en el importador o en la app.

## Diseño
### Ámbito (enunciado del BOP, `title` en `exam.json`)
- **Tema 19** (11 del bloque II). Lenguaje de programación Java. Arquitectura. Frameworks.
- **Tema 20** (12). Entorno de desarrollo Microsoft .NET.
- **Tema 21** (13). Desarrollo web con HTML y CSS. Lenguaje JavaScript.
- **Tema 22** (14). Programación de páginas y aplicaciones web con PHP.
- **Tema 25** (17). Servidores web. Tecnologías para el desarrollo de aplicaciones web. Aplicaciones en entornos PHP y MySQL.

### Decisiones que se aprueban con esta spec
1. **Fuentes que la spec 0022 no nombra.** Su lista es de ejemplos («documentación oficial del fabricante o proyecto», «especificaciones y estándares oficiales»). Se admiten expresamente:
   - **Documentación de MySQL** (dev.mysql.com, manual de referencia 9.7). Ya figuraba como fuente prevista en la issue.
   - **W3C CSS:** solo las especificaciones de la «definición oficial» de CSS en el CSS Snapshot 2026, §2.1. Las del §2.2 y siguientes (Grid, Cascade 5, Display 3…) no entran, ni los borradores de trabajo (Selectors 4, Sizing 3, Position 3).
   - **Apache HTTP Server 2.4** (httpd.apache.org) y **nginx** (nginx.org): documentación oficial de los dos proyectos. Tema 25.
   - **Frameworks de Java:** especificaciones de **Jakarta EE 11** (Servlet, Persistence, CDI) y documentación de **Spring Framework**. El tema dice «Frameworks» y ni la JLS ni la API de Java SE los definen.
   - **WHATWG DOM**, ya nombrada, y **RFC 6265, 8259 y 3875** del IETF, además de las de HTTP.
2. **Versión citada cuando la última no es LTS.** Se cita la última LTS y se pregunta solo lo que es igual en la última publicada, que también se ejecuta. Java: 25 (vigente la 27). .NET: 10 (vigente la 11). MySQL: 9.7 (vigente la 26.10 Innovation). Así el contenido sirve durante más tiempo y coincide con la issue. PHP no tiene LTS: se cita el manual y se pregunta lo que es igual en la 8.5 y la 8.6.
3. **Sin norma.** Ninguna de las 50 preguntas cita el BOE. La NTI de Catálogo de estándares (`BOE-A-2012-13501`) enumera HTML, CSS y HTTP con versión y estado, pero no define nada: no se cita.

### Fuentes técnicas (versión vigente comprobada el 25-11-2026)
| Fuente | Vigente | Se cita | Formato de `legalReference` |
|---|---|---|---|
| Java: JLS, JVMS y API | Java SE 27 (15-09-2026, no LTS); 25 es la última LTS | Java SE 25 | `JLS (Java SE 25), §8.4.8`; `JVMS (Java SE 25), §2.5.3`; `API de Java SE 25, java.util.List` |
| Jakarta EE | Jakarta EE 11 (la 12, en desarrollo) | Servlet 6.1, Persistence 3.2, CDI 4.1 | `Jakarta Servlet 6.1, §2.3` |
| Spring Framework | 7.1.0 (la 7.0, con soporte) | 7.1 | `Spring Framework 7.1, «Bean Scopes»` |
| .NET, C# y ASP.NET Core | .NET 11 y C# 15 (10-11-2026, STS); .NET 10 es la última LTS (las dos con soporte hasta 14-11-2028); .NET 8 y 9, sin soporte desde el 10-11-2026 | .NET 10 y C# 14 | `Microsoft Learn (.NET 10), «Fundamentals of garbage collection»` |
| HTML | HTML Living Standard (WHATWG) | Edición del 25-11-2026 | `HTML Living Standard (WHATWG, 25-11-2026), §4.12.1` |
| DOM | DOM Living Standard (WHATWG) | Edición descargada el 25-11-2026 | `DOM Living Standard (WHATWG, 25-11-2026), §2.9` |
| CSS | CSS Snapshot 2026: CSS 2.1 revisado (CSS2), Selectors 3, Box 3, UI 3 (recomendaciones); Cascade 4, Flexbox 1, Variables 1, Color 4 (candidatas) | Las mismas | `CSS 2 (W3C), §8.3.1`; `Selectors Level 3 (W3C), §9`; `CSS Flexbox 1 (W3C), §9.5` |
| JavaScript | ECMA-262, 17.ª edición (junio de 2026) | ECMAScript 2026 | `ECMA-262 (2026), §14.3.1` |
| PHP | 8.6.0 (19-11-2026); con soporte activo 8.4 y 8.5; solo de seguridad 8.2 y 8.3 | Manual de PHP | `Manual de PHP, «Traits»` |
| HTTP | RFC 9110, 9111 y 9112 (STD, junio de 2022) | Las mismas | `RFC 9110, §9.2.2` |
| Cookies | RFC 6265 (abril de 2011). La 6265bis está en la cola del RFC Editor | RFC 6265 | `RFC 6265, §4.1.2.6` |
| JSON, CGI | RFC 8259 (STD 90); RFC 3875 (informativa) | Las mismas | `RFC 8259, §3` |
| Apache HTTP Server | 2.4.71 (11-11-2026) | 2.4 | `Apache HTTP Server 2.4, «Name-based Virtual Host Support»` |
| nginx | 1.30.8 estable, 1.31.10 principal; documentación sin versión | nginx | `Documentación de nginx, «How nginx processes a request»` |
| MySQL | 9.7.4 LTS, 8.4.13 LTS, 26.10 Innovation | 9.7 | `MySQL 9.7, «INSERT ... ON DUPLICATE KEY UPDATE Statement»` |

Notas:
- **Java 25 y 27:** comparados los capítulos 4, 5, 8-12, 14 y 15 de la JLS y 1, 2 y 5 de la JVMS, las diferencias son de redacción (y la versión del formato `class`). Lo nuevo de la 26 y la 27 es preview, incubación o de implementación (recolector G1, cabeceras compactas): fuera.
- **Microsoft Learn** no está versionado en los temas de .NET y C#. Se usa lo que el texto no limita a C# 15 o .NET 11. Los artículos son de fechas muy distintas (2016 a 2026): ver contradicciones.
- **PHP:** el manual en inglés (el español va por detrás). Las notas de usuarios («User Contributed Notes») no son fuente.
- **CSS en fase de candidata:** se usa solo lo que implementan Chromium, Firefox y WebKit con el mismo resultado.
- **WHATWG y ECMA-262:** los «Living Standards» cambian a diario. Se pregunta el núcleo estable y la referencia lleva la fecha del texto descargado.
- **nginx:** la documentación describe la rama principal. Solo se pregunta lo que la página no marca como nuevo desde la 1.29 y que se ejecuta igual en la estable.
- Las fuentes en inglés se citan traducidas y sin comillas angulares; el término original, entre paréntesis si hace falta.
- **Datos volátiles excluidos:** valores por defecto, límites, números de versión, fechas de soporte, rutas de instalación, nombres de paquetes, opciones de línea de órdenes recientes, cuotas de mercado y productos.

### Contradicciones y textos que quedan fuera
| Texto | Motivo |
|---|---|
| **Java.** `main` de instancia y ficheros de código compactos (JLS §7.3 y §12.1.4, Java 25) | Cambian la respuesta clásica de «punto de entrada». Los fragmentos usan `public static void main(String[] args)` |
| Finalización (`finalize`, JLS §12.6), `Integer(int)`, `Thread.stop`, Security Manager, applets | Obsoletos o en retirada |
| Patrones con tipos primitivos, concurrencia estructurada, constantes perezosas, API vectorial | Preview o incubación en la 25 y la 27 |
| Recolector por defecto, zona Metaspace, JIT, tamaños de pila y montículo, JDK frente a JRE | De la implementación (HotSpot), no de la JVMS. La 27 cambia el recolector por defecto |
| Área de métodos: «lógicamente parte del montículo» (JVMS §2.5.4) frente a área propia | Se presta a dos respuestas. No se pregunta dónde está |
| Orden de iteración de `HashMap` y `Set.of`; caché de enteros fuera de −128 a 127 | Sin garantizar |
| `javax.*` frente a `jakarta.*`; `SingleThreadModel` | Cambio de nombres y retirado de Servlet |
| Spring: ámbito por defecto del bean; inyección por campo o por constructor | Valor por defecto, y recomendación valorativa |
| Persistence: diferencias entre 3.2 y versiones anteriores; texto de Servlet sobre `HEAD` heredado | Dos respuestas según la versión |
| **.NET.** «El código gestionado lo compila el JIT» frente a Native AOT y ReadyToRun | La página del proceso de ejecución (2017) y la de Native AOT se apartan. El enunciado dice «con el compilador JIT» o no lo pregunta |
| Ensamblados: GAC, nombres seguros, módulos, dominios de aplicación, seguridad de acceso del código | Solo valen para .NET Framework |
| Tipos de valor «en la pila» y de referencia «en el montículo» | «Boxing and Unboxing» lo usa como ejemplo, pero depende del contexto. Se pregunta la semántica de copia |
| Umbral del montículo de objetos grandes; montículo como «generación 3» | Configurable, y denominación no uniforme |
| .NET Standard: «para código nuevo» y tablas de versiones | Recomendación que cambia con cada versión |
| C# 15 (uniones, jerarquías cerradas, `break` con etiqueta) y C# 14 (miembros de extensión, `field`) | Recientes o no citables en la versión anterior |
| DI: validación de ámbitos y excepciones al liberar el ámbito (cambia en la 11); orden recomendado completo del middleware | Depende del entorno o de la versión |
| **HTML, CSS y JS.** HTML 5.x del W3C frente al estándar vivo de WHATWG | El W3C remite al de WHATWG desde 2019. Siempre «Según el estándar HTML de WHATWG…» |
| Esquema de títulos por secciones (outline algorithm) y elementos obsoletos (`center`, `font`, `frame`) | Retirado del estándar, u obsoletos |
| `alt`, `lang`, `label`, ARIA, `viewport` y contraste como objeto | Son del tema 17 |
| `box-sizing`: CSS UI 3 (recomendación) y Sizing 3 (borrador que la sustituye) | Se cita CSS UI 3 y solo lo que es igual en las dos |
| Especificidad de `:is()`, `:where()`, `:has()` y `:not()` con lista (Selectors 4, borrador); `position: sticky` (Position 3) | No están en las especificaciones admitidas |
| Orden de la cascada con capas (`@layer`, Cascade 5) | Fuera de la definición oficial. Las preguntas de cascada no usan capas ni estilos de usuario |
| ECMA-262 frente a HTML: temporizadores, bucle de eventos, `window`, `fetch` | No son de ECMA-262. `setTimeout` se cita por el estándar HTML y las promesas por ECMA-262 |
| Anexo B (`__proto__`, `escape`, funciones en bloques) y modo no estricto (`this` global, variables implícitas) | Comportamiento heredado que depende del modo. El enunciado dice el modo si influye |
| Novedades de ES2025 y ES2026 | Recientes. Solo lo que no ha cambiado desde ES2020 |
| **PHP.** Retornar dentro de `finally` (obsoleto en 8.6), `(boolean)` y `(integer)`, comillas invertidas, `case` con `;`, `null` como clave de array, incremento de cadenas no numéricas, `__sleep` y `__wakeup` (8.5) | Obsoletos desde la 8.5 o la 8.6 |
| `??` y `empty()` con propiedades mágicas, `foreach` por referencia, valor por defecto en propiedades `readonly`, orden de enlace de los traits, comparación laxa de objetos con `bool`, conversión de `NAN` y de flotantes fuera de rango | Cambian en la 8.5 o la 8.6 |
| Comparación laxa de número y cadena (`0 == "a"`) antes de PHP 8 | Si entra, «En PHP 8…» |
| Algoritmo y coste de `password_hash`, directivas de `php.ini` y de sesión por defecto, contenido de `$_REQUEST` | Volátiles o dependen de la configuración |
| Extensión `mysql_*`, `register_globals`, `magic_quotes`, `mysqli` en modo de errores | Retirados, o cambiaron de valor por defecto |
| **Servidores.** RFC 9110, 301 y 302: el cliente «puede» cambiar POST por GET «por razones históricas», frente a 307 y 308 | No se pregunta el cambio de método con 301 ni 302 |
| RFC 7230 a 7235 y 2616 | Sustituidas por la 9110, 9111 y 9112 |
| TLS 1.3: RFC 8446 | Sustituida por la RFC 9846 (tema 12): no se usa |
| Cookies: `SameSite`, prefijos `__Host-` y duración máxima | Solo están en la 6265bis, aún sin publicar. Se pregunta lo que es igual en las dos |
| Apache: MPM por defecto, primer `VirtualHost` como predeterminado, valores de los MPM, `AllowOverride` por defecto, sintaxis 2.2 (`Order`, `Allow`) | Valores por defecto o versión anterior |
| nginx: bloques `location` con predicado (1.31.5), `server` por defecto, `if` | Rama principal o configuración |
| MySQL: motor, juego de caracteres y modo SQL por defecto; `VALUES()` en `ON DUPLICATE KEY UPDATE`; claves ajenas a columnas no únicas; caché de consultas | Valores por defecto, obsoletos o retirados |
| MySQL: `SET DEFAULT`, `NO ACTION` y `RESTRICT`; nulos en `UNIQUE`; niveles de aislamiento; `UNION`, CTE y vistas | Son del tema 10 o ya están en `bases-de-datos-sql` |
| PDO: emulación de sentencias preparadas y modo de error | Depende del controlador y de la versión |

### Reparto orientativo
Se puede mover 1 pregunta entre apartados, sin salirse del ámbito del tema. «(f)»: con fragmento ejecutado.

| Tema | Apartado | Fuente | Nº |
|---|---|---|---|
| 19 | Arquitectura: máquina virtual, áreas de datos, carga, enlace e inicialización | JVMS §1.2, 2.5, 5; JLS cap. 12 | 2 |
| 19 | Tipos, conversiones e identidad de objetos (f) | JLS caps. 4, 5 y 15 | 1 |
| 19 | Clases e interfaces: herencia, sobrescritura y ocultación, `static`, `final`, interfaces (1 f) | JLS caps. 8 y 9 | 2 |
| 19 | Excepciones: comprobadas, `try` con recursos (f) | JLS cap. 11, §14.20 | 1 |
| 19 | Genéricos, colecciones o streams | JLS §4.5-4.6; API de Java SE | 1 |
| 19 | Frameworks: Servlet (ciclo de vida), Persistence o CDI, contenedor de Spring | Jakarta EE 11; Spring Framework 7.1 | 3 |
| 20 | CLR y ejecución gestionada: IL, metadatos, independencia del lenguaje (CTS y CLS) | Microsoft Learn (.NET) | 2 |
| 20 | Recolección de basura y liberación de recursos (`IDisposable`, `using`) (1 f) | Microsoft Learn (.NET, C#) | 2 |
| 20 | Ensamblados y modos de publicación | Microsoft Learn (.NET) | 1 |
| 20 | C#, tipos: valor y referencia, boxing, `record`, `ref`/`out` (f) | Microsoft Learn (C#) | 1 |
| 20 | C#, herencia y acceso: `virtual`, `override`, `new`, `sealed`, modificadores (f) | Microsoft Learn (C#) | 1 |
| 20 | C#, delegados, iteradores (`yield`) o `async`/`await` (1 f) | Microsoft Learn (C#) | 2 |
| 20 | ASP.NET Core o extensiones: duración de los servicios, middleware | Microsoft Learn (.NET, ASP.NET Core) | 1 |
| 21 | HTML: estructura y semántica; formularios y validación; `script` (`async`, `defer`, módulos) | HTML Living Standard | 3 |
| 21 | CSS: cascada y especificidad (f); modelo de cajas y márgenes (f); posicionamiento, flotantes o flexbox | CSS 2, Selectors 3, Cascade 4, UI 3, Flexbox 1 | 3 |
| 21 | JavaScript: ámbito y clausuras (f); tipos, igualdad y conversiones (f); `this`, prototipos y clases (f) | ECMA-262 | 3 |
| 21 | JavaScript: promesas y tareas, o eventos del DOM (f) | ECMA-262; DOM; HTML | 1 |
| 22 | Tipos, comparación y conversiones (f) | Manual de PHP | 1 |
| 22 | Ámbito de variables y referencias (f) | Manual de PHP | 1 |
| 22 | Funciones: argumentos, `strict_types`, clausuras y funciones flecha (f) | Manual de PHP | 1 |
| 22 | Arrays y estructuras de control (`match`, `foreach`) (f) | Manual de PHP | 1 |
| 22 | Objetos: clases abstractas, interfaces, traits, enlace estático en tiempo de ejecución, excepciones (1 f) | Manual de PHP | 2 |
| 22 | Organización: espacios de nombres, autocarga, `include` y `require` | Manual de PHP | 1 |
| 22 | Aplicación web: superglobales y subida de ficheros; sesiones y cookies; salida y contraseñas | Manual de PHP | 3 |
| 25 | HTTP: métodos, códigos de estado | RFC 9110 | 2 |
| 25 | Tecnologías web: caché, cookies o JSON | RFC 9111, 6265, 8259 | 1 |
| 25 | Apache: MPM, hosts virtuales, secciones de configuración o `.htaccess` (f) | Apache HTTP Server 2.4 | 2 |
| 25 | nginx: procesos, elección de `location`, proxy inverso (f) | Documentación de nginx | 1 |
| 25 | PHP en el servidor: CGI, FastCGI, PHP-FPM | RFC 3875; Manual de PHP; Apache; nginx | 1 |
| 25 | PHP y MySQL: sentencias preparadas; transacciones y confirmación implícita (1 f) | Manual de PHP; MySQL 9.7 | 2 |
| 25 | MySQL: motores de almacenamiento, `AUTO_INCREMENT`, `ON DUPLICATE KEY UPDATE`, `REPLACE` (f) | MySQL 9.7 | 1 |

Total previsto: 50 preguntas con fuente técnica y ninguna con norma. Todas se listan en el PR.

### Criterios propios de esta tanda (además de los de las specs 0004 y 0022)
- **Lecciones de las revisiones de las tandas 4 a 6:**
  - sin nombres de ministerios ni de Secretarías de Estado en ningún campo;
  - la correcta no es la más larga ni la más corta, y las 4 opciones comparten estructura y palabras iniciales;
  - ningún distractor es un componente real de la lista que se pregunta, salvo que se pregunte la definición de un solo elemento;
  - sin dobles negaciones heredadas de la fuente; sin absolutos («solo», «siempre», «nunca») que estén únicamente en los distractores;
  - sin distractores numéricos del mismo apartado; los umbrales se preguntan por su valor exacto;
  - sin preguntas gemelas; si dos comparten apartado, como mucho una es de definición y la otra un caso;
  - un enunciado no nombra la respuesta de otra pregunta y un caso no parafrasea la opción de otra;
  - las `explanation` no parafrasean mal otros apartados ni desvelan otra pregunta, y sus cuentas se recalculan;
  - en los fragmentos, se buscan los valores por defecto ocultos que harían correcto un distractor.
- **Enunciado con la versión o la tecnología:** «En Java…», «En C#…», «En .NET…», «Según el estándar HTML de WHATWG…», «En CSS…», «En JavaScript (ECMA-262)…», «En PHP 8…», «Según la RFC 9110…», «En Apache HTTP Server 2.4…», «En nginx…», «En MySQL…». La versión menor, solo en `legalReference`.
- **Distractores reales:** otras palabras clave, métodos, elementos, directivas o códigos de la misma tecnología. Nada inventado. Se comprueba que no son correctos en otro apartado, versión ni implementación; los que son código o salida también se ejecutan.
- **Útiles para un C1 de programador:** el dato central. Sin trampas de sintaxis rebuscadas ni memorización de firmas.
- **Sin solapes con el supuesto 0001** (.NET). Fuera del tema 20: MVC y capas, DTO, EF Core (entidades, Fluent API, migraciones, `AsNoTracking`, precisión decimal), LINQ (`Where`, `GroupBy`, `Select`, `OrderByDescending`, `Sum`, ejecución diferida e `IQueryable`), Minimal API, `ProblemDetails`, `UseExceptionHandler`, códigos 400, 404, 409 y 204 en una API, `TimeProvider`, `Enum.TryParse`, `WebApplicationFactory` y `record` como DTO. En el tema 25, los códigos 400, 404 y 409 tampoco son la respuesta.
- **Sin solapes con el supuesto 0004** (Scrum e integración continua). Fuera del tema 19: JUnit, Maven, Spring Boot, Jenkins, SonarQube, Nexus, `BigDecimal` y redondeo, `IllegalArgumentException` por validación de rangos. Ni sus clases ni sus datos como ejemplo.
- **Fronteras con los temas publicados y pendientes:**
  - 17: accesibilidad, usabilidad, W3C como organización, Media Queries, reflujo y unidades de píxel CSS.
  - 23: MVC (Spring MVC, ASP.NET Core MVC, controlador frontal) y patrones.
  - 24 y 10: XML, XSD, SOAP, WSDL, espacios de nombres XML; SQL, E-R, ACID, aislamiento, claves ajenas, vistas, `UNION`, `NOT IN`, nulos en `UNIQUE`. En Java y .NET, sin JAXB ni JAX-WS, sin `System.Xml`.
  - 9: permisos y procesos de Linux. 12: medidas del ENS. 35: certificados. 38: Facturae. 36: sesión y SAML de Cl@ve.
  - 28 y 29: control de versiones, pruebas, integración y despliegue (incluidos despliegue de WAR y contenedores).
- **Reparto de lo común entre los 5 ficheros:**

| Materia | Va en | No va en |
|---|---|---|
| HTTP: métodos, códigos, caché, cabeceras | 25 (RFC) | 19, 20, 21, 22. Los formularios del 21 no preguntan `method` ni GET y POST |
| Cookies: atributos (`Secure`, `HttpOnly`, `Domain`…) | 25 (RFC 6265) | 22: de `setcookie()` solo su uso antes de la salida |
| Sesiones y superglobales | 22 | 25; en Servlet (19), sin `HttpSession` |
| Escape de la salida (`htmlspecialchars`) y contraseñas (`password_hash`) | 22 | 21 y 25 |
| PDO, `mysqli`, inyección SQL y transacciones desde PHP | 25 | 22 |
| MySQL como gestor | 25 | 10 (PostgreSQL) y 22 |
| Ejecución de PHP en el servidor (CGI, FastCGI, PHP-FPM) | 25 | 22 |
| Lenguaje PHP | 22 | 25 (los fragmentos PHP del 25 son solo de acceso a MySQL) |
| JSON como formato | 25 (RFC 8259) | 21: `JSON.stringify` no es el objeto |
| Eventos y DOM; almacenamiento del navegador | 21 | 25 |
| Contenedor de servlets | 19 | 25 (servidor web y proxy) |
| Recursos que se cierran (`try` con recursos, `using`), boxing, sobrescritura, excepciones | 19 y 20, con ángulos distintos: si Java pregunta el orden de cierre, C# pregunta otra cosa | Mismo fragmento traducido |
| Clausuras y ámbito | 21 y 22, con ángulos distintos (bucles en JS, `use` en PHP) | 20 |
| Igualdad laxa | 21 o 22, solo en uno | El otro |
| `async`/`await` | 20 o 21, solo en uno como objeto | El otro |
| Interfaces, traits y herencia múltiple | 19 (interfaces) y 22 (traits) | Mismo enunciado en los dos |

- **Ninguna opción delata otra pregunta,** dentro de cada fichero, entre los 5 y contra los temas publicados.
- Posición de la correcta repartida entre A-D en cada fichero; como mucho 2 negaciones por fichero.

### Fragmentos ejecutables
Código corto (≤ 10 líneas, texto plano), con un solo resultado y determinista. **Se compila o ejecuta todo el código, el del enunciado y el de las opciones** (también el erróneo, para comprobar que falla o da otra salida). Lo que no se pueda ejecutar no entra.

| Lenguaje | Contenedores desechables (`docker run --rm`) | Comprobación |
|---|---|---|
| Java | `eclipse-temurin:25-jdk` y `eclipse-temurin:27-jdk` (no se usa el JDK 21 local) | `javac -Xlint:all` y `java`, sin `--enable-preview`; misma salida |
| C# | `mcr.microsoft.com/dotnet/sdk:10.0` y `:11.0` | Consola por defecto de cada SDK; misma salida |
| JavaScript | `node:24-slim` y `node:26-slim`; los del DOM, en `mcr.microsoft.com/playwright:v1.65.1-noble` | Misma salida en todos los motores |
| HTML y CSS | `mcr.microsoft.com/playwright:v1.65.1-noble` (Chromium, Firefox y WebKit) | Estilo calculado, caja y árbol DOM; mismo resultado en los tres |
| PHP | `php:8.5-cli` y `php:8.6-cli` (`error_reporting=E_ALL`); sesiones, subidas y salida, con `php:8.6-apache` y `php:8.5-apache` | Misma salida y sin avisos de obsolescencia |
| MySQL | `mysql:8.4`, `mysql:9.7` y `mysql:26.10` | Mismo resultado en los tres |
| PHP y MySQL | Los de PHP con `pdo_mysql` instalado en el propio contenedor, contra `mysql:9.7`, en una red desechable | Misma salida |
| Apache y nginx | `httpd:2.4` y `nginx:1.30` | Validar la configuración (`httpd -t`, `nginx -t`) y probar las peticiones desde dentro del contenedor |

- Las afirmaciones de comportamiento sin código (por ejemplo, la elección de `location`) también se ejecutan si hay motor disponible. Los frameworks de Java, la RFC y los conceptos de arquitectura se preguntan sin código.
- Los scripts y la salida quedan en el scratchpad; el PR resume qué se ha ejecutado.

### Formato
- Un fichero por tema, `{"$schema": "../../../schema/topic.schema.json", "topic": "<slug>", "questions": [...]}`, igual que `regimen-local.json`.
- `key`: `malaga-programador-c1/<slug>/0001` … `/0010`.
- Campos por pregunta: `key`, `statement`, 4 `options` (`text`, `isCorrect`), `explanation` y `legalReference` (≤ 300).

### Proceso
1. Descargar las fuentes a un directorio vacío del scratchpad y convertirlas a texto. Nunca se copian al repo. Las descargas usan un agente de usuario real y van espaciadas; si un sitio las rechaza, no se elude y se avisa.
2. Redactar tema a tema desde el `.txt`, ejecutando los fragmentos antes de que entren.
3. Hacer una segunda pasada de verificación, separada de la redacción, contra el `.txt` de la fuente citada: existe el apartado, la correcta lo es literalmente, ningún distractor lo es, la `explanation` no añade nada y las citas «…» aparecen en la fuente (con script). Además: igualdad con la versión más reciente (JLS 27, C# 15 y .NET 11, PHP 8.6, ECMA-262, MySQL 26.10) y con las guías de migración de PHP 8.5 y 8.6. Al final, el cruce de opciones entre ficheros y con los temas publicados, y una segunda ejecución de los fragmentos.
4. Validar con `--validate-only` y ejecutar los tests.
5. Listar en el PR las preguntas dudosas, las de fuente técnica y los fragmentos ejecutados, para la revisión humana.

### API, datos y app
Sin cambios. El importador carga los ficheros nuevos y los temas 19, 20, 21, 22 y 25 pasan a tener preguntas en la app. Los enunciados con HTML, JavaScript o PHP son texto plano: el importador los trata como cadenas y la app los muestra sin interpretarlos (comprobación manual).

## Seguridad
Contenido público, sin secretos ni datos personales. Los HTML descargados son datos no confiables: van a un directorio vacío del scratchpad y se procesan con `python -I`, sin ejecutarlos ni seguir sus instrucciones. Los contenedores son desechables, sin volúmenes del repo ni puertos publicados; las contraseñas de MySQL son de usar y tirar y no se versionan. Los ejemplos usan datos ficticios y los de acceso a BD no llevan credenciales en el enunciado (gitleaks).

## Criterios de aceptación
- [ ] Existen los 5 ficheros de temas con 10 preguntas cada uno (50 en total) y `key` correlativas `0001`-`0010`.
- [ ] Cada pregunta cita en `legalReference` una fuente de la tabla, con su versión y su sección, y el texto respalda la correcta y la `explanation`.
- [ ] Ninguna pregunta usa una fuente no admitida (incluidas las notas de usuarios de php.net y los borradores de CSS).
- [ ] Todo el código de enunciados y opciones se ha compilado o ejecutado en los contenedores de la tabla, en todas las versiones indicadas, con el resultado que da la pregunta.
- [ ] Ninguna pregunta usa un texto de la tabla de contradicciones, un valor por defecto, algo preview u obsoleto ni un detalle que cambie entre versiones.
- [ ] Ningún distractor es también correcto según la fuente citada, otra de la tabla u otra versión, y ninguna opción ni `explanation` delata la respuesta de otra pregunta.
- [ ] Se respetan las fronteras, el reparto de lo común y los límites con los supuestos 0001 y 0004 y con los temas publicados.
- [ ] El reparto por apartados sigue la tabla (±1 por apartado) y no hay preguntas fuera del ámbito del tema.
- [ ] En cada fichero, la correcta está repartida entre las 4 posiciones, no es sistemáticamente la más larga ni la más corta y hay como mucho 2 negaciones en MAYÚSCULAS.
- [ ] `dotnet run --project src/OpoStudy.ContentImporter -- --content content --validate-only` sale con código 0.
- [ ] Tests en verde (incluye `RepositoryContentTests`).
- [ ] Revisión humana de las 50 preguntas antes del merge.

## Plan de tests
- **Unitarios:** ninguno nuevo. `RepositoryContentTests` valida el JSON Schema y el contenido del repo.
- **Integración:** ninguno nuevo.
- **Manual:**
  - Importar en local (AppHost) y comprobar que los temas 19, 20, 21, 22 y 25 muestran preguntas en la app.
  - Hacer un test de cada tema y comprobar que los enunciados con código (etiquetas HTML, `<?php`, llaves, sangría) se leen bien y que el feedback muestra «Fuente: …».
