# 0032 — Sistema de diseño accesible y checklist visual

- **Issue:** #90
- **Milestone:** v0.5.0
- **Estado:** implementada

## Objetivo
Las pantallas nuevas de v0.5.0 deben nacer accesibles y con un criterio visual común. Se fijan por escrito los requisitos de accesibilidad (WCAG 2.2 AA) y un checklist de revisión visual. Se añaden dos herramientas baratas para comprobarlos: el cálculo de contraste de la paleta y una auditoría del volcado de `uiautomator`. El rediseño completo pasa a v1.0.0 y la publicación en tienda, a v1.1.0.

## Alcance
**Incluye:**
- `docs/diseno/sistema-diseno.md`: sección de accesibilidad ampliada (WCAG 2.2 AA) y checklist de revisión visual.
- `tools/a11y/ContrastCheck.cs`: calcula el contraste de la paleta de `Colors.xaml` en claro y oscuro.
- `tools/a11y/AuditUiDump.cs`: audita un volcado de `uiautomator` y detecta controles sin etiqueta y zonas táctiles menores de 48 dp.
- Servicio `IScreenReaderAnnouncer` para los avisos dinámicos (Core + implementación MAUI).
- La auditoría se pasa a la pantalla de inicio (`HomePage`) y se corrige lo que salga.
- ADR 0001: nuevo roadmap (v1.0.0 = rediseño accesible; v1.1.0 = publicación en tienda).
- Skill `maui-ui`: añadir los pasos de auditoría y contraste a la revisión visual.

**No incluye:**
- Auditar ni corregir el resto de pantallas: se hace en el rediseño de v1.0.0 o cuando se toquen.
- Ejecutar los scripts en CI.
- Un servicio de "movimiento reducido": hoy no hay animaciones. Solo se documenta la regla.
- Usar el anunciador en las pantallas existentes. Se usará en las pantallas nuevas.

## Diseño
### Dominio
Nada nuevo. No hace falta tocar el glosario.

### API
Sin cambios.

### Datos
Sin cambios.

### App

#### 1. `docs/diseno/sistema-diseno.md`
- Cambia el título "Contraste (WCAG 2.1 AA)" por "Contraste (WCAG 2.2 AA)". Separa la tabla en dos:
  - **Texto (4,5:1):** los pares actuales.
  - **Elementos gráficos y bordes de componentes (3:1):**
    - `Primary` sobre `Background` y sobre `Surface`: borde de `SecondaryButton`, foco y barra de progreso.
    - `Success` sobre `Background` y sobre `Surface`: `DayDotBorder` relleno y contorno, y estados del calendario.
    - `Error` y `Warning` sobre `Background` y sobre `Surface`, si se usan como trazo.
  - Las dos tablas se generan con `ContrastCheck.cs` y se pegan tal cual.
- Revisa los usos de `Outline` fuera de las tarjetas y clasifícalos: decorativo, o información que hay que ver (3:1):
  - `Styles.xaml:40`: fondo del botón deshabilitado. Exento.
  - `StatsPage.xaml:285`: `BoxView` de la gráfica.
  - Si `Outline` transmite información (por ejemplo, la pista de una barra que indica el 0 %), cambia ese uso a un token que pase 3:1, o añade una marca de texto. No cambies `Outline` en sí: sigue siendo decorativo.
- Sustituye la sección "Accesibilidad" por estos requisitos, cada uno con cómo se cumple en MAUI y cómo se comprueba:

