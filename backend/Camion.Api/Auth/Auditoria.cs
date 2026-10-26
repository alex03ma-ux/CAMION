using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Models;

namespace Camion.Api.Auth;

// HU-061, RN-SE-003: filas de auditoria_eventos. Nunca contraseñas, tokens ni el documento completo.
public static class Auditoria
{
    public const string EntidadUsuario = "Usuario";
    public const string RegistroAsesor = "REGISTRO_ASESOR";
    public const string Aprobar = "APROBAR";
    public const string Rechazar = "RECHAZAR";
    public const string Bloquear = "BLOQUEAR";
    public const string Desbloquear = "DESBLOQUEAR";
    public const string Reconsiderar = "RECONSIDERAR";

    // Solo agrega la fila al contexto; se guarda en la misma transacción que el cambio auditado.
    public static void Agregar(CamionDbContext db, string? actorId, string usuarioAfectadoId, string accion,
        string? estadoAnterior, string estadoNuevo, string? motivo, bool incluirMotivo)
    {
        var nuevos = new Dictionary<string, string?> { ["estado"] = estadoNuevo };
        if (incluirMotivo) nuevos["motivo"] = motivo;
        db.AuditoriaEventos.Add(new AuditoriaEvento
        {
            UsuarioId = actorId,
            Entidad = EntidadUsuario,
            EntidadId = usuarioAfectadoId,
            Accion = accion,
            DatosAnteriores = estadoAnterior is null
                ? null
                : JsonSerializer.Serialize(new Dictionary<string, string> { ["estado"] = estadoAnterior }),
            DatosNuevos = JsonSerializer.Serialize(nuevos),
            CreadoEn = DateTime.UtcNow
        });
    }
}
