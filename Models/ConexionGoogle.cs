using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

// Permiso que dio el alumno para crear eventos en su Google Calendar.
// El refresh token se guarda cifrado con Data Protection.
public class ConexionGoogle
{
    [Key]
    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public string RefreshTokenCifrado { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Correo { get; set; }

    public DateTime FechaConexion { get; set; } = DateTime.UtcNow;
}
