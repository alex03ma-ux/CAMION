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

    /// <summary>CA-P5-38: nota de 5 a 500 caracteres (tras quitar espacios).</summary>
    public static Dictionary<string, string[]> ValidarNota(string? texto)
    {
        var errores = new Dictionary<string, string[]>();
        if ((texto ?? "").Trim().Length is < 5 or > 500) errores["texto"] = [MensajeNota];
        return errores;
    }

    /// <summary>CA-P5-39: resultado de la lista; OTRO exige observación de 5 a 500; los demás la admiten hasta 500.</summary>
    public static Dictionary<string, string[]> ValidarCierre(CerrarLeadRequest? r, out string? resultado, out string? observacion)
    {
        var errores = new Dictionary<string, string[]>();
        resultado = (r?.Resultado ?? "").Trim().ToUpperInvariant();
        if (!Lead.Resultados.Contains(resultado))
        {
            errores["resultado"] = [MensajeResultado];
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
