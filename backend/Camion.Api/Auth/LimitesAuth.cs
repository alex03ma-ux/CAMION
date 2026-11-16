namespace Camion.Api.Auth;

/// <summary>
/// P9 (HU-059, RN-SE-001, RN-SE-006, INF-2; CA-P9-38 a CA-P9-47): cupos por IP del registro (comprador y asesor comparten
/// cupo) y del inicio de sesión (sección "LimitesAuth"). Las pruebas los suben en CamionApiFactory; LimitesAuthTests los baja.
/// </summary>
public sealed class OpcionesLimitesAuth
{
    public int RegistroLimiteVentana { get; set; } = 5;
    public int RegistroVentanaMinutos { get; set; } = 60;
    public int RegistroLimiteDiario { get; set; } = 20;
    public int LoginLimiteVentana { get; set; } = 20;
    public int LoginVentanaMinutos { get; set; } = 15;
}

/// <summary>
/// Marca las rutas de autenticación con cupo propio por IP. El limitador global de Program.cs solo las limita con esta marca;
/// los cupos son independientes de <see cref="Camion.Api.Leads.LimitePeticionesContacto"/> y
/// <see cref="LimitePeticionesRecuperacion"/> (CA-P9-44).
/// </summary>
public sealed class LimitePeticionesAuth(string cupo)
{
    public const string Registro = "registro";
    public const string Login = "login";

    public const string MensajeRegistro =
        "Hiciste muchos intentos de registro desde tu conexión. Intenta de nuevo más tarde.";
    public const string MensajeLogin =
        "Hiciste muchos intentos de inicio de sesión. Espera unos minutos e intenta de nuevo.";

    public string Cupo { get; } = cupo;

    public string Mensaje => Cupo == Registro ? MensajeRegistro : MensajeLogin;
}
