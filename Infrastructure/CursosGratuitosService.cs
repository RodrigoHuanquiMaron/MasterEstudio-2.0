using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Distributed;

namespace PlataformaCursos.Infrastructure;

public static class Fuentes
{
    public const string Microsoft = "Microsoft Learn";
    public const string YouTube = "YouTube";
}

// Un curso gratuito traído de una plataforma externa
public class CursoGratuito
{
    public string Fuente { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Imagen { get; set; }
    public string? Autor { get; set; }
    public int Minutos { get; set; }           // 0 = desconocido
    public bool MinutosEstimados { get; set; } // YouTube: se estima por la cantidad de videos
    public int? Videos { get; set; }
    public string? Nivel { get; set; }
    public double Popularidad { get; set; }

    public int HorasAproximadas => Math.Max(1, (int)Math.Ceiling(Minutos / 60.0));
}

public class ResultadoCursosGratuitos
{
    public List<CursoGratuito> Cursos { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

// Trae cursos gratuitos de Microsoft Learn (catálogo público) y de YouTube (Data API v3).
// Todo se guarda en Redis 24 horas para cargar rápido y no gastar la cuota de YouTube.
public class CursosGratuitosService
{
    private static readonly TimeSpan Duracion = TimeSpan.FromHours(24);
    private static readonly string[] UrlsMicrosoft =
    {
        "https://learn.microsoft.com/api/catalog/?type=learningPaths&locale=es-es",
        "https://learn.microsoft.com/api/learn/catalog/?type=learningPaths&locale=es-es"
    };

    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CursosGratuitosService> _logger;
    private readonly string? _youtubeKey;

    public CursosGratuitosService(HttpClient http, IDistributedCache cache, IConfiguration config,
        ILogger<CursosGratuitosService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _youtubeKey = config["YouTube:ApiKey"];
    }

    public bool YouTubeConfigurado => !string.IsNullOrWhiteSpace(_youtubeKey);

    public async Task<ResultadoCursosGratuitos> BuscarAsync(string? texto, string? fuente)
    {
        var resultado = new ResultadoCursosGratuitos();
        texto = texto?.Trim();

        var microsoft = new List<CursoGratuito>();
        var youtube = new List<CursoGratuito>();

        if (fuente is null or "" or Fuentes.Microsoft)
        {
            var catalogo = await CatalogoMicrosoftAsync();
            if (catalogo is null)
                resultado.Avisos.Add("Microsoft Learn no respondió en este momento. Inténtalo más tarde.");
            else
                microsoft = FiltrarMicrosoft(catalogo, texto);
        }

        if (fuente is null or "" or Fuentes.YouTube)
        {
            if (!YouTubeConfigurado)
                resultado.Avisos.Add("Los cursos de YouTube no están activos: falta la clave YouTube__ApiKey.");
            else
            {
                var consulta = string.IsNullOrEmpty(texto) ? "curso completo gratis programación" : $"curso {texto}";
                var lista = await BuscarYouTubeAsync(consulta);
                if (lista is null)
                    resultado.Avisos.Add("YouTube no respondió o se agotó la cuota diaria. Inténtalo más tarde.");
                else
                    youtube = lista;
            }
        }

        // Se intercalan las fuentes para que se vean ambas
        for (var i = 0; i < Math.Max(microsoft.Count, youtube.Count); i++)
        {
            if (i < microsoft.Count) resultado.Cursos.Add(microsoft[i]);
            if (i < youtube.Count) resultado.Cursos.Add(youtube[i]);
        }
        return resultado;
    }

    // ---------- Microsoft Learn ----------

    private async Task<List<CursoGratuito>?> CatalogoMicrosoftAsync()
    {
        const string clave = "gratis:mslearn:es";
        var guardado = await LeerCacheAsync(clave);
        if (guardado is not null)
        {
            _logger.LogInformation("CURSOS GRATIS: Microsoft Learn desde caché ({Cantidad})", guardado.Count);
            return guardado;
        }

        foreach (var url in UrlsMicrosoft)
        {
            try
            {
                using var respuesta = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!respuesta.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Microsoft Learn respondió {Codigo} en {Url}", (int)respuesta.StatusCode, url);
                    continue;
                }

                await using var stream = await respuesta.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);
                if (!json.RootElement.TryGetProperty("learningPaths", out var rutas)) continue;

                var lista = new List<CursoGratuito>();
                foreach (var r in rutas.EnumerateArray())
                {
                    var titulo = Cadena(r, "title");
                    var enlace = Cadena(r, "url");
                    if (string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(enlace)) continue;

                    lista.Add(new CursoGratuito
                    {
                        Fuente = Fuentes.Microsoft,
                        Id = Cadena(r, "uid") ?? enlace,
                        Titulo = titulo,
                        Descripcion = Resumir(Cadena(r, "summary")),
                        Url = QuitarSeguimiento(enlace),
                        Imagen = Cadena(r, "social_image_url") ?? Cadena(r, "icon_url"),
                        Autor = "Microsoft",
                        Minutos = r.TryGetProperty("duration_in_minutes", out var d) && d.TryGetInt32(out var m) ? m : 0,
                        Nivel = Nivel(r),
                        Popularidad = r.TryGetProperty("popularity", out var p) && p.TryGetDouble(out var v) ? v : 0
                    });
                }

                lista = lista.OrderByDescending(c => c.Popularidad).ToList();
                await GuardarCacheAsync(clave, lista);
                _logger.LogInformation("CURSOS GRATIS: {Cantidad} rutas descargadas de Microsoft Learn", lista.Count);
                return lista;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el catálogo de Microsoft Learn ({Url})", url);
            }
        }
        return null;
    }