| Requisito | Criterio WCAG | Cómo se cumple | Cómo se comprueba |
|---|---|---|---|
| Contraste de texto ≥ 4,5:1 | 1.4.3 | Solo pares de la tabla de texto | `ContrastCheck.cs` |
| Contraste de elementos gráficos y bordes de controles ≥ 3:1 | 1.4.11 | Tabla de elementos gráficos; `Outline` solo decorativo | `ContrastCheck.cs` |
| Zona táctil ≥ 48x48 dp | 2.5.8 (24 px; el proyecto exige 48 dp, como Android) | `MinTouchTarget` en estilos; iconos dentro de un contenedor de 48 dp | `AuditUiDump.cs` |
| Texto ampliable al 200 % sin cortes ni solapes | 1.4.4 | `FontAutoScalingEnabled` activo; sin alturas fijas en contenedores de texto; `LineBreakMode="WordWrap"`; pantallas dentro de un `ScrollView` | `adb shell settings put system font_scale 2.0` y captura |
| Movimiento reducido | 2.3.3 | Si se añade una animación, no se reproduce cuando Android tiene las animaciones desactivadas (`ValueAnimator.AreAnimatorsEnabled()` en Android); la información nunca depende de la animación | Activar "Quitar animaciones" y repetir el flujo |
| El estado nunca se comunica solo con color | 1.4.1 | Texto o marca (✓, ✗, •) junto al color | Captura en escala de grises (`adb shell settings put secure accessibility_display_daltonizer_enabled 1` y `accessibility_display_daltonizer 0`) |
| Controles con nombre accesible | 4.1.2 | Texto visible o `SemanticProperties.Description`; en controles con texto, el nombre empieza por ese texto | `AuditUiDump.cs` y TalkBack |
| Encabezados | 1.3.1 | `SemanticProperties.HeadingLevel`: `Level1` en el título de página y `Level2` en las secciones | TalkBack, navegación por encabezados |
| Avisos dinámicos | 4.1.3 | Los resultados y errores que aparecen sin cambiar de pantalla (corrección de una respuesta, error al enviar, tiempo agotado) se anuncian con `IScreenReaderAnnouncer` | TalkBack |
| Orden de foco lógico | 2.4.3 | El orden del XAML sigue el orden visual; no se usa `TabIndex` salvo justificación | TalkBack, deslizar por la pantalla |

- Añade la sección "Checklist de revisión visual", que se repasa en cada pantalla nueva o modificada (el PR la incluye marcada):
  - **Jerarquía:** un solo `DisplayLabel` o `TitleLabel` por pantalla; una sola acción principal (`PrimaryButton`) visible a la vez; lo secundario con `SecondaryButton` o `TextButton`; la información más importante, arriba.
  - **Espaciado:** solo tokens de `Spacing.xaml`, sin números sueltos; `SpaceM` dentro de tarjetas, `SpaceL` entre tarjetas.
  - **Tipografía:** solo los estilos de la tabla; sin `FontSize` sueltos; como máximo tres tamaños por pantalla.
  - **Color:** solo tokens; sin hex en las vistas; el color de estado siempre acompañado de texto o marca.
  - **Iconografía:** iconos o emojis siempre con una etiqueta visible o `SemanticProperties.Description`; un mismo icono significa siempre lo mismo (✓ correcto o cumplido, ✗ incorrecto, 🛡️ protector, 🔥 racha); los decorativos, ocultos al lector (`AutomationProperties.IsInAccessibleTree="False"`).
  - **Movimiento:** sin animaciones decorativas; si hay transición, menos de 300 ms y respetando "Quitar animaciones".
  - **Estados:** cada pantalla con datos define cargando, vacío, error y éxito.
  - **Estados vacíos:** el texto dice qué falta y qué hacer, y hay un botón a la acción (por ejemplo, "Aún no has hecho ningún test" + "Empezar un test"); nunca una lista en blanco.
  - **Claro, oscuro y 200 %:** capturas en los tres casos, sin cortes ni solapes.
  - **Auditoría:** `AuditUiDump.cs` sin hallazgos (o cada hallazgo, justificado en el PR).

