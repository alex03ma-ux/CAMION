using System.Globalization;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Leads;

/// <summary>
/// P6 (HU-034, RN-BU-001, D-17, D-29, D-32, D-56 a A): criterios de la búsqueda "Busco un vehículo". Contrato:
/// docs/criterios/P6-contrato-api.md. Categoría obligatoria; marca, modelo, años, presupuesto, departamento y ciudad
/// opcionales. Los nombres se leen aunque la marca, el modelo o la categoría se desactiven después (CA-P6-42).
/// </summary>
public static class ReglasBusqueda
{
    /// <summary>
    /// CA-P6-17 a CA-P6-21: valida los criterios contra los catálogos activos y añade los errores a <paramref name="errores"/>.
    /// Devuelve la fila lista para guardar (sin LeadId) o null si hay algún error de criterios.
    /// </summary>
    public static async Task<LeadBusqueda?> ValidarAsync(CamionDbContext db, BusquedaRequest r, int anioActual,
        Dictionary<string, string[]> errores, CancellationToken ct)
    {
        var antes = errores.Count;
        var tipoValido = r.TipoId is { } tipo &&
                         await db.TiposVehiculo.AsNoTracking().AnyAsync(t => t.Id == tipo && t.Activo, ct);
        if (!tipoValido) errores["tipoId"] = [ReglasLeads.MensajeCategoriaBusqueda];

        var marcaValida = false;
        if (r.MarcaId is { } marca)
        {
            marcaValida = await db.Marcas.AsNoTracking().AnyAsync(m => m.Id == marca && m.Activo, ct);
            if (!marcaValida) errores["marcaId"] = [ReglasLeads.MensajeMarcaBusqueda];
        }
        if (r.LineaId is { } linea)
        {
            // El modelo exige una marca válida y ser de ella; D-32: si está clasificado, debe ser de la categoría.
            var valida = marcaValida &&
                         await db.Lineas.AsNoTracking().AnyAsync(l => l.Id == linea && l.Activo && l.MarcaId == r.MarcaId, ct);
            if (valida && tipoValido &&
                await db.LineasTipos.AsNoTracking().AnyAsync(lt => lt.LineaId == linea, ct) &&
                !await db.LineasTipos.AsNoTracking().AnyAsync(lt => lt.LineaId == linea && lt.TipoVehiculoId == r.TipoId, ct))
                valida = false;
            if (!valida) errores["lineaId"] = [ReglasLeads.MensajeModeloBusqueda];
        }

        var maximoAnio = anioActual + 1;
        bool AnioFuera(int? anio) => anio is { } a && (a < ReglasLeads.AnioMinimoBusqueda || a > maximoAnio);
        if (AnioFuera(r.AnioDesde)) errores["anioDesde"] = [ReglasLeads.MensajeAniosBusqueda];
        if (AnioFuera(r.AnioHasta)) errores["anioHasta"] = [ReglasLeads.MensajeAniosBusqueda];
        else if (!AnioFuera(r.AnioDesde) && r.AnioDesde is { } d && r.AnioHasta is { } h && d > h)
            errores["anioHasta"] = [ReglasLeads.MensajeAniosBusqueda];

        if (r.PresupuestoMaximo is { } p && (p < ReglasLeads.PresupuestoMinimo || p > ReglasLeads.PresupuestoMaximo))
            errores["presupuestoMaximo"] = [ReglasLeads.MensajePresupuesto];

        long? departamento = null;
        var departamentoValido = true;
        if (r.DepartamentoId is { } dep)
        {
            departamentoValido = await db.Departamentos.AsNoTracking().AnyAsync(x => x.Id == dep, ct);
            if (departamentoValido) departamento = dep;
            else errores["departamentoId"] = [ReglasLeads.MensajeDepartamentoBusqueda];
        }
        if (r.MunicipioId is { } mun)
        {
            var deMunicipio = await db.Municipios.AsNoTracking().Where(m => m.Id == mun)
                .Select(m => (long?)m.DepartamentoId).FirstOrDefaultAsync(ct);
            if (deMunicipio is null || (r.DepartamentoId is not null && (!departamentoValido || deMunicipio != departamento)))
                errores["municipioId"] = [ReglasLeads.MensajeCiudadBusqueda];
            else
                departamento = deMunicipio;
        }

        if (errores.Count > antes || !tipoValido) return null;
        return new LeadBusqueda
        {
            TipoVehiculoId = r.TipoId!.Value,
            MarcaId = r.MarcaId,
            LineaId = r.LineaId,
            AnioDesde = (short?)r.AnioDesde,
            AnioHasta = (short?)r.AnioHasta,
            PresupuestoMaxCop = r.PresupuestoMaximo,
            DepartamentoId = departamento,
            MunicipioId = r.MunicipioId
        };
    }

    /// <summary>CA-P6-42: criterios con sus nombres (consulta los catálogos sin filtrar por activo).</summary>
    public static async Task<BusquedaCriterios?> CriteriosAsync(CamionDbContext db, long leadId, CancellationToken ct) =>
        (await CriteriosAsync(db, [leadId], ct)).GetValueOrDefault(leadId);