    private static List<CursoGratuito> FiltrarMicrosoft(List<CursoGratuito> catalogo, string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return catalogo.Take(12).ToList();

        var palabras = SinTildes(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 1).ToList();
        return catalogo
            .Select(c => new
            {
                Curso = c,
                Titulo = SinTildes(c.Titulo),
                Texto = SinTildes(c.Titulo + " " + c.Descripcion)
            })
            .Where(x => palabras.All(p => x.Texto.Contains(p)))
            .OrderByDescending(x => palabras.Count(p => x.Titulo.Contains(p)))
            .ThenByDescending(x => x.Curso.Popularidad)
            .Take(12)
            .Select(x => x.Curso)
            .ToList();
    }

    private static string? Nivel(JsonElement r)
    {
        if (!r.TryGetProperty("levels", out var niveles) || niveles.ValueKind != JsonValueKind.Array) return null;
        var ids = niveles.EnumerateArray().Select(n => n.GetString()).ToList();
        if (ids.Contains("beginner")) return "Principiante";
        if (ids.Contains("intermediate")) return "Intermedio";
        if (ids.Contains("advanced")) return "Avanzado";
        return null;
    }

    // ---------- YouTube ----------

    private async Task<List<CursoGratuito>?> BuscarYouTubeAsync(string consulta)
    {
        var clave = "gratis:yt:" + SinTildes(consulta);
        var guardado = await LeerCacheAsync(clave);
        if (guardado is not null)
        {
            _logger.LogInformation("CURSOS GRATIS: YouTube \"{Consulta}\" desde caché", consulta);
            return guardado;
        }

        try
        {
            // 1. Buscar listas de reproducción (cuesta 100 unidades de la cuota diaria de 10 000)
            var urlBusqueda = "https://www.googleapis.com/youtube/v3/search?part=snippet&type=playlist&maxResults=12" +
                              "&relevanceLanguage=es&safeSearch=strict" +
                              $"&q={Uri.EscapeDataString(consulta)}&key={Uri.EscapeDataString(_youtubeKey!)}";
            using var respuesta = await _http.GetAsync(urlBusqueda);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube respondió {Codigo}: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
                return null;
            }

            var lista = new List<CursoGratuito>();
            using (var json = JsonDocument.Parse(cuerpo))
            {
                foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (!item.GetProperty("id").TryGetProperty("playlistId", out var idNodo)) continue;
                    var id = idNodo.GetString();
                    var s = item.GetProperty("snippet");
                    if (string.IsNullOrEmpty(id)) continue;

                    string? imagen = null;
                    if (s.TryGetProperty("thumbnails", out var miniaturas))
                        foreach (var tam in new[] { "high", "medium", "default" })
                            if (imagen is null && miniaturas.TryGetProperty(tam, out var t))
                                imagen = Cadena(t, "url");

                    lista.Add(new CursoGratuito
                    {
                        Fuente = Fuentes.YouTube,
                        Id = id,
                        Titulo = WebUtility.HtmlDecode(Cadena(s, "title") ?? ""),
                        Descripcion = Resumir(WebUtility.HtmlDecode(Cadena(s, "description") ?? "")),
                        Url = $"https://www.youtube.com/playlist?list={id}",
                        Imagen = imagen,
                        Autor = WebUtility.HtmlDecode(Cadena(s, "channelTitle") ?? "")
                    });
                }
            }

