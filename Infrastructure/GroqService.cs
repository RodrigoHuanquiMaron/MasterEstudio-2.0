using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlataformaCursos.Infrastructure;

// ---------- Datos que se intercambian con la página ----------

public class PreguntaQuiz
{
    public string Enunciado { get; set; } = string.Empty;
    public List<string> Opciones { get; set; } = new();
    public int Correcta { get; set; }
    public string Explicacion { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
}

public class MensajeChat
{
    public string Rol { get; set; } = "user";        // "user" o "assistant"
    public string Texto { get; set; } = string.Empty;
}

public class RespuestaTutor
{
    public string Respuesta { get; set; } = string.Empty;
    public List<string> Temas { get; set; } = new();
}

// Error con un mensaje que se puede mostrar al alumno
public class GroqException : Exception
{
    public GroqException(string mensaje) : base(mensaje) { }
}

// Habla con la API de Groq (compatible con OpenAI) desde el servidor.
// La clave nunca llega al navegador.
public class GroqService
{
    private const string Url = "https://api.groq.com/openai/v1/chat/completions";
    private const string ModeloPorDefecto = "openai/gpt-oss-120b";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly ILogger<GroqService> _logger;
    private readonly string? _apiKey;
    private readonly string _modelo;

    public GroqService(HttpClient http, IConfiguration config, ILogger<GroqService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["Groq:ApiKey"];
        var modelo = config["Groq:Model"];
        _modelo = string.IsNullOrWhiteSpace(modelo) ? ModeloPorDefecto : modelo;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_apiKey);

    // ---------- Preguntas de opción múltiple ----------

    public async Task<List<PreguntaQuiz>> GenerarPreguntasAsync(string texto, int cantidad, string dificultad,
        IReadOnlyCollection<string> yaHechas)
    {
        cantidad = Math.Clamp(cantidad, 1, 10);

        var evitar = yaHechas.Count == 0
            ? ""
            : "\nNo repitas ni reformules estas preguntas que el alumno ya respondió:\n- " +
              string.Join("\n- ", yaHechas.TakeLast(25));

        var sistema =
            "Eres un profesor universitario que prepara cuestionarios en español para que un alumno repase. " +
            "Respondes SOLO con un objeto JSON válido, sin texto adicional.";

        var usuario =
            $"Con el siguiente material de estudio crea exactamente {cantidad} preguntas de opción múltiple de dificultad {dificultad}.\n" +
            "Reglas:\n" +
            "- Cada pregunta tiene exactamente 4 opciones y solo una es correcta.\n" +
            "- Las opciones incorrectas deben ser creíbles, no absurdas.\n" +
            "- Basa todo en el material; no inventes datos que no aparezcan en él.\n" +
            "- \"correcta\" es el índice (0 a 3) de la opción correcta.\n" +
            "- \"explicacion\": 1 o 2 frases que expliquen por qué es correcta.\n" +
            "- \"tema\": el concepto que evalúa la pregunta, en 2 a 5 palabras.\n" +
            evitar +
            "\nFormato exacto:\n" +
            "{\"preguntas\":[{\"enunciado\":\"...\",\"opciones\":[\"...\",\"...\",\"...\",\"...\"],\"correcta\":0,\"explicacion\":\"...\",\"tema\":\"...\"}]}\n\n" +
            "MATERIAL DE ESTUDIO:\n" + texto;

        var contenido = await ChatAsync(new[] { ("system", sistema), ("user", usuario) }, json: true, maxTokens: 4000);
        var raiz = LeerObjeto(contenido);

        var lista = new List<PreguntaQuiz>();
        if (raiz?["preguntas"] is not JsonArray preguntas) return lista;

        foreach (var nodo in preguntas)
        {
            try
            {
                var p = nodo.Deserialize<PreguntaQuiz>(Json);
                if (p is null || string.IsNullOrWhiteSpace(p.Enunciado)) continue;

                p.Opciones = p.Opciones.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
                if (p.Opciones.Count < 2 || p.Correcta < 0 || p.Correcta >= p.Opciones.Count) continue;

                Mezclar(p);
                lista.Add(p);
            }
            catch (JsonException) { /* se descarta la pregunta mal formada */ }
        }

        _logger.LogInformation("GROQ: {Cantidad} preguntas generadas ({Dificultad})", lista.Count, dificultad);
        return lista;
    }

    // Cambia el orden de las opciones para que la correcta no quede siempre en la misma posición
    private static void Mezclar(PreguntaQuiz p)
    {
        var correcta = p.Opciones[p.Correcta];
        p.Opciones = p.Opciones.OrderBy(_ => Random.Shared.Next()).ToList();
        p.Correcta = p.Opciones.IndexOf(correcta);
    }

    // ---------- Tutor para profundizar ----------

