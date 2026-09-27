using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCursos.Models;

namespace PlataformaCursos.Data;

public static class SeedData
{
    public const string RolAdmin = "Administrador";
    public const string RolUsuario = "Usuario";

    public static async Task InicializarAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var env = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        // Crea o actualiza las tablas en PostgreSQL
        await db.Database.MigrateAsync();

        // 1. Roles
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in new[] { RolAdmin, RolUsuario })
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        // 2. Administrador inicial
        //    En Render se definen las variables Admin__Email y Admin__Password.
        //    Solo en desarrollo se usan valores por defecto.
        var config = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];

        if (env.IsDevelopment())
        {
            email ??= "admin@cursos.com";
            password ??= "Admin123";
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No se creó el administrador: define las variables Admin__Email y Admin__Password.");
        }
        else if (await userManager.FindByEmailAsync(email) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                NombreCompleto = "Administrador",
                EmailConfirmed = true
            };
            var resultado = await userManager.CreateAsync(admin, password);
            if (resultado.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, RolAdmin);
                logger.LogInformation("Administrador creado: {Email}", email);
            }
            else
            {
                logger.LogError("No se pudo crear el administrador: {Errores}",
                    string.Join(", ", resultado.Errors.Select(e => e.Description)));
            }
        }

        // 3. Cursos de prueba
        if (!await db.Cursos.AnyAsync())
        {
            db.Cursos.AddRange(
                new Curso { Titulo = "Introducción a C#", Descripcion = "Variables, condicionales, bucles y clases.", Horas = 20 },
                new Curso { Titulo = "ASP.NET Core MVC", Descripcion = "Controladores, vistas, modelos y rutas.", Horas = 30 },
                new Curso { Titulo = "Bases de datos con SQL", Descripcion = "Consultas, relaciones e índices.", Horas = 25 }
            );
            await db.SaveChangesAsync();
        }
    }
}