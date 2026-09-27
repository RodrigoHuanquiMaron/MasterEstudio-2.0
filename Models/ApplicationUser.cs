using Microsoft.AspNetCore.Identity;

namespace PlataformaCursos.Models;

public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
    public ICollection<Donacion> Donaciones { get; set; } = new List<Donacion>();
}