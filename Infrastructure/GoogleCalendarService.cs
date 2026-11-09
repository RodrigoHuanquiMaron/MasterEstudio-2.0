using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCursos.Data;
using PlataformaCursos.Models;

namespace PlataformaCursos.Infrastructure;

public record EventoCreado(string Id, string? Url);

// Conecta la cuenta de Google del alumno (OAuth 2.0) y crea o borra eventos en su Google Calendar.
// Usa las mismas credenciales del login con Google: Authentication__Google__ClientId / ClientSecret.
public class GoogleCalendarService
{
    public const string Alcances = "openid email https://www.googleapis.com/auth/calendar.events";
    private const string UrlToken = "https://oauth2.googleapis.com/token";
    private const string UrlEventos = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly IDataProtector _protector;
    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly string? _clientId;
    private readonly string? _clientSecret;

    public GoogleCalendarService(HttpClient http, ApplicationDbContext db, IDistributedCache cache,
        IDataProtectionProvider proteccion, IConfiguration config, ILogger<GoogleCalendarService> logger)
    {
        _http = http;
        _db = db;
        _cache = cache;
        _protector = proteccion.CreateProtector("MasterEstudio.GoogleCalendar");
        _logger = logger;
        _clientId = config["Authentication:Google:ClientId"];
        _clientSecret = config["Authentication:Google:ClientSecret"];
    }

    public bool Disponible => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    // ---------- Conexión (OAuth) ----------

