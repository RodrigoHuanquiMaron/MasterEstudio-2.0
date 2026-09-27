using Microsoft.Extensions.Options;

namespace PlataformaCursos.Infrastructure;

// Datos de PieHost, leídos de variables de entorno (PieHost__ClusterId, etc.)
public class PieHostOptions
{
    public string ClusterId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Canal { get; set; } = "cursos";

    // Para escuchar en el navegador basta la API key
    public bool Configurado => !string.IsNullOrWhiteSpace(ClusterId) && !string.IsNullOrWhiteSpace(ApiKey);

    // Para publicar desde el servidor hace falta también el secreto
    public bool PuedePublicar => Configurado && !string.IsNullOrWhiteSpace(ApiSecret);

    public string UrlWebSocket =>
        $"wss://{ClusterId}.piesocket.com/v3/{Uri.EscapeDataString(Canal)}?api_key={Uri.EscapeDataString(ApiKey)}&notify_self=1";

    public string UrlPublicar => $"https://{ClusterId}.piesocket.com/api/publish";
}

// Publica eventos en PieHost desde el servidor
public class PieHostService
{
    public const string EventoCursoActualizado = "CursoActualizado";

    private readonly HttpClient _http;
    private readonly PieHostOptions _opciones;
    private readonly ILogger<PieHostService> _logger;

    public PieHostService(HttpClient http, IOptions<PieHostOptions> opciones, ILogger<PieHostService> logger)
    {
        _http = http;
        _opciones = opciones.Value;
        _logger = logger;
    }

    // accion: "Agregado" o "Eliminado"
    public async Task PublicarCursoActualizadoAsync(int cursoId, string accion, string titulo)
    {
        if (!_opciones.PuedePublicar)
        {
            _logger.LogWarning("PieHost no está configurado: no se publicó {Evento}", EventoCursoActualizado);
            return;
        }

        var cuerpo = new
        {
            key = _opciones.ApiKey,
            secret = _opciones.ApiSecret,
            roomId = _opciones.Canal,
            channelId = _opciones.Canal,
            message = new
            {
                @event = EventoCursoActualizado,
                data = new { id = cursoId, accion, titulo, fecha = DateTime.UtcNow }
            }
        };

        try
        {
            var respuesta = await _http.PostAsJsonAsync(_opciones.UrlPublicar, cuerpo);
            var texto = await respuesta.Content.ReadAsStringAsync();

            if (respuesta.IsSuccessStatusCode)
                _logger.LogInformation("PieHost: evento {Evento} publicado (curso {Id}, {Accion}). Respuesta: {Respuesta}",
                    EventoCursoActualizado, cursoId, accion, texto);
            else
                _logger.LogWarning("PieHost respondió {Codigo}: {Respuesta}", (int)respuesta.StatusCode, texto);
        }
        catch (Exception ex)
        {
            // Si PieHost falla, el curso ya quedó guardado; solo se pierde el aviso en tiempo real
            _logger.LogWarning(ex, "No se pudo publicar el evento en PieHost");
        }
    }
}