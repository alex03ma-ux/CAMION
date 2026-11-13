using System.Globalization;
using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Reportes;

// P8 (HU-061, RN-SE-003, D-62 d, D-72 A/A): línea de tiempo de solo lectura de un vehículo (con sus publicaciones y
// ventas) y de una suscripción (con sus pagos y lo que deshizo D-67), con las acciones en palabras. Nunca contraseñas,
// tokens, documentos, observaciones de venta, nota de reserva ni datos de clientes (CA-P8-58).
public static class ConsultasActividad
{
    public const string SobreVehiculo = "VEHICULO";
    public const string SobrePublicacion = "PUBLICACION";
    public const string SobreVenta = "VENTA";
    public const string SobreSuscripcion = "SUSCRIPCION";
    public const string SobrePago = "PAGO";
    public const string Sistema = "Sistema";
    public const string TextoModificado = "(texto modificado)";

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    public static async Task<PaginaFilas<EventoActividad>> VehiculoAsync(CamionDbContext db, long vehiculoId, int pagina, int tamano,
        CancellationToken ct)
    {
        var pubs = (await db.Publicaciones.AsNoTracking().Where(p => p.VehiculoId == vehiculoId).Select(p => p.Id).ToListAsync(ct))
            .Select(Id).ToList();
        var ventas = (await db.VentasVehiculo.AsNoTracking().Where(v => v.VehiculoId == vehiculoId).Select(v => v.Id).ToListAsync(ct))
            .Select(Id).ToList();
        var propio = Id(vehiculoId);
        var q = db.AuditoriaEventos.AsNoTracking().Where(e =>
            (e.Entidad == Auditoria.EntidadVehiculo && e.EntidadId == propio) ||
            (e.Entidad == Auditoria.EntidadPublicacion && pubs.Contains(e.EntidadId)) ||
            (e.Entidad == Auditoria.EntidadVenta && ventas.Contains(e.EntidadId)));
        return await PaginaAsync(db, q, pagina, tamano, ct);
    }

    public static async Task<PaginaFilas<EventoActividad>> SuscripcionAsync(CamionDbContext db, long suscripcionId, int pagina,
        int tamano, CancellationToken ct)
    {
        var pagos = (await db.Pagos.AsNoTracking().Where(p => p.SuscripcionId == suscripcionId).Select(p => p.Id).ToListAsync(ct))
            .Select(Id).ToList();
        var propio = Id(suscripcionId);
        var q = db.AuditoriaEventos.AsNoTracking().Where(e =>
            (e.Entidad == Auditoria.EntidadSuscripcion && e.EntidadId == propio) ||
            (e.Entidad == Auditoria.EntidadPago && pagos.Contains(e.EntidadId)));
        return await PaginaAsync(db, q, pagina, tamano, ct);
    }

    private static async Task<PaginaFilas<EventoActividad>> PaginaAsync(CamionDbContext db, IQueryable<AuditoriaEvento> q,
        int pagina, int tamano, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        var saltar = (long)(pagina - 1) * tamano;
        var eventos = saltar >= total
            ? []
            : await q.OrderByDescending(e => e.CreadoEn).ThenByDescending(e => e.Id).Skip((int)saltar).Take(tamano).ToListAsync(ct);
        var contexto = await Contexto.CargarAsync(db, eventos, ct);
        return new PaginaFilas<EventoActividad>(eventos.Select(contexto.Describir).ToArray(), total, pagina, tamano);
    }

    /// <summary>Nombres que necesita la página de eventos: personas, asesores, planes y catálogos.</summary>
    private sealed class Contexto
    {
        private Dictionary<string, string> _usuarios = new();
        private Dictionary<long, string> _asesores = new();
        private Dictionary<long, string> _planes = new();
        private readonly Dictionary<string, Dictionary<long, string>> _catalogos = new();

