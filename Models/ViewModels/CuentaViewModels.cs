using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu correo.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [Display(Name = "Correo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Recordarme")]
    public bool RecordarMe { get; set; }
}

public class RegistroViewModel
{
    [Required(ErrorMessage = "Ingresa tu nombre.")]
    [StringLength(100)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu correo.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [Display(Name = "Correo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa una contraseña.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}public class MiSesionViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public DateTime? InicioSesion { get; set; }
    public int PaginasVisitadas { get; set; }
    public string IdSesion { get; set; } = string.Empty;
    public bool CookiePersistente { get; set; }
    public DateTime? ExpiraCookie { get; set; }
    public string? CorreoRecordado { get; set; }
}