            // 2. Cantidad de videos de cada lista (cuesta 1 unidad)
            if (lista.Count > 0)
            {
                var ids = string.Join(",", lista.Select(c => c.Id));
                var urlListas = $"https://www.googleapis.com/youtube/v3/playlists?part=contentDetails&maxResults=50&id={ids}&key={Uri.EscapeDataString(_youtubeKey!)}";
                using var r2 = await _http.GetAsync(urlListas);
                if (r2.IsSuccessStatusCode)
                {
                    using var json2 = JsonDocument.Parse(await r2.Content.ReadAsStringAsync());
                    foreach (var item in json2.RootElement.GetProperty("items").EnumerateArray())
                    {
                        var curso = lista.FirstOrDefault(c => c.Id == Cadena(item, "id"));
                        if (curso is null) continue;
                        if (item.GetProperty("contentDetails").TryGetProperty("itemCount", out var n) && n.TryGetInt32(out var videos))
                        {
                            curso.Videos = videos;
                            curso.Minutos = videos * 15;   // estimado: 15 minutos por video
                            curso.MinutosEstimados = true;
                        }
                    }
                }
                // Se descartan listas con muy pocos videos (no suelen ser cursos)
                lista = lista.Where(c => c.Videos is null or >= 5).ToList();
            }

            await GuardarCacheAsync(clave, lista);
            _logger.LogInformation("CURSOS GRATIS: YouTube \"{Consulta}\" -> {Cantidad} listas", consulta, lista.Count);
            return lista;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar YouTube");
            return null;
        }
    }

    // ---------- Utilidades ----------

    private async Task<List<CursoGratuito>?> LeerCacheAsync(string clave)
    {
        try
        {
            var json = await _cache.GetStringAsync(clave);
            return json is null ? null : JsonSerializer.Deserialize<List<CursoGratuito>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer {Clave} de la caché", clave);
            return null;
        }
    }

    private async Task GuardarCacheAsync(string clave, List<CursoGratuito> lista)
    {
        try
        {
            await _cache.SetStringAsync(clave, JsonSerializer.Serialize(lista),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duracion });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar {Clave} en la caché", clave);
        }
    }

    private static string? Cadena(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // Quita etiquetas HTML y recorta la descripción
    private static string? Resumir(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(texto, "<.*?>", " ")), @"\s+", " ").Trim();
        return limpio.Length > 220 ? limpio[..220].TrimEnd() + "…" : limpio;
    }

    private static string QuitarSeguimiento(string url)
    {
        var i = url.IndexOf('?');
        return i > 0 ? url[..i] : url;
    }

    public static string SinTildes(string texto)
    {
        var normal = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normal)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    // Solo se aceptan enlaces de estas plataformas al agregar un curso a "Mis cursos"
    public static string? FuenteDeUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        if (uri.Host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase)) return Fuentes.Microsoft;
        if (uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase))
            return Fuentes.YouTube;
        return null;
    }
}
