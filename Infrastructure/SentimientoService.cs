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

// Analiza el mensaje de una donación (positivo, negativo o neutral).
// 1. Intenta con un modelo de clasificación de Hugging Face.
// 2. Si Hugging Face falla o no está configurado, usa Groq como respaldo.
public class SentimientoService
{
    private const string ModeloHfPorDefecto = "cardiffnlp/twitter-xlm-roberta-base-sentiment";
    private const string ModeloGroqPorDefecto = "openai/gpt-oss-120b";

    private readonly HttpClient _http;
    private readonly ILogger<SentimientoService> _logger;
    private readonly string? _tokenHf;
    private readonly string _modeloHf;
    private readonly string? _claveGroq;
    private readonly string _modeloGroq;

    public SentimientoService(HttpClient http, IConfiguration config, ILogger<SentimientoService> logger)
    {
        _http = http;
        _logger = logger;
        _tokenHf = config["HuggingFace:Token"];
        _modeloHf = string.IsNullOrWhiteSpace(config["HuggingFace:Model"]) ? ModeloHfPorDefecto : config["HuggingFace:Model"]!;
        _claveGroq = config["Groq:ApiKey"];
        _modeloGroq = string.IsNullOrWhiteSpace(config["Groq:Model"]) ? ModeloGroqPorDefecto : config["Groq:Model"]!;
    }

    private bool HfConfigurado => !string.IsNullOrWhiteSpace(_tokenHf);
    private bool GroqConfigurado => !string.IsNullOrWhiteSpace(_claveGroq);
    public bool Configurado => HfConfigurado || GroqConfigurado;

    public async Task<ResultadoSentimiento> AnalizarAsync(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new ResultadoSentimiento(Sentimientos.SinMensaje, null);

        if (!Configurado)
        {
            _logger.LogWarning("Análisis de sentimiento sin configurar: falta HuggingFace:Token o Groq:ApiKey");
            return new ResultadoSentimiento(Sentimientos.SinAnalizar, null);
        }

        if (HfConfigurado)
        {
            var hf = await ConHuggingFaceAsync(texto);
            if (hf is not null) return hf;
        }

        if (GroqConfigurado)
        {
            var groq = await ConGroqAsync(texto);
            if (groq is not null) return groq;
        }

        return new ResultadoSentimiento(Sentimientos.SinAnalizar, null);
    }

    // ---------- 1. Hugging Face (modelo de clasificación de texto) ----------

    private async Task<ResultadoSentimiento?> ConHuggingFaceAsync(string texto)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post,
                $"https://router.huggingface.co/hf-inference/models/{_modeloHf}");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenHf);
            solicitud.Content = JsonContent.Create(new { inputs = texto });

            using var respuesta = await _http.SendAsync(solicitud);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hugging Face respondió {Codigo} con el modelo {Modelo}: {Cuerpo}",
                    (int)respuesta.StatusCode, _modeloHf, cuerpo);
                return null;
            }

            // La respuesta es [[{label, score}, ...]] o [{label, score}, ...]
            using var json = JsonDocument.Parse(cuerpo);
            var lista = json.RootElement;
            if (lista.ValueKind == JsonValueKind.Array && lista.GetArrayLength() > 0 &&
                lista[0].ValueKind == JsonValueKind.Array)
                lista = lista[0];
            if (lista.ValueKind != JsonValueKind.Array) return null;

            string? mejorEtiqueta = null;
            double mejorPuntaje = -1;
            foreach (var item in lista.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var puntaje = item.GetProperty("score").GetDouble();
                if (puntaje > mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorEtiqueta = item.GetProperty("label").GetString();
                }
            }
            if (mejorEtiqueta is null) return null;

            var resultado = new ResultadoSentimiento(Traducir(mejorEtiqueta), Math.Round(mejorPuntaje, 3));
            _logger.LogInformation("SENTIMIENTO (Hugging Face): {Etiqueta} ({Original}, confianza {Confianza})",
                resultado.Etiqueta, mejorEtiqueta, resultado.Confianza);
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo analizar el sentimiento con Hugging Face");
            return null;
        }
    }

    // ---------- 2. Groq (modelo de lenguaje) como respaldo ----------

    private async Task<ResultadoSentimiento?> ConGroqAsync(string texto)
    {
        try
        {
            var cuerpo = new Dictionary<string, object>
            {
                ["model"] = _modeloGroq,
                ["messages"] = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Clasificas el sentimiento de mensajes que los alumnos dejan al donar a una plataforma educativa. " +
                                  "Responde SOLO con JSON: {\"sentimiento\":\"positivo|negativo|neutral\",\"confianza\":0.0-1.0}. " +
                                  "Una queja, crítica o insulto es negativo; un elogio o agradecimiento es positivo; " +
                                  "una sugerencia o pedido sin emoción clara es neutral."
                    },
                    new { role = "user", content = texto.Length > 500 ? texto[..500] : texto }
                },
                ["temperature"] = 0,
                ["max_completion_tokens"] = 300,
                ["response_format"] = new { type = "json_object" }
            };
            if (_modeloGroq.StartsWith("openai/gpt-oss", StringComparison.OrdinalIgnoreCase))
                cuerpo["reasoning_effort"] = "low";

            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _claveGroq);
            solicitud.Content = JsonContent.Create(cuerpo);

            using var respuesta = await _http.SendAsync(solicitud);
            var texto2 = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq respondió {Codigo} al analizar sentimiento: {Cuerpo}", (int)respuesta.StatusCode, texto2);
                return null;
            }

            using var doc = JsonDocument.Parse(texto2);
            var contenido = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            var inicio = contenido.IndexOf('{');
            var fin = contenido.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio) return null;

            using var json = JsonDocument.Parse(contenido[inicio..(fin + 1)]);
            var etiqueta = json.RootElement.TryGetProperty("sentimiento", out var s) ? s.GetString() : null;
            double? confianza = json.RootElement.TryGetProperty("confianza", out var c) && c.TryGetDouble(out var v)
                ? Math.Round(Math.Clamp(v, 0, 1), 3) : null;
            if (string.IsNullOrWhiteSpace(etiqueta)) return null;

            var resultado = new ResultadoSentimiento(Traducir(etiqueta), confianza);
            _logger.LogInformation("SENTIMIENTO (Groq): {Etiqueta} (confianza {Confianza})", resultado.Etiqueta, resultado.Confianza);
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo analizar el sentimiento con Groq");
            return null;
        }
    }

    // Convierte la etiqueta del modelo (negative, Positive, 1 star, LABEL_2, positivo...) a español
    private static string Traducir(string etiqueta)
    {
        var e = etiqueta.ToLowerInvariant();
        if (e.Contains("neg")) return Sentimientos.Negativa;
        if (e.Contains("pos")) return Sentimientos.Positiva;
        if (e.Contains("neu")) return Sentimientos.Neutral;
        if (e == "label_0" || e.StartsWith("1") || e.StartsWith("2")) return Sentimientos.Negativa;
        if (e == "label_2" || e.StartsWith("4") || e.StartsWith("5")) return Sentimientos.Positiva;
        return Sentimientos.Neutral;
    }
}
