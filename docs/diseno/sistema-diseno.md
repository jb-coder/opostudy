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

### Contraste (WCAG 2.1 AA)

Mínimo 4,5:1 para texto normal. Ratios calculados con la fórmula de luminancia relativa de WCAG para cada par texto/fondo que se usa en la app:

| Par (texto sobre fondo) | Mínimo | Claro | Oscuro | Resultado |
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

Notas:
- `Outline` (claro 1,57:1 sobre `Background`, oscuro 1,82:1) es **solo decorativo** (borde de tarjeta y separadores): no identifica ningún control, así que WCAG 1.4.11 (3:1) no aplica. Los controles que necesitan borde visible, como `SecondaryButton`, usan `Primary` (6,41:1 claro, 10,17:1 oscuro), que sí supera 3:1. No uses `Outline` como único indicador de un componente interactivo.
- Los botones deshabilitados están exentos de contraste mínimo por WCAG, pero siguen siendo legibles (`OnSurfaceVariant` sobre `Outline`).
- Para recalcular: luminancia relativa `L = 0,2126 R + 0,7152 G + 0,0722 B` (con canales sRGB linealizados) y ratio `(L1 + 0,05) / (L2 + 0,05)`, con `L1` la mayor. Si se cambia un color, hay que repetir la tabla.

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

## Estilos

Implícitos (se aplican solos): `ContentPage` (fondo), `Shell`, `Label` (Body + `OnSurface`), `Button` (principal), `Border` (tarjeta), `Entry` (campo de texto: fondo `Surface`, texto `OnSurface`, marcador `OnSurfaceVariant`, alto mínimo 48), `Picker` (selector: mismos colores y alto que `Entry`, título `OnSurfaceVariant`), `ActivityIndicator`, `RefreshView`, `CollectionView`.

Con clave:

| Clave | Para |
|---|---|
| `PrimaryButton` | Botón principal (base del `Button` implícito) |
| `SecondaryButton` | Botón de contorno (borde `Primary`) |
| `TextButton` | Botón de texto, sin fondo |
| `CardBorder` | Tarjeta (`Surface` + trazo `Outline`, base del `Border` implícito) |
| `DayDotBorder` | Punto de día (círculo de 32 dp): contorno si no se cumplió, relleno con `Success` si sí |
| `ErrorLabel`, `SuccessLabel`, `WarningLabel` | Mensajes de estado |
| `DisplayLabel`, `TitleLabel`, `SubtitleLabel`, `BodyLabel`, `CaptionLabel` | Tipografía |

En modo `Debug`, la pantalla de inicio tiene un botón "Catálogo de estilos" (ruta `styleguide`) para revisar todo en claro y oscuro (`adb shell cmd uimode night yes|no`).

## Accesibilidad

- Todo control táctil mide al menos 48x48 dp (`MinimumHeightRequest`/`MinimumWidthRequest` en los estilos de botón).
- `SemanticProperties.Description` en controles sin texto visible e imágenes; `SemanticProperties.HeadingLevel` en los títulos de página (`Level1`) y de sección (`Level2`).
- El estado (cargando, error, éxito) nunca se comunica solo con color: siempre hay texto.
- Contraste AA en todos los pares texto/fondo (tabla anterior), en claro y oscuro.

## Patrones de pantalla

1. **Vista + ViewModel.** La página XAML hereda de `ViewModelPage`, recibe el ViewModel por constructor y declara `x:DataType` en la raíz y en cada `DataTemplate` (bindings compilados). El code-behind solo llama al constructor base: sin lógica.
2. **ViewModel sin MAUI.** Vive en `OpoStudy.Mobile.Core`, hereda de `ViewModelBase` (`IsBusy`, `ErrorMessage`, `HasError`), usa `[ObservableProperty]` (propiedades parciales) y `[RelayCommand]`. Nunca `async void`. Se prueba con xUnit sin emulador.
3. **Carga de datos.** `OnAppearingAsync` carga la primera vez; un `RefreshCommand` recarga y se enlaza a `RefreshView`. Estados: cargando (indicador + texto), éxito, error (mensaje en `ErrorLabel` + botón "Reintentar").
4. **Tarjeta.** `Border` con `CardBorder` y un `VerticalStackLayout` con `Spacing="{StaticResource SpaceM}"`; título con `SubtitleLabel`.
5. **Mensajes de error.** Siempre en español y sin detalles internos: salen de `ApiErrorMessages.From(exception)`.
6. **Navegación.** Rutas en `Routes` (Core) y navegación mediante `INavigationService`; los ViewModels no conocen `Shell`.
7. **Pantallas nuevas.** Registra ViewModel y página como `Transient` en `MauiProgram` y la ruta en `AppShell`.
