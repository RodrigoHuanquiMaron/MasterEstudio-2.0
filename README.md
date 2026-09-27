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