namespace PlataformaCursos.Models.ViewModels;

// ---------- Usuario ----------

public class ActividadViewModel
{
    public string Icono { get; set; } = "bi-dot";
    public string Texto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}

public class PanelUsuarioViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public string? Carrera { get; set; }
    public string? Ciclo { get; set; }

    public int CursosTotales { get; set; }
    public int CursosActivos { get; set; }
    public int Completados { get; set; }
    public decimal? PromedioNota { get; set; }
    public double HorasTotales { get; set; }
    public double HorasSemana { get; set; }

    public List<Inscripcion> Inscripciones { get; set; } = new();
    public List<Curso> CursosDisponibles { get; set; } = new();

    public double[] HorasPorDia { get; set; } = new double[7];
    public int IndiceHoy { get; set; }

    public List<ActividadViewModel> Actividades { get; set; } = new();
}

public class MisCursosViewModel
{
    public string Filtro { get; set; } = "todos";
    public List<Inscripcion> Inscripciones { get; set; } = new();
    public int Total { get; set; }
    public int EnCurso { get; set; }
    public int Completados { get; set; }
    public int NoIniciados { get; set; }
}

public class DesempenoViewModel
{
    public List<Inscripcion> Inscripciones { get; set; } = new();
    public decimal? PromedioNota { get; set; }
    public int CreditosAprobados { get; set; }
    public int CreditosTotales { get; set; }
    public int Completados { get; set; }
    public double HorasTotales { get; set; }

    public string[] Semanas { get; set; } = Array.Empty<string>();
    public double[] HorasPorSemana { get; set; } = Array.Empty<double>();

    public int RangoA { get; set; }
    public int RangoB { get; set; }
    public int RangoC { get; set; }
    public int Pendientes { get; set; }

    public List<string> Fortalezas { get; set; } = new();
    public List<string> AreasMejora { get; set; } = new();

    public double HorasEstaSemana { get; set; }
    public double HorasSemanaAnterior { get; set; }
    public int SesionesEstaSemana { get; set; }
}

public class EstudioViewModel
{
    public List<Curso> Cursos { get; set; } = new();
    public int? CursoId { get; set; }
    public int SegundosHoy { get; set; }
    public List<SesionEstudio> Recientes { get; set; } = new();
    public List<ResultadoQuiz> ResultadosQuiz { get; set; } = new();
    public bool TutorConfigurado { get; set; }
}

public class AporteViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? Mensaje { get; set; }
    public DateTime Fecha { get; set; }
}

public class HonorViewModel
{
    public int Posicion { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class DonacionesViewModel
{
    public decimal TotalRecaudado { get; set; }
    public int TotalDonaciones { get; set; }
    public int Donantes { get; set; }
    public List<AporteViewModel> Recientes { get; set; } = new();
    public List<HonorViewModel> TablaHonor { get; set; } = new();
    public List<Donacion> MiHistorial { get; set; } = new();
    public decimal MiTotal { get; set; }
}

// ---------- Administrador ----------

public class ResumenAdminViewModel
{
    public int TotalUsuarios { get; set; }
    public int TotalCursos { get; set; }
    public int TotalInscripciones { get; set; }
    public decimal TotalDonado { get; set; }
    public int DonacionesPositivas { get; set; }
    public int DonacionesNegativas { get; set; }
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
    public double HorasEstudio { get; set; }
}

public class AdminDonacionesViewModel
{
    public List<Donacion> Donaciones { get; set; } = new();
    public int Positivas { get; set; }
    public int Negativas { get; set; }
    public int Neutrales { get; set; }
    public int SinAnalizar { get; set; }
    public bool IaConfigurada { get; set; }
}