#### 2. `tools/a11y/ContrastCheck.cs` (app de un solo fichero de .NET 10)
- Se ejecuta con `dotnet run tools/a11y/ContrastCheck.cs [-- <ruta a Colors.xaml>]`. Por defecto usa `src/OpoStudy.Mobile/Resources/Styles/Colors.xaml`.
- Lee los `Color` con `x:Key` (`<Token>Light` y `<Token>Dark`) usando `XDocument`.
- La lista de pares está en el propio script:
  - `(foreground, background, minimum, kind)`, con `kind` = `Text` o `Graphic`.
  - Los pares son los de las dos tablas del punto 1.
  - Para cada par calcula claro y oscuro con la fórmula de luminancia relativa de WCAG: canal linealizado `c <= 0.04045 ? c/12.92 : ((c+0.055)/1.055)^2.4`.
- Salida:
  - Las dos tablas en Markdown, con el mismo formato que el documento: ratio con 2 decimales y coma decimal, y `OK` o `FALLA`.
  - Código de salida `1` si algún par falla y `0` si todos pasan.
  - Un token que no existe es un error con mensaje claro y código `2`.
- Sin dependencias NuGet. Código en inglés y sin warnings: hereda `TreatWarningsAsErrors` de `Directory.Build.props`.

#### 3. `tools/a11y/AuditUiDump.cs` (app de un solo fichero de .NET 10)
- Uso:
  ```bash
  adb shell uiautomator dump /sdcard/ui.xml && adb pull /sdcard/ui.xml "$SCRATCH/ui.xml"
  dotnet run tools/a11y/AuditUiDump.cs -- "$SCRATCH/ui.xml" --density <dpi> [--package <applicationId>]
  ```
  - `<dpi>` sale de `adb shell wm density`.
  - `--package` vale por defecto el `ApplicationId` del csproj de Mobile. Los nodos de otros paquetes, como `com.android.systemui`, se ignoran.
- Reglas, por cada `node` del volcado:
  - **`unlabeled`:** el nodo tiene `clickable`, `long-clickable` o `checkable` a `true`, pero ni él ni ninguno de sus descendientes tiene `text` o `content-desc` no vacíos.
  - **`small-target`:** el nodo tiene `clickable`, `long-clickable` o `checkable` a `true`, y su ancho o su alto en dp (`px * 160 / density`) es menor de 48.
    - Se toleran 0,5 dp por redondeo.
    - Los nodos cortados por el borde de la pantalla o de un scroll también salen. El PR justifica cada excepción.
- Salida:
  - Una línea por hallazgo: `regla`, `class`, `resource-id`, `text` o `content-desc`, `bounds` y tamaño en dp.
  - Al final, un resumen.
  - Código de salida `1` si hay hallazgos, `0` si no hay y `2` si el fichero no es válido.
- La lógica de reglas va en métodos estáticos puros (el XML entra como `XElement`) para que sea fácil de leer. No se añade un proyecto de tests para los scripts: se validan a mano con el volcado real (ver plan de tests).

#### 4. `IScreenReaderAnnouncer`
- **Core** (`OpoStudy.Mobile.Core`, junto a `INavigationService`): `public interface IScreenReaderAnnouncer { void Announce(string message); }`.
- **MAUI** (`OpoStudy.Mobile`): `ScreenReaderAnnouncer` llama a `SemanticScreenReader.Default.Announce(message)` y no hace nada si `message` está vacío. Se registra como `Singleton` en `MauiProgram`.
- Se documenta en "Patrones de pantalla" (punto nuevo): los ViewModels reciben `IScreenReaderAnnouncer` por constructor y anuncian los resultados y errores que aparecen sin navegar. En los tests se sustituye por un fake.

#### 5. Pantalla de inicio
- Con el AppHost y el emulador levantados (skill `maui-ui`), entra con un usuario de prueba en `HomePage`:
  - Volcado de `uiautomator` y `AuditUiDump.cs`.
  - Capturas en claro, en oscuro y con `font_scale 2.0`.
