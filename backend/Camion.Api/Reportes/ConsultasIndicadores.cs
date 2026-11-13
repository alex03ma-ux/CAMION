using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Reportes;

// P8: indicadores del administrador (HU-075, HU-080, RN-RE-001, RN-RE-006, D-69 A/A) y "Mis indicadores" del asesor
// (D-70 A). Solo lectura (el barrido de D-46 lo hace el endpoint antes). Sin datos de clientes ni visitas (D-76 A).
public static class ConsultasIndicadores
{
    private sealed record Metricas(
        Dictionary<string, int> RecibidasPorOrigen,
        int ContactosCerrados,
        int VentaConcretada,
        int VentasVigentes,
        long ValorVendido,
        int CreditosRemitidos,
        long Ingresos,
        double? TiempoMedio);

    private static async Task<Metricas> CalcularAsync(CamionDbContext db, FiltrosReporte f, CancellationToken ct)
    {
        var (ini, fin) = (f.InicioUtc, f.FinUtc);
        var recibidas = await db.Leads.AsNoTracking().Where(l => l.CreadoEn >= ini && l.CreadoEn < fin)
            .GroupBy(l => l.Origen).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct);
        var cerradas = await db.Leads.AsNoTracking()
            .Where(l => l.Estado == Lead.Cerrado && l.CerradoEn >= ini && l.CerradoEn < fin)
            .Select(l => new { l.Id, l.Origen, l.ResultadoCierre, l.CreadoEn, CerradoEn = l.CerradoEn!.Value }).ToListAsync(ct);
        var contactos = cerradas.Where(l => l.Origen == Lead.OrigenContactoVehiculo).ToList();
        var primer = await ConsultasReportes.PrimerCierreAsync(db, cerradas.Select(l => l.Id).ToList(), ct);
        var ventas = (await ConsultasReportes.VentasAsync(db, f.Desde, f.Hasta, null, null, null, ct)).Where(v => !v.Revertida).ToList();
        var pagos = (await ConsultasReportes.PagosAsync(db, db.Pagos.AsNoTracking(), f, ct)).Where(p => p.Estado == Pago.Registrado);

