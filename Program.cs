using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;

// Números con punto decimal en Windows y en Render (evita errores al enviar notas como 91.5)
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Base de datos PostgreSQL: usa DATABASE_URL (Render / user-secrets) o la cadena local
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(ConexionBD.Obtener(builder.Configuration)));

// Guarda en PostgreSQL las llaves que cifran las cookies
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("MasterEstudio");

// Identity con roles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Login con Google (solo se activa si existen las credenciales)
var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecreto = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecreto))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleId;
        options.ClientSecret = googleSecreto;
    });
}

// Cookie de autenticación: recuerda al usuario
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.LogoutPath = "/Cuenta/Logout";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";

    options.Cookie.Name = "MasterEstudio.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Caché distribuida: Redis si existe REDIS_URL; si no, memoria local.
var configuracionRedis = ConexionRedis.Obtener(builder.Configuration);
if (configuracionRedis is not null)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuracionRedis;
        options.InstanceName = "masterestudio:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddScoped<CacheService>();

// PieHost: WebSocket en tiempo real
builder.Services.Configure<PieHostOptions>(builder.Configuration.GetSection("PieHost"));
builder.Services.AddHttpClient<PieHostService>(client => client.Timeout = TimeSpan.FromSeconds(10));

// Algolia: buscador de talleres
builder.Services.Configure<AlgoliaOptions>(builder.Configuration.GetSection("Algolia"));
builder.Services.AddHttpClient<AlgoliaService>(client => client.Timeout = TimeSpan.FromSeconds(10));

// Cursos gratuitos de Microsoft Learn y YouTube
builder.Services.AddHttpClient<CursosGratuitosService>(client => client.Timeout = TimeSpan.FromSeconds(60));

// Tutor IA: preguntas y explicaciones con Groq
builder.Services.AddHttpClient<GroqService>(client => client.Timeout = TimeSpan.FromSeconds(90));

// Machine learning: análisis de sentimiento de donaciones con Hugging Face
builder.Services.AddHttpClient<SentimientoService>(client => client.Timeout = TimeSpan.FromSeconds(15));

// Sesión del servidor: datos temporales del usuario
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "MasterEstudio.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.Logger.LogInformation(configuracionRedis is null
    ? "Caché: memoria local (REDIS_URL no definido)"
    : "Caché: Redis conectado");

// Migraciones, roles, admin y cursos de prueba
using (var scope = app.Services.CreateScope())
{
    await SeedData.InicializarAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Render ya redirige a HTTPS por su cuenta, por eso no se usa UseHttpsRedirection
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseSession();
app.UseMiddleware<SesionUsuarioMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
