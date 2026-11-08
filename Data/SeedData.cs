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

        // 4. Completa instructor, categoría y créditos de los cursos de prueba que aún no los tienen
        var detalles = new Dictionary<string, (string Instructor, string Categoria, int Creditos)>
        {
            ["Introducción a C#"] = ("Ing. Roberto Gómez", "Tecnología", 4),
            ["ASP.NET Core MVC"] = ("Mg. Alan García", "Tecnología", 4),
            ["Bases de datos con SQL"] = ("Dra. Alicia Méndez", "Tecnología", 3)
        };

        var sinDetalle = await db.Cursos.Where(c => c.Categoria == null).ToListAsync();
        foreach (var curso in sinDetalle)
        {
            if (detalles.TryGetValue(curso.Titulo, out var d))
            {
                curso.Instructor ??= d.Instructor;
                curso.Categoria = d.Categoria;
                curso.Creditos = d.Creditos;
            }
            else if (curso.Creditos < 1)
            {
                curso.Creditos = 3;
            }
        }
        if (sinDetalle.Count > 0)
            await db.SaveChangesAsync();

        // 5. Talleres de prueba
        if (!await db.Talleres.AnyAsync())
        {
            var hoy = DateTime.UtcNow.Date;
            db.Talleres.AddRange(
                new Taller
                {
                    Titulo = "Git y GitHub desde cero",
                    Descripcion = "Ramas, commits, pull requests y resolución de conflictos con ejemplos reales.",
                    Facilitador = "Mg. Alan García",
                    Categoria = "Tecnología",
                    Modalidad = "Virtual",
                    Lugar = "Google Meet",
                    Fecha = hoy.AddDays(5).AddHours(23),
                    DuracionMinutos = 120,
                    Cupos = 40
                },
                new Taller
                {
                    Titulo = "Técnicas de estudio y concentración",
                    Descripcion = "Método Pomodoro, mapas mentales y planificación semanal para rendir mejor en los exámenes.",
                    Facilitador = "Lic. Ana Lucía Torres",
                    Categoria = "Otros",
                    Modalidad = "Presencial",
                    Lugar = "Auditorio principal",
                    Fecha = hoy.AddDays(9).AddHours(20),
                    DuracionMinutos = 90,
                    Cupos = 60
                },
                new Taller
                {
                    Titulo = "Excel para contabilidad",
                    Descripcion = "Fórmulas, tablas dinámicas y gráficos aplicados a estados financieros.",
                    Facilitador = "CPC. Carlos Mendoza",
                    Categoria = "Contabilidad",
                    Modalidad = "Híbrido",
                    Lugar = "Laboratorio 3 / Zoom",
                    Fecha = hoy.AddDays(14).AddHours(22),
                    DuracionMinutos = 150,
                    Cupos = 25
                }
            );
            await db.SaveChangesAsync();
        }
    }
}
