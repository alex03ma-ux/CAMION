using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Models;

namespace Camion.Api.Leads;

/// <summary>
/// P5 (CA-P5-20 a CA-P5-22, D-42, D-44): valores configurables del contacto público (sección "Contactos" y
/// "PoliticaDatos:Version"). Las pruebas suben los límites para no volverse frágiles; una clase propia los baja.
/// </summary>
public sealed class OpcionesContacto
{
    public int LimiteVentanaCorta { get; set; } = 5;
    public int VentanaCortaMinutos { get; set; } = 10;
    public int LimiteDiario { get; set; } = 20;
    public int LimiteTelefonoDia { get; set; } = 10;
    public int TamanoMaximoBytes { get; set; } = 8192;
    // D-42: WhatsApp del portal (316 615 2907) con el indicativo 57, sin +.
    public string WhatsappPortal { get; set; } = "573166152907";
}

/// <summary>
/// Marca las rutas a las que se aplica la limitación de peticiones por IP del contacto público (POST /api/contactos;
/// P5b marcará las suyas). El limitador global de Program.cs solo actúa sobre las rutas con esta marca.
/// </summary>
public sealed class LimitePeticionesContacto;

// P5: mensajes y validaciones de contactos y leads (docs/criterios/P5-contrato-api.md).
public static class ReglasLeads
{
    public const string VersionPoliticaPorDefecto = "BORRADOR-2026-11-04";
    public const string MensajePorDefecto = "Me interesa este vehículo";

    public const string MensajeExito = "¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp.";
    public const string MensajeRevisar = "Revisa los datos marcados.";
    public const string MensajeFiltros = "Revisa los filtros.";
    public const string MensajeSlug = "Indica el vehículo que quieres contactar.";
    public const string MensajeNombre = "Escribe tu nombre (entre 2 y 150 caracteres).";
    public const string MensajeTelefono = "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.";
    public const string MensajeCorreo = "Escribe un correo válido o deja el campo vacío.";
    public const string MensajeMensaje = "El mensaje no puede pasar de 500 caracteres.";
    public const string MensajeAutorizacion = "Debes autorizar el tratamiento de tus datos para enviar el formulario.";
    public const string MensajeNoDisponible = "Este vehículo ya no está disponible. Mira otros vehículos en el catálogo.";
    public const string MensajePropio = "Este vehículo es tuyo; no puedes contactarte a ti mismo.";
    public const string MensajeDemasiadoGrande = "La solicitud es demasiado grande.";
    public const string MensajeMuchas = "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.";

    // P5b (HU-N01, HU-N02; docs/criterios/P5b-contrato-api.md).
    public const string MensajeExitoAviso = "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.";
    public const string MensajeDescripcion = "Describe tu vehículo (máximo 500 caracteres).";
    public const string MensajeDescripcionLarga = "La descripción no puede pasar de 500 caracteres.";
    public const string MensajeTipoVehiculo = "Elige el tipo de vehículo.";
    public const string MensajeSeguro = "Solicitud de seguro todo riesgo";
    public const string MensajeOrigen = "Origen no válido.";
    public const string MensajeResultadoNoAplica = "Ese resultado no aplica a esta solicitud.";
    public const string MensajeAvisoAdministracion = "Esta solicitud la atiende siempre la administración.";

    // P6 (HU-034 a HU-042; docs/criterios/P6-contrato-api.md).
    public const string MensajeCredito = "Solicitud de crédito";
    public const string MensajeCreditoVehiculo = "Solicitud de crédito para este vehículo";
    public const string MensajeComentarioLargo = "El comentario no puede pasar de 500 caracteres.";
    public const string MensajeBusqueda = "Busco un vehículo";
    public const string MensajeCategoriaBusqueda = "Elige la categoría del vehículo que buscas.";
    public const string MensajeMarcaBusqueda = "Elige una marca de la lista.";
    public const string MensajeModeloBusqueda = "Elige un modelo de la lista.";
    public const string MensajeAniosBusqueda = "Revisa el rango de años.";
    public const string MensajePresupuesto = "Escribe un valor en pesos entre 1.000.000 y 5.000.000.000, o deja el campo vacío.";
    public const string MensajeDepartamentoBusqueda = "Elige un departamento de la lista.";
    public const string MensajeCiudadBusqueda = "Elige una ciudad de la lista.";
    public const string MensajeSoloBusqueda = "Solo se pueden relacionar vehículos a una búsqueda.";
    public const string MensajeBusquedaCerrada = "Esta búsqueda está cerrada.";
    public const string MensajeNoPublicado = "Ese vehículo no está publicado.";
    public const string MensajeYaRelacionado = "Ese vehículo ya está relacionado con esta búsqueda.";
    public const string MensajeTopeRelacionados = "Esta búsqueda ya tiene 10 vehículos relacionados.";
    public const string MensajeNoRelacionado = "Ese vehículo no está relacionado con esta búsqueda.";
    public const string MensajeElegirVehiculo = "Elige un vehículo.";
    public const string MensajeNotaRelacion = "La nota no puede pasar de 300 caracteres.";
    public const string MensajeSolicitudNoEncontrada = "Solicitud no encontrada.";
    public const int AnioMinimoBusqueda = 1950;
    public const long PresupuestoMinimo = 1_000_000;
    public const long PresupuestoMaximo = 5_000_000_000;

