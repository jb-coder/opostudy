# Sistema de diseño de OpoStudy

Referencia de los tokens y estilos de la app (`src/OpoStudy.Mobile/Resources/Styles/`). Spec: `docs/specs/0007-base-app-diseno.md`.

La app sigue el tema del sistema (claro/oscuro). No hay selector manual de tema. Los nombres de recurso van en inglés.

## Colores

Cada token es un par claro/oscuro definido en `Colors.xaml` como `<Token>Light` y `<Token>Dark`. Los estilos los combinan con `AppThemeBinding`:

```xml
<Setter Property="TextColor"
        Value="{AppThemeBinding Light={StaticResource OnSurfaceLight}, Dark={StaticResource OnSurfaceDark}}" />
```

| Token | Claro | Oscuro | Uso |
|---|---|---|---|
| `Primary` | `#1E5AA8` | `#9CC2FF` | Botones principales, enlaces, foco |
| `OnPrimary` | `#FFFFFF` | `#0B2545` | Texto sobre `Primary` |
| `Background` | `#F7F8FA` | `#121417` | Fondo de página |
| `Surface` | `#FFFFFF` | `#1D2026` | Tarjetas, `Border` |
| `OnSurface` | `#1B1D21` | `#E6E8EC` | Texto principal |
| `OnSurfaceVariant` | `#4A505A` | `#B4BAC4` | Texto secundario |
| `Outline` | `#C4C9D1` | `#3C424C` | Bordes y separadores (decorativos) |
| `Success` | `#1B7A3A` | `#7FD69B` | Respuesta correcta |
| `Error` | `#B3261E` | `#F2B8B5` | Respuesta incorrecta, errores |
| `Warning` | `#8A5300` | `#FFC870` | Avisos |

### Contraste (WCAG 2.2 AA)

Ratios calculados con `dotnet run tools/a11y/ContrastCheck.cs` (fórmula de luminancia relativa de WCAG sobre `Colors.xaml`). Las tablas se pegan tal cual; si se cambia un color, se repite el script y se actualizan. El script termina con código 1 si algún par falla.

**Texto (4,5:1)**, criterio 1.4.3:

| Par (primer plano sobre fondo) | Mínimo | Claro | Oscuro | Resultado |
|---|---|---|---|---|
| `OnPrimary` sobre `Primary` | 4,5:1 | 6,81:1 | 8,48:1 | OK |
| `OnSurface` sobre `Background` | 4,5:1 | 15,88:1 | 15,04:1 | OK |
| `OnSurface` sobre `Surface` | 4,5:1 | 16,88:1 | 13,30:1 | OK |
| `OnSurfaceVariant` sobre `Background` | 4,5:1 | 7,64:1 | 9,46:1 | OK |
| `OnSurfaceVariant` sobre `Surface` | 4,5:1 | 8,12:1 | 8,36:1 | OK |
| `Primary` sobre `Background` | 4,5:1 | 6,41:1 | 10,17:1 | OK |
| `Primary` sobre `Surface` | 4,5:1 | 6,81:1 | 8,99:1 | OK |
| `Success` sobre `Background` | 4,5:1 | 5,08:1 | 10,54:1 | OK |
| `Success` sobre `Surface` | 4,5:1 | 5,40:1 | 9,32:1 | OK |
| `Error` sobre `Background` | 4,5:1 | 6,15:1 | 10,81:1 | OK |
| `Error` sobre `Surface` | 4,5:1 | 6,54:1 | 9,56:1 | OK |
| `Warning` sobre `Background` | 4,5:1 | 5,96:1 | 12,08:1 | OK |
| `Warning` sobre `Surface` | 4,5:1 | 6,33:1 | 10,68:1 | OK |

**Elementos gráficos y bordes de componentes (3:1)**, criterio 1.4.11:

| Par (primer plano sobre fondo) | Mínimo | Claro | Oscuro | Resultado |
|---|---|---|---|---|
| `Primary` sobre `Background` | 3:1 | 6,41:1 | 10,17:1 | OK |
| `Primary` sobre `Surface` | 3:1 | 6,81:1 | 8,99:1 | OK |
| `Success` sobre `Background` | 3:1 | 5,08:1 | 10,54:1 | OK |
| `Success` sobre `Surface` | 3:1 | 5,40:1 | 9,32:1 | OK |
| `Error` sobre `Background` | 3:1 | 6,15:1 | 10,81:1 | OK |
| `Error` sobre `Surface` | 3:1 | 6,54:1 | 9,56:1 | OK |
| `Warning` sobre `Background` | 3:1 | 5,96:1 | 12,08:1 | OK |
| `Warning` sobre `Surface` | 3:1 | 6,33:1 | 10,68:1 | OK |

Usos de `Outline` fuera de las tarjetas, clasificados:

