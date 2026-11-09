using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

// Recordatorios del alumno (Panel) y conexión con Google Calendar
[Authorize(Roles = SeedData.RolUsuario)]
public class CalendarioController : Controller
{
    private const string ClaveEstado = "GoogleCalendar.Estado";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GoogleCalendarService _google;

    public CalendarioController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, GoogleCalendarService google)
    {
        _db = db;
        _userManager = userManager;
        _google = google;
    }

    private string UsuarioId => _userManager.GetUserId(User)!;

    // La dirección a la que Google devuelve al alumno (debe estar registrada en Google Cloud)
    private string UrlRetorno => Url.Action(nameof(Callback), "Calendario", null, Request.Scheme)!;

    private IActionResult AlPanel() => Redirect(Url.Action("Panel", "Usuario") + "#recordatorios");

    // ---------- Recordatorios ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(RecordatorioFormModel model)
    {
        if (!ModelState.IsValid || model.Fecha is null)
        {
            TempData["Error"] = "Revisa el recordatorio: el título y la fecha son obligatorios.";
            return AlPanel();
        }

        var id = UsuarioId;
        string? nombreCurso = null;
        if (model.CursoId is int cursoId)
        {
            nombreCurso = await _db.Inscripciones
                .Where(i => i.UsuarioId == id && i.CursoId == cursoId)
                .Select(i => i.Curso!.Titulo)
                .FirstOrDefaultAsync();
            if (nombreCurso is null) model.CursoId = null;
        }

        // La fecha llega en hora local (del formulario) y se guarda en UTC
        var fecha = DateTime.SpecifyKind(model.Fecha.Value, DateTimeKind.Local).ToUniversalTime();

        var recordatorio = new Recordatorio
        {
            UsuarioId = id,
            CursoId = model.CursoId,
            Titulo = model.Titulo.Trim(),
            Tipo = Recordatorio.Tipos.Contains(model.Tipo) ? model.Tipo : "Otro",
            Notas = string.IsNullOrWhiteSpace(model.Notas) ? null : model.Notas.Trim(),
            Fecha = fecha,
            DuracionMinutos = model.DuracionMinutos,
            AvisoMinutosAntes = model.AvisoMinutosAntes
        };
        _db.Recordatorios.Add(recordatorio);
        await _db.SaveChangesAsync();   // 1. Primero se guarda en PostgreSQL

        var mensaje = $"Recordatorio \"{recordatorio.Titulo}\" creado.";
        if (model.EnviarAGoogle && await _db.ConexionesGoogle.AnyAsync(c => c.UsuarioId == id))
        {
            // 2. Luego se crea el evento en Google Calendar
            var evento = await _google.CrearEventoAsync(id, recordatorio, nombreCurso);
            if (evento is not null)
            {
                recordatorio.GoogleEventId = evento.Id;
                recordatorio.GoogleEventUrl = evento.Url;
                await _db.SaveChangesAsync();
                mensaje += " También se agregó a tu Google Calendar.";
            }
            else
            {
                mensaje += " No se pudo agregar a Google Calendar; usa el botón de Google para agregarlo manualmente.";
            }
        }

        TempData["Mensaje"] = mensaje;
        return AlPanel();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Completar(int id)
    {
        var r = await _db.Recordatorios.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == UsuarioId);
        if (r is not null)
        {
            r.Completado = !r.Completado;
            await _db.SaveChangesAsync();
        }
        return AlPanel();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var usuarioId = UsuarioId;
        var r = await _db.Recordatorios.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId);
        if (r is null) return AlPanel();

        if (r.GoogleEventId is not null)
            await _google.EliminarEventoAsync(usuarioId, r.GoogleEventId);

        _db.Recordatorios.Remove(r);
        await _db.SaveChangesAsync();
        TempData["Mensaje"] = $"Recordatorio \"{r.Titulo}\" eliminado.";
        return AlPanel();
    }

    // ---------- Conexión con Google Calendar ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Conectar()
    {
        if (!_google.Disponible)
        {
            TempData["Error"] = "Google Calendar no está configurado en el servidor.";
            return AlPanel();
        }

        // "state" evita que otra página complete la conexión en nombre del alumno
        var estado = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        HttpContext.Session.SetString(ClaveEstado, estado);
        return Redirect(_google.UrlAutorizacion(UrlRetorno, estado));
    }

    [HttpGet]
    public async Task<IActionResult> Callback(string? code, string? state, string? error)
    {
        var esperado = HttpContext.Session.GetString(ClaveEstado);
        HttpContext.Session.Remove(ClaveEstado);

        if (error is not null)
        {
            TempData["Error"] = error == "access_denied"
                ? "No diste permiso para usar Google Calendar."
                : $"Google devolvió un error: {error}";
            return AlPanel();
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || state != esperado)
        {
            TempData["Error"] = "La conexión con Google expiró o no es válida. Inténtalo de nuevo.";
            return AlPanel();
        }

        var ok = await _google.CompletarConexionAsync(UsuarioId, code, UrlRetorno);
        TempData[ok ? "Mensaje" : "Error"] = ok
            ? "¡Listo! Tus nuevos recordatorios también se guardarán en Google Calendar."
            : "No se pudo conectar con Google Calendar. Inténtalo de nuevo.";
        return AlPanel();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desconectar()
    {
        await _google.DesconectarAsync(UsuarioId);
        TempData["Mensaje"] = "Google Calendar desconectado. Los eventos que ya creaste siguen en tu calendario.";
        return AlPanel();
    }
}