    public static async Task<Dictionary<long, BusquedaCriterios>> CriteriosAsync(CamionDbContext db,
        IReadOnlyCollection<long> leadIds, CancellationToken ct)
    {
        if (leadIds.Count == 0) return [];
        var filas = await db.LeadsBusqueda.AsNoTracking().Where(b => leadIds.Contains(b.LeadId))
            .Select(b => new FilaCriterios
            {
                LeadId = b.LeadId,
                TipoId = b.TipoVehiculoId,
                Tipo = db.TiposVehiculo.Where(t => t.Id == b.TipoVehiculoId).Select(t => t.Nombre).FirstOrDefault(),
                MarcaId = b.MarcaId,
                Marca = db.Marcas.Where(m => m.Id == b.MarcaId).Select(m => m.Nombre).FirstOrDefault(),
                LineaId = b.LineaId,
                Linea = db.Lineas.Where(l => l.Id == b.LineaId).Select(l => l.Nombre).FirstOrDefault(),
                AnioDesde = b.AnioDesde,
                AnioHasta = b.AnioHasta,
                Presupuesto = b.PresupuestoMaxCop,
                DepartamentoId = b.DepartamentoId,
                Departamento = db.Departamentos.Where(d => d.Id == b.DepartamentoId).Select(d => d.Nombre).FirstOrDefault(),
                MunicipioId = b.MunicipioId,
                Municipio = db.Municipios.Where(m => m.Id == b.MunicipioId).Select(m => m.Nombre).FirstOrDefault()
            }).ToListAsync(ct);
        return filas.ToDictionary(f => f.LeadId, f => new BusquedaCriterios(f.TipoId, f.Tipo ?? "", f.MarcaId, f.Marca,
            f.LineaId, f.Linea, f.AnioDesde, f.AnioHasta, f.Presupuesto, f.DepartamentoId, f.Departamento, f.MunicipioId,
            f.Municipio));
    }

    /// <summary>Criterios con nombres a partir de una fila aún no guardada (texto del contacto repetido).</summary>
    public static async Task<BusquedaCriterios> ConNombresAsync(CamionDbContext db, LeadBusqueda b, CancellationToken ct) => new(
        b.TipoVehiculoId,
        await db.TiposVehiculo.AsNoTracking().Where(t => t.Id == b.TipoVehiculoId).Select(t => t.Nombre).FirstAsync(ct),
        b.MarcaId, b.MarcaId is { } m ? await db.Marcas.AsNoTracking().Where(x => x.Id == m).Select(x => x.Nombre).FirstOrDefaultAsync(ct) : null,
        b.LineaId, b.LineaId is { } l ? await db.Lineas.AsNoTracking().Where(x => x.Id == l).Select(x => x.Nombre).FirstOrDefaultAsync(ct) : null,
        b.AnioDesde, b.AnioHasta, b.PresupuestoMaxCop,
        b.DepartamentoId, b.DepartamentoId is { } d ? await db.Departamentos.AsNoTracking().Where(x => x.Id == d).Select(x => x.Nombre).FirstOrDefaultAsync(ct) : null,
        b.MunicipioId, b.MunicipioId is { } u ? await db.Municipios.AsNoTracking().Where(x => x.Id == u).Select(x => x.Nombre).FirstOrDefaultAsync(ct) : null);

    /// <summary>"Camión Kenworth T800": categoría, marca y modelo (resumen de "Mis solicitudes").</summary>
    public static string Titulo(BusquedaCriterios c) =>
        string.Join(' ', new[] { c.Tipo, c.Marca, c.Linea }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>CA-P6-32: resumen legible de los criterios para el evento CONTACTO_REPETIDO (máx. 500).</summary>
    public static string TextoRepetido(BusquedaCriterios c, string mensaje)
    {
        var partes = new List<string> { c.Tipo };
        var marcaModelo = string.Join(' ', new[] { c.Marca, c.Linea }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (marcaModelo.Length > 0) partes.Add(marcaModelo);
        if (c.AnioDesde is { } d && c.AnioHasta is { } h) partes.Add($"años {d} a {h}");
        else if (c.AnioDesde is { } d2) partes.Add($"desde {d2}");
        else if (c.AnioHasta is { } h2) partes.Add($"hasta el año {h2}");
        if (c.PresupuestoMaximo is { } p) partes.Add("hasta " + Pesos(p));
        if (c.Municipio is not null) partes.Add(c.Departamento is null ? c.Municipio : $"{c.Municipio} ({c.Departamento})");
        else if (c.Departamento is not null) partes.Add(c.Departamento);
        var texto = "Busca: " + string.Join(" · ", partes) + ". " + mensaje;
        return texto.Length <= ReglasLeads.MaxMensaje ? texto : texto[..ReglasLeads.MaxMensaje];
    }

    // $150.000.000 (separador de miles con punto, como en Colombia), sin depender de la cultura del servidor.
    public static string Pesos(long valor) => "$" + valor.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    private sealed class FilaCriterios
    {
        public long LeadId { get; init; }
        public long TipoId { get; init; }
        public string? Tipo { get; init; }
        public long? MarcaId { get; init; }
        public string? Marca { get; init; }
        public long? LineaId { get; init; }
        public string? Linea { get; init; }
        public short? AnioDesde { get; init; }
        public short? AnioHasta { get; init; }
        public long? Presupuesto { get; init; }
        public long? DepartamentoId { get; init; }
        public string? Departamento { get; init; }
        public long? MunicipioId { get; init; }
        public string? Municipio { get; init; }
    }
}