    public string UrlAutorizacion(string urlRetorno, string estado) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(_clientId!)}" +
        $"&redirect_uri={Uri.EscapeDataString(urlRetorno)}" +
        "&response_type=code" +
        $"&scope={Uri.EscapeDataString(Alcances)}" +
        "&access_type=offline&prompt=consent&include_granted_scopes=true" +
        $"&state={Uri.EscapeDataString(estado)}";

    // Cambia el código que devuelve Google por los tokens y guarda la conexión
    public async Task<bool> CompletarConexionAsync(string usuarioId, string codigo, string urlRetorno)
    {
        var respuesta = await _http.PostAsync(UrlToken, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = codigo,
            ["client_id"] = _clientId!,
            ["client_secret"] = _clientSecret!,
            ["redirect_uri"] = urlRetorno,
            ["grant_type"] = "authorization_code"
        }));
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google rechazó el código de autorización: {Cuerpo}", cuerpo);
            return false;
        }

        using var json = JsonDocument.Parse(cuerpo);
        var raiz = json.RootElement;
        var refresh = raiz.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        if (string.IsNullOrEmpty(refresh))
        {
            _logger.LogWarning("Google no devolvió refresh_token");
            return false;
        }

        var correo = raiz.TryGetProperty("id_token", out var idToken) ? CorreoDelIdToken(idToken.GetString()) : null;

        var conexion = await _db.ConexionesGoogle.FindAsync(usuarioId);
        if (conexion is null)
        {
            conexion = new ConexionGoogle { UsuarioId = usuarioId };
            _db.ConexionesGoogle.Add(conexion);
        }
        conexion.RefreshTokenCifrado = _protector.Protect(refresh);
        conexion.Correo = correo;
        conexion.FechaConexion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (raiz.TryGetProperty("access_token", out var a) && raiz.TryGetProperty("expires_in", out var e))
            await GuardarAccessTokenAsync(usuarioId, a.GetString()!, e.GetInt32());

        _logger.LogInformation("GOOGLE CALENDAR: cuenta conectada ({Correo})", correo);
        return true;
    }

    public async Task DesconectarAsync(string usuarioId)
    {
        var conexion = await _db.ConexionesGoogle.FindAsync(usuarioId);
        if (conexion is null) return;

        try
        {
            var refresh = _protector.Unprotect(conexion.RefreshTokenCifrado);
            await _http.PostAsync("https://oauth2.googleapis.com/revoke",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refresh }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo revocar el acceso en Google");
        }

        _db.ConexionesGoogle.Remove(conexion);
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync(ClaveToken(usuarioId));
    }

    // ---------- Eventos ----------

    public async Task<EventoCreado?> CrearEventoAsync(string usuarioId, Recordatorio r, string? nombreCurso)
    {
        var token = await AccessTokenAsync(usuarioId);
        if (token is null) return null;

        var inicio = new DateTimeOffset(r.Fecha.ToLocalTime());
        var fin = inicio.AddMinutes(r.DuracionMinutos);

        var descripcion = new StringBuilder();
        descripcion.AppendLine($"{r.Tipo}{(nombreCurso is null ? "" : " · " + nombreCurso)}");
        if (!string.IsNullOrWhiteSpace(r.Notas)) descripcion.AppendLine().AppendLine(r.Notas);
        descripcion.AppendLine().Append("Creado desde MasterEstudio");

        var avisos = new List<object>();
        if (r.AvisoMinutosAntes > 0)
        {
            avisos.Add(new { method = "popup", minutes = r.AvisoMinutosAntes });
            avisos.Add(new { method = "email", minutes = r.AvisoMinutosAntes });
        }

        var evento = new
        {
            summary = $"{Emoji(r.Tipo)} {r.Titulo}",
            description = descripcion.ToString(),
            start = new { dateTime = inicio.ToString("yyyy-MM-dd'T'HH:mm:sszzz") },
            end = new { dateTime = fin.ToString("yyyy-MM-dd'T'HH:mm:sszzz") },
            reminders = new { useDefault = false, overrides = avisos },
            colorId = r.Tipo switch { "Examen" => "11", "Entrega" => "6", "Exposición" => "3", "Estudio" => "9", _ => "8" }
        };

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, UrlEventos);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Content = JsonContent.Create(evento);

        using var respuesta = await _http.SendAsync(solicitud);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google Calendar respondió {Codigo} al crear el evento: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
            return null;
        }

        using var json = JsonDocument.Parse(cuerpo);
        var id = json.RootElement.GetProperty("id").GetString()!;
        var url = json.RootElement.TryGetProperty("htmlLink", out var l) ? l.GetString() : null;
        _logger.LogInformation("GOOGLE CALENDAR: evento creado \"{Titulo}\"", r.Titulo);
        return new EventoCreado(id, url);
    }

    public async Task EliminarEventoAsync(string usuarioId, string eventoId)
    {
        var token = await AccessTokenAsync(usuarioId);
        if (token is null) return;

        using var solicitud = new HttpRequestMessage(HttpMethod.Delete, $"{UrlEventos}/{Uri.EscapeDataString(eventoId)}");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await _http.SendAsync(solicitud);

        if (!respuesta.IsSuccessStatusCode && respuesta.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))
            _logger.LogWarning("Google Calendar respondió {Codigo} al borrar el evento", (int)respuesta.StatusCode);
    }

    // ---------- Tokens ----------

    private static string ClaveToken(string usuarioId) => $"gcal:token:{usuarioId}";

    private async Task GuardarAccessTokenAsync(string usuarioId, string token, int segundos) =>
        await _cache.SetStringAsync(ClaveToken(usuarioId), _protector.Protect(token),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(60, segundos - 120)) });

    // Devuelve un access token válido; lo renueva con el refresh token si hace falta
    private async Task<string?> AccessTokenAsync(string usuarioId)
    {
        if (!Disponible) return null;

        try
        {
            var guardado = await _cache.GetStringAsync(ClaveToken(usuarioId));
            if (guardado is not null) return _protector.Unprotect(guardado);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el token de Google de la caché");
        }

        var conexion = await _db.ConexionesGoogle.FindAsync(usuarioId);
        if (conexion is null) return null;

        var respuesta = await _http.PostAsync(UrlToken, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId!,
            ["client_secret"] = _clientSecret!,
            ["refresh_token"] = _protector.Unprotect(conexion.RefreshTokenCifrado),
            ["grant_type"] = "refresh_token"
        }));
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogWarning("No se pudo renovar el token de Google: {Cuerpo}", cuerpo);
            // invalid_grant: el alumno quitó el permiso o el token venció; se borra la conexión
            if (cuerpo.Contains("invalid_grant"))
            {
                _db.ConexionesGoogle.Remove(conexion);
                await _db.SaveChangesAsync();
            }
            return null;
        }

        using var json = JsonDocument.Parse(cuerpo);
        var token = json.RootElement.GetProperty("access_token").GetString()!;
        var expira = json.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
        await GuardarAccessTokenAsync(usuarioId, token, expira);
        return token;
    }

    // El id_token es un JWT: la parte central (payload) trae el correo
    private static string? CorreoDelIdToken(string? jwt)
    {
        try
        {
            var partes = jwt?.Split('.');
            if (partes is null || partes.Length < 2) return null;
            var base64 = partes[1].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(base64));
            return json.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    // ---------- Enlace manual (sin conectar la cuenta) ----------

    // Abre Google Calendar con el evento ya escrito; el alumno solo pulsa "Guardar"
    public static string EnlacePlantilla(Recordatorio r, string? nombreCurso)
    {
        var inicio = r.Fecha.ToUniversalTime();
        var fin = inicio.AddMinutes(r.DuracionMinutos);
        var detalles = $"{r.Tipo}{(nombreCurso is null ? "" : " · " + nombreCurso)}" +
                       (string.IsNullOrWhiteSpace(r.Notas) ? "" : "\n\n" + r.Notas);
        return "https://calendar.google.com/calendar/render?action=TEMPLATE" +
               $"&text={Uri.EscapeDataString($"{Emoji(r.Tipo)} {r.Titulo}")}" +
               $"&dates={inicio:yyyyMMdd'T'HHmmss'Z'}/{fin:yyyyMMdd'T'HHmmss'Z'}" +
               $"&details={Uri.EscapeDataString(detalles)}";
    }

    public static string Emoji(string tipo) => tipo switch
    {
        "Examen" => "📝",
        "Entrega" => "📤",
        "Exposición" => "🎤",
        "Estudio" => "📚",
        _ => "📌"
    };
}
