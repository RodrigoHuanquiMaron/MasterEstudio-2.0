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
        await db.Database.MigrateAsync();

        // 1. Roles
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in new[] { RolAdmin, RolUsuario })
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        // 2. Administrador inicial (en la Parte 3 estos valores vendrán de variables de entorno)
        var config = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = config["Admin:Email"] ?? "admin@cursos.com";
        var password = config["Admin:Password"] ?? "Admin123";

        if (await userManager.FindByEmailAsync(email) is null)
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
                await userManager.AddToRoleAsync(admin, RolAdmin);
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