| Uso | Clasificación |
|---|---|
| `Styles.xaml`: fondo del botón deshabilitado | Exento (WCAG no exige contraste a controles deshabilitados); el texto sigue legible |
| `Styles.xaml`: trazo de `CardBorder` | Decorativo: la tarjeta no es un control |
| `ProgressBarTrack.cs`: pista de `ProgressBar` | Decorativo: el valor siempre va en texto junto a la barra y en `SemanticProperties.Description` |
| `StatsPage.xaml`: `BoxView` de la gráfica de notas | Decorativo: el porcentaje está en texto sobre la barra y en `SemanticProperties.Description`; la barra de relleno usa `Primary` (3:1) |
| `StyleGuidePage.xaml`: muestra del color en el catálogo | Decorativo (solo muestra el token) |

Ninguno transmite información únicamente con `Outline`, así que no hay que cambiar ningún uso. `Outline` sigue siendo decorativo: no lo uses como único indicador de un componente interactivo (para eso, `Primary`, que supera 3:1).

Notas:
- Los controles que necesitan borde visible, como `SecondaryButton`, usan `Primary`.
- Para recalcular a mano: luminancia relativa `L = 0,2126 R + 0,7152 G + 0,0722 B` (con canales sRGB linealizados: `c <= 0,04045 ? c/12,92 : ((c+0,055)/1,055)^2,4`) y ratio `(L1 + 0,05) / (L2 + 0,05)`, con `L1` la mayor.

## Tipografía

Fuente OpenSans (Regular y Semibold). El escalado de fuente del sistema está activo: no desactives `FontAutoScalingEnabled`.

| Estilo | Tamaño | Peso | Color | Uso |
|---|---|---|---|---|
| `DisplayLabel` | 28 | Semibold | `OnSurface` | Título de la app / pantalla principal |
| `TitleLabel` | 22 | Semibold | `OnSurface` | Títulos de sección |
| `SubtitleLabel` | 18 | Semibold | `OnSurface` | Títulos de tarjeta |
| `BodyLabel` (implícito en `Label`) | 16 | Regular | `OnSurface` | Texto general |
| `CaptionLabel` | 13 | Regular | `OnSurfaceVariant` | Notas, texto secundario |

Los tamaños son recursos `x:Double` (`FontSizeDisplay`, `FontSizeTitle`, `FontSizeSubtitle`, `FontSizeBody`, `FontSizeCaption`).

## Espaciado

Escala de 4 (`Spacing.xaml`):

| Recurso | Valor |
|---|---|
| `SpaceXs` | 4 |
| `SpaceS` | 8 |
| `SpaceM` | 12 |
| `SpaceL` | 16 |
| `SpaceXl` | 24 |
| `SpaceXxl` | 32 |
| `PagePadding` (`Thickness`) | 16 |
| `CardPadding` (`Thickness`) | 16 |
| `CornerRadius` | 12 |
| `MinTouchTarget` | 48 |
| `DimmedOpacity` | 0,6 (elementos atenuados o no disponibles; nunca como único indicador de estado) |
| `AnswerEditorMinHeight` | 160 (alto mínimo del editor de respuestas de los supuestos) |
| `ListHeaderPadding`, `ListFooterPadding` (`Thickness`) | 0,0,0,12 y 0,12,0,0 (separación de la cabecera y del pie de una lista con `CollectionView`) |
| `CalendarCornerRadius` (`CornerRadius`) | 8 (esquinas de cada día del calendario) |
| `CalendarCellGap` (`Thickness`) | 2 (margen de cada día del calendario; 4 dp entre celdas) |
| `IndexCellMargin` (`Thickness`) | 4 (margen de cada número del índice de preguntas del simulacro; celdas de 48 dp) |
| `CalendarTodayStroke` | 2 (grosor del borde del día de hoy en el calendario) |
| `ChartBarAreaHeight` | 96 (alto de las barras de la gráfica de notas de supuestos) |
| `ChartBarWidth` | 32 (ancho de una barra de la gráfica) |
| `ChartBarSlotWidth` | 56 (ancho de la columna de una barra: porcentaje, barra y fecha) |

## Estilos

Implícitos (se aplican solos): `ContentPage` (fondo), `Shell`, `Label` (Body + `OnSurface`), `Button` (principal), `Border` (tarjeta), `Entry` (campo de texto: fondo `Surface`, texto `OnSurface`, marcador `OnSurfaceVariant`, alto mínimo 48), `ProgressBar` (progreso en `Primary`; la pista `Outline` se aplica en Android con `Platforms/Android/ProgressBarTrack.cs`, porque MAUI no tiene propiedad para ella), `Picker` (selector: mismos colores y alto que `Entry`, título `OnSurfaceVariant`), `ActivityIndicator`, `RefreshView`, `CollectionView`.

Con clave:

