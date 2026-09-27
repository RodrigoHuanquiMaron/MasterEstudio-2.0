using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

[Authorize(Roles = SeedData.RolUsuario)]
public class UsuarioController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CacheService _cache;

    public UsuarioController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, CacheService cache)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
    }

    private string UsuarioId => _userManager.GetUserId(User)!;

    // ---------- Lecturas desde PostgreSQL (solo se usan cuando no hay caché) ----------

    private Task<List<Curso>> LeerCatalogoAsync() =>
        _db.Cursos
            .OrderBy(c => c.Titulo)
            .Select(c => new Curso
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Descripcion = c.Descripcion,
                Horas = c.Horas,
                FechaCreacion = c.FechaCreacion
            })
            .ToListAsync();

    private Task<List<Inscripcion>> LeerInscripcionesAsync(string usuarioId) =>
        _db.Inscripciones
            .Where(i => i.UsuarioId == usuarioId)
            .OrderBy(i => i.Curso!.Titulo)
            .Select(i => new Inscripcion
            {
                Id = i.Id,
                UsuarioId = i.UsuarioId,
                CursoId = i.CursoId,
                Progreso = i.Progreso,
                Nota = i.Nota,
                FechaInscripcion = i.FechaInscripcion,
                Curso = new Curso
                {
                    Id = i.Curso!.Id,
                    Titulo = i.Curso.Titulo,
                    Descripcion = i.Curso.Descripcion,
                    Horas = i.Curso.Horas
                }
            })
            .ToListAsync();

    private Task<List<Donacion>> LeerDonacionesAsync(string usuarioId) =>
        _db.Donaciones
            .AsNoTracking()
            .Where(d => d.UsuarioId == usuarioId)
            .OrderByDescending(d => d.Fecha)
            .ToListAsync();

    // ---------- Pantallas ----------

    // Panel principal: resumen + catálogo de cursos disponibles
    public async Task<IActionResult> Panel()
    {
        var id = UsuarioId;
        var inscripciones = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));
        var donaciones = await _cache.DonacionesAsync(id, () => LeerDonacionesAsync(id));
        var catalogo = await _cache.CatalogoAsync(LeerCatalogoAsync);
        var idsInscritos = inscripciones.Select(i => i.CursoId).ToHashSet();

        var model = new PanelUsuarioViewModel
        {
            // El nombre viene de la sesión (Parte 2), sin consultar la base
            Nombre = HttpContext.Session.GetString(SesionKeys.Nombre) ?? User.Identity?.Name ?? "",
            CursosInscritos = inscripciones.Count,
            ProgresoPromedio = inscripciones.Count == 0 ? 0 : (int)inscripciones.Average(i => i.Progreso),
            TotalDonado = donaciones.Sum(d => d.Monto),
            CursosDisponibles = catalogo.Where(c => !idsInscritos.Contains(c.Id)).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscribirse(int cursoId)
    {
        var id = UsuarioId;
        var existeCurso = await _db.Cursos.AnyAsync(c => c.Id == cursoId);
        var yaInscrito = await _db.Inscripciones.AnyAsync(i => i.UsuarioId == id && i.CursoId == cursoId);

        if (existeCurso && !yaInscrito)
        {
            _db.Inscripciones.Add(new Inscripcion { UsuarioId = id, CursoId = cursoId });
            await _db.SaveChangesAsync();
            await _cache.InvalidarUsuarioAsync(id);   // primero se guarda, luego se invalida la caché
            TempData["Mensaje"] = "Te inscribiste en el curso.";
        }
        return RedirectToAction(nameof(MisCursos));
    }

    public async Task<IActionResult> MisCursos()
    {
        var id = UsuarioId;
        var inscripciones = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));
        return View(inscripciones);
    }

    // Simula el avance en un curso para que "Desempeño" tenga datos
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id)
    {
        var usuarioId = UsuarioId;
        var inscripcion = await _db.Inscripciones.FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioId);
        if (inscripcion is not null)
        {
            inscripcion.Progreso = Math.Min(100, inscripcion.Progreso + 10);
            await _db.SaveChangesAsync();
            await _cache.InvalidarUsuarioAsync(usuarioId);
        }
        return RedirectToAction(nameof(MisCursos));
    }

    public async Task<IActionResult> Desempeno()
    {
        var id = UsuarioId;
        var inscripciones = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));
        return View(inscripciones.OrderByDescending(i => i.Progreso).ToList());
    }

    public async Task<IActionResult> Donaciones()
    {
        var id = UsuarioId;
        var historial = await _cache.DonacionesAsync(id, () => LeerDonacionesAsync(id));
        return View(new DonacionesViewModel { Historial = historial, Total = historial.Sum(d => d.Monto) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Donar(decimal monto, string? mensaje)
    {
        if (monto < 1 || monto > 10000)
        {
            TempData["Error"] = "El monto debe estar entre 1 y 10 000.";
            return RedirectToAction(nameof(Donaciones));
        }

        var id = UsuarioId;
        _db.Donaciones.Add(new Donacion { UsuarioId = id, Monto = monto, Mensaje = mensaje });
        await _db.SaveChangesAsync();
        await _cache.InvalidarUsuarioAsync(id);
        TempData["Mensaje"] = "Gracias, tu donación quedó registrada.";
        return RedirectToAction(nameof(Donaciones));
    }
}