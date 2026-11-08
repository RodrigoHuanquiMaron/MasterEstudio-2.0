using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

[Authorize]
public class TalleresController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AlgoliaService _algolia;

    public TalleresController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AlgoliaService algolia)
    {
        _db = db;
        _userManager = userManager;
        _algolia = algolia;
    }

    private string UsuarioId => _userManager.GetUserId(User)!;

    // Convierte la hora que escribió el admin (hora local) a UTC para guardarla en PostgreSQL
    private static DateTime ALocalUtc(DateTime fecha) =>
        fecha.Kind == DateTimeKind.Utc ? fecha : DateTime.SpecifyKind(fecha, DateTimeKind.Local).ToUniversalTime();

    // ---------- Vista del alumno: lista y buscador ----------

    public async Task<IActionResult> Index(string? q, string? modalidad)
    {
        var texto = q?.Trim();
        var origen = "Lista";
        List<Taller> talleres;

        if (string.IsNullOrEmpty(texto))
        {
            // Sin búsqueda: lista habitual desde la base, próximos primero
            talleres = await _db.Talleres.AsNoTracking()
                .OrderBy(t => t.Fecha < DateTime.UtcNow).ThenBy(t => t.Fecha)
                .ToListAsync();
        }
        else
        {
            var ids = await _algolia.BuscarAsync(texto);
            if (ids is not null)
            {
                // Algolia devuelve los Id por relevancia; se muestran solo los que siguen en la base
                origen = "Algolia";
                var encontrados = await _db.Talleres.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync();
                talleres = encontrados.OrderBy(t => ids.IndexOf(t.Id)).ToList();
            }
            else
            {
                // Respaldo si Algolia no está configurado o falla
                origen = "Base de datos";
                var patron = $"%{texto}%";
                talleres = await _db.Talleres.AsNoTracking()
                    .Where(t => EF.Functions.ILike(t.Titulo, patron)
                             || (t.Descripcion != null && EF.Functions.ILike(t.Descripcion, patron))
                             || (t.Facilitador != null && EF.Functions.ILike(t.Facilitador, patron))
                             || (t.Categoria != null && EF.Functions.ILike(t.Categoria, patron)))
                    .OrderBy(t => t.Fecha)
                    .ToListAsync();
            }
        }

        if (!string.IsNullOrEmpty(modalidad))
            talleres = talleres.Where(t => t.Modalidad == modalidad).ToList();

        var tarjetas = await ArmarTarjetasAsync(talleres);
        var misIds = await _db.InscripcionesTaller.Where(i => i.UsuarioId == UsuarioId).Select(i => i.TallerId).ToListAsync();
        var misTalleres = await _db.Talleres.AsNoTracking().Where(t => misIds.Contains(t.Id)).OrderBy(t => t.Fecha).ToListAsync();

        var model = new TalleresViewModel
        {
            Busqueda = texto,
            Modalidad = modalidad,
            Origen = origen,
            AlgoliaConfigurado = _algolia.Configurado,
            Talleres = tarjetas,
            MisTalleres = await ArmarTarjetasAsync(misTalleres)
        };
        return View(model);
    }

    private async Task<List<TallerTarjetaViewModel>> ArmarTarjetasAsync(List<Taller> talleres)
    {
        var ids = talleres.Select(t => t.Id).ToList();
        var conteos = await _db.InscripcionesTaller
            .Where(i => ids.Contains(i.TallerId))
            .GroupBy(i => i.TallerId)
            .Select(g => new { TallerId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.TallerId, x => x.Cantidad);

        var usuarioId = UsuarioId;
        var mios = await _db.InscripcionesTaller
            .Where(i => i.UsuarioId == usuarioId && ids.Contains(i.TallerId))
            .Select(i => i.TallerId)
            .ToListAsync();

        return talleres.Select(t => new TallerTarjetaViewModel
        {
            Taller = t,
            Inscritos = conteos.GetValueOrDefault(t.Id),
            YoInscrito = mios.Contains(t.Id)
        }).ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolUsuario)]
    public async Task<IActionResult> Inscribirse(int id, string? q, string? modalidad)
    {
        var usuarioId = UsuarioId;
        var taller = await _db.Talleres.FindAsync(id);
        if (taller is null) return NotFound();

        var inscritos = await _db.InscripcionesTaller.CountAsync(i => i.TallerId == id);
        var yaInscrito = await _db.InscripcionesTaller.AnyAsync(i => i.TallerId == id && i.UsuarioId == usuarioId);

        if (taller.Fecha < DateTime.UtcNow)
            TempData["Error"] = "Este taller ya se realizó.";
        else if (yaInscrito)
            TempData["Error"] = "Ya estás inscrito en este taller.";
        else if (inscritos >= taller.Cupos)
            TempData["Error"] = "Ya no quedan cupos en este taller.";
        else
        {
            _db.InscripcionesTaller.Add(new InscripcionTaller { UsuarioId = usuarioId, TallerId = id });
            await _db.SaveChangesAsync();
            TempData["Mensaje"] = $"Te inscribiste en \"{taller.Titulo}\".";
        }
        return RedirectToAction(nameof(Index), new { q, modalidad });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolUsuario)]
    public async Task<IActionResult> Cancelar(int id, string? q, string? modalidad)
    {
        var usuarioId = UsuarioId;
        var inscripcion = await _db.InscripcionesTaller
            .Include(i => i.Taller)
            .FirstOrDefaultAsync(i => i.TallerId == id && i.UsuarioId == usuarioId);

        if (inscripcion is not null)
        {
            _db.InscripcionesTaller.Remove(inscripcion);
            await _db.SaveChangesAsync();
            TempData["Mensaje"] = $"Cancelaste tu inscripción en \"{inscripcion.Taller?.Titulo}\".";
        }
        return RedirectToAction(nameof(Index), new { q, modalidad });
    }

    // ---------- Administración ----------

    [Authorize(Roles = SeedData.RolAdmin)]
    public async Task<IActionResult> Gestionar()
    {
        var talleres = await _db.Talleres.AsNoTracking()
            .Include(t => t.Inscripciones)
            .OrderBy(t => t.Fecha)
            .ToListAsync();
        ViewData["AlgoliaConfigurado"] = _algolia.Configurado;
        return View(talleres);
    }

    [HttpGet]
    [Authorize(Roles = SeedData.RolAdmin)]
    public IActionResult Crear()
    {
        ViewData["Accion"] = nameof(Crear);
        var taller = new Taller { Fecha = DateTime.Now.Date.AddDays(7).AddHours(18) };
        return View("Form", taller);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolAdmin)]
    public async Task<IActionResult> Crear([Bind("Titulo,Descripcion,Facilitador,Categoria,Modalidad,Lugar,Fecha,DuracionMinutos,Cupos,ImagenUrl")] Taller taller)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Accion"] = nameof(Crear);
            return View("Form", taller);
        }

        taller.Fecha = ALocalUtc(taller.Fecha);
        _db.Talleres.Add(taller);
        await _db.SaveChangesAsync();          // 1. PostgreSQL
        await _algolia.GuardarAsync(taller);   // 2. Algolia

        TempData["Mensaje"] = $"Taller \"{taller.Titulo}\" creado.";
        return RedirectToAction(nameof(Gestionar));
    }

    [HttpGet]
    [Authorize(Roles = SeedData.RolAdmin)]
    public async Task<IActionResult> Editar(int id)
    {
        var taller = await _db.Talleres.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (taller is null) return NotFound();

        taller.Fecha = taller.Fecha.ToLocalTime();   // el formulario muestra la hora local
        ViewData["Accion"] = nameof(Editar);
        return View("Form", taller);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolAdmin)]
    public async Task<IActionResult> Editar(int id, [Bind("Titulo,Descripcion,Facilitador,Categoria,Modalidad,Lugar,Fecha,DuracionMinutos,Cupos,ImagenUrl")] Taller datos)
    {
        var taller = await _db.Talleres.FindAsync(id);
        if (taller is null) return NotFound();

        if (!ModelState.IsValid)
        {
            datos.Id = id;
            ViewData["Accion"] = nameof(Editar);
            return View("Form", datos);
        }

        taller.Titulo = datos.Titulo;
        taller.Descripcion = datos.Descripcion;
        taller.Facilitador = datos.Facilitador;
        taller.Categoria = datos.Categoria;
        taller.Modalidad = datos.Modalidad;
        taller.Lugar = datos.Lugar;
        taller.Fecha = ALocalUtc(datos.Fecha);
        taller.DuracionMinutos = datos.DuracionMinutos;
        taller.Cupos = datos.Cupos;
        taller.ImagenUrl = datos.ImagenUrl;

        await _db.SaveChangesAsync();
        await _algolia.GuardarAsync(taller);

        TempData["Mensaje"] = $"Taller \"{taller.Titulo}\" actualizado.";
        return RedirectToAction(nameof(Gestionar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolAdmin)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var taller = await _db.Talleres.FindAsync(id);
        if (taller is null) return NotFound();

        _db.Talleres.Remove(taller);           // sus inscripciones se borran en cascada
        await _db.SaveChangesAsync();          // 1. PostgreSQL
        await _algolia.EliminarAsync(id);      // 2. Algolia: ya no aparecerá en la búsqueda

        TempData["Mensaje"] = $"Taller \"{taller.Titulo}\" eliminado.";
        return RedirectToAction(nameof(Gestionar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolAdmin)]
    public async Task<IActionResult> Sincronizar()
    {
        if (!_algolia.Configurado)
        {
            TempData["Error"] = "Algolia no está configurado: faltan Algolia__AppId y Algolia__AdminKey.";
            return RedirectToAction(nameof(Gestionar));
        }

        var talleres = await _db.Talleres.AsNoTracking().ToListAsync();
        var ok = await _algolia.ReindexarAsync(talleres);

        if (ok) TempData["Mensaje"] = $"Se sincronizaron {talleres.Count} talleres con Algolia.";
        else TempData["Error"] = "No se pudo sincronizar con Algolia. Revisa las claves y los logs.";
        return RedirectToAction(nameof(Gestionar));
    }
}