    // D-57 a (A): estados mostrados de créditos y búsquedas.
    public const string EstadoRegistrado = "REGISTRADO";
    public const string EstadoEnGestion = "EN_GESTION";
    public const string EstadoCerradoGestion = "CERRADO";

    public const string MensajeNoEncontrado = "Contacto no encontrado.";
    public const string MensajeNota = "La nota debe tener entre 5 y 500 caracteres.";
    public const string MensajeCerrado = "Este contacto está cerrado.";
    public const string MensajeYaCerrado = "Este contacto ya está cerrado.";
    public const string MensajeYaAbierto = "Este contacto ya está abierto.";
    public const string MensajeResultado = "Elige el resultado del cierre.";
    public const string MensajeObservacionOtro = "Escribe la observación (5 a 500 caracteres).";
    public const string MensajeObservacionLarga = "La observación no puede tener más de 500 caracteres.";
    public const string MensajeAsesorAprobado = "Elige un asesor aprobado.";
    public const string MensajeMismoResponsable = "El contacto ya está asignado a ese responsable.";
    public const string MensajeEstado = "Estado no válido.";
    public const string MensajeResponsable = "Responsable no válido.";
    public const string MensajeVehiculo = "Vehículo no válido.";
    public const string MensajeFecha = "Escribe una fecha válida (aaaa-mm-dd).";
    public const string MensajeFechaRango = "La fecha final debe ser igual o posterior a la inicial.";
    public const string MensajePagina = "Página o tamaño no válidos.";

    // D-46 (B): motivos del paso automático a Administración, y texto del lead que sigue al vehículo (D-34).
    public const string MotivoPlanVencido = "Plan vencido";
    public const string MotivoAsesorBloqueado = "Asesor bloqueado";
    public const string TextoVehiculoReasignado = "El vehículo se reasignó a otro asesor.";

    public const string ResponsableAdministracion = "ADMINISTRACION";
    public const string ResponsableAsesor = "ASESOR";
    public const string ActorSistema = "SISTEMA";
    public const string ActorUsuario = "USUARIO";
    public const string EstadoVendida = "VENDIDA";

    public const int MaxSlug = 200;
    public const int MaxMensaje = 500;

    public sealed record DatosContacto(string Slug, string Nombre, string Telefono, string? Correo, string Mensaje);

    /// <summary>CA-P5-03 a CA-P5-07 y CA-P5-14. Devuelve los errores por campo y, si no hay, los datos normalizados.</summary>
    public static Dictionary<string, string[]> ValidarContacto(ContactoRequest r, out DatosContacto? datos)
    {
        datos = null;
        var errores = new Dictionary<string, string[]>();
        var slug = (r.Slug ?? "").Trim();
        if (slug.Length is 0 or > MaxSlug) errores["slug"] = [MensajeSlug];
        var nombre = (r.Nombre ?? "").Trim();
        if (nombre.Length is < 2 or > 150) errores["nombre"] = [MensajeNombre];
        var telefono = ValidacionAuth.NormalizarTelefonoColombia(r.Telefono);
        if (telefono is null) errores["telefono"] = [MensajeTelefono];
        var correo = ValidacionAuth.NormalizarEmail(r.Correo);
        if (correo.Length > 254 || (correo.Length > 0 && !ValidacionAuth.EmailValido(correo)))
            errores["correo"] = [MensajeCorreo];
        var mensaje = (r.Mensaje ?? "").Trim();
        if (mensaje.Length > MaxMensaje) errores["mensaje"] = [MensajeMensaje];
        if (r.AutorizacionDatos != true) errores["autorizacionDatos"] = [MensajeAutorizacion];
        if (errores.Count == 0)
            datos = new DatosContacto(slug, nombre, telefono!, correo.Length == 0 ? null : correo,
                mensaje.Length == 0 ? MensajePorDefecto : mensaje);
        return errores;
    }

