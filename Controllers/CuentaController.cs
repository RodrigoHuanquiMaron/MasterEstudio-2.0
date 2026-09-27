using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PlataformaCursos.Data;
using PlataformaCursos.Models;
using PlataformaCursos.Models.ViewModels;

namespace PlataformaCursos.Controllers;

public class CuentaController : Controller
{
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
        ViewData["ReturnUrl"] = returnUrl;
        return View();
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
            // Todo registro público es Usuario; los administradores se crean desde el seed
            await _userManager.AddToRoleAsync(usuario, SeedData.RolUsuario);
            await _signInManager.SignInAsync(usuario, isPersistent: false);
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
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccesoDenegado() => View();
}