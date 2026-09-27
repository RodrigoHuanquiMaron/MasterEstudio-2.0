using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

[Authorize(Roles = SeedData.RolUsuario)]
public class UsuarioController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsuarioController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string UsuarioId => _userManager.GetUserId(User)!;

    // Panel principal: resumen + catálogo de cursos disponibles
    public async Task<IActionResult> Panel()
    {
        var usuario = await _userManager.GetUserAsync(User);
        var inscripciones = await _db.Inscripciones.Where(i => i.UsuarioId == UsuarioId).ToListAsync();
        var donaciones = await _db.Donaciones.Where(d => d.UsuarioId == UsuarioId).ToListAsync();
        var idsInscritos = inscripciones.Select(i => i.CursoId).ToList();

        var model = new PanelUsuarioViewModel
        {
            Nombre = usuario?.NombreCompleto ?? "",
            CursosInscritos = inscripciones.Count,
            ProgresoPromedio = inscripciones.Count == 0 ? 0 : (int)inscripciones.Average(i => i.Progreso),
            TotalDonado = donaciones.Sum(d => d.Monto),
            CursosDisponibles = await _db.Cursos
                .Where(c => !idsInscritos.Contains(c.Id))
                .OrderBy(c => c.Titulo)
                .ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscribirse(int cursoId)
    {
        var existeCurso = await _db.Cursos.AnyAsync(c => c.Id == cursoId);
        var yaInscrito = await _db.Inscripciones.AnyAsync(i => i.UsuarioId == UsuarioId && i.CursoId == cursoId);

        if (existeCurso && !yaInscrito)
        {
            _db.Inscripciones.Add(new Inscripcion { UsuarioId = UsuarioId, CursoId = cursoId });
            await _db.SaveChangesAsync();
            TempData["Mensaje"] = "Te inscribiste en el curso.";
        }
        return RedirectToAction(nameof(MisCursos));
    }

    public async Task<IActionResult> MisCursos()
    {
        var inscripciones = await _db.Inscripciones
            .Include(i => i.Curso)
            .Where(i => i.UsuarioId == UsuarioId)
            .OrderBy(i => i.Curso!.Titulo)
            .ToListAsync();
        return View(inscripciones);
    }

    // Simula el avance en un curso para que "Desempeño" tenga datos
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id)
    {
        var inscripcion = await _db.Inscripciones.FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == UsuarioId);
        if (inscripcion is not null)
        {
            inscripcion.Progreso = Math.Min(100, inscripcion.Progreso + 10);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(MisCursos));
    }

    public async Task<IActionResult> Desempeno()
    {
        var inscripciones = await _db.Inscripciones
            .Include(i => i.Curso)
            .Where(i => i.UsuarioId == UsuarioId)
            .OrderByDescending(i => i.Progreso)
            .ToListAsync();
        return View(inscripciones);
    }

    public async Task<IActionResult> Donaciones()
    {
        var historial = await _db.Donaciones
            .Where(d => d.UsuarioId == UsuarioId)
            .OrderByDescending(d => d.Fecha)
            .ToListAsync();
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

        _db.Donaciones.Add(new Donacion { UsuarioId = UsuarioId, Monto = monto, Mensaje = mensaje });
        await _db.SaveChangesAsync();
        TempData["Mensaje"] = "Gracias, tu donación quedó registrada.";
        return RedirectToAction(nameof(Donaciones));
    }
}