        public static async Task<Contexto> CargarAsync(CamionDbContext db, IReadOnlyList<AuditoriaEvento> eventos, CancellationToken ct)
        {
            var c = new Contexto();
            if (eventos.Count == 0) return c;
            var ids = eventos.Select(e => e.UsuarioId).OfType<string>().Distinct().ToList();
            c._usuarios = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);
            c._asesores = await ConsultasReportes.NombresAsesoresAsync(db, ct);
            c._planes = await db.Planes.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
            if (eventos.Any(e => e.Accion == Auditoria.Editar && e.Entidad == Auditoria.EntidadVehiculo))
            {
                c._catalogos["tipoId"] = await db.TiposVehiculo.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["marcaId"] = await db.Marcas.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["lineaId"] = await db.Lineas.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["carroceriaId"] = await db.TiposCarroceria.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["combustibleId"] = await db.Combustibles.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["materialId"] = await db.MaterialesCarroceria.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["tipoMaquinaId"] = await db.TiposMaquina.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
                c._catalogos["municipioId"] = await db.Municipios.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
            }
            return c;
        }

        private string Asesor(long? id) => id is { } a ? _asesores.GetValueOrDefault(a) ?? $"asesor #{a}" : "Administración";

        private string Plan(long? id) => id is { } p ? _planes.GetValueOrDefault(p) ?? $"#{p}" : "";

        public EventoActividad Describir(AuditoriaEvento e)
        {
            var antes = Leer(e.DatosAnteriores);
            var nuevos = Leer(e.DatosNuevos);
            var sobre = e.Entidad switch
            {
                Auditoria.EntidadVehiculo => SobreVehiculo,
                Auditoria.EntidadPublicacion => SobrePublicacion,
                Auditoria.EntidadVenta => SobreVenta,
                Auditoria.EntidadSuscripcion => SobreSuscripcion,
                Auditoria.EntidadPago => SobrePago,
                _ => e.Entidad.ToUpperInvariant()
            };
            long.TryParse(e.EntidadId, NumberStyles.None, CultureInfo.InvariantCulture, out var sobreId);
            var cambios = e.Accion == Auditoria.Editar ? Cambios(sobre, antes, nuevos) : [];
            var quien = e.UsuarioId is { } u ? _usuarios.GetValueOrDefault(u) ?? "Usuario eliminado" : Sistema;
            // D-28: en RESERVAR el "motivo" es la nota de reserva (puede nombrar al comprador): no se muestra.
            var motivo = sobre == SobreVehiculo && e.Accion == Auditoria.Reservar ? null : Texto(nuevos, "motivo");
            return new EventoActividad(e.Id, ConsultasCatalogo.Utc(e.CreadoEn), quien, sobre, sobreId, e.Accion,
                Descripcion(sobre, e.Accion, sobreId, antes, nuevos), motivo, cambios);
        }

