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

    // Si es true, en la tabla de honor aparece como "Estudiante Anónimo"
    public bool Anonima { get; set; }

    // Resultado del análisis de sentimiento (machine learning): Positiva, Negativa, Neutral...
    [StringLength(20)]
    public string? Sentimiento { get; set; }

    public double? SentimientoConfianza { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
