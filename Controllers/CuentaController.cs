using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PlataformaCursos.Data;
using PlataformaCursos.Infrastructure;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

public class CuentaController : Controller
{
    private const string CookieCorreo = "MasterEstudio.UltimoCorreo";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public CuentaController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Si la cookie de "Recordarme" sigue vigente, no hace falta volver a entrar
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;

        // Rellena el correo si el usuario pidió que lo recordáramos
        var correoGuardado = Request.Cookies[CookieCorreo];
        return View(new LoginViewModel
        {
            Email = correoGuardado ?? string.Empty,
            RecordarMe = correoGuardado is not null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        // RecordarMe = true  -> cookie persistente de 7 días
        // RecordarMe = false -> cookie de sesión (se borra al cerrar el navegador)
        var resultado = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RecordarMe, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            var usuario = await _userManager.FindByEmailAsync(model.Email);
            if (usuario is not null)
            {
                var roles = await _userManager.GetRolesAsync(usuario);
                SesionHelper.Cargar(HttpContext.Session, usuario, roles.FirstOrDefault() ?? SeedData.RolUsuario);
            }

            GuardarCorreoRecordado(model.Email, model.RecordarMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, resultado.IsLockedOut
            ? "Cuenta bloqueada por varios intentos fallidos. Vuelve a intentarlo en 5 minutos."
            : "Correo o contraseña incorrectos.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Registro() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(RegistroViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var usuario = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            NombreCompleto = model.NombreCompleto
        };

        var resultado = await _userManager.CreateAsync(usuario, model.Password);
        if (resultado.Succeeded)
        {
            await _userManager.AddToRoleAsync(usuario, SeedData.RolUsuario);
            await _signInManager.SignInAsync(usuario, isPersistent: false);
            SesionHelper.Cargar(HttpContext.Session, usuario, SeedData.RolUsuario);
            return RedirectToAction("Panel", "Usuario");
        }

        foreach (var error in resultado.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();          // borra los datos de la sesión
        await _signInManager.SignOutAsync();  // borra la cookie de autenticación
        return RedirectToAction("Index", "Home");
    }

    // Muestra el estado de la sesión y las cookies (útil para la demostración)
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> MiSesion()
    {
        var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var sesion = HttpContext.Session;

        DateTime? inicio = null;
        if (DateTime.TryParse(sesion.GetString(SesionKeys.InicioSesion), null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var fecha))
            inicio = fecha.ToLocalTime();

        var model = new MiSesionViewModel
        {
            Nombre = sesion.GetString(SesionKeys.Nombre) ?? "",
            Rol = sesion.GetString(SesionKeys.Rol) ?? "",
            InicioSesion = inicio,
            PaginasVisitadas = sesion.GetInt32(SesionKeys.PaginasVisitadas) ?? 0,
            IdSesion = sesion.Id,
            CookiePersistente = auth.Properties?.IsPersistent ?? false,
            ExpiraCookie = auth.Properties?.ExpiresUtc?.ToLocalTime().DateTime,
            CorreoRecordado = Request.Cookies[CookieCorreo]
        };
        return View(model);
    }

    [HttpGet]
    public IActionResult AccesoDenegado() => View();

    private void GuardarCorreoRecordado(string email, bool recordar)
    {
        if (recordar)
        {
            Response.Cookies.Append(CookieCorreo, email, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });
        }
        else
        {
            Response.Cookies.Delete(CookieCorreo);
        }
    }
}