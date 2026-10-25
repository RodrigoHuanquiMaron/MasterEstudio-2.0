using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PlataformaCursos.Infrastructure;

public static class Sentimientos
{
    public const string Positiva = "Positiva";
    public const string Negativa = "Negativa";
    public const string Neutral = "Neutral";
    public const string SinMensaje = "Sin mensaje";
    public const string SinAnalizar = "Sin analizar";
}

public record ResultadoSentimiento(string Etiqueta, double? Confianza);

// Analiza el mensaje de una donación con un modelo de machine learning de Hugging Face
// (clasificación de texto / análisis de sentimiento, gratuito con un token personal).
public class SentimientoService
{
    private const string ModeloPorDefecto = "tabularisai/multilingual-sentiment-analysis";

    private readonly HttpClient _http;
    private readonly ILogger<SentimientoService> _logger;
    private readonly string? _token;
    private readonly string _modelo;

    public SentimientoService(HttpClient http, IConfiguration config, ILogger<SentimientoService> logger)
    {
        _http = http;
        _logger = logger;
        _token = config["HuggingFace:Token"];
        var modelo = config["HuggingFace:Model"];
        _modelo = string.IsNullOrWhiteSpace(modelo) ? ModeloPorDefecto : modelo;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_token);

    public async Task<ResultadoSentimiento> AnalizarAsync(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new ResultadoSentimiento(Sentimientos.SinMensaje, null);

        if (!Configurado)
        {
            _logger.LogWarning("Hugging Face no está configurado (falta HuggingFace:Token); la donación queda sin analizar");
            return new ResultadoSentimiento(Sentimientos.SinAnalizar, null);
        }

        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post,
                $"https://router.huggingface.co/hf-inference/models/{_modelo}");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            solicitud.Content = JsonContent.Create(new { inputs = texto });

            using var respuesta = await _http.SendAsync(solicitud);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hugging Face respondió {Codigo}: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
                return new ResultadoSentimiento(Sentimientos.SinAnalizar, null);
            }

            // La respuesta es [[{label, score}, ...]] o [{label, score}, ...]
            using var json = JsonDocument.Parse(cuerpo);
            var lista = json.RootElement;
            if (lista.ValueKind == JsonValueKind.Array && lista.GetArrayLength() > 0 &&
                lista[0].ValueKind == JsonValueKind.Array)
                lista = lista[0];

            string? mejorEtiqueta = null;
            double mejorPuntaje = -1;
            foreach (var item in lista.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var etiqueta = item.GetProperty("label").GetString();
                var puntaje = item.GetProperty("score").GetDouble();
                if (puntaje > mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorEtiqueta = etiqueta;
                }
            }

            if (mejorEtiqueta is null)
                return new ResultadoSentimiento(Sentimientos.SinAnalizar, null);

            var resultado = new ResultadoSentimiento(Traducir(mejorEtiqueta), Math.Round(mejorPuntaje, 3));
            _logger.LogInformation("Sentimiento de la donación: {Etiqueta} ({Original}, confianza {Confianza})",
                resultado.Etiqueta, mejorEtiqueta, resultado.Confianza);
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo analizar el sentimiento con Hugging Face");
            return new ResultadoSentimiento(Sentimientos.SinAnalizar, null);
        }
    }

    // Convierte la etiqueta del modelo (Very Negative, Positive, 1 star, LABEL_2...) a español
    private static string Traducir(string etiqueta)
    {
        var e = etiqueta.ToLowerInvariant();
        if (e.Contains("neg")) return Sentimientos.Negativa;
        if (e.Contains("pos")) return Sentimientos.Positiva;
        if (e.Contains("neu")) return Sentimientos.Neutral;
        if (e.StartsWith("1") || e.StartsWith("2")) return Sentimientos.Negativa;
        if (e.StartsWith("4") || e.StartsWith("5")) return Sentimientos.Positiva;
        return Sentimientos.Neutral;
    }
}