    public sealed record DatosAviso(string Origen, string Nombre, string Telefono, string? Correo, string Mensaje,
        string? TipoVehiculo);

    /// <summary>CA-P5b-03 a CA-P5b-09 y CA-P5b-19 (HU-N01).</summary>
    public static Dictionary<string, string[]> ValidarVendeTuCamion(VendeTuCamionRequest r, out DatosAviso? datos)
    {
        datos = null;
        var errores = ValidarTitular(r.Nombre, r.Telefono, r.Correo, r.AutorizacionDatos,
            out var nombre, out var telefono, out var correo);
        var descripcion = (r.Descripcion ?? "").Trim();
        if (descripcion.Length == 0) errores["descripcion"] = [MensajeDescripcion];
        else if (descripcion.Length > MaxMensaje) errores["descripcion"] = [MensajeDescripcionLarga];
        if (errores.Count == 0)
            datos = new DatosAviso(Lead.OrigenVendeTuCamion, nombre, telefono!, correo, descripcion, null);
        return errores;
    }

    /// <summary>CA-P5b-13 a CA-P5b-17 y CA-P5b-19 (HU-N02, D-50 a).</summary>
    public static Dictionary<string, string[]> ValidarSeguro(SeguroTodoRiesgoRequest r, out DatosAviso? datos)
    {
        datos = null;
        var errores = ValidarTitular(r.Nombre, r.Telefono, r.Correo, r.AutorizacionDatos,
            out var nombre, out var telefono, out var correo);
        var tipo = (r.TipoVehiculo ?? "").Trim().ToUpperInvariant();
        if (!Lead.TiposVehiculo.Contains(tipo)) errores["tipoVehiculo"] = [MensajeTipoVehiculo];
        if (errores.Count == 0)
            datos = new DatosAviso(Lead.OrigenSeguroTodoRiesgo, nombre, telefono!, correo, MensajeSeguro, tipo);
        return errores;
    }

    /// <summary>
    /// P6 (CA-P6-07 a CA-P6-12, CA-P6-27; D-55 B): datos de contacto y comentario. conVehiculo decide el texto fijo cuando el
    /// comentario llega vacío. Solo se leen los campos de CreditoRequest: lo financiero no existe en el contrato.
    /// </summary>
    public static Dictionary<string, string[]> ValidarCredito(CreditoRequest r, bool conVehiculo, out DatosAviso? datos)
    {
        datos = null;
        var errores = ValidarTitular(r.Nombre, r.Telefono, r.Correo, r.AutorizacionDatos,
            out var nombre, out var telefono, out var correo);
        var mensaje = (r.Mensaje ?? "").Trim();
        if (mensaje.Length > MaxMensaje) errores["mensaje"] = [MensajeComentarioLargo];
        if (errores.Count == 0)
            datos = new DatosAviso(Lead.OrigenCredito, nombre, telefono!, correo,
                mensaje.Length > 0 ? mensaje : conVehiculo ? MensajeCreditoVehiculo : MensajeCredito, null);
        return errores;
    }

    /// <summary>P6 (CA-P6-16, CA-P6-22, CA-P6-27): datos de contacto y descripción de la búsqueda (los criterios, en ReglasBusqueda).</summary>
    public static Dictionary<string, string[]> ValidarTitularBusqueda(BusquedaRequest r, out DatosAviso? datos)
    {
        datos = null;
        var errores = ValidarTitular(r.Nombre, r.Telefono, r.Correo, r.AutorizacionDatos,
            out var nombre, out var telefono, out var correo);
        var descripcion = (r.Descripcion ?? "").Trim();
        if (descripcion.Length > MaxMensaje) errores["descripcion"] = [MensajeDescripcionLarga];
        if (errores.Count == 0)
            datos = new DatosAviso(Lead.OrigenBusqueda, nombre, telefono!, correo,
                descripcion.Length > 0 ? descripcion : MensajeBusqueda, null);
        return errores;
    }

