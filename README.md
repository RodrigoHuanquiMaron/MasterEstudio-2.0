# MasterEstudio 2.0 — Plataforma de Cursos

## Parte 1 — Autenticación y paneles
Login, registro, roles Usuario y Administrador, panel de usuario (Panel principal,
Mis cursos, Desempeño, Donaciones) y panel de administrador (perfiles y cursos).

## Parte 2 — Sesiones y cookies

### Modelo elegido
Se usa un modelo **híbrido**:

| Elemento | Dónde vive | Para qué sirve | Duración |
|---|---|---|---|
| Cookie `MasterEstudio.Auth` | Navegador (cifrada) | Identidad del usuario (ASP.NET Core Identity) | 7 días con "Recordarme"; si no, hasta cerrar el navegador |
| Sesión `ISession` (cookie `MasterEstudio.Session`) | Servidor (memoria; Redis en la Parte 4) | Datos temporales: nombre, rol, inicio, páginas visitadas | 30 min sin actividad |
| Cookie `MasterEstudio.UltimoCorreo` | Navegador | Rellenar el correo en el login | 30 días |

### Por qué este modelo
- La identidad va en una cookie cifrada de Identity, que es el estándar de ASP.NET Core MVC.
- Los datos temporales van en la sesión del servidor, no en el navegador.
- Si la sesión expira pero la cookie de "Recordarme" sigue vigente, `SesionUsuarioMiddleware`
  reconstruye la sesión automáticamente.
- Todas las cookies son `HttpOnly` y `SameSite=Lax`.

### Modelos de datos
`ApplicationUser` (extiende IdentityUser), `Curso`, `Inscripcion` (usuario–curso con progreso)
y `Donacion`.

## Parte 3 — Base de datos PostgreSQL

- Proveedor: Npgsql (EF Core) con migraciones `InicialPostgres`.
- La app lee la conexión de la variable `DATABASE_URL` (formato `postgresql://...` de Render)
  y la convierte en `Infrastructure/ConexionBD.cs`.
- Las llaves de cifrado de cookies se guardan en la tabla `DataProtectionKeys`, así las
  sesiones con "Recordarme" sobreviven a cada despliegue.
- Las migraciones se aplican automáticamente al iniciar.

### Variables de entorno (Render)

| Variable | Valor |
|---|---|
| `DATABASE_URL` | Internal Database URL de la base en Render |
| `Admin__Email` | Correo del administrador inicial |
| `Admin__Password` | Contraseña del administrador inicial |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` (Render usa un proxy HTTPS) |

En desarrollo, `DATABASE_URL` se guarda con `dotnet user-secrets` (nunca en el repositorio).


## Parte 4 — Caché con Redis

- Redis (Render Key Value) como `IDistributedCache`, configurado con la variable `REDIS_URL`.
- Patrón cache-aside en `Infrastructure/CacheService.cs`, expiración de 60 segundos.
- Datos en caché: catálogo de cursos, inscripciones y donaciones por usuario, y resumen del admin.
- Invalidación: después de guardar en PostgreSQL se borran las claves afectadas.
  Al agregar o eliminar un curso se cambia `cursos:version`, lo que invalida el catálogo y los
  cursos de todos los usuarios a la vez.
- Los logs muestran `CACHE HIT` (lectura desde Redis) o `CACHE MISS` (lectura desde PostgreSQL).
- Las sesiones de la Parte 2 también se guardan en Redis.

| Variable | Valor en Render |
|---|---|
| `REDIS_URL` | Internal Key Value URL (`redis://red-...:6379`) |
## Parte 5 — Tiempo real con PieHost

- Al agregar o eliminar un curso, el servidor: 1) guarda en PostgreSQL, 2) invalida Redis y
  3) publica el evento `CursoActualizado` (id, acción, título) en PieHost mediante su API REST
  (`Infrastructure/PieHostService.cs`). El secreto de PieHost nunca llega al navegador.
- La página del usuario se conecta por WebSocket al canal `cursos` (`wwwroot/js/tiempo-real.js`).
  Al recibir el evento muestra un aviso y actualiza el contenido sin recargar.
- Al reconectar, la página vuelve a consultar el estado vigente.

| Variable | Valor en Render |
|---|---|
| `PieHost__ClusterId` | Cluster ID de PieSocket |
| `PieHost__ApiKey` | API Key |
| `PieHost__ApiSecret` | API Secret |

## Despliegue

- URL pública: https://TU-SERVICIO.onrender.com
- Commit desplegado: (se verifica en Render → Events)