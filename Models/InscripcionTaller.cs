namespace PlataformaCursos.Models;

public class InscripcionTaller
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public int TallerId { get; set; }
    public Taller? Taller { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
