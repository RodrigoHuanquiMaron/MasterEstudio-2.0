namespace PlataformaCursos.Infrastructure;

// Convierte REDIS_URL (redis://... o rediss://... de Render) al formato de StackExchange.Redis.
// Devuelve null si no hay REDIS_URL: en ese caso se usa una caché en memoria.
public static class ConexionRedis
{
    public static string? Obtener(IConfiguration config)
    {
        var url = config["REDIS_URL"];
        if (string.IsNullOrWhiteSpace(url)) return null;

        // Si ya viene en formato "host:puerto,password=...", se usa tal cual
        if (!url.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
            return url;

        var uri = new Uri(url);
        var partes = new List<string> { $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)}" };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var credenciales = uri.UserInfo.Split(':', 2);
            if (credenciales.Length == 2)
            {
                if (!string.IsNullOrEmpty(credenciales[0]))
                    partes.Add($"user={Uri.UnescapeDataString(credenciales[0])}");
                partes.Add($"password={Uri.UnescapeDataString(credenciales[1])}");
            }
            else
            {
                partes.Add($"password={Uri.UnescapeDataString(credenciales[0])}");
            }
        }

        // rediss:// (con doble s) significa conexión cifrada: la usa el link externo
        if (uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
            partes.Add("ssl=true");

        partes.Add("abortConnect=false");
        return string.Join(",", partes);
    }
}