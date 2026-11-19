# 0007 — Base de la app y sistema de diseño

- **Issue:** #17
- **Milestone:** v0.1.0-alpha
- **Estado:** implementada

> La issue apuntaba a `0006-base-app-diseno.md`, pero el número 0006 ya lo usa la spec de los endpoints de tests.

## Objetivo
Sustituir la plantilla de MAUI por la arquitectura sobre la que se construirán el login (#18) y la pantalla de test (#19): MVVM con ViewModels que se prueban sin emulador, Shell, inyección de dependencias, cliente Refit sobre `OpoStudy.Contracts` y un sistema de diseño documentado, accesible y con modo oscuro.

## Alcance
**Incluye:**
- Proyecto nuevo `OpoStudy.Mobile.Core` (`net10.0`, sin MAUI) con ViewModels, abstracciones de plataforma y el cliente de la API. Tests en `OpoStudy.Mobile.Core.Tests`.
- `OpoStudy.Mobile` queda como capa fina: vistas XAML, implementaciones de las abstracciones y registro en DI.
- URL de la API configurable en compilación (emulador o dispositivo USB con `adb reverse`).
- Pantalla de inicio que comprueba la conexión con la API (`GET /api/v1/version`, anónimo) con estados de carga, éxito y error con reintento.
- Catálogo de estilos (solo en `Debug`) para revisar el sistema de diseño en claro y oscuro.
- Sistema de diseño (colores, tipografía, espaciados, estilos implícitos, modo oscuro, accesibilidad) documentado en `docs/diseno/sistema-diseno.md`.
- Retirar `MainPage`, `dotnet_bot.png` y los colores morados de la plantilla (icono y splash incluidos).

**No incluye:**
- Login, token de Firebase y `DelegatingHandler` de autenticación (#18).
- Pantallas de oposiciones, temas y test (#19).
- Caché offline con SQLite y notificaciones (más adelante).
- Selector manual de tema: la app sigue el tema del sistema.
- URL de producción por Tailscale y HTTPS (#21).

## Diseño
### Dominio
Sin cambios. Sin términos nuevos en el glosario.

### API
Sin cambios. La app solo usa `GET /api/v1/version` (anónimo) → `VersionResponse`.

### Datos
Sin cambios.

### App
#### Proyectos
| Proyecto | TFM | Contenido | Dependencias |
|---|---|---|---|
| `src/OpoStudy.Mobile.Core` | `net10.0` | ViewModels, `Routes`, interfaces de plataforma, `IOpoStudyApi` (Refit), `ApiOptions`, `ApiErrorMessages` | `OpoStudy.Contracts`, `CommunityToolkit.Mvvm`, `Refit` |
| `src/OpoStudy.Mobile` | `net10.0-android` | Vistas XAML, `Services/` (implementaciones MAUI), `MauiProgram`, recursos | `OpoStudy.Mobile.Core`, `Refit.HttpClientFactory` |
| `tests/OpoStudy.Mobile.Core.Tests` | `net10.0` | Tests de ViewModels y helpers | xUnit v3, Shouldly, NSubstitute |

- `OpoStudy.Mobile.Core` y sus tests entran en `OpoStudy.slnx` y en `OpoStudy.Backend.slnf`, así el job `backend` de CI los compila y ejecuta sin workload de MAUI.
- Regla de arquitectura nueva (NetArchTest, en `OpoStudy.Architecture.Tests`): `OpoStudy.Mobile.Core` no depende de `Microsoft.Maui*`.
- Versiones en `Directory.Packages.props` (CPM), grupo nuevo `App`. NSubstitute va al grupo `Tests` (ya figura en el ADR).

#### Estructura de `OpoStudy.Mobile.Core`
```
Api/IOpoStudyApi.cs          // Refit: [Get("/api/v1/version")] Task<VersionResponse> GetVersionAsync(CancellationToken)
Api/ApiOptions.cs            // record con BaseAddress (Uri)
Api/ApiErrorMessages.cs      // Exception → mensaje en español para la UI
Navigation/Routes.cs         // constantes de ruta: "home", "styleguide"
Navigation/INavigationService.cs  // Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
ViewModels/ViewModelBase.cs  // ObservableObject + IsBusy + ErrorMessage + HasError; implementa IPageLifecycleAware
ViewModels/IPageLifecycleAware.cs // Task OnAppearingAsync()
ViewModels/HomeViewModel.cs
```
- CommunityToolkit.Mvvm con **propiedades parciales** (`[ObservableProperty] public partial bool IsBusy { get; set; }`) y `[RelayCommand]`. Nada de `async void`.
- `ApiErrorMessages.From(Exception)`:
  - `ApiException` con ProblemDetails (`Refit.ValidationApiException` / contenido `application/problem+json`) → su `detail`, o su `title` si no hay `detail`.
  - Otro `ApiException` → "El servidor ha respondido con un error. Inténtalo de nuevo más tarde."
  - `HttpRequestException` → "No se puede conectar con el servidor. Revisa tu conexión."
  - `TaskCanceledException`/`TimeoutException` → "El servidor tarda demasiado en responder. Inténtalo de nuevo."
  - Cualquier otra → "Ha ocurrido un error inesperado."
- `HomeViewModel(IOpoStudyApi api)`:
  - Propiedades: `ApiVersion` (`string?`), `IsConnected` (`bool`), más las de la base.
  - `OnAppearingAsync()` carga solo la primera vez (si ya hay versión, no repite la llamada).
  - `RefreshCommand` (`[RelayCommand]` asíncrono): `IsBusy = true`, limpia el error, llama a `GetVersionAsync`, rellena `ApiVersion` e `IsConnected`; si falla, `IsConnected = false` y `ErrorMessage = ApiErrorMessages.From(ex)`; `IsBusy = false` en `finally`. No se puede lanzar mientras `IsBusy` (`CanExecute`).
  - No conoce MAUI: ni `Shell`, ni `MainThread`, ni `Preferences`.

#### Capa MAUI (`OpoStudy.Mobile`)
- `Views/HomePage.xaml`, `Views/StyleGuidePage.xaml` con `x:DataType` en la raíz y en cada `DataTemplate`.
- `Views/ViewModelPage.cs`: `ContentPage` base que recibe el ViewModel por constructor, lo asigna a `BindingContext` y en `OnAppearing` llama a `IPageLifecycleAware.OnAppearingAsync()`. El code-behind de cada página solo llama al constructor base: **sin lógica**.
- `Services/ShellNavigationService.cs` implementa `INavigationService` con `Shell.Current.GoToAsync`.
- `AppShell.xaml`: `ShellContent` de `home` (sin barra de pestañas mientras solo haya una pantalla). La ruta `styleguide` se registra en `AppShell` solo en `DEBUG`, y en `HomePage` hay un botón "Catálogo de estilos" visible solo en `DEBUG` (propiedad `IsStyleGuideVisible` en el ViewModel inyectada por constructor con un `AppEnvironment`/flag, no `#if` en el ViewModel).
- `MauiProgram.cs`: registra `ApiOptions`, `AddRefitClient<IOpoStudyApi>()` con `BaseAddress` y timeout de 15 s, `INavigationService`, ViewModels y páginas como `Transient`.
- Fuentes: se mantiene OpenSans (Regular y Semibold), ya incluida.

#### URL de la API
- Propiedad MSBuild `ApiBaseUrl` en `OpoStudy.Mobile.csproj`, que se escribe como `AssemblyMetadata("ApiBaseUrl", ...)` y lee `MauiProgram` al crear `ApiOptions`.
- Valor por defecto en `Debug`: `http://10.0.2.2:5054` (emulador → `localhost` del PC, puerto del AppHost).
- Dispositivo USB: `adb reverse tcp:5054 tcp:5054` y compilar con `-p:ApiBaseUrl=http://localhost:5054`.
- En `Release` no hay valor por defecto: si falta, `MauiProgram` lanza una excepción clara al arrancar. Se definirá en #21.
- Android bloquea HTTP en claro: `Platforms/Android/Resources/xml/network_security_config.xml` lo permite **solo** para `10.0.2.2` y `localhost`, y se referencia desde `AndroidManifest.xml`.
- El README (sección de desarrollo local) explica ambos modos.

#### Sistema de diseño
Recursos con nombre en `Resources/Styles/Colors.xaml`, `Typography.xaml` (nuevo), `Spacing.xaml` (nuevo) y `Styles.xaml` (reescrito, sin los estilos sobrantes de la plantilla). Los nombres de recurso van en inglés.

**Colores** (pares claro/oscuro; los estilos usan `AppThemeBinding`):

| Token | Claro | Oscuro | Uso |
|---|---|---|---|
| `Primary` | `#1E5AA8` | `#9CC2FF` | Botones principales, enlaces, foco |
| `OnPrimary` | `#FFFFFF` | `#0B2545` | Texto sobre `Primary` |
| `Background` | `#F7F8FA` | `#121417` | Fondo de página |
| `Surface` | `#FFFFFF` | `#1D2026` | Tarjetas, `Border` |
| `OnSurface` | `#1B1D21` | `#E6E8EC` | Texto principal |
| `OnSurfaceVariant` | `#4A505A` | `#B4BAC4` | Texto secundario |
| `Outline` | `#C4C9D1` | `#3C424C` | Bordes y separadores |
| `Success` | `#1B7A3A` | `#7FD69B` | Respuesta correcta |
| `Error` | `#B3261E` | `#F2B8B5` | Respuesta incorrecta, errores |
| `Warning` | `#8A5300` | `#FFC870` | Avisos |

- Contraste mínimo **4,5:1** para texto normal (WCAG AA) y **3:1** para componentes y texto grande, comprobado para cada par texto/fondo usado. La comprobación (script o tabla) se deja en la doc.
- `Platforms/Android/Resources/values/colors.xml`, icono y splash pasan a `Primary` (`#1E5AA8`).

**Tipografía** (`x:Double` + estilos con clave): `Display` 28 Semibold, `Title` 22 Semibold, `Subtitle` 18 Semibold, `Body` 16 Regular (estilo implícito de `Label`), `Caption` 13 Regular en `OnSurfaceVariant`. Se respeta el escalado de fuente del sistema (no se desactiva `FontAutoScalingEnabled`).

**Espaciado** (escala de 4): `SpaceXs` 4, `SpaceS` 8, `SpaceM` 12, `SpaceL` 16, `SpaceXl` 24, `SpaceXxl` 32 como `x:Double`, y `Thickness` para el margen de página (`PagePadding` = 16) y el relleno de tarjeta (`CardPadding` = 16). Radio de esquina `CornerRadius` 12.

**Estilos implícitos**: `ContentPage` (fondo), `Label` (`Body`, `OnSurface`), `Button` (principal: `Primary`/`OnPrimary`, `MinimumHeightRequest` 48, radio 12), `Border` (tarjeta: `Surface`, trazo `Outline`), `ActivityIndicator` (`Primary`), `RefreshView`, `CollectionView`. Estilos con clave: `SecondaryButton` (contorno), `TextButton`, `CardBorder`, `ErrorLabel`, los de tipografía.

**Accesibilidad**:
- Todo control táctil mide al menos 48×48 dp (`MinimumHeightRequest`/`MinimumWidthRequest` en los estilos).
- `SemanticProperties.Description` en controles sin texto visible e imágenes; `SemanticProperties.HeadingLevel` en los títulos de página.
- El estado (cargando, error) no se comunica solo con color: siempre hay texto.

**Documentación** `docs/diseno/sistema-diseno.md` (en español): tokens con sus valores y uso, tabla de contraste, estilos disponibles, reglas de accesibilidad y patrones de pantalla (los mismos de la skill local `maui-ui`).

#### Navegación
| Ruta | Página | ViewModel | Notas |
|---|---|---|---|
| `home` | `HomePage` | `HomeViewModel` | Raíz del Shell |
| `styleguide` | `StyleGuidePage` | — (estática) | Solo `DEBUG` |

`HomePage`: título "OpoStudy", subtítulo con la oposición inicial (texto fijo por ahora), tarjeta "Estado del servidor" con indicador de carga, versión ("Conectado · API 0.1.0") o mensaje de error + botón "Reintentar"; `RefreshView` que ejecuta `RefreshCommand`.

`StyleGuidePage`: muestra colores, tipografías, botones (principal, secundario, texto, deshabilitado), tarjeta y etiquetas de éxito/error/aviso.

#### Notas de implementación
- Refit 17 genera los clientes en compilación: se registra con `AddRefitGeneratedClient<IOpoStudyApi>(ApiJsonContext.Default)`. `ApiJsonContext` (`JsonSerializerContext` con valores web) debe incluir **cada DTO nuevo** que use la app.
- `HomeViewModel` recibe además `INavigationService`, `AppEnvironment` (para `IsStyleGuideVisible`) e `ILogger<HomeViewModel>`; la versión se muestra sin los metadatos de compilación (`1.0.0+abc` → `1.0.0`).
- `ApiErrorMessages` recorre las excepciones internas: Refit envuelve los fallos de red (`ApiRequestException` → `TaskCanceledException` → `TimeoutException`). En Android, `JavaNetworkExceptionHandler` traduce las excepciones de Java a `HttpRequestException`/`TimeoutException`.
- Barra de estado: `MainActivity` aplica el color `Background` y los iconos claros u oscuros según el tema. Limitación conocida: si se cambia el tema del sistema con una página apilada abierta (el catálogo), su franja superior no se repinta hasta volver a entrar.
- En `Release` sin `ApiBaseUrl` la app se cierra al arrancar: el workflow de release tendrá que pasarlo (#21).

## Seguridad
- Sin secretos en la app. La URL de la API no es secreta, pero no se versionan URLs reales de Tailscale.
- HTTP en claro permitido solo para `10.0.2.2` y `localhost` (desarrollo).
- Los mensajes de error que se muestran al usuario no incluyen trazas ni detalles internos (solo `detail`/`title` de ProblemDetails, que ya están pensados para el usuario).

## Criterios de aceptación
- [ ] La app arranca en el emulador con la nueva estructura, en tema claro y oscuro, y muestra la versión de la API cuando el AppHost está levantado (o el error y "Reintentar" si no lo está).
- [ ] Los ViewModels se prueban sin emulador (`OpoStudy.Mobile.Core.Tests` en el job `backend` de CI).
- [ ] `OpoStudy.Mobile.Core` no depende de MAUI (test de arquitectura).
- [ ] No quedan `MainPage`, `dotnet_bot.png` ni colores de la plantilla.
- [ ] Sistema de diseño documentado en `docs/diseno/sistema-diseno.md` con contrastes AA.
- [ ] `dotnet build src/OpoStudy.Mobile -f net10.0-android` sin warnings.

## Plan de tests
- Unitarios (`OpoStudy.Mobile.Core.Tests`, `IOpoStudyApi` con NSubstitute):
  - `RefreshCommand_ApiResponds_SetsVersionAndConnected`
  - `RefreshCommand_ApiUnreachable_SetsErrorAndNotConnected`
  - `RefreshCommand_AfterError_ClearsPreviousError`
  - `RefreshCommand_WhileBusy_CannotExecute`
  - `OnAppearingAsync_CalledTwice_LoadsOnce`
  - `IsBusy_DuringRequest_IsTrueAndResetsAfter`
  - `ApiErrorMessages.From_*` para cada caso de la tabla (ProblemDetails con `detail`, solo `title`, `ApiException` sin cuerpo, `HttpRequestException`, `TaskCanceledException`, otra).
- Arquitectura: `MobileCore_ShouldNot_DependOnMaui`.
- Manual: emulador con el AppHost levantado y parado, en claro y oscuro (`adb shell cmd uimode night yes|no`), capturas con `adb exec-out screencap -p`; recorrido con TalkBack de la pantalla de inicio.
