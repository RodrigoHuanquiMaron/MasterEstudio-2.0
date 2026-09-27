using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlataformaCursos.Data;
using PlataformaCursos.Models;

namespace PlataformaCursos.Controllers;

public class HomeController : Controller
{
    // Envía a cada rol a su propio panel
    public IActionResult Index()
    {
        if (User.IsInRole(SeedData.RolAdmin))
            return RedirectToAction("Index", "Admin");

        if (User.IsInRole(SeedData.RolUsuario))
            return RedirectToAction("Panel", "Usuario");

        return View(); // página de bienvenida para visitantes
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}