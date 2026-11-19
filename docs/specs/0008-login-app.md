# 0008 — Login en la app con Google y correo

- **Issue:** #18
- **Milestone:** v0.1.0-alpha
- **Estado:** implementada

> La issue apuntaba a `0007-login-app.md`, pero el número 0007 ya lo usa la spec de la base de la app.

## Objetivo
Que la app solo se use con sesión iniciada en Firebase Auth (Google o correo y contraseña), que la sesión sobreviva al cierre de la app y que cada llamada a la API lleve el ID token como `Bearer`. Sin el SDK nativo de Firebase: la app usa la API REST de Firebase Auth desde `OpoStudy.Mobile.Core` (que se prueba sin emulador) y, para Google, el Credential Manager de Android.

## Alcance
**Incluye:**
- Registro, inicio de sesión y recuperación de contraseña con correo y contraseña.
- Inicio de sesión con Google (Credential Manager → ID token de Google → `signInWithIdp` de Firebase).
- Sesión persistida en `SecureStorage`, ID token renovado automáticamente y enviado como `Bearer` desde Refit.
- Cerrar sesión y borrar la cuenta (API + Firebase), pendiente desde la spec 0005.
- Pantallas: arranque, login, registro y recuperación de contraseña. Tarjeta de cuenta en la pantalla de inicio.
- Configuración de Firebase leída de `google-services.json`, que no se versiona: copia manual en local y secret en el workflow de release.
- Regla de gitleaks para `google-services.json`.

