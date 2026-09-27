using PlataformaCursos.Models;

namespace PlataformaCursos.Infrastructure;

// Nombres de las claves que se guardan en la sesión
public static class SesionKeys
{
    public const string Nombre = "Usuario.Nombre";
    public const string Rol = "Usuario.Rol";
    public const string InicioSesion = "Usuario.InicioSesion";
    public const string PaginasVisitadas = "Usuario.PaginasVisitadas";
}

public static class SesionHelper
{
    // Llena la sesión con los datos básicos del usuario
    public static void Cargar(ISession sesion, ApplicationUser usuario, string rol)
    {
        sesion.SetString(SesionKeys.Nombre, usuario.NombreCompleto);
        sesion.SetString(SesionKeys.Rol, rol);
        sesion.SetString(SesionKeys.InicioSesion, DateTime.UtcNow.ToString("o"));
        sesion.SetInt32(SesionKeys.PaginasVisitadas, 0);
    }
}