        private string Descripcion(string sobre, string accion, long id, Dictionary<string, JsonElement> antes,
            Dictionary<string, JsonElement> nuevos)
        {
            switch (sobre, accion)
            {
                case (SobreVehiculo, Auditoria.Crear): return "Registró el vehículo";
                case (SobreVehiculo, Auditoria.Editar): return "Editó el vehículo";
                case (SobreVehiculo, Auditoria.Reservar): return "Reservó el vehículo";
                case (SobreVehiculo, Auditoria.Liberar): return "Liberó la reserva";
                case (SobreVehiculo, Auditoria.Inactivar): return "Inactivó el vehículo";
                case (SobreVehiculo, Auditoria.Reactivar): return "Reactivó el vehículo";
                case (SobreVehiculo, Auditoria.Reasignar):
                    return $"Reasignó de {Asesor(Entero(antes, "asesorId"))} a {Asesor(Entero(nuevos, "asesorId"))}";
                case (SobreVehiculo or SobreVenta, Auditoria.RevertirVenta): return "Revirtió la venta";
                case (SobrePublicacion, Auditoria.Crear): return "Creó la publicación";
                case (SobrePublicacion, Auditoria.Editar): return "Editó la publicación";
                case (SobrePublicacion, Auditoria.Inactivar): return "Ocultó la publicación";
                case (SobrePublicacion, Auditoria.Reactivar): return "Reactivó la publicación";
                case (SobrePublicacion, Auditoria.Renovar): return "Renovó la publicación";
                case (SobrePublicacion, Auditoria.Eliminar): return "Eliminó la publicación";
                case (SobrePublicacion, Auditoria.Destacar): return "Destacó la publicación";
                case (SobrePublicacion, Auditoria.QuitarDestacado): return "Quitó el destacado";
                case (SobreVenta, Auditoria.Vender):
                {
                    var precio = Entero(nuevos, "precioFinalCop");
                    var origen = Etiquetas.De(Etiquetas.OrigenVenta, Texto(nuevos, "origen"));
                    var texto = "Marcó vendido";
                    if (precio is { } pr) texto += " por " + Etiquetas.Pesos(pr);
                    if (origen is not null) texto += $" ({origen})";
                    return texto;
                }
                case (SobreSuscripcion, Auditoria.Asignar):
                    return $"Asignó el plan {Plan(Entero(nuevos, "planId"))}{Hasta(nuevos)}";
                case (SobreSuscripcion, Auditoria.Renovar): return $"Renovó el plan{Hasta(nuevos)}";
                case (SobreSuscripcion, Auditoria.CambiarPlan):
                    return antes.Count > 0
                        ? $"Cambió de plan: pasa a la suscripción #{Entero(nuevos, "suscripcionNuevaId")}"
                        : $"Inició con el plan {Plan(Entero(nuevos, "planId"))} por cambio de plan (antes, suscripción #{Entero(nuevos, "suscripcionAnteriorId")}){Hasta(nuevos)}";
                case (SobreSuscripcion, Auditoria.Suspender): return "Suspendió la suscripción";
                case (SobreSuscripcion, Auditoria.Reactivar): return "Reactivó la suscripción";
                case (SobreSuscripcion, Auditoria.Cancelar): return "Canceló la suscripción";
                case (SobreSuscripcion, Auditoria.DeshacerPago):
                {
                    var pago = Entero(nuevos, "pagoId");
                    return Texto(nuevos, "tipo") switch
                    {
                        AnulacionPagos.TipoDeshacerRenovacion => $"Deshizo la renovación al anular el pago #{pago}{Hasta(nuevos)}",
                        AnulacionPagos.TipoCancelarAsignacion => $"Canceló la asignación al anular el pago #{pago}",
                        AnulacionPagos.TipoDeshacerCambioPlan =>
                            $"Deshizo el cambio de plan al anular el pago #{pago}: vuelve al plan {Plan(Entero(nuevos, "planId"))}{Hasta(nuevos)}",
                        _ => $"Deshizo lo que dio el pago #{pago}"
                    };
                }
                case (SobrePago, Auditoria.Registrar):
                {
                    var valor = Entero(nuevos, "valorCop");
                    var medio = Etiquetas.De(Etiquetas.Medio, Texto(nuevos, "medio"));
                    var concepto = Etiquetas.De(Etiquetas.Concepto, Texto(nuevos, "concepto"));
                    var texto = $"Registró el pago #{id}";
                    if (valor is { } v) texto += " de " + Etiquetas.Pesos(v);
                    if (medio is not null) texto += " por " + medio;
                    if (concepto is not null) texto += $" ({concepto})";
                    return texto;
                }
                case (SobrePago, Auditoria.Anular): return $"Anuló el pago #{id}";
                default: return $"Acción {accion}";
            }
        }

        private static string Hasta(Dictionary<string, JsonElement> datos) =>
            Fecha(datos, "finVigencia") is { } fin ? " hasta el " + fin : "";

        // ---------------------------------------------------------------- cambios de un EDITAR (CA-P8-58)

