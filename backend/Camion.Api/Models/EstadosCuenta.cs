namespace Camion.Api.Models;

// Valores de AspNetUsers.EstadoCuenta (P2-contrato-api.md). Es la única fuente que decide si se puede entrar.
public static class EstadosCuenta
{
    public const string Activa = "ACTIVA";
    public const string Pendiente = "PENDIENTE";
    public const string Rechazada = "RECHAZADA";
    public const string Bloqueada = "BLOQUEADA";

    public static readonly string[] Todos = [Activa, Pendiente, Rechazada, Bloqueada];

    // Texto legible en minúscula para el 409 (aclaración del coordinador): "pendiente", "activa", "rechazada", "bloqueada".
    public static string Mostrar(string estado) => estado.ToLowerInvariant();
}
