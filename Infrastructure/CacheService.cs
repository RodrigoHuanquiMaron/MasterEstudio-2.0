using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCursos.Models;

namespace PlataformaCursos.Infrastructure;

// Nombres de las claves que se guardan en Redis
public static class Claves
{
    public const string VersionCatalogo = "cursos:version";
    public const string ResumenAdmin = "admin:resumen";
    public static string Catalogo(string version) => $"cursos:catalogo:v{version}";
    public static string Inscripciones(string usuarioId, string version) => $"usuario:{usuarioId}:inscripciones:v{version}";
    public static string Donaciones(string usuarioId) => $"usuario:{usuarioId}:donaciones";
}

public class CacheService
{
    public static readonly TimeSpan Duracion = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly string _origen;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger, IConfiguration config)
    {
        _cache = cache;
        _logger = logger;
        _origen = ConexionRedis.Obtener(config) is null ? "memoria local" : "Redis";
    }

    // Patrón "cache-aside": primero busca en Redis; si no está, lee de PostgreSQL y lo guarda 60 s
    public async Task<T> ObtenerOCrearAsync<T>(string clave, Func<Task<T>> leerDeBase)
    {
        string? json;
        try
        {
            json = await _cache.GetStringAsync(clave);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer {Clave} de {Origen}; se usa PostgreSQL", clave, _origen);
            return await leerDeBase();
        }

        if (json is not null)
        {
            _logger.LogInformation("CACHE HIT  {Clave} -> lectura desde {Origen}", clave, _origen);
            return JsonSerializer.Deserialize<T>(json, OpcionesJson)!;
        }

        _logger.LogInformation("CACHE MISS {Clave} -> lectura desde PostgreSQL", clave);
        var datos = await leerDeBase();

        try
        {
            await _cache.SetStringAsync(clave, JsonSerializer.Serialize(datos, OpcionesJson),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duracion });
            _logger.LogInformation("Guardado en {Origen}: {Clave} (expira en {Segundos} s)",
                _origen, clave, Duracion.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar {Clave} en {Origen}", clave, _origen);
        }

        return datos;
    }

    public async Task<List<Curso>> CatalogoAsync(Func<Task<List<Curso>>> leerDeBase)
        => await ObtenerOCrearAsync(Claves.Catalogo(await VersionCatalogoAsync()), leerDeBase);

    public async Task<List<Inscripcion>> InscripcionesAsync(string usuarioId, Func<Task<List<Inscripcion>>> leerDeBase)
        => await ObtenerOCrearAsync(Claves.Inscripciones(usuarioId, await VersionCatalogoAsync()), leerDeBase);

    public Task<List<Donacion>> DonacionesAsync(string usuarioId, Func<Task<List<Donacion>>> leerDeBase)
        => ObtenerOCrearAsync(Claves.Donaciones(usuarioId), leerDeBase);

    // Se llama cuando un usuario se inscribe, avanza o dona
    public async Task InvalidarUsuarioAsync(string usuarioId)
    {
        var version = await VersionCatalogoAsync();
        await EliminarAsync(Claves.Inscripciones(usuarioId, version));
        await EliminarAsync(Claves.Donaciones(usuarioId));
        await EliminarAsync(Claves.ResumenAdmin);
    }

    // Se llama cuando el administrador agrega o elimina un curso.
    // Cambiar la versión invalida de golpe el catálogo y los cursos de todos los usuarios.
    public async Task CursosCambiaronAsync()
    {
        try
        {
            await _cache.SetStringAsync(Claves.VersionCatalogo, DateTime.UtcNow.Ticks.ToString());
            _logger.LogInformation("Catálogo de cursos invalidado en {Origen} (nueva versión)", _origen);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar el catálogo en {Origen}", _origen);
        }
        await EliminarAsync(Claves.ResumenAdmin);
    }

    private async Task<string> VersionCatalogoAsync()
    {
        try { return await _cache.GetStringAsync(Claves.VersionCatalogo) ?? "0"; }
        catch { return "0"; }
    }

    private async Task EliminarAsync(string clave)
    {
        try
        {
            await _cache.RemoveAsync(clave);
            _logger.LogInformation("Clave invalidada en {Origen}: {Clave}", _origen, clave);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar {Clave} en {Origen}", clave, _origen);
        }
    }
}