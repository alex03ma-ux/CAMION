using System.Net;
using System.Text;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P6 — búsqueda "Busco un vehículo" (HU-034 a HU-038; RN-BU-001, RN-BU-002; D-7, D-17, D-29, D-32, D-56, D-57, D-58 A):
/// criterios, validaciones, repetidos, gestión, coincidencias bajo demanda y vehículos relacionados por el administrador.
/// Criterios: docs/criterios/criterios-P6-creditos.md. Contrato: docs/criterios/P6-contrato-api.md. SQLite en memoria.
/// </summary>
public sealed class BusquedasTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private Task<long> Antioquia() => _api.Leer(db => db.Municipios.Where(m => m.Id == _api.Medellin).Select(m => m.DepartamentoId).SingleAsync());
    private Task<long> DeptoBogota() => _api.Leer(db => db.Municipios.Where(m => m.Id == _api.Bogota).Select(m => m.DepartamentoId).SingleAsync());

    [Fact(DisplayName = "CA-P6-16, 24, 37: búsqueda anónima con todos los criterios -> 201; lead BUSQUEDA sin publicación en Administración, criterios en leads_busqueda, consentimiento BUSQUEDA; auditoría sin criterios ni datos personales")]
    public async Task Anonima_con_criterios()
    {
        _api.Hora(T);
        var telefono = TelefonoUnico();
        var antioquia = await Antioquia();
        var lead = await _api.BusquedaNueva(_api.Camion, telefono: telefono, cambios:
        [
            ("marcaId", _api.Kenworth), ("lineaId", _api.T800), ("anioDesde", 2015), ("anioHasta", 2020),
            ("presupuestoMaximo", 150_000_000L), ("departamentoId", antioquia), ("municipioId", _api.Medellin),
            ("descripcion", "  Doble troque con platón (prueba)  ")
        ]);
        Assert.Equal(Lead.OrigenBusqueda, lead.Origen);
        Assert.Null(lead.PublicacionId);
        Assert.Null(lead.TipoVehiculo);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Equal("Doble troque con platón (prueba)", lead.Mensaje);

        var c = (await _api.CriteriosDb(lead.Id))!;
        Assert.Equal((_api.Camion, _api.Kenworth, _api.T800, (short?)2015, (short?)2020, 150_000_000L, antioquia, _api.Medellin),
            (c.TipoVehiculoId, c.MarcaId!.Value, c.LineaId!.Value, c.AnioDesde, c.AnioHasta, c.PresupuestoMaxCop!.Value,
                c.DepartamentoId!.Value, c.MunicipioId!.Value));
        Assert.Equal(ConsentimientoDatos.Busqueda, Assert.Single(await _api.ConsentimientosDb(lead.Id)).Formulario);
        Assert.Equal(new[] { EventoLead.Creado, EventoLead.Asignado }, (await _api.EventosDb(lead.Id)).Select(e => e.Tipo).ToArray());

        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id));
        var datos = JsonDe(audit.DatosNuevos);
        Assert.Equal("BUSQUEDA", datos["origen"].GetString());
        Assert.False(datos["conVehiculo"].GetBoolean());
        foreach (var dato in new[] { telefono, "Buscador Ficticio", "150000000", "Doble troque", "2015", _api.Kenworth.ToString() + "," })
            Assert.DoesNotContain(dato, audit.DatosNuevos!);
    }

    [Fact(DisplayName = "CA-P6-16, 22: solo la categoría -> criterios opcionales en NULL y mensaje fijo; solo la ciudad -> el departamento se toma de ella")]
    public async Task Solo_categoria_y_ciudad()
    {
        _api.Hora(T);
        var lead = await _api.BusquedaNueva(_api.Carro);
        var c = (await _api.CriteriosDb(lead.Id))!;
        Assert.Equal(_api.Carro, c.TipoVehiculoId);
        Assert.Null(c.MarcaId);
        Assert.Null(c.LineaId);
        Assert.Null(c.AnioDesde);
        Assert.Null(c.PresupuestoMaxCop);
        Assert.Null(c.MunicipioId);
        Assert.Null(c.DepartamentoId);
        Assert.Equal(MsgBusca, lead.Mensaje);

        var conCiudad = await _api.BusquedaNueva(_api.Maquinaria, cambios: [("municipioId", _api.Bogota)]);
        Assert.Equal(await DeptoBogota(), (await _api.CriteriosDb(conCiudad.Id))!.DepartamentoId);
        var soloDepto = await _api.BusquedaNueva(_api.Maquinaria, cambios: [("departamentoId", await Antioquia())]);
        Assert.Null((await _api.CriteriosDb(soloDepto.Id))!.MunicipioId);
    }

    public static TheoryData<string, string, string> Invalidos => new()
    {
        { "sin-tipo", "tipoId", MsgCategoria },
        { "tipo-inexistente", "tipoId", MsgCategoria },
        { "tipo-inactivo", "tipoId", MsgCategoria },
        { "marca-inactiva", "marcaId", MsgMarca },
        { "marca-inexistente", "marcaId", MsgMarca },
        { "modelo-de-otra-marca", "lineaId", MsgModelo },
        { "modelo-sin-marca", "lineaId", MsgModelo },
        { "modelo-inexistente", "lineaId", MsgModelo },
        { "anio-1949", "anioDesde", MsgAnios },
        { "anio-futuro", "anioHasta", MsgAnios },
        { "anios-invertidos", "anioHasta", MsgAnios },
        { "presupuesto-bajo", "presupuestoMaximo", MsgPresupuesto },
        { "presupuesto-alto", "presupuestoMaximo", MsgPresupuesto },
        { "departamento-inexistente", "departamentoId", MsgDepartamento },
        { "ciudad-de-otro-departamento", "municipioId", MsgCiudad },
        { "ciudad-inexistente", "municipioId", MsgCiudad },
        { "descripcion-501", "descripcion", MsgDescripcionLarga },
        { "sin-nombre", "nombre", MsgNombre },
        { "telefono-malo", "telefono", MsgTelefono },
        { "sin-autorizacion", "autorizacionDatos", MsgAutorizacion }
    };

    [Theory(DisplayName = "CA-P6-17 a 22, 27: criterios y datos inválidos -> 400 en su campo; no se guarda nada")]
    [MemberData(nameof(Invalidos))]
    public async Task Validaciones(string caso, string campo, string mensaje)
    {
        _api.Hora(T);
        var cuerpo = CuerpoBusqueda(_api.Camion);
        var anio = T.AddHours(-5).Year;
        switch (caso)
        {
            case "sin-tipo": cuerpo["tipoId"] = null; break;
            case "tipo-inexistente": cuerpo["tipoId"] = 999_999; break;
            case "tipo-inactivo": cuerpo["tipoId"] = _api.Remolque; break;
            case "marca-inactiva": cuerpo["marcaId"] = _api.Dina; break;
            case "marca-inexistente": cuerpo["marcaId"] = 999_999; break;
            case "modelo-de-otra-marca": cuerpo["marcaId"] = _api.Kenworth; cuerpo["lineaId"] = _api.Npr; break;
            case "modelo-sin-marca": cuerpo["lineaId"] = _api.T800; break;
            case "modelo-inexistente": cuerpo["marcaId"] = _api.Kenworth; cuerpo["lineaId"] = 999_999; break;
            case "anio-1949": cuerpo["anioDesde"] = 1949; break;
            case "anio-futuro": cuerpo["anioHasta"] = anio + 2; break;
            case "anios-invertidos": cuerpo["anioDesde"] = 2020; cuerpo["anioHasta"] = 2019; break;
            case "presupuesto-bajo": cuerpo["presupuestoMaximo"] = 999_999; break;
            case "presupuesto-alto": cuerpo["presupuestoMaximo"] = 5_000_000_001L; break;
            case "departamento-inexistente": cuerpo["departamentoId"] = 999_999; break;
            case "ciudad-de-otro-departamento": cuerpo["departamentoId"] = await Antioquia(); cuerpo["municipioId"] = _api.Bogota; break;
            case "ciudad-inexistente": cuerpo["municipioId"] = 999_999; break;
            case "descripcion-501": cuerpo["descripcion"] = new string('d', 501); break;
            case "sin-nombre": cuerpo["nombre"] = " "; break;
            case "telefono-malo": cuerpo["telefono"] = "12345"; break;
            case "sin-autorizacion": cuerpo["autorizacionDatos"] = false; break;
        }
        var antes = (await _api.TotalLeads(), await _api.Leer(db => db.LeadsBusqueda.CountAsync()));
        await Error400(await _api.Solicitar(RutaBusqueda, cuerpo), campo, mensaje);
        Assert.Equal(antes, (await _api.TotalLeads(), await _api.Leer(db => db.LeadsBusqueda.CountAsync())));
    }

    [Fact(DisplayName = "CA-P6-19, 20: límites aceptados (1950, año actual + 1, 1.000.000 y 5.000.000.000); tipos incorrectos -> 400 formato; trampa -> 201 sin guardar")]
    public async Task Limites_formato_y_trampa()
    {
        _api.Hora(T);
        var anio = T.AddHours(-5).Year;
        var a = await _api.BusquedaNueva(_api.Camion, cambios: [("anioDesde", 1950), ("anioHasta", anio + 1), ("presupuestoMaximo", 1_000_000)]);
        Assert.Equal((short)(anio + 1), (await _api.CriteriosDb(a.Id))!.AnioHasta);
        var b = await _api.BusquedaNueva(_api.Camion, cambios: [("presupuestoMaximo", 5_000_000_000L)]);
        Assert.Equal(5_000_000_000L, (await _api.CriteriosDb(b.Id))!.PresupuestoMaxCop);

        var antes = await _api.TotalLeads();
        var malo = new HttpRequestMessage(HttpMethod.Post, RutaBusqueda)
        {
            Content = new StringContent($"{{\"nombre\":\"Ana Prueba\",\"telefono\":\"3001234567\",\"tipoId\":\"camion\",\"autorizacionDatos\":true}}",
                Encoding.UTF8, "application/json")
        };
        await Error(await _api.C.SendAsync(malo), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
        var grande = CuerpoBusqueda(_api.Camion);
        grande["descripcion"] = new string('d', 8300);
        await Error(await _api.Solicitar(RutaBusqueda, grande), HttpStatusCode.RequestEntityTooLarge, MsgGrande);
        var trampa = CuerpoBusqueda(_api.Camion);
        trampa["sitioWeb"] = "x";
        trampa["tipoId"] = null;
        await _api.SolicitudOk(RutaBusqueda, trampa);
        Assert.Equal(antes, await _api.TotalLeads());
    }

    [Fact(DisplayName = "CA-P6-18 (D-32): un modelo clasificado en otra categoría -> 400 en lineaId; en su categoría se acepta")]
    public async Task Modelo_de_otra_categoria()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await api.Db(db =>
        {
            db.LineasTipos.Add(new LineaTipo { LineaId = api.T800, TipoVehiculoId = api.Camion });
            return Task.CompletedTask;
        });
        var cuerpo = InventarioApi.Con(CuerpoBusqueda(api.Carro), ("marcaId", api.Kenworth), ("lineaId", api.T800));
        await Error400(await api.Solicitar(RutaBusqueda, cuerpo), "lineaId", MsgModelo);
        await api.SolicitudOk(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(api.Camion), ("marcaId", api.Kenworth), ("lineaId", api.T800)));
    }

    [Fact(DisplayName = "CA-P6-32, 33: misma búsqueda ABIERTA del teléfono -> CONTACTO_REPETIDO con un resumen legible de los criterios nuevos; los guardados no cambian; cerrada -> nueva")]
    public async Task Repetida()
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var lead = await _api.BusquedaNueva(_api.Carro, telefono: t);
        await _api.SolicitudOk(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(_api.Camion, t),
            ("marcaId", _api.Kenworth), ("lineaId", _api.T800), ("anioDesde", 2015), ("anioHasta", 2020),
            ("presupuestoMaximo", 150_000_000), ("municipioId", _api.Medellin), ("descripcion", "Con platón")));
        Assert.Equal(1, await _api.LeadsDeTelefono(t));
        var repetido = (await _api.EventosDb(lead.Id))[^1];
        Assert.Equal(EventoLead.ContactoRepetido, repetido.Tipo);
        Assert.Equal("Busca: Camión · Kenworth T800 · años 2015 a 2020 · hasta $150.000.000 · Medellín (Antioquia). Con platón",
            repetido.Texto);
        Assert.Equal(_api.Carro, (await _api.CriteriosDb(lead.Id))!.TipoVehiculoId);
        Assert.Equal(2, (await _api.ConsentimientosDb(lead.Id)).Count);

        // Un crédito del mismo teléfono es otro origen: no cuenta como repetido de la búsqueda.
        await _api.SolicitudOk(RutaCredito, CuerpoCredito(telefono: t));
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        await Esperar(await _api.Cerrar(_api.Admin.Token, lead.Id, "SIN_VEHICULO"), HttpStatusCode.OK);
        var nueva = await _api.BusquedaNueva(_api.Carro, telefono: t);
        Assert.NotEqual(lead.Id, nueva.Id);
    }

    [Fact(DisplayName = "CA-P6-42, 44, 46: detalle con los criterios legibles (aunque la marca se desactive después) y vehiculosRelacionados []; Registrado -> En gestión; resultados de la búsqueda y 400 a los que no aplican")]
    public async Task Detalle_y_cierre()
    {
        _api.Hora(T);
        var (marca, linea) = (new Marca { Nombre = Unico("Marca P6") }, new Linea { Nombre = "Modelo P6" });
        await _api.Db(db =>
        {
            linea.Marca = marca;
            db.Lineas.Add(linea);
            return Task.CompletedTask;
        });
        var lead = await _api.BusquedaNueva(_api.Camion, cambios:
            [("marcaId", marca.Id), ("lineaId", linea.Id), ("anioDesde", 2010), ("presupuestoMaximo", 80_000_000), ("municipioId", _api.Medellin)]);
        await _api.Db(async db => (await db.Marcas.SingleAsync(m => m.Id == marca.Id)).Activo = false);

        var d = await _api.DetalleAdmin(lead.Id);
        Assert.Equal("BUSQUEDA", d.GetProperty("origen").GetString());
        Assert.Equal("REGISTRADO", d.GetProperty("estadoGestion").GetString());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("vehiculo").ValueKind);
        var b = d.GetProperty("busqueda");
        Assert.Equal(_api.Camion, b.GetProperty("tipoId").GetInt64());
        Assert.Equal("Camión", b.GetProperty("tipo").GetString());
        Assert.Equal(marca.Nombre, b.GetProperty("marca").GetString());
        Assert.Equal("Modelo P6", b.GetProperty("linea").GetString());
        Assert.Equal(2010, b.GetProperty("anioDesde").GetInt32());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("anioHasta").ValueKind);
        Assert.Equal(80_000_000, b.GetProperty("presupuestoMaximo").GetInt64());
        Assert.Equal("Medellín", b.GetProperty("municipio").GetString());
        Assert.Equal("Antioquia", b.GetProperty("departamento").GetString());
        Assert.Equal(0, d.GetProperty("vehiculosRelacionados").GetArrayLength());

        Assert.Equal("EN_GESTION", (await Esperar(await _api.Nota(_api.Admin.Token, lead.Id), HttpStatusCode.Created))
            .GetProperty("estadoGestion").GetString());
        foreach (var malo in new[] { "REMITIDO_ENTIDAD", "ATENDIDA", "VENTA_CONCRETADA", "VEHICULO_NO_DISPONIBLE" })
            await Error400(await _api.Cerrar(_api.Admin.Token, lead.Id, malo), "resultado", MsgNoAplica);
        var cerrado = await Esperar(await _api.Cerrar(_api.Admin.Token, lead.Id, "VEHICULO_ENCONTRADO"), HttpStatusCode.OK);
        Assert.Equal("CERRADO", cerrado.GetProperty("estadoGestion").GetString());
        // Reasignar nunca (D-58 A).
        await Error(await _api.ReasignarLead(_api.Admin.Token, lead.Id, _api.A.PerfilId), HttpStatusCode.Conflict, MsgAvisoAdmin);

        // Los avisos siguen sin admitir los resultados nuevos.
        var aviso = await _api.AvisoNuevo(RutaSeguro);
        foreach (var malo in new[] { "REMITIDO_ENTIDAD", "VEHICULO_ENCONTRADO", "SIN_VEHICULO" })
            await Error400(await _api.Cerrar(_api.Admin.Token, aviso.Id, malo), "resultado", MsgNoAplica);
    }

    [Fact(DisplayName = "CA-P6-50, 53, 55, 56: relacionar una publicación visible (201, historial, auditoría, sin lead para el asesor); retirar sin borrar la fila; relacionar de nuevo reactiva la misma fila; un vehículo que deja de verse queda marcado")]
    public async Task Relacionar_y_retirar()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Relacion Busqueda");
        var lead = await _api.BusquedaNueva(_api.Camion);
        var leadsAsesor = (await _api.IdsLeads(asesor.Token)).Length;
        var totalLeads = await _api.TotalLeads();

        var r = await Esperar(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub), "  Coincide en precio (prueba)  "),
            HttpStatusCode.Created);
        var rel = Assert.Single(r.GetProperty("vehiculosRelacionados").EnumerateArray());
        Assert.Equal(IdDe(pub), rel.GetProperty("publicacionId").GetInt64());
        Assert.Equal(slug, rel.GetProperty("slug").GetString());
        Assert.True(rel.GetProperty("visible").GetBoolean());
        Assert.Equal("Coincide en precio (prueba)", rel.GetProperty("nota").GetString());
        Assert.Equal(100_000_000, rel.GetProperty("precioCop").GetInt64());
        Assert.False(string.IsNullOrEmpty(rel.GetProperty("relacionadoPor").GetString()));
        var evento = (await _api.EventosDb(lead.Id))[^1];
        Assert.Equal(EventoLead.VehiculoRelacionado, evento.Tipo);
        Assert.Equal("Camión de prueba listo", evento.Texto);
        Assert.Equal(_api.Admin.Id, evento.ActorUsuarioId);
        var audit = (await _api.AuditoriaLead(lead.Id))[^1];
        Assert.Equal("RELACIONAR_VEHICULO", audit.Accion);
        Assert.Equal(IdDe(pub), JsonDe(audit.DatosNuevos)["publicacionId"].GetInt64());
        Assert.Equal(totalLeads, await _api.TotalLeads());
        Assert.Equal(leadsAsesor, (await _api.IdsLeads(asesor.Token)).Length);

        await Error(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub)), HttpStatusCode.Conflict, MsgYaRelacionado);

        var retirado = await Esperar(await _api.Retirar(_api.Admin.Token, lead.Id, IdDe(pub)), HttpStatusCode.OK);
        Assert.Equal(0, retirado.GetProperty("vehiculosRelacionados").GetArrayLength());
        var fila = Assert.Single(await _api.RelacionesDb(lead.Id));
        Assert.NotNull(fila.RetiradoEn);
        Assert.Equal(_api.Admin.Id, fila.RetiradoPor);
        Assert.Equal(EventoLead.VehiculoRetirado, (await _api.EventosDb(lead.Id))[^1].Tipo);
        Assert.Equal("RETIRAR_VEHICULO", (await _api.AuditoriaLead(lead.Id))[^1].Accion);
        await Error(await _api.Retirar(_api.Admin.Token, lead.Id, IdDe(pub)), HttpStatusCode.NotFound, MsgNoRelacionado);

        await Esperar(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub)), HttpStatusCode.Created);
        var reactivada = Assert.Single(await _api.RelacionesDb(lead.Id));
        Assert.Equal(fila.Id, reactivada.Id);
        Assert.Null(reactivada.RetiradoEn);
        Assert.Null(reactivada.Nota);

        // CA-P6-56: si deja de ser visible, el administrador lo ve marcado con su estado actual.
        await _api.Inactivada(asesor.Token, IdDe(pub));
        var marcado = Assert.Single((await _api.DetalleAdmin(lead.Id)).GetProperty("vehiculosRelacionados").EnumerateArray());
        Assert.False(marcado.GetProperty("visible").GetBoolean());
        Assert.NotEqual("PUBLICADA", marcado.GetProperty("estadoPublicacion").GetString());
    }

    [Fact(DisplayName = "CA-P6-52: no visible o inexistente -> 409; sin publicacionId o nota de 301 -> 400; búsqueda cerrada -> 409 al relacionar y retirar")]
    public async Task Relacionar_rechazos()
    {
        _api.Hora(T);
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor Rechazo Relacion");
        var (_, pub2, _) = await _api.AsesorPublicado("Asesor Rechazo Relacion 2");
        var lead = await _api.BusquedaNueva(_api.Camion);
        await Error400(await _api.Relacionar(_api.Admin.Token, lead.Id, null), "publicacionId", MsgElegirVehiculo);
        await Error400(await _api.Relacionar(_api.Admin.Token, lead.Id, 0), "publicacionId", MsgElegirVehiculo);
        await Error400(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub), new string('n', 301)), "nota", MsgNotaRelacion);
        await Error(await _api.Relacionar(_api.Admin.Token, lead.Id, 999_999), HttpStatusCode.Conflict, MsgNoPublicado);
        await _api.Inactivada(asesor.Token, IdDe(pub));
        await Error(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub)), HttpStatusCode.Conflict, MsgNoPublicado);
        Assert.Empty(await _api.RelacionesDb(lead.Id));

        await Esperar(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub2), new string('n', 300)), HttpStatusCode.Created);
        await Esperar(await _api.Cerrar(_api.Admin.Token, lead.Id, "NO_RESPONDIO"), HttpStatusCode.OK);
        await Error(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub2)), HttpStatusCode.Conflict, MsgBusquedaCerrada);
        await Error(await _api.Retirar(_api.Admin.Token, lead.Id, IdDe(pub2)), HttpStatusCode.Conflict, MsgBusquedaCerrada);
        // Coincidencias sigue disponible (solo lectura) con la búsqueda cerrada.
        Assert.Equal(HttpStatusCode.OK, (await _api.Coincidencias(_api.Admin.Token, lead.Id)).StatusCode);
    }

    [Fact(DisplayName = "CA-P6-52: tope de 10 relacionadas vigentes -> 409; al retirar una se puede relacionar otra")]
    public async Task Tope_de_diez()
    {
        _api.Hora(T);
        var (asesor, _) = await _api.AsesorConPlan("Asesor Tope Relacion", limite: 11);
        var pubs = new List<long>();
        for (var i = 0; i < 11; i++) pubs.Add(IdDe(await _api.Publicada(asesor, $"Camión tope {i}")));
        var lead = await _api.BusquedaNueva(_api.Camion);
        foreach (var p in pubs.Take(10)) await Esperar(await _api.Relacionar(_api.Admin.Token, lead.Id, p), HttpStatusCode.Created);
        await Error(await _api.Relacionar(_api.Admin.Token, lead.Id, pubs[10]), HttpStatusCode.Conflict, MsgTope);
        await Esperar(await _api.Retirar(_api.Admin.Token, lead.Id, pubs[0]), HttpStatusCode.OK);
        await Esperar(await _api.Relacionar(_api.Admin.Token, lead.Id, pubs[10]), HttpStatusCode.Created);
        Assert.Equal(10, (await _api.DetalleAdmin(lead.Id)).GetProperty("vehiculosRelacionados").GetArrayLength());
    }

    [Fact(DisplayName = "CA-P6-51, D-56 c A: coincidencias = publicaciones visibles que cumplen categoría, marca, modelo, años, presupuesto y ciudad o departamento; marca las ya relacionadas; no crea nada")]
    public async Task Coincidencias_bajo_demanda()
    {
        _api.Hora(T);
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor Coincidencias");   // Camión Chevrolet NPR 2019, 100M, Medellín
        var carro = await _api.CrearId(asesor, "carro");
        var pubCarro = IdDe(await Esperar(await _api.Publicar(asesor.Token, carro, "Carro de prueba"), HttpStatusCode.Created));
        var camion = IdDe(pub);

        async Task<long[]> Ids(long tipo, params (string, object?)[] cambios) =>
            await _api.IdsCoincidencias((await _api.BusquedaNueva(tipo, cambios: cambios)).Id);

        var amplia = await Ids(_api.Camion);
        Assert.Contains(camion, amplia);
        Assert.DoesNotContain(pubCarro, amplia);
        Assert.Contains(pubCarro, await Ids(_api.Carro));
        Assert.Contains(camion, await Ids(_api.Camion, ("marcaId", _api.Chevrolet), ("lineaId", _api.Npr),
            ("anioDesde", 2019), ("anioHasta", 2019), ("presupuestoMaximo", 100_000_000), ("municipioId", _api.Medellin)));
        Assert.Contains(camion, await Ids(_api.Camion, ("departamentoId", await Antioquia())));
        Assert.DoesNotContain(camion, await Ids(_api.Camion, ("marcaId", _api.Kenworth)));
        Assert.DoesNotContain(camion, await Ids(_api.Camion, ("anioDesde", 2020)));
        Assert.DoesNotContain(camion, await Ids(_api.Camion, ("anioHasta", 2018)));
        Assert.DoesNotContain(camion, await Ids(_api.Camion, ("presupuestoMaximo", 99_999_999)));
        Assert.DoesNotContain(camion, await Ids(_api.Camion, ("municipioId", _api.Bogota)));
        Assert.DoesNotContain(camion, await Ids(_api.Camion, ("departamentoId", await DeptoBogota())));

        var lead = await _api.BusquedaNueva(_api.Camion);
        var antes = (await _api.TotalLeads(), (await _api.EventosDb(lead.Id)).Count, (await _api.AuditoriaLead(lead.Id)).Count);
        var json = await Esperar(await _api.Coincidencias(_api.Admin.Token, lead.Id), HttpStatusCode.OK);
        Assert.True(json.GetProperty("total").GetInt32() >= 1);
        var item = json.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("publicacionId").GetInt64() == camion);
        Assert.False(item.GetProperty("relacionado").GetBoolean());
        Assert.Equal("Camión de prueba listo", item.GetProperty("vehiculo").GetProperty("titulo").GetString());
        Assert.Equal(antes, (await _api.TotalLeads(), (await _api.EventosDb(lead.Id)).Count, (await _api.AuditoriaLead(lead.Id)).Count));
        await Esperar(await _api.Relacionar(_api.Admin.Token, lead.Id, camion), HttpStatusCode.Created);
        json = await Esperar(await _api.Coincidencias(_api.Admin.Token, lead.Id), HttpStatusCode.OK);
        Assert.True(json.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("publicacionId").GetInt64() == camion)
            .GetProperty("relacionado").GetBoolean());

        // Inactiva: deja de ser coincidencia.
        await _api.Inactivada(asesor.Token, camion);
        Assert.DoesNotContain(camion, await _api.IdsCoincidencias(lead.Id));
    }

    [Fact(DisplayName = "CA-P6-24, 60, 61, 62, 63: el asesor nunca ve búsquedas (lista, resumen, detalle, notas, cierre, coincidencias, relacionar y retirar -> 404); comprador 403; sin token 401")]
    public async Task Permisos()
    {
        _api.Hora(T);
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor Permisos Busqueda");
        var resumen = (await _api.Resumen(asesor.Token)).ToString();
        var lead = await _api.BusquedaNueva(_api.Camion, token: asesor.Token);
        Assert.Equal(asesor.UsuarioId, lead.UsuarioId);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.DoesNotContain(lead.Id, await _api.IdsLeads(asesor.Token));
        Assert.Equal(resumen, (await _api.Resumen(asesor.Token)).ToString());
        foreach (var r in new[]
                 {
                     await _api.DetalleLead(asesor.Token, lead.Id), await _api.Nota(asesor.Token, lead.Id),
                     await _api.Cerrar(asesor.Token, lead.Id, "SIN_VEHICULO"), await _api.Coincidencias(asesor.Token, lead.Id),
                     await _api.Relacionar(asesor.Token, lead.Id, IdDe(pub)), await _api.Retirar(asesor.Token, lead.Id, IdDe(pub))
                 })
            await Error(r, HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ReasignarLead(asesor.Token, lead.Id, asesor.PerfilId)).StatusCode);

        foreach (var r in new[]
                 {
                     await _api.Relacionar(_api.TokenComprador, lead.Id, IdDe(pub)), await _api.Coincidencias(_api.TokenComprador, lead.Id),
                     await _api.Retirar(_api.TokenComprador, lead.Id, IdDe(pub))
                 })
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        foreach (var r in new[]
                 {
                     await _api.Relacionar(null, lead.Id, IdDe(pub)), await _api.Coincidencias(null, lead.Id),
                     await _api.Retirar(null, lead.Id, IdDe(pub))
                 })
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Empty(await _api.RelacionesDb(lead.Id));
    }
}
