using System.ComponentModel.DataAnnotations;

namespace PlataformaCursos.Models.ViewModels;

public class RecordatorioFormModel
{
    [Required(ErrorMessage = "Escribe un título.")]
    [StringLength(120)]
    public string Titulo { get; set; } = string.Empty;

    public string Tipo { get; set; } = "Examen";

    public int? CursoId { get; set; }

    [Required(ErrorMessage = "Elige la fecha y hora.")]
    public DateTime? Fecha { get; set; }

    [Range(15, 480)]
    public int DuracionMinutos { get; set; } = 60;

    [Range(0, 10080)]
    public int AvisoMinutosAntes { get; set; } = 1440;

    [StringLength(500)]
    public string? Notas { get; set; }

    public bool EnviarAGoogle { get; set; } = true;
}

public class RecordatoriosPanelViewModel
{
    public List<Recordatorio> Proximos { get; set; } = new();
    public int Vencidos { get; set; }
    public bool GoogleDisponible { get; set; }   // el servidor tiene ClientId y ClientSecret
    public bool GoogleConectado { get; set; }
    public string? GoogleCorreo { get; set; }
}
