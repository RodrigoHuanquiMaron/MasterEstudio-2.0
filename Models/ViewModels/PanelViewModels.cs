namespace PlataformaCursos.Models.ViewModels;

public class PanelUsuarioViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public int CursosInscritos { get; set; }
    public int ProgresoPromedio { get; set; }
    public decimal TotalDonado { get; set; }
    public List<Curso> CursosDisponibles { get; set; } = new();
}

public class DonacionesViewModel
{
    public List<Donacion> Historial { get; set; } = new();
    public decimal Total { get; set; }
}

public class ResumenAdminViewModel
{
    public int TotalUsuarios { get; set; }
    public int TotalCursos { get; set; }
    public int TotalInscripciones { get; set; }
    public decimal TotalDonado { get; set; }
}

public class UsuarioResumenViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int CursosInscritos { get; set; }
    public DateTime FechaRegistro { get; set; }
}

public class PerfilUsuarioViewModel
{
    public ApplicationUser Usuario { get; set; } = null!;
    public string Rol { get; set; } = string.Empty;
    public List<Inscripcion> Inscripciones { get; set; } = new();
    public List<Donacion> Donaciones { get; set; } = new();
    public decimal TotalDonado { get; set; }
}