using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models;

public class Donacion
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    [Range(1, 10000, ErrorMessage = "El monto debe estar entre 1 y 10 000.")]
    public decimal Monto { get; set; }

    [StringLength(200)]
    public string? Mensaje { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}