namespace Camion.Api.Models;

// P5 (HU-029 a HU-033, RN-CL-001 a RN-CL-004, D-7, D-9, D-46, D-48; migración 013): tabla nueva leads.
// No usa la tabla solicitudes del script 002 (no mapeada, exige usuario y correo, contradice D-9).
// ResponsableAsesorId = perfiles_asesor.id; null = Administración (cualquier administrador).
public sealed class Lead
{
    public const string Abierto = "ABIERTO";
    public const string Cerrado = "CERRADO";
    public static readonly string[] Estados = [Abierto, Cerrado];

    // CA-P5-39: resultados de cierre (sirven al reporte HU-077 de P8).
    public const string VentaConcretada = "VENTA_CONCRETADA";
    public const string NoInteresado = "NO_INTERESADO";
    public const string NoRespondio = "NO_RESPONDIO";
    public const string VehiculoNoDisponible = "VEHICULO_NO_DISPONIBLE";
    public const string DuplicadoInvalido = "DUPLICADO_INVALIDO";
    public const string Otro = "OTRO";
    public static readonly string[] Resultados =
        [VentaConcretada, NoInteresado, NoRespondio, VehiculoNoDisponible, DuplicadoInvalido, Otro];

    public long Id { get; set; }
    public long PublicacionId { get; set; }
    public string? UsuarioId { get; set; }
    public string Nombre { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string? Correo { get; set; }
    public string Mensaje { get; set; } = "";
    public string Estado { get; set; } = Abierto;
    public long? ResponsableAsesorId { get; set; }
    public bool AsignacionManual { get; set; }
    public bool VistoPorResponsable { get; set; }
    public string? ResultadoCierre { get; set; }
    public string? ObservacionCierre { get; set; }
    public string? CerradoPor { get; set; }
    public DateTime? CerradoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public Publicacion? Publicacion { get; set; }
}

// P5 (RN-SE-003, CA-P5-28, CA-P5-42): historial del lead; solo inserciones. ActorUsuarioId null = SISTEMA.
// Nunca guarda el teléfono ni el correo del visitante.
public sealed class EventoLead
{
    public const string Creado = "CREADO";
    public const string Asignado = "ASIGNADO";
    public const string ContactoRepetido = "CONTACTO_REPETIDO";
    public const string Nota = "NOTA";
    public const string Reasignado = "REASIGNADO";
    public const string CerradoTipo = "CERRADO";
    public const string Reabierto = "REABIERTO";
    public static readonly string[] Tipos = [Creado, Asignado, ContactoRepetido, Nota, Reasignado, CerradoTipo, Reabierto];

    public long Id { get; set; }
    public long LeadId { get; set; }
    public string Tipo { get; set; } = Creado;
    public string? ActorUsuarioId { get; set; }
    public bool Automatico { get; set; }
    public long? ResponsableAnteriorAsesorId { get; set; }
    public long? ResponsableNuevoAsesorId { get; set; }
    public string? Texto { get; set; }
    // Solo en CERRADO: el resultado elegido (se conserva aunque el lead se reabra).
    public string? ResultadoCierre { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

// P5 (D-44, RN-SE-004, CA-P5-15 a CA-P5-19): prueba de la autorización de tratamiento de datos, común a P5, P5b y los
// registros. Solo inserciones; ningún endpoint la edita ni la borra.
public sealed class ConsentimientoDatos
{
    public const string ContactoVehiculo = "CONTACTO_VEHICULO";
    public const string VendeTuCamion = "VENDE_TU_CAMION";
    public const string SeguroTodoRiesgo = "SEGURO_TODO_RIESGO";
    public const string RegistroComprador = "REGISTRO_COMPRADOR";
    public const string InscripcionAsesor = "INSCRIPCION_ASESOR";
    public static readonly string[] Formularios =
        [ContactoVehiculo, VendeTuCamion, SeguroTodoRiesgo, RegistroComprador, InscripcionAsesor];

    public long Id { get; set; }
    public string Formulario { get; set; } = ContactoVehiculo;
    public string VersionPolitica { get; set; } = "";
    public DateTime AceptadoEn { get; set; } = DateTime.UtcNow;
    public string? UsuarioId { get; set; }
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
}