        private static readonly Dictionary<string, string> CamposVehiculo = new()
        {
            ["tipoId"] = "Categoría", ["marcaId"] = "Marca", ["lineaId"] = "Modelo", ["modeloAnio"] = "Año",
            ["kilometraje"] = "Kilometraje", ["horasUso"] = "Horas de uso", ["placa"] = "Placa", ["carroceriaId"] = "Carrocería",
            ["combustibleId"] = "Combustible", ["cilindrajeCc"] = "Cilindraje (cc)", ["pesoBrutoKg"] = "Peso bruto (kg)",
            ["capacidadCargaKg"] = "Capacidad de carga (kg)", ["largoM"] = "Largo (m)", ["anchoM"] = "Ancho (m)",
            ["altoM"] = "Alto (m)", ["materialId"] = "Material", ["tipoMaquinaId"] = "Tipo de máquina",
            ["municipioId"] = "Municipio", ["precioCop"] = "Precio", ["precioAnteriorCop"] = "Precio anterior",
            ["descripcionTecnica"] = "Descripción técnica", ["asesorId"] = "Asesor", ["estado"] = "Estado"
        };

        private static readonly Dictionary<string, string> CamposPublicacion = new()
        {
            ["titulo"] = "Título", ["descripcionComercial"] = "Descripción comercial", ["negociable"] = "Negociable",
            ["precioCop"] = "Precio"
        };

        private static readonly HashSet<string> TextosLargos = ["descripcionTecnica", "descripcionComercial"];

        private CambioActividad[] Cambios(string sobre, Dictionary<string, JsonElement> antes, Dictionary<string, JsonElement> nuevos)
        {
            var etiquetas = sobre == SobreVehiculo ? CamposVehiculo : sobre == SobrePublicacion ? CamposPublicacion : null;
            if (etiquetas is null) return [];
            // Solo los campos conocidos: cualquier otro (p. ej. una nota de reserva) nunca se muestra.
            return nuevos.Keys.Concat(antes.Keys).Distinct().Where(etiquetas.ContainsKey)
                .Select(k => TextosLargos.Contains(k)
                    ? new CambioActividad(etiquetas[k], null, TextoModificado)
                    : new CambioActividad(etiquetas[k], Valor(k, antes), Valor(k, nuevos)))
                .ToArray();
        }

        private string? Valor(string campo, Dictionary<string, JsonElement> datos)
        {
            if (!datos.TryGetValue(campo, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (_catalogos.TryGetValue(campo, out var catalogo) && Entero(datos, campo) is { } idc)
                return catalogo.GetValueOrDefault(idc) ?? $"#{idc}";
            return campo switch
            {
                "asesorId" => Asesor(Entero(datos, campo)),
                "precioCop" or "precioAnteriorCop" when v.ValueKind == JsonValueKind.Number => Etiquetas.Pesos((long)Math.Round(v.GetDecimal())),
                "estado" => Etiquetas.De(Etiquetas.EstadoVehiculo, v.ToString()),
                _ => v.ValueKind switch
                {
                    JsonValueKind.True => "Sí",
                    JsonValueKind.False => "No",
                    JsonValueKind.Number => v.GetDecimal().ToString(CultureInfo.GetCultureInfo("es-CO")),
                    _ => v.ToString()
                }
            };
        }

        // ---------------------------------------------------------------- lectura del JSON guardado

        private static Dictionary<string, JsonElement> Leer(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                    : new();
            }
            catch (JsonException)
            {
                return new();
            }
        }

        private static long? Entero(Dictionary<string, JsonElement> d, string clave) =>
            d.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n)
                ? (long)Math.Round(n)
                : null;

        private static string? Texto(Dictionary<string, JsonElement> d, string clave) =>
            d.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string? Fecha(Dictionary<string, JsonElement> d, string clave) =>
            d.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var f)
                ? Etiquetas.FechaUtc(f.Kind == DateTimeKind.Local ? f.ToUniversalTime() : f)
                : null;
    }
}
