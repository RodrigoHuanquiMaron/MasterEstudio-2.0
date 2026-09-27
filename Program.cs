using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;

var builder = WebApplication.CreateBuilder(args);

// Base de datos (SQLite por ahora; en la Parte 3 se cambia a PostgreSQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// Cookie de autenticación: recuerda al usuario
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.LogoutPath = "/Cuenta/Logout";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";

    options.Cookie.Name = "MasterEstudio.Auth";
    options.Cookie.HttpOnly = true;                               // JavaScript no puede leerla
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);                // duración con "Recordarme"
    options.SlidingExpiration = true;                             // se renueva si el usuario sigue activo
});

// Sesión del servidor: datos temporales del usuario
builder.Services.AddDistributedMemoryCache();   // en la Parte 4 se cambia por Redis
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();                        // 1. ¿quién eres? (lee la cookie)
app.UseSession();                               // 2. carga la sesión
app.UseMiddleware<SesionUsuarioMiddleware>();   // 3. reconstruye la sesión si hace falta
app.UseAuthorization();                         // 4. ¿qué puedes hacer?

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();