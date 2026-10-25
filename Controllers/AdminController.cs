using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

[Authorize(Roles = SeedData.RolAdmin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CacheService _cache;
    private readonly PieHostService _pieHost;
    private readonly SentimientoService _sentimiento;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        CacheService cache, PieHostService pieHost, SentimientoService sentimiento)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
        _pieHost = pieHost;
        _sentimiento = sentimiento;
    }

    // Resumen con caché de 60 s
    public async Task<IActionResult> Index()
    {
        var model = await _cache.ObtenerOCrearAsync(Claves.ResumenAdmin, async () =>
        {
            var montos = await _db.Donaciones.Select(d => d.Monto).ToListAsync();
            return new ResumenAdminViewModel
            {
                TotalUsuarios = await _db.Users.CountAsync(),
                TotalCursos = await _db.Cursos.CountAsync(),
                TotalInscripciones = await _db.Inscripciones.CountAsync(),
                TotalDonado = montos.Sum(),
                DonacionesPositivas = await _db.Donaciones.CountAsync(d => d.Sentimiento == Sentimientos.Positiva),
                DonacionesNegativas = await _db.Donaciones.CountAsync(d => d.Sentimiento == Sentimientos.Negativa)
            };
        });
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
        var segundos = await _db.SesionesEstudio.Where(s => s.UsuarioId == id).Select(s => s.Segundos).ToListAsync();

        var model = new PerfilUsuarioViewModel
        {
            Usuario = usuario,
            Rol = roles.FirstOrDefault() ?? "Sin rol",
            Inscripciones = await _db.Inscripciones
                .Include(i => i.Curso)
                .Where(i => i.UsuarioId == id)
                .OrderBy(i => i.Curso!.Titulo)
                .ToListAsync(),
            Donaciones = donaciones,
            TotalDonado = donaciones.Sum(d => d.Monto),
            HorasEstudio = segundos.Sum(x => (long)x) / 3600.0
        };
        return View(model);
    }

    // El administrador asigna (o borra) la calificación de un alumno en un curso
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calificar(int inscripcionId, decimal? nota)
    {
        var inscripcion = await _db.Inscripciones.Include(i => i.Curso).FirstOrDefaultAsync(i => i.Id == inscripcionId);
        if (inscripcion is null) return NotFound();

        if (nota.HasValue && (nota < 0 || nota > 100))
        {
            TempData["Error"] = "La calificación debe estar entre 0 y 100.";
            return RedirectToAction(nameof(Perfil), new { id = inscripcion.UsuarioId });
        }

        inscripcion.Nota = nota.HasValue ? Math.Round(nota.Value, 1) : null;
        inscripcion.FechaCalificacion = nota.HasValue ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();                              // 1. PostgreSQL
        await _cache.InvalidarUsuarioAsync(inscripcion.UsuarioId);  // 2. Redis

        TempData["Mensaje"] = nota.HasValue
            ? $"Calificación de {inscripcion.Curso?.Titulo} guardada: {Calificacion.Texto(inscripcion.Nota)}."
            : $"Calificación de {inscripcion.Curso?.Titulo} eliminada.";
        return RedirectToAction(nameof(Perfil), new { id = inscripcion.UsuarioId });
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
    public IActionResult CrearCurso()
    {
        ViewData["Accion"] = nameof(CrearCurso);
        return View("FormCurso", new Curso());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCurso([Bind("Titulo,Descripcion,Horas,Instructor,Categoria,ImagenUrl,Creditos")] Curso curso)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Accion"] = nameof(CrearCurso);
            return View("FormCurso", curso);
        }

        _db.Cursos.Add(curso);
        await _db.SaveChangesAsync();                                                     // 1. PostgreSQL
        await _cache.CursosCambiaronAsync();                                              // 2. Redis
        await _pieHost.PublicarCursoActualizadoAsync(curso.Id, "Agregado", curso.Titulo); // 3. PieHost

        TempData["Mensaje"] = $"Curso \"{curso.Titulo}\" agregado.";
        return RedirectToAction(nameof(Cursos));
    }

    [HttpGet]
    public async Task<IActionResult> EditarCurso(int id)
    {
        var curso = await _db.Cursos.FindAsync(id);
        if (curso is null) return NotFound();

        ViewData["Accion"] = nameof(EditarCurso);
        return View("FormCurso", curso);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarCurso(int id, [Bind("Titulo,Descripcion,Horas,Instructor,Categoria,ImagenUrl,Creditos")] Curso datos)
    {
        var curso = await _db.Cursos.FindAsync(id);
        if (curso is null) return NotFound();

        if (!ModelState.IsValid)
        {
            datos.Id = id;
            ViewData["Accion"] = nameof(EditarCurso);
            return View("FormCurso", datos);
        }

        curso.Titulo = datos.Titulo;
        curso.Descripcion = datos.Descripcion;
        curso.Horas = datos.Horas;
        curso.Instructor = datos.Instructor;
        curso.Categoria = datos.Categoria;
        curso.ImagenUrl = datos.ImagenUrl;
        curso.Creditos = datos.Creditos;

        await _db.SaveChangesAsync();
        await _cache.CursosCambiaronAsync();

        TempData["Mensaje"] = $"Curso \"{curso.Titulo}\" actualizado.";
        return RedirectToAction(nameof(Cursos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCurso(int id)
    {
        var curso = await _db.Cursos.FindAsync(id);
        if (curso is null) return NotFound();

        _db.Cursos.Remove(curso);                                                    // inscripciones y sesiones se borran en cascada
        await _db.SaveChangesAsync();                                                // 1. PostgreSQL
        await _cache.CursosCambiaronAsync();                                         // 2. Redis
        await _pieHost.PublicarCursoActualizadoAsync(id, "Eliminado", curso.Titulo); // 3. PieHost

        TempData["Mensaje"] = $"Curso \"{curso.Titulo}\" eliminado.";
        return RedirectToAction(nameof(Cursos));
    }

    // ---------- Donaciones con análisis de sentimiento (machine learning) ----------
    public async Task<IActionResult> Donaciones(string? sentimiento)
    {
        var todas = await _db.Donaciones
            .AsNoTracking()
            .Include(d => d.Usuario)
            .OrderByDescending(d => d.Fecha)
            .ToListAsync();

        var model = new AdminDonacionesViewModel
        {
            Donaciones = string.IsNullOrEmpty(sentimiento)
                ? todas
                : todas.Where(d => d.Sentimiento == sentimiento).ToList(),
            Positivas = todas.Count(d => d.Sentimiento == Sentimientos.Positiva),
            Negativas = todas.Count(d => d.Sentimiento == Sentimientos.Negativa),
            Neutrales = todas.Count(d => d.Sentimiento == Sentimientos.Neutral),
            SinAnalizar = todas.Count(d => d.Sentimiento is null || d.Sentimiento == Sentimientos.SinAnalizar),
            IaConfigurada = _sentimiento.Configurado
        };
        ViewData["Filtro"] = sentimiento;
        return View(model);
    }

    // Vuelve a analizar las donaciones que quedaron sin resultado
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalizarPendientes()
    {
        var pendientes = await _db.Donaciones
            .Where(d => d.Sentimiento == null || d.Sentimiento == Sentimientos.SinAnalizar)
            .ToListAsync();

        var analizadas = 0;
        foreach (var d in pendientes)
        {
            var resultado = await _sentimiento.AnalizarAsync(d.Mensaje);
            d.Sentimiento = resultado.Etiqueta;
            d.SentimientoConfianza = resultado.Confianza;
            if (resultado.Etiqueta != Sentimientos.SinAnalizar) analizadas++;
        }
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Se analizaron {analizadas} de {pendientes.Count} donaciones pendientes.";
        return RedirectToAction(nameof(Donaciones));
    }
}