- Corrige en `HomePage.xaml` (y en estilos, si el problema es del estilo) todo hallazgo de la auditoría y del checklist.
- Si la corrección es de un estilo compartido, revisa también el catálogo (`StyleGuidePage`).
- Con la ayuda del checklist, anota en el PR los problemas que veas en otras pantallas, sin corregirlos.

#### 6. ADR 0001
- En "Flujo de trabajo", cambia la línea "`v1.0.0` es la primera versión estable, publicable en tienda" por:
  - `v1.0.0` es la primera versión estable: el rediseño accesible de la app (WCAG 2.2 AA en todas las pantallas, según `docs/diseno/sistema-diseno.md`).
  - `v1.1.0` es la publicación en tienda (Google Play).
- Añade un apartado breve "Roadmap" con los milestones existentes: v0.5.0, v0.6.0, v0.7.0, v1.0.0, v1.1.0 y v1.2.0. El título y la descripción de cada uno salen de `gh api repos/:owner/:repo/milestones`.
- Busca `tienda` y `v1.0.0` en `README.md` y `docs/` y alinea las menciones al nuevo roadmap.

#### 7. Skill `maui-ui`
- En el paso de revisión visual, añade el volcado y `AuditUiDump.cs`, la captura con `font_scale 2.0` y el repaso del checklist del sistema de diseño.

## Seguridad
- Sin superficie nueva: los scripts son herramientas locales que leen ficheros locales.
- Los volcados y las capturas pueden contener datos del usuario de prueba. Se guardan en el scratchpad, **nunca en el repo**.
- No se versiona ningún `ui.xml` ni ninguna captura.

## Criterios de aceptación
- [x] `sistema-diseno.md` documenta los requisitos WCAG 2.2 AA (tabla del punto 1), el checklist visual y el patrón `IScreenReaderAnnouncer`.
- [x] `dotnet run tools/a11y/ContrastCheck.cs` termina con código 0. Sus tablas, en claro y oscuro, están pegadas en el documento.
- [x] Los usos de `Outline` fuera de las tarjetas están clasificados. Ninguno transmite información con menos de 3:1.
- [x] `AuditUiDump.cs` sobre el volcado de `HomePage` termina con código 0, o cada hallazgo restante está justificado en el PR.
- [x] Las capturas de `HomePage` en claro, en oscuro y con `font_scale 2.0` no muestran cortes ni solapes.
- [x] `IScreenReaderAnnouncer` está en Core, implementado y registrado en MAUI.
- [x] El ADR 0001 y el README reflejan el roadmap: v1.0.0 es el rediseño accesible y v1.1.0, la publicación en tienda.
- [x] La skill `maui-ui` incluye la auditoría, el 200 % y el checklist (cambio local: la skill no se versiona).
- [x] `dotnet build OpoStudy.Backend.slnf` y `dotnet build src/OpoStudy.Mobile -f net10.0-android` compilan sin warnings, y los tests siguen en verde.

## Plan de tests
- **Unitarios:**
  - `ScreenReaderAnnouncer` es una envoltura de una línea sobre la API de MAUI, así que no lleva test propio.
  - Ningún ViewModel cambia en esta issue.
- **Integración:** no aplica.
- **Manual:**
  - `ContrastCheck.cs`: los ratios coinciden con la tabla actual del documento (p. ej., `OnPrimary`/`Primary` en claro, 6,81:1).
  - `ContrastCheck.cs`: si se cambia temporalmente `PrimaryLight` a `#9CC2FF`, falla con código 1. Después se restaura.
  - `AuditUiDump.cs`: un volcado de `HomePage` antes de corregir, y otro después.
  - `AuditUiDump.cs`: si se quita temporalmente la `SemanticProperties.Description` de un control sin texto, sale un hallazgo `unlabeled`.
  - Capturas de `HomePage` en claro, en oscuro y al 200 %, adjuntas en la conversación, no en el repo.
  - TalkBack en `HomePage`: el título se anuncia como encabezado y todos los botones tienen nombre.
