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

    // P4a (HU-013, HU-019, HU-020, RN-VE-009): inventario de vehículos.
    public const string EntidadVehiculo = "Vehiculo";
    public const string Crear = "CREAR";
    public const string Editar = "EDITAR";
    public const string Reservar = "RESERVAR";
    public const string Liberar = "LIBERAR";
    public const string Inactivar = "INACTIVAR";
    public const string Reactivar = "REACTIVAR";

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

    // P4a: fila de auditoría de un vehículo. En EDITAR, anteriores y nuevos llevan solo los campos que cambiaron.
    public static void AgregarVehiculo(CamionDbContext db, string? actorId, long vehiculoId, string accion,
        IReadOnlyDictionary<string, object?>? anteriores, IReadOnlyDictionary<string, object?> nuevos, DateTime ahora)
    {
        db.AuditoriaEventos.Add(new AuditoriaEvento
        {
            UsuarioId = actorId,
            Entidad = EntidadVehiculo,
            EntidadId = vehiculoId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Accion = accion,
            DatosAnteriores = anteriores is null ? null : JsonSerializer.Serialize(anteriores),
            DatosNuevos = JsonSerializer.Serialize(nuevos),
            CreadoEn = ahora
        });
    }
}
