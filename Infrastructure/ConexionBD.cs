using Npgsql;

namespace PlataformaCursos.Infrastructure;

// Obtiene la cadena de conexión a PostgreSQL.
// 1. Si existe la variable DATABASE_URL (Render o user-secrets), la usa.
// 2. Si no, usa ConnectionStrings:DefaultConnection de appsettings.
public static class ConexionBD
{
    public static string Obtener(IConfiguration config)
    {
        var url = config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url))
            return DesdeUrl(url);

        return config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Falta la conexión a la base: define DATABASE_URL o ConnectionStrings:DefaultConnection.");
    }

    // Convierte postgresql://usuario:clave@host:puerto/base al formato de Npgsql
    private static string DesdeUrl(string url)
    {
        var uri = new Uri(url);
        var credenciales = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(credenciales[0]),
            Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            // El link externo (con dominio .render.com) exige SSL; el interno de Render no
            SslMode = uri.Host.Contains('.') ? SslMode.Require : SslMode.Disable
        };

        return builder.ConnectionString;
    }
}