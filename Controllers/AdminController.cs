using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

[Authorize(Roles = SeedData.RolAdmin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var montos = await _db.Donaciones.Select(d => d.Monto).ToListAsync();
        var model = new ResumenAdminViewModel
        {
            TotalUsuarios = await _db.Users.CountAsync(),
            TotalCursos = await _db.Cursos.CountAsync(),
            TotalInscripciones = await _db.Inscripciones.CountAsync(),
            TotalDonado = montos.Sum()
        };
        return View(model);
    }

    // ---------- Usuarios ----------
    public async Task<IActionResult> Usuarios()
    {
        var idsAdmin = (await _userManager.GetUsersInRoleAsync(SeedData.RolAdmin)).Select(u => u.Id).ToHashSet();

        var usuarios = await _db.Users
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new UsuarioResumenViewModel
            {
                Id = u.Id,
                Nombre = u.NombreCompleto,
                Email = u.Email ?? "",
                CursosInscritos = u.Inscripciones.Count,
                FechaRegistro = u.FechaRegistro
            })
            .ToListAsync();

        foreach (var u in usuarios)
            u.Rol = idsAdmin.Contains(u.Id) ? SeedData.RolAdmin : SeedData.RolUsuario;

        return View(usuarios);
    }

    public async Task<IActionResult> Perfil(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(usuario);
        var donaciones = await _db.Donaciones.Where(d => d.UsuarioId == id).OrderByDescending(d => d.Fecha).ToListAsync();

        var model = new PerfilUsuarioViewModel
        {
            Usuario = usuario,
            Rol = roles.FirstOrDefault() ?? "Sin rol",
            Inscripciones = await _db.Inscripciones.Include(i => i.Curso).Where(i => i.UsuarioId == id).ToListAsync(),
            Donaciones = donaciones,
            TotalDonado = donaciones.Sum(d => d.Monto)
        };
        return View(model);
    }

    // ---------- Cursos ----------
    public async Task<IActionResult> Cursos()
    {
        var cursos = await _db.Cursos
            .Include(c => c.Inscripciones)
            .OrderBy(c => c.Titulo)
            .ToListAsync();
        return View(cursos);
    }

    [HttpGet]
    public IActionResult CrearCurso() => View(new Curso());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCurso([Bind("Titulo,Descripcion,Horas")] Curso curso)
    {
        if (!ModelState.IsValid) return View(curso);

        _db.Cursos.Add(curso);
        await _db.SaveChangesAsync();
        TempData["Mensaje"] = $"Curso \"{curso.Titulo}\" agregado.";
        return RedirectToAction(nameof(Cursos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCurso(int id)
    {
        var curso = await _db.Cursos.FindAsync(id);
        if (curso is null) return NotFound();

        _db.Cursos.Remove(curso);   // sus inscripciones se borran en cascada
        await _db.SaveChangesAsync();
        TempData["Mensaje"] = $"Curso \"{curso.Titulo}\" eliminado.";
        return RedirectToAction(nameof(Cursos));
    }
}