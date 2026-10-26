using System.Text.RegularExpressions;
using Camion.Api.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Camion.Api.Auth;

public static partial class ValidacionAuth
{
    public const string MensajeNombre = "Escribe tu nombre completo (2 a 150 caracteres).";
    public const string MensajeEmail = "Escribe un correo válido.";
    public const string MensajeEmailLargo = "El correo no puede tener más de 254 caracteres.";
    public const string MensajeTelefono = "Escribe un teléfono válido (solo números, espacios, +, - o paréntesis).";
    public const string MensajePasswordMinimo = "Debe tener al menos 8 caracteres.";
    public const string MensajePasswordMayuscula = "Debe tener al menos una letra mayúscula.";
    public const string MensajePasswordMinuscula = "Debe tener al menos una letra minúscula.";
    public const string MensajePasswordNumero = "Debe tener al menos un número.";
    public const string MensajePasswordMaximo = "No puede tener más de 128 caracteres.";
    public const string MensajeTipoDocumento = "Elige un tipo de documento válido.";
    public const string MensajeNumeroDocumento = "Escribe un número de documento válido (4 a 30 letras, números o guiones).";
    public const string MensajeNombreComercial = "El nombre comercial no puede tener más de 150 caracteres.";
    public const string MensajeMotivo = "Escribe el motivo (5 a 500 caracteres).";
    public const string MensajeMotivoLargo = "El motivo no puede tener más de 500 caracteres.";

    // PROPUESTO en el contrato P2.
    public static readonly string[] TiposDocumento = ["CC", "CE", "NIT", "PAS"];

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FormatoEmail();

