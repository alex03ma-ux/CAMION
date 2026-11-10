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
    // P5b (CA-P5b-41, migración 014): "Solicitud atendida", solo para los avisos.
    public const string Atendida = "ATENDIDA";
    // P6 (D-57 b A, migración 015): crédito "Remitido a entidad financiera"; búsqueda "Encontró vehículo" y
    // "Sin vehículo disponible". Sin "aprobado" ni "no aprobado" (D-45).
    public const string RemitidoEntidad = "REMITIDO_ENTIDAD";
    public const string VehiculoEncontrado = "VEHICULO_ENCONTRADO";
    public const string SinVehiculo = "SIN_VEHICULO";
    public static readonly string[] Resultados =
        [VentaConcretada, NoInteresado, NoRespondio, VehiculoNoDisponible, DuplicadoInvalido, Otro, Atendida,
         RemitidoEntidad, VehiculoEncontrado, SinVehiculo];
    public static readonly string[] ResultadosContacto =
        [VentaConcretada, NoInteresado, NoRespondio, VehiculoNoDisponible, DuplicadoInvalido, Otro];
    public static readonly string[] ResultadosAviso = [Atendida, NoInteresado, NoRespondio, DuplicadoInvalido, Otro];
    public static readonly string[] ResultadosCredito =
        [RemitidoEntidad, Atendida, NoInteresado, NoRespondio, DuplicadoInvalido, Otro];
    public static readonly string[] ResultadosBusqueda =
        [VehiculoEncontrado, SinVehiculo, NoInteresado, NoRespondio, DuplicadoInvalido, Otro];

    /// <summary>CA-P5b-41, CA-P6-45, CA-P6-46: resultados de cierre válidos para el origen del lead.</summary>
    public static string[] ResultadosDe(string origen) => origen switch
    {
        OrigenContactoVehiculo => ResultadosContacto,
        OrigenCredito => ResultadosCredito,
        OrigenBusqueda => ResultadosBusqueda,
        _ => ResultadosAviso
    };

    // P5b (HU-N01, HU-N02, D-6, migración 014): un aviso es un lead sin publicación. Mismos códigos que
    // consentimientos_datos.formulario. CK_Lead_Origen y CK_Lead_AvisoAdmin (D-52 A) los protegen en la base.
    public const string OrigenContactoVehiculo = "CONTACTO_VEHICULO";
    public const string OrigenVendeTuCamion = "VENDE_TU_CAMION";
    public const string OrigenSeguroTodoRiesgo = "SEGURO_TODO_RIESGO";
    // P6 (HU-034, HU-039, D-54 A, D-55 B, D-56; migración 015): crédito (con publicación desde la ficha o sin ella desde la
    // portada) y búsqueda (sin publicación; criterios en leads_busqueda). Siempre en Administración (D-58 A).
    public const string OrigenCredito = "CREDITO";
    public const string OrigenBusqueda = "BUSQUEDA";
    public static readonly string[] Origenes =
        [OrigenContactoVehiculo, OrigenVendeTuCamion, OrigenSeguroTodoRiesgo, OrigenCredito, OrigenBusqueda];

    // D-50 (a): tipo de vehículo del seguro.
    public static readonly string[] TiposVehiculo = ["CAMION", "CARRO", "MAQUINARIA", "OTRO"];

    // "Solicitud de Administración": todo origen distinto del contacto de un vehículo (avisos, créditos y búsquedas). Nunca
    // se reasigna a un asesor, el asesor no la ve (D-52 A, D-58 A) y CK_Lead_AvisoAdmin lo garantiza en la base.
    public static bool EsAviso(string origen) => origen != OrigenContactoVehiculo;

    // P6 (D-57 a A): créditos y búsquedas muestran Registrado / En gestión / Cerrado.
    public static bool TieneEstadoGestion(string origen) => origen is OrigenCredito or OrigenBusqueda;

    public long Id { get; set; }
    // Null en los avisos (P5b), en la búsqueda y en el crédito pedido desde la portada (P6).
    public long? PublicacionId { get; set; }
    public string Origen { get; set; } = OrigenContactoVehiculo;
    // Solo en SEGURO_TODO_RIESGO.
    public string? TipoVehiculo { get; set; }
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
    // P6 (HU-036, D-56 b A, migración 015): el administrador relaciona o retira un vehículo de una búsqueda.
    public const string VehiculoRelacionado = "VEHICULO_RELACIONADO";
    public const string VehiculoRetirado = "VEHICULO_RETIRADO";
    public static readonly string[] Tipos =
        [Creado, Asignado, ContactoRepetido, Nota, Reasignado, CerradoTipo, Reabierto, VehiculoRelacionado, VehiculoRetirado];

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
    // P6 (migración 015): los dos formularios nuevos usan el mismo código que su origen.
    public const string Credito = "CREDITO";
    public const string Busqueda = "BUSQUEDA";
    public static readonly string[] Formularios =
        [ContactoVehiculo, VendeTuCamion, SeguroTodoRiesgo, RegistroComprador, InscripcionAsesor, Credito, Busqueda];

    public long Id { get; set; }
    public string Formulario { get; set; } = ContactoVehiculo;
    public string VersionPolitica { get; set; } = "";
    public DateTime AceptadoEn { get; set; } = DateTime.UtcNow;
    public string? UsuarioId { get; set; }
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
}

// P6 (HU-034, D-56 a A, migración 015): criterios de una búsqueda "Busco un vehículo"; una fila por lead BUSQUEDA.
// Categoría obligatoria; el resto opcional. No se edita después de creada (CA-P6-48).
public sealed class LeadBusqueda
{
    public long LeadId { get; set; }
    public long TipoVehiculoId { get; set; }
    public long? MarcaId { get; set; }
    public long? LineaId { get; set; }
    public short? AnioDesde { get; set; }
    public short? AnioHasta { get; set; }
    // Pesos enteros (BIGINT).
    public long? PresupuestoMaxCop { get; set; }
    public long? DepartamentoId { get; set; }
    public long? MunicipioId { get; set; }
}

// P6 (HU-036, D-56 b A, migración 015): publicación que el administrador relaciona con una búsqueda. Retirar no borra la
// fila (RetiradoEn); relacionar de nuevo la misma publicación reactiva la fila (UNIQUE lead + publicación).
public sealed class BusquedaVehiculo
{
    public const int MaxNota = 300;
    public const int MaxVigentes = 10;

    public long Id { get; set; }
    public long LeadId { get; set; }
    public long PublicacionId { get; set; }
    public string? Nota { get; set; }
    public string? RelacionadoPor { get; set; }
    public DateTime RelacionadoEn { get; set; } = DateTime.UtcNow;
    public string? RetiradoPor { get; set; }
    public DateTime? RetiradoEn { get; set; }

    public Publicacion? Publicacion { get; set; }
}