        return new Metricas(
            Lead.Origenes.ToDictionary(o => o, o => recibidas.FirstOrDefault(x => x.Key == o)?.Total ?? 0),
            contactos.Count,
            contactos.Count(l => l.ResultadoCierre == Lead.VentaConcretada),
            ventas.Count,
            ventas.Sum(v => v.Valor),
            cerradas.Count(l => l.Origen == Lead.OrigenCredito && l.ResultadoCierre == Lead.RemitidoEntidad),
            pagos.Sum(p => p.Valor),
            ConsultasReportes.MediaDias(cerradas.Select(l => (l.CreadoEn, primer.TryGetValue(l.Id, out var p) ? p : l.CerradoEn))));
    }

    private static double? Porcentaje(int parte, int total) =>
        total == 0 ? null : Math.Round(100.0 * parte / total, 1, MidpointRounding.AwayFromZero);

    private static Indicador Comparar(long actual, long anterior) => new(actual, anterior, actual - anterior);

    private static IndicadorDecimal Comparar(double? actual, double? anterior) =>
        new(actual, anterior, actual is { } a && anterior is { } b ? Math.Round(a - b, 1, MidpointRounding.AwayFromZero) : null);

    private static int Administracion(Metricas m) => m.RecibidasPorOrigen.Where(x => Lead.EsAviso(x.Key)).Sum(x => x.Value);

    public static async Task<IndicadoresAdminResponse> AdminAsync(CamionDbContext db, FiltrosReporte f, DateTime ahora,
        CancellationToken ct)
    {
        var anterior = f.PeriodoAnterior();
        var a = await CalcularAsync(db, f, ct);
        var b = await CalcularAsync(db, anterior, ct);
        var valores = new IndicadoresValores(
            Comparar(a.RecibidasPorOrigen[Lead.OrigenContactoVehiculo], b.RecibidasPorOrigen[Lead.OrigenContactoVehiculo]),
            Comparar(Porcentaje(a.VentaConcretada, a.ContactosCerrados), Porcentaje(b.VentaConcretada, b.ContactosCerrados)),
            Comparar(a.VentasVigentes, b.VentasVigentes),
            Comparar(a.ValorVendido, b.ValorVendido),
            Comparar(Administracion(a), Administracion(b)),
            Comparar(a.CreditosRemitidos, b.CreditosRemitidos),
            Comparar(a.Ingresos, b.Ingresos),
            Comparar(a.TiempoMedio, b.TiempoMedio));
        var administracion = Lead.Origenes.Where(Lead.EsAviso)
            .Select(o => new OrigenComparado(o, a.RecibidasPorOrigen[o], b.RecibidasPorOrigen[o])).ToArray();
        var visibles = await ConsultasCatalogo.Visibles(db, ahora).CountAsync(ct);

        // Gráficos (CA-P8-36): ventas de los 12 meses que terminan en el mes de "hasta" y tres cortes del período.
        var ultimoMes = new DateOnly(f.Hasta.Year, f.Hasta.Month, 1);
        var primerMes = ultimoMes.AddMonths(-11);
        var ventas12 = (await ConsultasReportes.VentasAsync(db, primerMes, ultimoMes.AddMonths(1).AddDays(-1), null, null, null, ct))
            .Where(v => !v.Revertida).ToList();
        var porMes = Enumerable.Range(0, 12).Select(i => primerMes.AddMonths(i)).Select(mes =>
        {
            var g = ventas12.Where(v => v.FechaVenta.Year == mes.Year && v.FechaVenta.Month == mes.Month).ToList();
            return new VentasMes(mes.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), g.Count, g.Sum(v => v.Valor));
        }).ToArray();
        var ventasPeriodo = (await ConsultasReportes.VentasAsync(db, f.Desde, f.Hasta, null, null, null, ct)).Where(v => !v.Revertida).ToList();
        var (ini, fin) = (f.InicioUtc, f.FinUtc);
        var resultados = await db.Leads.AsNoTracking()
            .Where(l => l.Origen == Lead.OrigenContactoVehiculo && l.Estado == Lead.Cerrado && l.CerradoEn >= ini && l.CerradoEn < fin)
            .GroupBy(l => l.ResultadoCierre).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct);
        var graficos = new GraficosIndicadores(
            porMes,
            ConsultasReportes.PorOrigen(ventasPeriodo),
            Lead.Origenes.Select(o => new ConteoOrigen(o, a.RecibidasPorOrigen[o])).ToArray(),
            Lead.ResultadosContacto.Select(r => new ConteoResultado(r, resultados.FirstOrDefault(x => x.Key == r)?.Total ?? 0)).ToArray());

        return new IndicadoresAdminResponse(new Periodo(f.Desde, f.Hasta), new Periodo(anterior.Desde, anterior.Hasta), valores,
            administracion, visibles, graficos);
    }

    /// <summary>
    /// D-70 A (CA-P8-51 a 53): los vehículos que hoy son del asesor y los contactos de vehículo de los que hoy es responsable.
    /// Sin avisos, créditos, búsquedas ni pagos, sin cifras de otros asesores ni del portal.
    /// </summary>
    public static async Task<IndicadoresAsesorResponse> AsesorAsync(CamionDbContext db, long perfilId, FiltrosReporte f,
        DateTime ahora, CancellationToken ct)
    {
        var porEstado = await db.Vehiculos.AsNoTracking().Where(v => v.AsesorId == perfilId)
            .GroupBy(v => v.Estado).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct);
        var visibles = await ConsultasCatalogo.Visibles(db, ahora).CountAsync(p => p.Vehiculo!.AsesorId == perfilId, ct);
        var (ini, fin) = (f.InicioUtc, f.FinUtc);
        var mios = db.Leads.AsNoTracking()
            .Where(l => l.ResponsableAsesorId == perfilId && l.Origen == Lead.OrigenContactoVehiculo);
        var recibidos = await mios.Where(l => l.CreadoEn >= ini && l.CreadoEn < fin).Select(l => l.Estado).ToListAsync(ct);
        var resultados = await mios.Where(l => l.Estado == Lead.Cerrado && l.CerradoEn >= ini && l.CerradoEn < fin)
            .GroupBy(l => l.ResultadoCierre).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct);
        var ventas = (await ConsultasReportes.VentasAsync(db, f.Desde, f.Hasta, perfilId, null, null, ct)).Where(v => !v.Revertida).ToList();

        return new IndicadoresAsesorResponse(
            new Periodo(f.Desde, f.Hasta),
            Vehiculo.Estados.ToDictionary(e => e, e => porEstado.FirstOrDefault(x => x.Key == e)?.Total ?? 0),
            visibles,
            recibidos.Count,
            recibidos.Count(e => e == Lead.Abierto),
            Lead.ResultadosContacto.Select(r => new ConteoResultado(r, resultados.FirstOrDefault(x => x.Key == r)?.Total ?? 0)).ToArray(),
            new CantidadValor(ventas.Count, ventas.Sum(v => v.Valor)));
    }
}
