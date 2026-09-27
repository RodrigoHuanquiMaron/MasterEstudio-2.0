using Microsoft.AspNetCore.Identity;
using PlataformaCursos.Models;

namespace PlataformaCursos.Infrastructure;

// Se ejecuta en cada petición:
// 1. Si el usuario está autenticado (por cookie) pero su sesión está vacía, la reconstruye.
// 2. Cuenta las páginas visitadas durante la sesión.
public class SesionUsuarioMiddleware
{
    private readonly RequestDelegate _next;

    public SesionUsuarioMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<SesionUsuarioMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sesion = context.Session;
            await sesion.LoadAsync();

            if (string.IsNullOrEmpty(sesion.GetString(SesionKeys.Nombre)))
            {
                var usuario = await userManager.GetUserAsync(context.User);
                if (usuario is not null)
                {
                    var roles = await userManager.GetRolesAsync(usuario);
                    SesionHelper.Cargar(sesion, usuario, roles.FirstOrDefault() ?? "Usuario");
                    logger.LogInformation("Sesión restaurada desde la cookie para {Email}", usuario.Email);
                }
            }

            var paginas = sesion.GetInt32(SesionKeys.PaginasVisitadas) ?? 0;
            sesion.SetInt32(SesionKeys.PaginasVisitadas, paginas + 1);
        }

        await _next(context);
    }
}