    [GeneratedRegex(@"^[0-9 +\-()]+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FormatoTelefono();

    // Documento: solo letras ASCII, dígitos y guiones.
    [GeneratedRegex(@"^[A-Za-z0-9\-]+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FormatoDocumento();

    public static string NormalizarEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static Dictionary<string, string[]> ValidarRegistro(RegistroCompradorRequest request) =>
        ValidarDatosBasicos(request.NombreCompleto, request.Email, request.Telefono, request.Password);

    // P2: mismas reglas de P1 para los datos básicos, más el documento y el nombre comercial.
    public static Dictionary<string, string[]> ValidarRegistroAsesor(RegistroAsesorRequest request)
    {
        var errores = ValidarDatosBasicos(request.NombreCompleto, request.Email, request.Telefono, request.Password);

        if (!TiposDocumento.Contains(NormalizarTipoDocumento(request.TipoDocumento)))
            errores["tipoDocumento"] = [MensajeTipoDocumento];

        var numero = NormalizarNumeroDocumento(request.NumeroDocumento);
        if (numero.Length is < 4 or > 30 || !FormatoDocumento().IsMatch(numero))
            errores["numeroDocumento"] = [MensajeNumeroDocumento];

        if ((request.NombreComercial ?? "").Trim().Length > 150)
            errores["nombreComercial"] = [MensajeNombreComercial];

        return errores;
    }

    public static string NormalizarTipoDocumento(string? tipo) => (tipo ?? "").Trim().ToUpperInvariant();

    // En mayúsculas para que el índice único (tipo, número) se comporte igual con cualquier intercalación.
    public static string NormalizarNumeroDocumento(string? numero) => (numero ?? "").Trim().ToUpperInvariant();

    public static string? NormalizarOpcional(string? valor)
    {
        var limpio = (valor ?? "").Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    // Motivo de las acciones del administrador: obligatorio (5 a 500) para rechazar y bloquear, opcional (hasta 500) en las demás.
    public static Dictionary<string, string[]> ValidarMotivo(string? motivo, bool obligatorio)
    {
        var errores = new Dictionary<string, string[]>();
        var limpio = (motivo ?? "").Trim();
        if (obligatorio && limpio.Length is < 5 or > 500)
            errores["motivo"] = [MensajeMotivo];
        else if (!obligatorio && limpio.Length > 500)
            errores["motivo"] = [MensajeMotivoLargo];
        return errores;
    }

    private static Dictionary<string, string[]> ValidarDatosBasicos(string? nombreCompleto, string? emailBruto,
        string? telefonoBruto, string? passwordBruto)
    {
        var errores = new Dictionary<string, string[]>();

        var nombre = (nombreCompleto ?? "").Trim();
        if (nombre.Length is < 2 or > 150)
            errores["nombreCompleto"] = [MensajeNombre];

        var email = NormalizarEmail(emailBruto);
        if (email.Length > 254)
            errores["email"] = [MensajeEmailLargo];
        else if (email.Length == 0 || !FormatoEmail().IsMatch(email))
            errores["email"] = [MensajeEmail];

        var telefono = (telefonoBruto ?? "").Trim();
        if (telefono.Length is < 7 or > 25 ||
            !FormatoTelefono().IsMatch(telefono) ||
            telefono.Count(char.IsAsciiDigit) < 7)
            errores["telefono"] = [MensajeTelefono];

        var password = ErroresPassword(passwordBruto ?? "");
        if (password.Count > 0)
            errores["password"] = password.ToArray();

        return errores;
    }

    // Mismo criterio que Identity: mayúscula, minúscula y dígito ASCII.
    public static List<string> ErroresPassword(string password)
    {
        var errores = new List<string>();
        if (password.Length < 8) errores.Add(MensajePasswordMinimo);
        if (password.Length > 128) errores.Add(MensajePasswordMaximo);
        if (!password.Any(char.IsAsciiLetterUpper)) errores.Add(MensajePasswordMayuscula);
        if (!password.Any(char.IsAsciiLetterLower)) errores.Add(MensajePasswordMinuscula);
        if (!password.Any(char.IsAsciiDigit)) errores.Add(MensajePasswordNumero);
        return errores;
    }

    // Traduce los errores de Identity que no cubre la validación previa; nunca se devuelven en inglés.
    public static Dictionary<string, string[]> TraducirErroresIdentity(IEnumerable<IdentityError> identityErrors)
    {
        var porCampo = new Dictionary<string, List<string>>();
        void Agregar(string campo, string mensaje)
        {
            if (!porCampo.TryGetValue(campo, out var lista)) porCampo[campo] = lista = [];
            if (!lista.Contains(mensaje)) lista.Add(mensaje);
        }

        foreach (var e in identityErrors)
        {
            switch (e.Code)
            {
                case nameof(IdentityErrorDescriber.PasswordTooShort): Agregar("password", MensajePasswordMinimo); break;
                case nameof(IdentityErrorDescriber.PasswordRequiresUpper): Agregar("password", MensajePasswordMayuscula); break;
                case nameof(IdentityErrorDescriber.PasswordRequiresLower): Agregar("password", MensajePasswordMinuscula); break;
                case nameof(IdentityErrorDescriber.PasswordRequiresDigit): Agregar("password", MensajePasswordNumero); break;
                case nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars):
                case nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric):
                    Agregar("password", "La contraseña no cumple los requisitos de seguridad."); break;
                case nameof(IdentityErrorDescriber.InvalidEmail):
                case nameof(IdentityErrorDescriber.InvalidUserName):
                    Agregar("email", MensajeEmail); break;
                // Otros códigos no corresponden a un campo del DTO; el endpoint responde con el texto general.
            }
        }
        return porCampo.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    public static bool EsDuplicado(IEnumerable<IdentityError> identityErrors) =>
        identityErrors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName));

    // Para la auditoría: el correo de un intento fallido no se registra completo (HU-062).
    public static string EnmascararEmail(string email)
    {
        var arroba = email.LastIndexOf('@');
        if (arroba <= 0) return "***";
        return $"{email[0]}***{email[arroba..]}";
    }
}