    /// <summary>P6 (D-57 a A): Registrado / En gestión / Cerrado de un crédito o una búsqueda; null en los demás orígenes.</summary>
    public static string? EstadoGestion(string origen, string estado, bool conNotas) =>
        !Lead.TieneEstadoGestion(origen) ? null
        : estado == Lead.Cerrado ? EstadoCerradoGestion
        : conNotas ? EstadoEnGestion
        : EstadoRegistrado;

    /// <summary>P6: etiqueta del tipo de vehículo del seguro (D-50 a).</summary>
    public static string EtiquetaTipoVehiculo(string? tipo) => tipo switch
    {
        "CAMION" => "Camión",
        "CARRO" => "Carro",
        "MAQUINARIA" => "Maquinaria",
        _ => "Otro"
    };

    /// <summary>CA-P5b-26: texto del evento CONTACTO_REPETIDO de un seguro.</summary>
    public static string TextoSeguroRepetido(string? tipo) => MensajeSeguro + ". Tipo de vehículo: " + EtiquetaTipoVehiculo(tipo) + ".";

    // Nombre, teléfono, correo y autorización: las mismas reglas del contacto de P5 (D-9, D-44, D-50 c).
    private static Dictionary<string, string[]> ValidarTitular(string? nombreCrudo, string? telefonoCrudo, string? correoCrudo,
        bool? autorizacion, out string nombre, out string? telefono, out string? correo)
    {
        var errores = new Dictionary<string, string[]>();
        nombre = (nombreCrudo ?? "").Trim();
        if (nombre.Length is < 2 or > 150) errores["nombre"] = [MensajeNombre];
        telefono = ValidacionAuth.NormalizarTelefonoColombia(telefonoCrudo);
        if (telefono is null) errores["telefono"] = [MensajeTelefono];
        var c = ValidacionAuth.NormalizarEmail(correoCrudo);
        if (c.Length > 254 || (c.Length > 0 && !ValidacionAuth.EmailValido(c))) errores["correo"] = [MensajeCorreo];
        correo = c.Length == 0 ? null : c;
        if (autorizacion != true) errores["autorizacionDatos"] = [MensajeAutorizacion];
        return errores;
    }

    /// <summary>CA-P5-38: nota de 5 a 500 caracteres (tras quitar espacios).</summary>
    public static Dictionary<string, string[]> ValidarNota(string? texto)
    {
        var errores = new Dictionary<string, string[]>();
        if ((texto ?? "").Trim().Length is < 5 or > 500) errores["texto"] = [MensajeNota];
        return errores;
    }

    /// <summary>CA-P5-39: resultado de la lista; OTRO exige observación de 5 a 500; los demás la admiten hasta 500.</summary>
    /// P5b (CA-P5b-41): un resultado de la lista que no corresponde al origen del lead da "Ese resultado no aplica".
    public static Dictionary<string, string[]> ValidarCierre(CerrarLeadRequest? r, string origen, out string? resultado,
        out string? observacion)
    {
        var errores = new Dictionary<string, string[]>();
        resultado = (r?.Resultado ?? "").Trim().ToUpperInvariant();
        if (!Lead.Resultados.Contains(resultado))
        {
            errores["resultado"] = [MensajeResultado];
            resultado = null;
        }
        // P6 (CA-P6-45, CA-P6-46): cada origen tiene su lista.
        else if (!Lead.ResultadosDe(origen).Contains(resultado))
        {
            errores["resultado"] = [MensajeResultadoNoAplica];
            resultado = null;
        }
        observacion = ValidacionAuth.NormalizarOpcional(r?.Observacion);
        var largo = observacion?.Length ?? 0;
        if (resultado == Lead.Otro && largo is < 5 or > 500) errores["observacion"] = [MensajeObservacionOtro];
        else if (largo > 500) errores["observacion"] = [MensajeObservacionLarga];
        return errores;
    }

    /// <summary>D-47: enlace wa.me del asesor (celular colombiano válido) o, si no, el del portal (D-42).</summary>
    public static string EnlaceWhatsapp(string? telefonoAsesor, bool esAdministrador, string whatsappPortal)
    {
        var normalizado = esAdministrador ? null : ValidacionAuth.NormalizarTelefonoColombia(telefonoAsesor);
        return "https://wa.me/" + (ValidacionAuth.EsCelularColombia(normalizado) ? normalizado![1..] : whatsappPortal);
    }
}