**No incluye:**
- Facebook (#20).
- Verificación del correo y vinculación de cuentas de distintos proveedores.
- Restricción de la API key de Firebase por app Android (cabeceras `X-Android-Package`/`X-Android-Cert`) y App Check: #21.
- Cambios en la API: `GET /me` y `DELETE /me` ya existen (0005).

## Diseño
### Dominio
Sin cambios. Sin términos nuevos en el glosario (la sesión es infraestructura de la app, no dominio).

### API
Sin cambios. La app empieza a usar:

| Método | Ruta | Auth | Request → Response |
|---|---|---|---|
| GET | `/api/v1/me` | Usuario | — → `200 UserProfileResponse` |
| DELETE | `/api/v1/me` | Usuario | — → `204` |

`IOpoStudyApi` gana `GetMeAsync(CancellationToken)` y `DeleteMeAsync(CancellationToken)`. `UserProfileResponse` se añade a `ApiJsonContext`.

### Datos
Sin cambios de esquema. En el dispositivo, la sesión se guarda en `SecureStorage` (ver más abajo).

### Configuración de Firebase
- `google-services.json` se coloca a mano en `src/OpoStudy.Mobile/Platforms/Android/google-services.json` (ya está en `.gitignore`).
- `OpoStudy.Mobile.csproj` lo incluye como `EmbeddedResource` con `LogicalName="google-services.json"` **solo si existe**. En `Release`, si no existe, la compilación falla con un `<Error>` claro. En `Debug` compila sin él (así el job `mobile-android` de CI no necesita secrets) y la app muestra el error al intentar iniciar sesión.
- `Mobile.Core/Auth/GoogleServicesConfig.Parse(Stream json, string packageName)` → `FirebaseOptions(string ApiKey, string GoogleWebClientId, string ProjectId)`:
  - Busca en `client[]` el que tenga `client_info.android_client_info.package_name == packageName`.
  - `ApiKey` = `api_key[0].current_key`; `GoogleWebClientId` = `oauth_client[]` con `client_type == 3`; `ProjectId` = `project_info.project_id`.
  - Si falta algo, lanza `InvalidOperationException` con un mensaje que diga qué falta (p. ej. "no hay cliente web de OAuth: activa Google en Firebase y vuelve a descargar el fichero").
- `MauiProgram` lee el recurso con `Assembly.GetManifestResourceStream` y `AppInfo.PackageName`. Si no existe, registra `FirebaseOptions` vacías (`IsConfigured == false`); `FirebaseAuthClient` lanza entonces `FirebaseAuthException("NOT_CONFIGURED")`.
- `release.yml`: antes del `publish`, escribe el fichero desde el secret `GOOGLE_SERVICES_JSON_BASE64` (environment `release`) y falla si falta, igual que con el keystore.
- `.gitleaks.toml`: regla con `path = '''(^|/)google-services\.json$'''` para que cualquier commit del fichero se detecte. Las API keys `AIza…` ya las cubre la regla por defecto `gcp-api-key`.
- README (desarrollo local): cómo descargar `google-services.json` de la consola de Firebase, dónde copiarlo, la huella SHA-1 del keystore de depuración de .NET (`%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`) y que el emulador necesita una imagen con Google Play y una cuenta de Google para probar Google.

### App
#### `OpoStudy.Mobile.Core/Auth/`
```
FirebaseOptions.cs          // record + IsConfigured
GoogleServicesConfig.cs     // Parse(...)
IFirebaseAuthClient.cs      // REST de Firebase Auth
FirebaseAuthClient.cs       // implementación con HttpClient
FirebaseAuthException.cs    // Code (string) a partir de error.message
FirebaseJsonContext.cs      // JsonSerializerContext de los DTO de Firebase (internos)
AuthSession.cs              // record: IdToken, RefreshToken, ExpiresAt, Email, Provider
ISecureStore.cs             // GetAsync/SetAsync/Remove (string)
IGoogleSignInProvider.cs    // Task<string?> GetIdTokenAsync(ct) (null = cancelado); Task SignOutAsync()
GoogleSignInException.cs    // NoAccount | Failed
IAuthService.cs / AuthService.cs
AuthHeaderHandler.cs        // DelegatingHandler
AuthErrorMessages.cs        // código → texto en español
```

**`FirebaseAuthClient`** (`HttpClient` tipado, timeout 15 s). Todas las llamadas llevan `?key=<ApiKey>`:

| Método | Endpoint | Cuerpo |
|---|---|---|
| `SignUpAsync(email, password)` | `POST https://identitytoolkit.googleapis.com/v1/accounts:signUp` | `{email, password, returnSecureToken: true}` |
| `SignInWithPasswordAsync(email, password)` | `…/accounts:signInWithPassword` | ídem |
| `SignInWithGoogleAsync(googleIdToken)` | `…/accounts:signInWithIdp` | `{postBody: "id_token=<t>&providerId=google.com", requestUri: "http://localhost", returnSecureToken: true, returnIdpCredential: true}` |
| `SendPasswordResetAsync(email)` | `…/accounts:sendOobCode` | `{requestType: "PASSWORD_RESET", email}` + cabecera `X-Firebase-Locale: es` |
| `RefreshAsync(refreshToken)` | `POST https://securetoken.googleapis.com/v1/token` | form `grant_type=refresh_token&refresh_token=<t>` (respuesta en snake_case) |
| `DeleteAccountAsync(idToken)` | `…/accounts:delete` | `{idToken}` |

- Las respuestas de inicio de sesión devuelven `FirebaseTokens(IdToken, RefreshToken, ExpiresIn, Email)`. `expiresIn`/`expires_in` llegan como **string**: se parsean a segundos.
- Error: cuerpo `{"error":{"message":"CODE"}}` o `"CODE : detalle"` → `FirebaseAuthException(Code = parte antes de " : ")`. Respuesta no JSON → `FirebaseAuthException("UNKNOWN")`. Los fallos de red se dejan pasar (`HttpRequestException`, `TaskCanceledException`) para que los traduzca `ApiErrorMessages`.

**`AuthService`** (singleton; recibe `IFirebaseAuthClient`, `ISecureStore`, `IGoogleSignInProvider`, `TimeProvider`, `ILogger`):
- Estado: `bool IsSignedIn`, `string? Email`, `string? Provider` (`password` | `google.com`); evento `SessionExpired`.
- `RestoreSessionAsync(ct)`: lee la clave `auth.session` (JSON de `AuthSession`). Si no existe, o no se puede leer o deserializar, borra la clave y queda sin sesión. **No** llama a la red: la app abre sin conexión.
- `SignInWithEmailAsync`, `SignUpWithEmailAsync`, `SignInWithGoogleAsync(ct)` (pide el token a `IGoogleSignInProvider`; si devuelve `null` → devuelve `false` sin error), `SendPasswordResetAsync`: tras un inicio de sesión correcto guarda la sesión (`ExpiresAt = ahora + ExpiresIn`) y actualiza el estado.
- `GetIdTokenAsync(ct)`: sin sesión → `null`. Si al token le quedan más de 5 minutos, lo devuelve; si no, lo renueva con `RefreshAsync` y guarda la nueva sesión (el refresh token puede cambiar). Un `SemaphoreSlim` garantiza **una sola** renovación con llamadas concurrentes. Si la renovación falla con `INVALID_REFRESH_TOKEN`, `TOKEN_EXPIRED`, `USER_DISABLED`, `USER_NOT_FOUND` o `INVALID_ID_TOKEN` → borra la sesión, lanza `SessionExpired` y devuelve `null`. Un fallo de red se propaga (la sesión se conserva).
- `InvalidateIdToken()`: marca el token como caducado para forzar la renovación en la siguiente llamada.
- `SignOutAsync()`: borra `auth.session`, el estado y llama a `IGoogleSignInProvider.SignOutAsync()` (limpia el estado del Credential Manager para que vuelva a salir el selector de cuentas); un fallo de este último se registra y se ignora.
- `DeleteFirebaseAccountAsync(ct)`: `DeleteAccountAsync` con un ID token válido y, después, cierra la sesión. Si Firebase responde `CREDENTIAL_TOO_OLD_LOGIN_AGAIN`, cierra la sesión igualmente y relanza la excepción.
- Nunca registra en logs tokens ni correos.

**`AuthHeaderHandler`** (en la cadena del cliente Refit): pide `GetIdTokenAsync`; si hay token añade `Authorization: Bearer <token>`. Si la respuesta es `401` y la petición llevaba token, llama a `InvalidateIdToken()` (sin reintentar: la siguiente acción del usuario usará un token nuevo).

**`AuthErrorMessages.From(string code)`** y `ApiErrorMessages.From` delegan en él para `FirebaseAuthException`:

| Código | Mensaje |
|---|---|
| `INVALID_LOGIN_CREDENTIALS`, `INVALID_PASSWORD`, `EMAIL_NOT_FOUND` | "Correo o contraseña incorrectos." |
| `EMAIL_EXISTS` | "Ya existe una cuenta con ese correo. Inicia sesión o recupera la contraseña." |
| `INVALID_EMAIL` | "El correo electrónico no es válido." |
| `WEAK_PASSWORD` | "La contraseña es demasiado débil. Usa al menos 6 caracteres." |
| `USER_DISABLED` | "Esta cuenta está desactivada." |
| `TOO_MANY_ATTEMPTS_TRY_LATER` | "Demasiados intentos. Espera unos minutos y vuelve a intentarlo." |
| `OPERATION_NOT_ALLOWED` | "Este método de acceso no está disponible." |
| `CREDENTIAL_TOO_OLD_LOGIN_AGAIN` | "Por seguridad, vuelve a iniciar sesión y repite el borrado de la cuenta." |
| `TOKEN_EXPIRED`, `INVALID_REFRESH_TOKEN`, `USER_NOT_FOUND`, `INVALID_ID_TOKEN` | "Tu sesión ha caducado. Vuelve a iniciar sesión." |
| `NOT_CONFIGURED` | "El inicio de sesión no está configurado en esta versión de la app." |
| otro | "No se ha podido completar la operación. Inténtalo de nuevo." |

`GoogleSignInException`: `NoAccount` → "No hay ninguna cuenta de Google en el dispositivo."; `Failed` → "No se ha podido iniciar sesión con Google."

#### ViewModels (`OpoStudy.Mobile.Core/ViewModels/`)
Todos heredan de `ViewModelBase` (`IsBusy`, `ErrorMessage`), comandos asíncronos con `CanExecute = !IsBusy` y validación local antes de llamar a la red (sin llamada si falla).

- `StartupViewModel(IAuthService, INavigationService)`: `OnAppearingAsync` → `RestoreSessionAsync` → `//home` si hay sesión, `//login` si no.
- `LoginViewModel`: `Email`, `Password`; `SignInCommand` (correo con `@` y punto en el dominio, contraseña no vacía) → `//home`; `SignInWithGoogleCommand` → `//home` (cancelar no muestra error); `GoToRegisterCommand` → `register`; `GoToForgotPasswordCommand` → `forgot-password` con el correo escrito como parámetro. Al salir con éxito se vacía `Password`.
- `RegisterViewModel`: `Email`, `Password`, `ConfirmPassword`; valida correo, contraseña de 6 caracteres o más ("La contraseña debe tener al menos 6 caracteres.") y que coincidan ("Las contraseñas no coinciden.") → `SignUpWithEmailAsync` → `//home`.
- `ForgotPasswordViewModel` (`IQueryAttributable` para recibir el correo): `SendCommand` → `SendPasswordResetAsync` → `IsSent = true` y `ConfirmationMessage` = "Si existe una cuenta con ese correo, te hemos enviado un enlace para restablecer la contraseña." (mismo texto exista o no la cuenta).
- `HomeViewModel` (cambios): recibe además `IAuthService` e `IDialogService`.
  - `RefreshCommand` llama a `GetVersionAsync` y después a `GetMeAsync`; expone `AccountEmail` (de `IAuthService.Email`) y `MemberSince` (`CreatedAt` de `/me`, formato `d 'de' MMMM 'de' yyyy` en `es-ES`).
  - `SignOutCommand` → `SignOutAsync` → `//login`.
  - `DeleteAccountCommand`: confirmación ("¿Borrar tu cuenta?", "Se borrarán tu cuenta y todo tu progreso. No se puede deshacer.", "Borrar", "Cancelar"); si se acepta: `DeleteMeAsync` (API) → `DeleteFirebaseAccountAsync` → `//login`. Si falla la API, no se toca Firebase y se muestra el error. Si Firebase responde `CREDENTIAL_TOO_OLD_LOGIN_AGAIN`, se navega a `//login` y se muestra el mensaje de la tabla (los datos de la API ya están borrados; al volver a entrar se crea un usuario vacío y se puede repetir el borrado).
- `IDialogService` (Core): `Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)` y `Task AlertAsync(string title, string message)` (para el mensaje tras navegar a login).

#### Capa MAUI (`OpoStudy.Mobile`)
- `Services/SecureStore.cs`: `SecureStorage.Default`. Si leer lanza (claves corruptas tras restaurar el dispositivo), `RemoveAll` y devuelve `null`.
- `Services/DialogService.cs`: `Shell.Current.DisplayAlertAsync`.
- `ShellNavigationService` ejecuta la navegación en el hilo principal (`MainThread.InvokeOnMainThreadAsync`).
- `Platforms/Android/GoogleSignInProvider.cs` (con su registro condicional para Android): `CredentialManager.Create(activity)` + `GetSignInWithGoogleOption.Builder(webClientId)` → `GoogleIdTokenCredential.CreateFrom(...).IdToken`. `GetCredentialCancellationException` → `null`; `NoCredentialException` → `GoogleSignInException(NoAccount)`; otra → `Failed`. `SignOutAsync` → `ClearCredentialStateAsync`. Paquetes (versiones en `Directory.Packages.props`, grupo `App`): `Xamarin.AndroidX.Credentials`, `Xamarin.AndroidX.Credentials.PlayServicesAuth`, `Xamarin.GoogleAndroid.Libraries.Identity.GoogleId`.
- `MauiProgram`: `FirebaseOptions`, `TimeProvider.System`, `AddHttpClient<IFirebaseAuthClient, FirebaseAuthClient>`, `IAuthService` singleton, `AuthHeaderHandler` transient y `.AddHttpMessageHandler<AuthHeaderHandler>()` en el cliente Refit, `ISecureStore`, `IDialogService`, `IGoogleSignInProvider`, nuevos ViewModels y páginas.
- `AppShell`: se suscribe a `IAuthService.SessionExpired` y navega a `//login`.
- `AndroidManifest.xml`: `android:allowBackup="false"` (SecureStorage no sobrevive a una copia restaurada y no hay nada local que merezca copia).
- Estilo implícito nuevo de `Entry` en `Styles.xaml` (fondo `Surface`, texto `OnSurface`, placeholder `OnSurfaceVariant`, `MinimumHeightRequest` 48) y documentado en `docs/diseno/sistema-diseno.md`.

#### Navegación
| Ruta | Página | ViewModel | Notas |
|---|---|---|---|
| `startup` | `StartupPage` | `StartupViewModel` | Primer `ShellContent`: logo + indicador de carga, sin barra |
| `login` | `LoginPage` | `LoginViewModel` | Raíz (`//login`), sin barra |
| `register` | `RegisterPage` | `RegisterViewModel` | Apilada sobre login |
| `forgot-password` | `ForgotPasswordPage` | `ForgotPasswordViewModel` | Apilada sobre login |
| `home` | `HomePage` | `HomeViewModel` | Raíz (`//home`) |

`startup`, `login` y `home` son `ShellContent` de primer nivel (navegación absoluta, sin volver atrás al login); `register` y `forgot-password` se registran con `Routing.RegisterRoute`.

**`LoginPage`**: título "OpoStudy" (`Display`, heading), subtítulo "Inicia sesión para guardar tu progreso"; botón secundario "Continuar con Google"; separador "o"; etiquetas visibles "Correo electrónico" y "Contraseña" encima de cada `Entry` (`Keyboard="Email"`, `IsPassword`); botón principal "Entrar"; botones de texto "¿Has olvidado tu contraseña?" y "Crear cuenta"; `ErrorLabel`; indicador de carga. `ScrollView` para que el teclado no tape los campos.

**`RegisterPage`**: título "Crear cuenta", correo, contraseña (con la ayuda "Al menos 6 caracteres" en `Caption`), repetir contraseña, botón "Crear cuenta".

**`ForgotPasswordPage`**: título "Recuperar contraseña", texto explicativo, correo, botón "Enviar enlace"; tras enviar, el mensaje de confirmación sustituye al formulario.

**`HomePage`**: nueva tarjeta "Tu cuenta" bajo la del servidor: correo, "Miembro desde …", botón secundario "Cerrar sesión" y botón de texto "Borrar cuenta" en color `Error`.

## Seguridad
- `google-services.json` fuera del repo (`.gitignore` + regla de gitleaks) e inyectado en release desde un secret. La API key de Firebase no es secreta (va dentro del APK), pero no se versiona.
- Tokens solo en `SecureStorage` (Android Keystore) y en memoria; `allowBackup="false"`. Nunca en logs, ni el correo.
- La recuperación de contraseña responde igual exista o no la cuenta (no revela qué correos están registrados).
- Borrar la cuenta pide confirmación y borra primero los datos de la API (con el token aún válido) y después la cuenta de Firebase.
- Todo el tráfico con Firebase y Google va por HTTPS; la configuración de red de `Debug` no cambia.
- Pendiente para #21: restringir la API key por app Android (y enviar `X-Android-Package`/`X-Android-Cert`) y App Check.

## Criterios de aceptación
- [ ] Se puede crear una cuenta con correo, iniciar sesión con correo y con Google, y recuperar la contraseña (llega el correo en español).
- [ ] Al cerrar la app (también desde recientes) y reabrirla, se entra directamente en inicio con la sesión conservada, incluso sin conexión.
- [ ] Pasada una hora, las llamadas a la API siguen funcionando (el token se renueva solo).
- [ ] La pantalla de inicio muestra el correo y la fecha de alta que devuelve `GET /me`.
- [ ] Cerrar sesión vuelve al login; con Google vuelve a salir el selector de cuentas.
- [ ] Borrar la cuenta elimina el usuario de la BD y de Firebase.
- [ ] `.gitignore` y gitleaks cubren `google-services.json`; el job `mobile-android` de CI compila sin él y `release.yml` lo exige.
- [ ] `dotnet build src/OpoStudy.Mobile -f net10.0-android` sin warnings y los tests de `OpoStudy.Mobile.Core.Tests` en verde.

## Plan de tests
- Unitarios (`OpoStudy.Mobile.Core.Tests`; `HttpMessageHandler` de prueba para el cliente REST, NSubstitute para el resto, `FakeTimeProvider` de `Microsoft.Extensions.TimeProvider.Testing`):
  - `GoogleServicesConfigTests`: fichero válido → `ApiKey`, `GoogleWebClientId`, `ProjectId`; otro `package_name` → excepción; sin `client_type` 3 → excepción con mensaje.
  - `FirebaseAuthClientTests`: URL con `key`, cuerpo de `signInWithPassword` y de `signInWithIdp`; `expiresIn` como string; `RefreshAsync` envía form y lee snake_case; error `WEAK_PASSWORD : …` → `Code == "WEAK_PASSWORD"`; cuerpo no JSON → `UNKNOWN`; `sendOobCode` lleva `X-Firebase-Locale: es`; sin configuración → `NOT_CONFIGURED`.
  - `AuthServiceTests`: `SignInWithEmailAsync_Success_PersistsSession`; `RestoreSessionAsync_StoredSession_IsSignedIn`; `RestoreSessionAsync_CorruptSession_ClearsAndSignedOut`; `GetIdTokenAsync_TokenFresh_DoesNotRefresh`; `GetIdTokenAsync_TokenNearExpiry_RefreshesAndPersists`; `GetIdTokenAsync_ConcurrentCalls_RefreshesOnce`; `GetIdTokenAsync_RefreshRejected_ClearsSessionAndRaisesExpired`; `GetIdTokenAsync_NetworkError_KeepsSession`; `SignInWithGoogleAsync_Cancelled_ReturnsFalse`; `SignOutAsync_ClearsSessionAndGoogleState`; `DeleteFirebaseAccountAsync_CredentialTooOld_SignsOutAndThrows`.
  - `AuthHeaderHandlerTests`: con sesión añade `Bearer`; sin sesión no añade cabecera; `401` invalida el token.
  - `AuthErrorMessagesTests`: cada fila de la tabla y un código desconocido.
  - `StartupViewModelTests`, `LoginViewModelTests`, `RegisterViewModelTests`, `ForgotPasswordViewModelTests`: validación sin llamada a la red, éxito con navegación, error de Firebase en `ErrorMessage`, `CanExecute` falso mientras `IsBusy`, Google cancelado sin error.
  - `HomeViewModelTests` (añadidos): carga de `/me`; cerrar sesión navega a `//login`; borrar cancelado no llama a nada; borrar aceptado llama en orden a `DeleteMeAsync` y `DeleteFirebaseAccountAsync` y navega; fallo de la API no borra en Firebase.
- Arquitectura: `MobileCore_ShouldNot_DependOnMaui` sigue en verde.
- Manual (skill `maui-ui`, emulador con Google Play y AppHost levantado con el projectId real): registro, login con correo, login con Google, recuperación (correo recibido), cerrar y reabrir la app (también en modo avión), cerrar sesión, borrar cuenta (comprobar BD y consola de Firebase). Capturas de login, registro y recuperación en claro y oscuro; recorrido con TalkBack del login.
