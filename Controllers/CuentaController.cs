using System.Security.Claims;
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
    private readonly ILogger<CuentaController> _logger;

    public CuentaController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        ILogger<CuentaController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
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

    // ---------- Login con Google ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LoginExterno(string proveedor, string? returnUrl = null)
    {
        var urlRetorno = Url.Action(nameof(LoginExternoRespuesta), "Cuenta", new { returnUrl });
        var propiedades = _signInManager.ConfigureExternalAuthenticationProperties(proveedor, urlRetorno);
        return Challenge(propiedades, proveedor);
    }

    [HttpGet]
    public async Task<IActionResult> LoginExternoRespuesta(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError is not null)
        {
            TempData["Error"] = $"No se pudo iniciar sesión con el proveedor externo: {remoteError}";
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["Error"] = "No se recibieron los datos del proveedor externo. Inténtalo de nuevo.";
            return RedirectToAction(nameof(Login));
        }

        // ¿Ya inició sesión antes con Google?
        var resultado = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        ApplicationUser? usuario;
        if (resultado.Succeeded)
        {
            usuario = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        }
        else
        {
            if (resultado.IsLockedOut)
            {
                TempData["Error"] = "Cuenta bloqueada temporalmente.";
                return RedirectToAction(nameof(Login));
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Google no compartió tu correo electrónico.";
                return RedirectToAction(nameof(Login));
            }

            // Si no existe una cuenta con ese correo, se crea como Usuario
            usuario = await _userManager.FindByEmailAsync(email);
            if (usuario is null)
            {
                usuario = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    NombreCompleto = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email
                };
                var creado = await _userManager.CreateAsync(usuario);
                if (!creado.Succeeded)
                {
                    TempData["Error"] = string.Join(" ", creado.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Login));
                }
                await _userManager.AddToRoleAsync(usuario, SeedData.RolUsuario);
            }

            await _userManager.AddLoginAsync(usuario, info);
            await _signInManager.SignInAsync(usuario, isPersistent: true);
        }

        if (usuario is not null)
        {
            var roles = await _userManager.GetRolesAsync(usuario);
            SesionHelper.Cargar(HttpContext.Session, usuario, roles.FirstOrDefault() ?? SeedData.RolUsuario);
            _logger.LogInformation("Inicio de sesión con {Proveedor}: {Email}", info.LoginProvider, usuario.Email);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    // ---------- Registro ----------

    [HttpGet]
    public IActionResult Registro() => View(new RegistroViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(RegistroViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var usuario = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            NombreCompleto = model.NombreCompleto,
            Carrera = model.Carrera,
            Ciclo = model.Ciclo
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
        HttpContext.Session.Clear();
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // Muestra el estado de la sesión y las cookies
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
