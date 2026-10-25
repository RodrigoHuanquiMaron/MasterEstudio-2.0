namespace PlataformaCursos.Models;

// Cada vez que el usuario usa el cronómetro y guarda, se crea una sesión de estudio
public class SesionEstudio
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public int CursoId { get; set; }
    public Curso? Curso { get; set; }

    public DateTime Inicio { get; set; } = DateTime.UtcNow;

    public int Segundos { get; set; }
}
