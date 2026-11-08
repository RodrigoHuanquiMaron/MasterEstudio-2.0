using System.Net.Http.Json;
using System.Text.Json;
using PlataformaCursos.Models;

namespace PlataformaCursos.Infrastructure;

// Datos de Algolia, leídos de variables de entorno (Algolia__AppId, etc.)
public class AlgoliaOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AdminKey { get; set; } = string.Empty;   // escribir en el índice (solo servidor)
    public string SearchKey { get; set; } = string.Empty;  // buscar (si falta, se usa AdminKey)
    public string IndexName { get; set; } = "talleres";

    public bool Configurado => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AdminKey);
}

// Habla con la API REST de Algolia desde el servidor.
// Las claves nunca llegan al navegador.
public class AlgoliaService
{
    private readonly HttpClient _http;
    private readonly AlgoliaOptions _opciones;
    private readonly ILogger<AlgoliaService> _logger;

    public AlgoliaService(HttpClient http, Microsoft.Extensions.Options.IOptions<AlgoliaOptions> opciones,
        ILogger<AlgoliaService> logger)
    {
        _http = http;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public bool Configurado => _opciones.Configurado;

    private string Indice => Uri.EscapeDataString(_opciones.IndexName);
    private string UrlEscritura => $"https://{_opciones.AppId}.algolia.net/1/indexes/{Indice}";
    private string UrlBusqueda => $"https://{_opciones.AppId}-dsn.algolia.net/1/indexes/{Indice}";

    private HttpRequestMessage Solicitud(HttpMethod metodo, string url, object? cuerpo, bool soloBusqueda = false)
    {
        var clave = soloBusqueda && !string.IsNullOrWhiteSpace(_opciones.SearchKey)
            ? _opciones.SearchKey
            : _opciones.AdminKey;

        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Add("X-Algolia-Application-Id", _opciones.AppId);
        solicitud.Headers.Add("X-Algolia-API-Key", clave);
        if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo);
        return solicitud;
    }

    // Lo que se guarda en Algolia por cada taller
    private static object Registro(Taller t) => new
    {
        objectID = t.Id.ToString(),
        titulo = t.Titulo,
        descripcion = t.Descripcion,
        facilitador = t.Facilitador,
        categoria = t.Categoria,
        modalidad = t.Modalidad,
        lugar = t.Lugar,
        fecha = new DateTimeOffset(DateTime.SpecifyKind(t.Fecha, DateTimeKind.Utc)).ToUnixTimeSeconds(),
        cupos = t.Cupos
    };

    // Busca en Algolia y devuelve los Id de los talleres en orden de relevancia.
    // Devuelve null si Algolia no está configurado o falla (el controlador usa la base de datos).
    public async Task<List<int>?> BuscarAsync(string texto)
    {
        if (!Configurado) return null;

        try
        {
            var parametros = $"query={Uri.EscapeDataString(texto)}&hitsPerPage=50&attributesToRetrieve=objectID";
            using var solicitud = Solicitud(HttpMethod.Post, $"{UrlBusqueda}/query", new { @params = parametros }, soloBusqueda: true);
            using var respuesta = await _http.SendAsync(solicitud);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Algolia respondió {Codigo} al buscar: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
                return null;
            }

            using var json = JsonDocument.Parse(cuerpo);
            var ids = new List<int>();
            foreach (var hit in json.RootElement.GetProperty("hits").EnumerateArray())
            {
                if (int.TryParse(hit.GetProperty("objectID").GetString(), out var id))
                    ids.Add(id);
            }

            _logger.LogInformation("ALGOLIA búsqueda \"{Texto}\" -> {Cantidad} resultados", texto, ids.Count);
            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo buscar en Algolia; se usa la base de datos");
            return null;
        }
    }

    // Crea o actualiza un taller en el índice
    public async Task GuardarAsync(Taller taller)
    {
        if (!Configurado) return;
        await EnviarAsync(HttpMethod.Put, $"{UrlEscritura}/{taller.Id}", Registro(taller),
            $"Taller {taller.Id} guardado en Algolia");
    }

    // Quita un taller del índice
    public async Task EliminarAsync(int tallerId)
    {
        if (!Configurado) return;
        await EnviarAsync(HttpMethod.Delete, $"{UrlEscritura}/{tallerId}", null,
            $"Taller {tallerId} eliminado de Algolia");
    }

    // Vacía el índice y vuelve a subir todos los talleres (botón "Sincronizar con Algolia")
    public async Task<bool> ReindexarAsync(IEnumerable<Taller> talleres)
    {
        if (!Configurado) return false;

        // 1. Qué campos se pueden buscar y en qué orden de importancia
        var ajustes = new
        {
            searchableAttributes = new[] { "titulo", "facilitador", "categoria", "descripcion", "modalidad", "lugar" },
            attributesForFaceting = new[] { "modalidad", "categoria" },
            customRanking = new[] { "asc(fecha)" }
        };
        if (!await EnviarAsync(HttpMethod.Put, $"{UrlEscritura}/settings", ajustes, "Ajustes del índice actualizados"))
            return false;

        // 2. Borrar lo que hubiera y subir todo de nuevo
        if (!await EnviarAsync(HttpMethod.Post, $"{UrlEscritura}/clear", null, "Índice vaciado"))
            return false;

        var lote = new
        {
            requests = talleres.Select(t => new { action = "addObject", body = Registro(t) }).ToArray()
        };
        if (lote.requests.Length == 0) return true;

        return await EnviarAsync(HttpMethod.Post, $"{UrlEscritura}/batch", lote,
            $"{lote.requests.Length} talleres subidos a Algolia");
    }

    private async Task<bool> EnviarAsync(HttpMethod metodo, string url, object? cuerpo, string mensajeOk)
    {
        try
        {
            using var solicitud = Solicitud(metodo, url, cuerpo);
            using var respuesta = await _http.SendAsync(solicitud);
            if (respuesta.IsSuccessStatusCode)
            {
                _logger.LogInformation("ALGOLIA {Mensaje}", mensajeOk);
                return true;
            }

            _logger.LogWarning("Algolia respondió {Codigo}: {Cuerpo}",
                (int)respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
            return false;
        }
        catch (Exception ex)
        {
            // Si Algolia falla, el taller ya quedó guardado en PostgreSQL
            _logger.LogWarning(ex, "No se pudo comunicar con Algolia");
            return false;
        }
    }
}