| Clave | Para |
|---|---|
| `PrimaryButton` | Botón principal (base del `Button` implícito): alto mínimo real de 48 dp (en Android `ButtonInsets` quita los 6 dp de margen de MaterialButton) y texto con `WordWrap`, para que crezca al ampliar la fuente |
| `SecondaryButton` | Botón de contorno (borde `Primary`) |
| `TextButton` | Botón de texto, sin fondo; deshabilitado: sin fondo, texto `OnSurfaceVariant` y `DimmedOpacity` |
| `CardBorder` | Tarjeta (`Surface` + trazo `Outline`, base del `Border` implícito) |
| `DayDotBorder` | Punto de día (círculo de 32 dp): contorno si no se cumplió, relleno con `Success` si sí |
| `CalendarCell`, `CalendarWeekdayCell` | `Grid` de una celda del calendario mensual (`FlexLayout.Basis` de un séptimo del ancho; 48 dp de alto en `CalendarCell`) |
| `CalendarDayBorder` | Día del calendario: borde redondeado de 8 dp, sin relleno; los triggers pintan el estado (objetivo cumplido, actividad, hoy), siempre junto a una marca de texto (✓, •, 🛡️) |
| `ErrorLabel`, `SuccessLabel`, `WarningLabel` | Mensajes de estado (`WarningLabel` también para avisos de límite, borrador o conexión: siempre acompañado de texto) |
| `AnswerEditorStyle` | `Editor` multilínea de respuesta (supuestos): fondo `Surface`, texto `OnSurface`, alto mínimo `AnswerEditorMinHeight` (160 dp, ~6 líneas); se envuelve en un `Border` y crece con `AutoSize="TextChanges"` |
| `DisplayLabel`, `TitleLabel`, `SubtitleLabel`, `BodyLabel`, `CaptionLabel` | Tipografía |

En modo `Debug`, la pantalla de inicio tiene un botón "Catálogo de estilos" (ruta `styleguide`) para revisar todo en claro y oscuro (`adb shell cmd uimode night yes|no`).

## Accesibilidad

Objetivo: WCAG 2.2 AA en todas las pantallas. Cada requisito indica cómo se cumple en MAUI y cómo se comprueba.

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

### Auditoría del volcado de `uiautomator`

```bash
adb shell uiautomator dump /sdcard/ui.xml && adb pull /sdcard/ui.xml "$SCRATCH/ui.xml"
dotnet run tools/a11y/AuditUiDump.cs -- "$SCRATCH/ui.xml" --density <dpi> [--package <applicationId>]
```

- `<dpi>` sale de `adb shell wm density`. `--package` vale por defecto el `ApplicationId` de la app; los nodos de otros paquetes (p. ej. `com.android.systemui`) se ignoran.
- Reglas: `unlabeled` (control interactivo sin `text` ni `content-desc` en él ni en sus descendientes) y `small-target` (ancho o alto menor de 48 dp, con 0,5 dp de tolerancia). Los nodos cortados por el borde de la pantalla o de un scroll también salen; el PR justifica cada excepción.
- Código de salida: `0` sin hallazgos, `1` con hallazgos, `2` si el fichero no es válido.
- Los volcados y las capturas pueden contener datos del usuario de prueba: se guardan fuera del repo.

## Checklist de revisión visual

Se repasa en cada pantalla nueva o modificada; el PR la incluye marcada.

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

## Patrones de pantalla

1. **Vista + ViewModel.** La página XAML hereda de `ViewModelPage`, recibe el ViewModel por constructor y declara `x:DataType` en la raíz y en cada `DataTemplate` (bindings compilados). El code-behind solo llama al constructor base: sin lógica.
2. **ViewModel sin MAUI.** Vive en `OpoStudy.Mobile.Core`, hereda de `ViewModelBase` (`IsBusy`, `ErrorMessage`, `HasError`), usa `[ObservableProperty]` (propiedades parciales) y `[RelayCommand]`. Nunca `async void`. Se prueba con xUnit sin emulador.
3. **Carga de datos.** `OnAppearingAsync` carga la primera vez; un `RefreshCommand` recarga y se enlaza a `RefreshView`. Estados: cargando (indicador + texto), éxito, error (mensaje en `ErrorLabel` + botón "Reintentar").
4. **Tarjeta.** `Border` con `CardBorder` y un `VerticalStackLayout` con `Spacing="{StaticResource SpaceM}"`; título con `SubtitleLabel`.
5. **Mensajes de error.** Siempre en español y sin detalles internos: salen de `ApiErrorMessages.From(exception)`.
6. **Navegación.** Rutas en `Routes` (Core) y navegación mediante `INavigationService`; los ViewModels no conocen `Shell`.
7. **Pantallas nuevas.** Registra ViewModel y página como `Transient` en `MauiProgram` y la ruta en `AppShell`.
8. **Avisos al lector de pantalla.** Los ViewModels reciben `IScreenReaderAnnouncer` (Core) por constructor y anuncian los resultados y errores que aparecen sin navegar (corrección de una respuesta, error al enviar, tiempo agotado). La implementación MAUI llama a `SemanticScreenReader.Default.Announce` y no hace nada con un mensaje vacío; está registrada como `Singleton`. En los tests se sustituye por un fake.
