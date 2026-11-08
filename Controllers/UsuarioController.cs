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
    private readonly SentimientoService _sentimiento;

    public UsuarioController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        CacheService cache, SentimientoService sentimiento)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
        _sentimiento = sentimiento;
    }

    private string UsuarioId => _userManager.GetUserId(User)!;

    // ---------- Lecturas desde PostgreSQL (se guardan en Redis 60 s) ----------

    private Task<List<Curso>> LeerCatalogoAsync() =>
        _db.Cursos
            .Where(c => c.PropietarioId == null)
            .OrderBy(c => c.Titulo)
            .Select(c => new Curso
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Descripcion = c.Descripcion,
                Horas = c.Horas,
                Instructor = c.Instructor,
                Categoria = c.Categoria,
                ImagenUrl = c.ImagenUrl,
                Creditos = c.Creditos,
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
                FechaCalificacion = i.FechaCalificacion,
                FechaInscripcion = i.FechaInscripcion,
                Curso = new Curso
                {
                    Id = i.Curso!.Id,
                    Titulo = i.Curso.Titulo,
                    Descripcion = i.Curso.Descripcion,
                    Horas = i.Curso.Horas,
                    Instructor = i.Curso.Instructor,
                    Categoria = i.Curso.Categoria,
                    ImagenUrl = i.Curso.ImagenUrl,
                    Creditos = i.Curso.Creditos,
                    PropietarioId = i.Curso.PropietarioId,
                    UrlExterna = i.Curso.UrlExterna,
                    Plataforma = i.Curso.Plataforma
                }
            })
            .ToListAsync();

    private Task<List<Donacion>> LeerDonacionesAsync(string usuarioId) =>
        _db.Donaciones
            .AsNoTracking()
            .Where(d => d.UsuarioId == usuarioId)
            .OrderByDescending(d => d.Fecha)
            .ToListAsync();

    // Lunes de la semana actual (hora local)
    private static DateTime InicioSemanaLocal()
    {
        var hoy = DateTime.Now.Date;
        var diferencia = ((int)hoy.DayOfWeek + 6) % 7;
        return hoy.AddDays(-diferencia);
    }

    private Task<List<SesionEstudio>> SesionesDesdeAsync(string usuarioId, DateTime desdeLocal)
    {
        var desdeUtc = DateTime.SpecifyKind(desdeLocal, DateTimeKind.Local).ToUniversalTime();
        return _db.SesionesEstudio
            .AsNoTracking()
            .Include(s => s.Curso)
            .Where(s => s.UsuarioId == usuarioId && s.Inicio >= desdeUtc)
            .OrderByDescending(s => s.Inicio)
            .ToListAsync();
    }

    private async Task<long> SegundosTotalesAsync(string usuarioId)
    {
        var segundos = await _db.SesionesEstudio
            .Where(s => s.UsuarioId == usuarioId)
            .Select(s => s.Segundos)
            .ToListAsync();
        return segundos.Sum(x => (long)x);
    }

    private static decimal? Promedio(IEnumerable<Inscripcion> inscripciones)
    {
        var notas = inscripciones.Where(i => i.Nota.HasValue).Select(i => i.Nota!.Value).ToList();
        return notas.Count == 0 ? null : Math.Round(notas.Average(), 1);
    }

    // ---------- Panel principal ----------

    public async Task<IActionResult> Panel()
    {
        var id = UsuarioId;
        var usuario = await _userManager.GetUserAsync(User);
        var inscripciones = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));
        var donaciones = await _cache.DonacionesAsync(id, () => LeerDonacionesAsync(id));
        var catalogo = await _cache.CatalogoAsync(LeerCatalogoAsync);
        var idsInscritos = inscripciones.Select(i => i.CursoId).ToHashSet();

        // Horas de estudio de esta semana, por día
        var inicioSemana = InicioSemanaLocal();
        var sesionesSemana = await SesionesDesdeAsync(id, inicioSemana);
        var horasPorDia = new double[7];
        foreach (var s in sesionesSemana)
        {
            var dia = (int)(s.Inicio.ToLocalTime().Date - inicioSemana).TotalDays;
            if (dia is >= 0 and < 7) horasPorDia[dia] += s.Segundos / 3600.0;
        }

        // Actividades recientes: estudio, inscripciones, calificaciones y donaciones
        var recientes = await _db.SesionesEstudio
            .AsNoTracking()
            .Include(s => s.Curso)
            .Where(s => s.UsuarioId == id)
            .OrderByDescending(s => s.Inicio)
            .Take(5)
            .ToListAsync();

        var actividades = new List<ActividadViewModel>();
        actividades.AddRange(recientes.Select(s => new ActividadViewModel
        {
            Icono = "bi-stopwatch",
            Texto = $"Estudiaste {Calificacion.Duracion(s.Segundos)} de {s.Curso?.Titulo}",
            Fecha = s.Inicio
        }));
        actividades.AddRange(inscripciones.Select(i => new ActividadViewModel
        {
            Icono = "bi-journal-plus",
            Texto = $"Te inscribiste en {i.Curso?.Titulo}",
            Fecha = i.FechaInscripcion
        }));
        actividades.AddRange(inscripciones.Where(i => i.FechaCalificacion.HasValue).Select(i => new ActividadViewModel
        {
            Icono = "bi-award",
            Texto = $"Obtuviste {Calificacion.Texto(i.Nota)} en {i.Curso?.Titulo}",
            Fecha = i.FechaCalificacion!.Value
        }));
        actividades.AddRange(donaciones.Select(d => new ActividadViewModel
        {
            Icono = "bi-heart",
            Texto = $"Donaste S/ {d.Monto:N2}",
            Fecha = d.Fecha
        }));

        var model = new PanelUsuarioViewModel
        {
            Nombre = usuario?.NombreCompleto ?? HttpContext.Session.GetString(SesionKeys.Nombre) ?? "",
            Carrera = usuario?.Carrera,
            Ciclo = usuario?.Ciclo,
            CursosTotales = inscripciones.Count,
            CursosActivos = inscripciones.Count(i => i.Progreso < 100),
            Completados = inscripciones.Count(i => i.Progreso >= 100),
            PromedioNota = Promedio(inscripciones),
            HorasTotales = await SegundosTotalesAsync(id) / 3600.0,
            HorasSemana = horasPorDia.Sum(),
            Inscripciones = inscripciones
                .OrderBy(i => i.Progreso >= 100)
                .ThenByDescending(i => i.Progreso)
                .ToList(),
            CursosDisponibles = catalogo.Where(c => !idsInscritos.Contains(c.Id)).ToList(),
            HorasPorDia = horasPorDia.Select(h => Math.Round(h, 2)).ToArray(),
            IndiceHoy = (int)(DateTime.Now.Date - inicioSemana).TotalDays,
            Actividades = actividades.OrderByDescending(a => a.Fecha).Take(5).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscribirse(int cursoId)
    {
        var id = UsuarioId;
        var existeCurso = await _db.Cursos.AnyAsync(c => c.Id == cursoId && c.PropietarioId == null);
        var yaInscrito = await _db.Inscripciones.AnyAsync(i => i.UsuarioId == id && i.CursoId == cursoId);

        if (existeCurso && !yaInscrito)
        {
            _db.Inscripciones.Add(new Inscripcion { UsuarioId = id, CursoId = cursoId });
            await _db.SaveChangesAsync();
            await _cache.InvalidarUsuarioAsync(id);
            TempData["Mensaje"] = "Te inscribiste en el curso.";
        }
        return RedirectToAction(nameof(MisCursos));
    }

    // ---------- Mis cursos ----------

    public async Task<IActionResult> MisCursos(string? estado)
    {
        var id = UsuarioId;
        var todas = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));
        var filtro = (estado ?? "todos").ToLowerInvariant();

        IEnumerable<Inscripcion> consulta = filtro switch
        {
            "encurso" => todas.Where(i => i.Progreso > 0 && i.Progreso < 100),
            "completado" => todas.Where(i => i.Progreso >= 100),
            "noiniciado" => todas.Where(i => i.Progreso == 0),
            _ => todas
        };

        var model = new MisCursosViewModel
        {
            Filtro = filtro,
            Inscripciones = consulta.ToList(),
            Total = todas.Count,
            EnCurso = todas.Count(i => i.Progreso > 0 && i.Progreso < 100),
            Completados = todas.Count(i => i.Progreso >= 100),
            NoIniciados = todas.Count(i => i.Progreso == 0)
        };
        return View(model);
    }

    // Simula el avance en un curso
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id, string? estado)
    {
        var usuarioId = UsuarioId;
        var inscripcion = await _db.Inscripciones.FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioId);
        if (inscripcion is not null)
        {
            inscripcion.Progreso = Math.Min(100, inscripcion.Progreso + 10);
            await _db.SaveChangesAsync();
            await _cache.InvalidarUsuarioAsync(usuarioId);
        }
        return RedirectToAction(nameof(MisCursos), new { estado });
    }

    // ---------- Cursos personales (externos) ----------

    [HttpGet]
    public IActionResult NuevoCursoPersonal() => View("FormCursoPersonal", new CursoPersonalViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoCursoPersonal(CursoPersonalViewModel model)
    {
        if (!ModelState.IsValid) return View("FormCursoPersonal", model);

        var id = UsuarioId;
        var curso = new Curso
        {
            Titulo = model.Titulo,
            Descripcion = model.Descripcion,
            Plataforma = model.Plataforma,
            UrlExterna = model.UrlExterna,
            Instructor = model.Instructor,
            Categoria = model.Categoria,
            Horas = model.Horas,
            Creditos = 0,              // los cursos personales no suman créditos académicos
            PropietarioId = id
        };

        // El alumno queda inscrito automáticamente en su propio curso
        curso.Inscripciones.Add(new Inscripcion { UsuarioId = id });
        _db.Cursos.Add(curso);
        await _db.SaveChangesAsync();
        await _cache.InvalidarUsuarioAsync(id);

        TempData["Mensaje"] = $"Agregaste tu curso \"{curso.Titulo}\". Ya puedes registrar tu avance y estudiarlo con el cronómetro.";
        return RedirectToAction(nameof(MisCursos));
    }

    [HttpGet]
    public async Task<IActionResult> EditarCursoPersonal(int id)
    {
        var curso = await _db.Cursos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.PropietarioId == UsuarioId);
        if (curso is null) return NotFound();

        return View("FormCursoPersonal", new CursoPersonalViewModel
        {
            Id = curso.Id,
            Titulo = curso.Titulo,
            Descripcion = curso.Descripcion,
            Plataforma = curso.Plataforma,
            UrlExterna = curso.UrlExterna,
            Instructor = curso.Instructor,
            Categoria = curso.Categoria,
            Horas = curso.Horas
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarCursoPersonal(int id, CursoPersonalViewModel model)
    {
        var usuarioId = UsuarioId;
        var curso = await _db.Cursos.FirstOrDefaultAsync(c => c.Id == id && c.PropietarioId == usuarioId);
        if (curso is null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("FormCursoPersonal", model);
        }

        curso.Titulo = model.Titulo;
        curso.Descripcion = model.Descripcion;
        curso.Plataforma = model.Plataforma;
        curso.UrlExterna = model.UrlExterna;
        curso.Instructor = model.Instructor;
        curso.Categoria = model.Categoria;
        curso.Horas = model.Horas;
        await _db.SaveChangesAsync();
        await _cache.InvalidarUsuarioAsync(usuarioId);

        TempData["Mensaje"] = $"Curso \"{curso.Titulo}\" actualizado.";
        return RedirectToAction(nameof(MisCursos));
    }

    // El alumno registra su propio progreso y nota en un curso personal
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarCursoPersonal(int id, int progreso, decimal? nota, string? estado)
    {
        var usuarioId = UsuarioId;
        var inscripcion = await _db.Inscripciones
            .Include(i => i.Curso)
            .FirstOrDefaultAsync(i => i.CursoId == id && i.UsuarioId == usuarioId && i.Curso!.PropietarioId == usuarioId);
        if (inscripcion is null) return NotFound();

        if (progreso < 0 || progreso > 100 || (nota.HasValue && (nota < 0 || nota > 100)))
        {
            TempData["Error"] = "El progreso y la nota deben estar entre 0 y 100.";
            return RedirectToAction(nameof(MisCursos), new { estado });
        }

        inscripcion.Progreso = progreso;
        if (inscripcion.Nota != nota)
        {
            inscripcion.Nota = nota.HasValue ? Math.Round(nota.Value, 1) : null;
            inscripcion.FechaCalificacion = nota.HasValue ? DateTime.UtcNow : null;
        }
        await _db.SaveChangesAsync();
        await _cache.InvalidarUsuarioAsync(usuarioId);

        TempData["Mensaje"] = $"Avance de \"{inscripcion.Curso?.Titulo}\" guardado.";
        return RedirectToAction(nameof(MisCursos), new { estado });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCursoPersonal(int id)
    {
        var usuarioId = UsuarioId;
        var curso = await _db.Cursos.FirstOrDefaultAsync(c => c.Id == id && c.PropietarioId == usuarioId);
        if (curso is null) return NotFound();

        _db.Cursos.Remove(curso);   // su inscripción y sus sesiones de estudio se borran en cascada
        await _db.SaveChangesAsync();
        await _cache.InvalidarUsuarioAsync(usuarioId);

        TempData["Mensaje"] = $"Eliminaste tu curso \"{curso.Titulo}\".";
        return RedirectToAction(nameof(MisCursos));
    }

    // ---------- Desempeño ----------

    public async Task<IActionResult> Desempeno()
    {
        var id = UsuarioId;
        var inscripciones = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));

        // Horas por semana de las últimas 8 semanas
        var inicioSemana = InicioSemanaLocal();
        var desde = inicioSemana.AddDays(-7 * 7);
        var sesiones = await SesionesDesdeAsync(id, desde);
        var horasPorSemana = new double[8];
        foreach (var s in sesiones)
        {
            var indice = (int)((s.Inicio.ToLocalTime().Date - desde).TotalDays / 7);
            if (indice is >= 0 and < 8) horasPorSemana[indice] += s.Segundos / 3600.0;
        }
        var semanas = Enumerable.Range(0, 8).Select(k => desde.AddDays(7 * k).ToString("dd/MM")).ToArray();

        var conNota = inscripciones.Where(i => i.Nota.HasValue).ToList();

        var fortalezas = conNota.Where(i => i.Nota >= 85)
            .OrderByDescending(i => i.Nota)
            .Select(i => $"{i.Curso?.Titulo}: {Calificacion.Texto(i.Nota)}")
            .ToList();
        fortalezas.AddRange(inscripciones.Where(i => i.Progreso >= 100 && !i.Nota.HasValue)
            .Select(i => $"Completaste {i.Curso?.Titulo}"));

        var mejoras = conNota.Where(i => i.Nota < 70)
            .OrderBy(i => i.Nota)
            .Select(i => $"Reforzar {i.Curso?.Titulo} ({Calificacion.Texto(i.Nota)})")
            .ToList();
        mejoras.AddRange(inscripciones.Where(i => i.Progreso == 0)
            .Select(i => $"Iniciar el curso {i.Curso?.Titulo}"));
        mejoras.AddRange(inscripciones.Where(i => i.Progreso > 0 && i.Progreso < 30)
            .Select(i => $"Avanzar en {i.Curso?.Titulo} (solo {i.Progreso}%)"));

        var model = new DesempenoViewModel
        {
            Inscripciones = inscripciones.OrderByDescending(i => i.Nota ?? -1).ToList(),
            PromedioNota = Promedio(inscripciones),
            CreditosTotales = inscripciones.Sum(i => i.Curso?.Creditos ?? 0),
            CreditosAprobados = inscripciones.Where(i => i.Progreso >= 100).Sum(i => i.Curso?.Creditos ?? 0),
            Completados = inscripciones.Count(i => i.Progreso >= 100),
            HorasTotales = await SegundosTotalesAsync(id) / 3600.0,
            Semanas = semanas,
            HorasPorSemana = horasPorSemana.Select(h => Math.Round(h, 2)).ToArray(),
            RangoA = conNota.Count(i => i.Nota >= 90),
            RangoB = conNota.Count(i => i.Nota >= 80 && i.Nota < 90),
            RangoC = conNota.Count(i => i.Nota < 80),
            Pendientes = inscripciones.Count - conNota.Count,
            Fortalezas = fortalezas.Take(5).ToList(),
            AreasMejora = mejoras.Take(5).ToList(),
            HorasEstaSemana = horasPorSemana[7],
            HorasSemanaAnterior = horasPorSemana[6],
            SesionesEstaSemana = sesiones.Count(s => s.Inicio.ToLocalTime().Date >= inicioSemana)
        };
        return View(model);
    }

    // ---------- Cronómetro de estudio ----------

    public async Task<IActionResult> Estudio(int? cursoId)
    {
        var id = UsuarioId;
        var inscripciones = await _cache.InscripcionesAsync(id, () => LeerInscripcionesAsync(id));
        var hoy = await SesionesDesdeAsync(id, DateTime.Now.Date);
        var recientes = await _db.SesionesEstudio
            .AsNoTracking()
            .Include(s => s.Curso)
            .Where(s => s.UsuarioId == id)
            .OrderByDescending(s => s.Inicio)
            .Take(5)
            .ToListAsync();

        var model = new EstudioViewModel
        {
            Cursos = inscripciones.Where(i => i.Curso is not null).Select(i => i.Curso!).ToList(),
            CursoId = cursoId,
            SegundosHoy = hoy.Sum(s => s.Segundos),
            Recientes = recientes
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarSesion(int cursoId, int segundos)
    {
        var id = UsuarioId;
        if (segundos < 5 || segundos > 12 * 3600)
            return Json(new { ok = false, mensaje = "La sesión debe durar entre 5 segundos y 12 horas." });

        var inscrito = await _db.Inscripciones.AnyAsync(i => i.UsuarioId == id && i.CursoId == cursoId);
        if (!inscrito)
            return Json(new { ok = false, mensaje = "Elige un curso en el que estés inscrito." });

        _db.SesionesEstudio.Add(new SesionEstudio
        {
            UsuarioId = id,
            CursoId = cursoId,
            Inicio = DateTime.UtcNow.AddSeconds(-segundos),
            Segundos = segundos
        });
        await _db.SaveChangesAsync();

        return Json(new { ok = true, mensaje = $"Sesión guardada: {Calificacion.Duracion(segundos)} de estudio." });
    }

    // ---------- Donaciones ----------

    public async Task<IActionResult> Donaciones()
    {
        var id = UsuarioId;
        var todas = await _db.Donaciones
            .AsNoTracking()
            .Include(d => d.Usuario)
            .OrderByDescending(d => d.Fecha)
            .ToListAsync();

        string NombrePublico(Donacion d) =>
            d.Anonima ? "Estudiante Anónimo" : (d.Usuario?.NombreCompleto ?? "Estudiante");

        var historial = await _cache.DonacionesAsync(id, () => LeerDonacionesAsync(id));

        var model = new DonacionesViewModel
        {
            TotalRecaudado = todas.Sum(d => d.Monto),
            TotalDonaciones = todas.Count,
            Donantes = todas.Select(d => d.UsuarioId).Distinct().Count(),
            Recientes = todas.Take(6).Select(d => new AporteViewModel
            {
                Nombre = NombrePublico(d),
                Monto = d.Monto,
                Mensaje = d.Mensaje,
                Fecha = d.Fecha
            }).ToList(),
            TablaHonor = todas
                .GroupBy(d => d.UsuarioId)
                .Select(g => new
                {
                    Nombre = g.All(d => d.Anonima) ? "Estudiante Anónimo" : NombrePublico(g.First(d => !d.Anonima)),
                    Total = g.Sum(d => d.Monto)
                })
                .OrderByDescending(x => x.Total)
                .Take(5)
                .Select((x, i) => new HonorViewModel { Posicion = i + 1, Nombre = x.Nombre, Total = x.Total })
                .ToList(),
            MiHistorial = historial,
            MiTotal = historial.Sum(d => d.Monto)
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Donar(decimal monto, string? mensaje, bool anonima)
    {
        if (monto < 1 || monto > 10000)
        {
            TempData["Error"] = "El monto debe estar entre 1 y 10 000.";
            return RedirectToAction(nameof(Donaciones));
        }

        var texto = string.IsNullOrWhiteSpace(mensaje) ? null : mensaje.Trim();
        if (texto is { Length: > 200 }) texto = texto[..200];

        // Machine learning: se analiza si el mensaje es positivo, negativo o neutral
        var analisis = await _sentimiento.AnalizarAsync(texto);

        var id = UsuarioId;
        _db.Donaciones.Add(new Donacion
        {
            UsuarioId = id,
            Monto = monto,
            Mensaje = texto,
            Anonima = anonima,
            Sentimiento = analisis.Etiqueta,
            SentimientoConfianza = analisis.Confianza
        });
        await _db.SaveChangesAsync();
        await _cache.InvalidarUsuarioAsync(id);

        TempData["Mensaje"] = "Gracias, tu donación quedó registrada.";
        return RedirectToAction(nameof(Donaciones));
    }
}
