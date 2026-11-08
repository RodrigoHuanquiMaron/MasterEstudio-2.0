using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;

namespace PlataformaCursos.Controllers;

// Tutor IA del cronómetro: lee el documento del alumno, genera preguntas y explica temas con Groq
[Authorize(Roles = SeedData.RolUsuario)]
[AutoValidateAntiforgeryToken]
public class TutorController : Controller
{
    private static readonly string[] Dificultades = { "Fácil", "Media", "Difícil" };
    private static readonly TimeSpan DuracionDocumento = TimeSpan.FromHours(3);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GroqService _groq;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TutorController> _logger;

    public TutorController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        GroqService groq, IDistributedCache cache, ILogger<TutorController> logger)
    {
        _db = db;
        _userManager = userManager;
        _groq = groq;
        _cache = cache;
        _logger = logger;
    }

    private string UsuarioId => _userManager.GetUserId(User)!;

    // El texto del documento se guarda en Redis (3 horas) para no tener que volver a subirlo
    private string ClaveDocumento(string documentoId) => $"tutor:{UsuarioId}:{documentoId}";

    public sealed record DocumentoGuardado(string Nombre, string Texto);

    private static string NormalizarDificultad(string? dificultad) =>
        Dificultades.Contains(dificultad) ? dificultad! : "Media";

    private IActionResult Error(string mensaje) => BadRequest(new { error = mensaje });

    // ---------- 1. Subir documento y primera ronda de preguntas ----------

    [HttpPost]
    [RequestSizeLimit(ExtractorTexto.TamanoMaximo + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ExtractorTexto.TamanoMaximo + 1024 * 1024)]
    public async Task<IActionResult> Subir(IFormFile? archivo, int cantidad = 5, string? dificultad = null)
    {
        if (archivo is null || archivo.Length == 0)
            return Error("Elige un archivo para continuar.");

        try
        {
            var texto = await ExtractorTexto.ExtraerAsync(archivo);
            var nombre = Path.GetFileNameWithoutExtension(archivo.FileName);
            if (nombre.Length > 140) nombre = nombre[..140];

            var documentoId = Guid.NewGuid().ToString("N");
            await _cache.SetStringAsync(ClaveDocumento(documentoId),
                JsonSerializer.Serialize(new DocumentoGuardado(nombre, texto)),
                new DistributedCacheEntryOptions { SlidingExpiration = DuracionDocumento });

            var nivel = NormalizarDificultad(dificultad);
            var preguntas = await _groq.GenerarPreguntasAsync(ExtractorTexto.Fragmento(texto, 0), cantidad, nivel, Array.Empty<string>());
            if (preguntas.Count == 0)
                return Error("La IA no pudo crear preguntas con este documento. Inténtalo de nuevo.");

            _logger.LogInformation("Tutor IA: documento \"{Nombre}\" ({Caracteres} caracteres)", nombre, texto.Length);
            return Json(new { documentoId, nombre, caracteres = texto.Length, preguntas });
        }
        catch (GroqException ex)
        {
            return Error(ex.Message);
        }
    }

    // ---------- 2. Más preguntas del mismo documento ----------

    public class MasPreguntasRequest
    {
        public string DocumentoId { get; set; } = string.Empty;
        public int Ronda { get; set; }
        public int Cantidad { get; set; } = 5;
        public string? Dificultad { get; set; }
        public List<string> YaHechas { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> Mas([FromBody] MasPreguntasRequest pedido)
    {
        var documento = await LeerDocumentoAsync(pedido.DocumentoId);
        if (documento is null)
            return Error("El documento expiró. Vuelve a subirlo para seguir practicando.");

        try
        {
            var preguntas = await _groq.GenerarPreguntasAsync(
                ExtractorTexto.Fragmento(documento.Texto, pedido.Ronda),
                pedido.Cantidad,
                NormalizarDificultad(pedido.Dificultad),
                pedido.YaHechas.Select(p => p.Length > 200 ? p[..200] : p).ToList());

            if (preguntas.Count == 0)
                return Error("La IA no pudo crear más preguntas. Inténtalo de nuevo.");
            return Json(new { preguntas });
        }
        catch (GroqException ex)
        {
            return Error(ex.Message);
        }
    }

    // ---------- 3. Profundizar: conversación con el tutor ----------

    public class ProfundizarRequest
    {
        public string? DocumentoId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public List<MensajeChat> Historial { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> Profundizar([FromBody] ProfundizarRequest pedido)
    {
        var mensaje = pedido.Mensaje?.Trim() ?? "";
        if (mensaje.Length == 0) return Error("Escribe tu duda.");
        if (mensaje.Length > 1500) mensaje = mensaje[..1500];

        string? material = null;
        if (!string.IsNullOrEmpty(pedido.DocumentoId))
        {
            var documento = await LeerDocumentoAsync(pedido.DocumentoId);
            if (documento is not null)
                material = BuscarFragmentoRelevante(documento.Texto, mensaje);
        }

        try
        {
            var respuesta = await _groq.ExplicarAsync(material, pedido.Historial, mensaje);
            return Json(respuesta);
        }
        catch (GroqException ex)
        {
            return Error(ex.Message);
        }
    }

    // Elige la parte del documento que más se parece a la duda, para no enviar todo el texto
    private static string BuscarFragmentoRelevante(string texto, string duda, int largo = 6_000)
    {
        if (texto.Length <= largo) return texto;

        var palabras = duda.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '¿', '!', '¡', ':', ';', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 3)
            .Distinct()
            .ToList();

        var mejorInicio = 0;
        var mejorPuntaje = -1;
        for (var inicio = 0; inicio < texto.Length; inicio += largo / 2)
        {
            var trozo = texto.Substring(inicio, Math.Min(largo, texto.Length - inicio)).ToLowerInvariant();
            var puntaje = palabras.Sum(p => trozo.Contains(p) ? 1 : 0);
            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                mejorInicio = inicio;
            }
        }
        return texto.Substring(mejorInicio, Math.Min(largo, texto.Length - mejorInicio));
    }

    // ---------- 4. Guardar el resultado de una ronda ----------

    public class ResultadoRequest
    {
        public string Documento { get; set; } = string.Empty;
        public string? Dificultad { get; set; }
        public int Correctas { get; set; }
        public int Total { get; set; }
        public int? CursoId { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> GuardarResultado([FromBody] ResultadoRequest pedido)
    {
        if (pedido.Total < 1 || pedido.Total > 50 || pedido.Correctas < 0 || pedido.Correctas > pedido.Total)
            return Error("Resultado no válido.");

        var id = UsuarioId;
        int? cursoId = null;
        if (pedido.CursoId is int c && await _db.Inscripciones.AnyAsync(i => i.UsuarioId == id && i.CursoId == c))
            cursoId = c;

        var documento = string.IsNullOrWhiteSpace(pedido.Documento) ? "Documento" : pedido.Documento.Trim();
        _db.ResultadosQuiz.Add(new ResultadoQuiz
        {
            UsuarioId = id,
            CursoId = cursoId,
            Documento = documento.Length > 150 ? documento[..150] : documento,
            Dificultad = NormalizarDificultad(pedido.Dificultad),
            Correctas = pedido.Correctas,
            Total = pedido.Total
        });
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    private async Task<DocumentoGuardado?> LeerDocumentoAsync(string documentoId)
    {
        if (string.IsNullOrWhiteSpace(documentoId) || documentoId.Length > 64) return null;
        try
        {
            var json = await _cache.GetStringAsync(ClaveDocumento(documentoId));
            return json is null ? null : JsonSerializer.Deserialize<DocumentoGuardado>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el documento del tutor desde la caché");
            return null;
        }
    }
}
