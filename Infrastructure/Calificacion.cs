namespace PlataformaCursos.Infrastructure;

// Ayudas de presentación para calificaciones, estados y categorías
public static class Calificacion
{
    public static string Letra(decimal? nota) => nota switch
    {
        null => "N/A",
        >= 93m => "A",
        >= 90m => "A-",
        >= 87m => "B+",
        >= 83m => "B",
        >= 80m => "B-",
        >= 77m => "C+",
        >= 70m => "C",
        >= 60m => "D",
        _ => "F"
    };

    // Ejemplo: "A- (91.4%)"
    public static string Texto(decimal? nota) =>
        nota is null ? "N/A" : $"{Letra(nota)} ({nota.Value:0.0}%)";

    public static string Estado(int progreso) =>
        progreso >= 100 ? "Completado" : progreso > 0 ? "En curso" : "No iniciado";

    public static string ClaseEstado(int progreso) =>
        progreso >= 100 ? "estado-completado" : progreso > 0 ? "estado-encurso" : "estado-noiniciado";

    public static string ClaseCategoria(string? categoria) => (categoria ?? "").ToLowerInvariant() switch
    {
        "matemática" or "matematica" => "cat-matematica",
        "tecnología" or "tecnologia" => "cat-tecnologia",
        "lógica" or "logica" => "cat-logica",
        "contabilidad" => "cat-contabilidad",
        "idiomas" => "cat-idiomas",
        _ => "cat-otros"
    };

    public static string IconoCategoria(string? categoria) => ClaseCategoria(categoria) switch
    {
        "cat-matematica" => "bi-calculator",
        "cat-tecnologia" => "bi-cpu",
        "cat-logica" => "bi-puzzle",
        "cat-contabilidad" => "bi-graph-up-arrow",
        "cat-idiomas" => "bi-translate",
        _ => "bi-journal-bookmark"
    };

    // Ejemplo: 5400 segundos -> "1 h 30 min"
    public static string Duracion(int segundos)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, segundos));
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} h {t.Minutes} min";
        if (t.TotalMinutes >= 1) return $"{t.Minutes} min";
        return $"{t.Seconds} s";
    }

    public static string Horas(double horas) => $"{horas:0.#} hrs";

    public static string Iniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "?";
        var partes = nombre.Split(new[] { ' ', '@', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var iniciales = string.Concat(partes.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        return iniciales.Length == 0 ? "?" : iniciales;
    }

    public static string ClaseSentimiento(string? sentimiento) => sentimiento switch
    {
        Sentimientos.Positiva => "sent-positiva",
        Sentimientos.Negativa => "sent-negativa",
        Sentimientos.Neutral => "sent-neutral",
        _ => "sent-otro"
    };
}