    public async Task<RespuestaTutor> ExplicarAsync(string? material, IEnumerable<MensajeChat> historial, string mensaje)
    {
        var sistema =
            "Eres un tutor universitario paciente que explica en español, con lenguaje sencillo y ejemplos cotidianos. " +
            "Usa párrafos cortos y, si ayuda, listas con guiones. Puedes usar **negritas** para los conceptos clave. " +
            "Si el alumno pregunta algo que no tiene que ver con estudiar, redirígelo amablemente a sus temas de estudio. " +
            "Responde SOLO con un objeto JSON: {\"respuesta\":\"tu explicación\",\"temas\":[\"2 a 4 temas relacionados para buscar más información\"]}.";

        if (!string.IsNullOrWhiteSpace(material))
            sistema += "\n\nMaterial que está estudiando el alumno (úsalo como referencia principal):\n" + material;

        var mensajes = new List<(string, string)> { ("system", sistema) };
        foreach (var m in historial.TakeLast(8))
        {
            var rol = m.Rol == "assistant" ? "assistant" : "user";
            var texto = m.Texto.Length > 1500 ? m.Texto[..1500] : m.Texto;
            mensajes.Add((rol, texto));
        }
        mensajes.Add(("user", mensaje));

        var contenido = await ChatAsync(mensajes, json: true, maxTokens: 2500);
        var raiz = LeerObjeto(contenido);

        var respuesta = new RespuestaTutor
        {
            Respuesta = Texto(raiz?["respuesta"]) ?? contenido,
            Temas = raiz?["temas"] is JsonArray temas
                ? temas.Select(t => Texto(t) ?? "").Where(t => t.Length > 0).Take(4).ToList()
                : new List<string>()
        };
        return respuesta;
    }

    // ---------- Llamada a la API ----------

    private async Task<string> ChatAsync(IEnumerable<(string Rol, string Texto)> mensajes, bool json, int maxTokens)
    {
        if (!Configurado)
            throw new GroqException("El tutor de IA no está configurado: falta la clave Groq__ApiKey.");

        var lista = mensajes.ToList();
        var (codigo, cuerpo) = await EnviarAsync(lista, json, maxTokens);

        // Si el modo JSON falla, se reintenta una vez sin él y se extrae el JSON del texto
        if (codigo == HttpStatusCode.BadRequest && json)
            (codigo, cuerpo) = await EnviarAsync(lista, false, maxTokens);

        if (codigo != HttpStatusCode.OK)
        {
            _logger.LogWarning("Groq respondió {Codigo}: {Cuerpo}", (int)codigo, cuerpo);
            throw new GroqException(codigo switch
            {
                HttpStatusCode.Unauthorized => "La clave de Groq no es válida. Revisa la variable Groq__ApiKey.",
                HttpStatusCode.TooManyRequests => "Se alcanzó el límite gratuito de Groq por ahora. Espera un minuto e inténtalo de nuevo.",
                HttpStatusCode.RequestEntityTooLarge => "El documento es demasiado largo para procesarlo de una vez.",
                _ => "El servicio de IA no respondió correctamente. Inténtalo de nuevo en unos segundos."
            });
        }

        using var doc = JsonDocument.Parse(cuerpo);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<(HttpStatusCode, string)> EnviarAsync(List<(string Rol, string Texto)> mensajes, bool json, int maxTokens)
    {
        var cuerpo = new Dictionary<string, object>
        {
            ["model"] = _modelo,
            ["messages"] = mensajes.Select(m => new { role = m.Rol, content = m.Texto }).ToArray(),
            ["temperature"] = 0.5,
            ["max_completion_tokens"] = maxTokens
        };
        if (json) cuerpo["response_format"] = new { type = "json_object" };

        // Los modelos que "razonan" gastan menos tokens con esfuerzo bajo
        if (_modelo.StartsWith("openai/gpt-oss", StringComparison.OrdinalIgnoreCase))
            cuerpo["reasoning_effort"] = "low";

        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, Url);
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            solicitud.Content = JsonContent.Create(cuerpo);

            using var respuesta = await _http.SendAsync(solicitud);
            return (respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }
        catch (TaskCanceledException)
        {
            throw new GroqException("La IA tardó demasiado en responder. Inténtalo de nuevo.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar con Groq");
            throw new GroqException("No se pudo conectar con el servicio de IA.");
        }
    }

    private static string? Texto(JsonNode? nodo) =>
        nodo is JsonValue valor && valor.TryGetValue<string>(out var texto) ? texto : nodo?.ToString();

    // Lee un objeto JSON aunque el modelo lo haya rodeado de texto o de ```json
    private static JsonObject? LeerObjeto(string texto)
    {
        var inicio = texto.IndexOf('{');
        var fin = texto.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio) return null;

        try { return JsonNode.Parse(texto[inicio..(fin + 1)]) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
