using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P6 — correcciones de las revisiones de seguridad y QA: M-P6-1 (D-7, repetidos por origen), O-1 (D-60, repetidos solo
/// con el mismo usuario; D-59 a) y coincidencias sin criterios (409 controlado). SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class P6CorreccionesTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "M-P6-1, D-7: un contacto desde la ficha no se suma a un crédito ABIERTO de esa ficha (crea su lead para el asesor dueño) y un crédito no se suma al contacto")]
    public async Task Repetido_por_origen()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Origen Repetido");
        var t = TelefonoUnico();
        var credito = await _api.CreditoNuevo(slug, telefono: t);
        var eventosCredito = (await _api.EventosDb(credito.Id)).Count;

        await _api.ContactoOk(CuerpoContacto(slug, t));
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        var contacto = await _api.LeadDeTelefono(t);
        Assert.Equal(Lead.OrigenContactoVehiculo, contacto.Origen);
        Assert.Equal(IdDe(pub), contacto.PublicacionId);
        Assert.Equal(asesor.PerfilId, contacto.ResponsableAsesorId);
        Assert.Contains(contacto.Id, await _api.IdsLeads(asesor.Token));
        Assert.Equal(eventosCredito, (await _api.EventosDb(credito.Id)).Count);

        // Inverso: otro crédito de esa ficha se suma al crédito, no al contacto.
        var eventosContacto = (await _api.EventosDb(contacto.Id)).Count;
        await _api.SolicitudOk(RutaCredito, CuerpoCredito(slug, t));
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        Assert.Equal(EventoLead.ContactoRepetido, (await _api.EventosDb(credito.Id))[^1].Tipo);
        Assert.Equal(eventosContacto, (await _api.EventosDb(contacto.Id)).Count);
        // Y otro contacto se suma al contacto.
        await _api.ContactoOk(CuerpoContacto(slug, t));
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        Assert.Equal(EventoLead.ContactoRepetido, (await _api.EventosDb(contacto.Id))[^1].Tipo);
    }

    public static TheoryData<string> Formularios => new() { "credito", "credito-ficha", "busqueda", "vende", "seguro", "contacto" };

    [Theory(DisplayName = "O-1, D-60, D-59 a: con sesión y un teléfono que ya tiene una solicitud ABIERTA anónima o de otro usuario -> solicitud nueva ligada a su cuenta (aparece en Mis solicitudes); la anterior no cambia de dueño; mismo usuario o dos anónimas -> repetido")]
    [MemberData(nameof(Formularios))]
    public async Task Repetido_solo_con_el_mismo_usuario(string formulario)
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor D60 " + formulario);
        var t = TelefonoUnico();
        var (ruta, cuerpo) = formulario switch
        {
            "credito" => (RutaCredito, (Func<Dictionary<string, object?>>)(() => CuerpoCredito(telefono: t))),
            "credito-ficha" => (RutaCredito, () => CuerpoCredito(slug, t)),
            "busqueda" => (RutaBusqueda, () => CuerpoBusqueda(_api.Camion, t)),
            "vende" => (RutaVende, () => CuerpoVende(t)),
            "seguro" => (RutaSeguro, () => CuerpoSeguro(t)),
            _ => ("/api/contactos", () => CuerpoContacto(slug, t))
        };
        async Task Enviar(string? token) =>
            Assert.Equal(HttpStatusCode.Created, (await _api.Enviar(HttpMethod.Post, ruta, token, cuerpo())).StatusCode);

        await Enviar(null);
        await Enviar(null);                                     // dos anónimas: repetido
        Assert.Equal(1, await _api.LeadsDeTelefono(t));
        var anonimo = await _api.LeadDeTelefono(t);

        var (token, id) = await _api.CompradorNuevo();
        await Enviar(token);                                    // con sesión: nueva, nunca se liga la anónima
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        Assert.Null((await _api.LeadDb(anonimo.Id)).UsuarioId);
        var propia = await _api.LeadDeTelefono(t);
        Assert.Equal(id, propia.UsuarioId);
        Assert.Contains(propia.Id, (await _api.ItemsMias(token)).Select(i => i.GetProperty("id").GetInt64()));
        Assert.DoesNotContain(anonimo.Id, (await _api.ItemsMias(token)).Select(i => i.GetProperty("id").GetInt64()));

        await Enviar(token);                                    // mismo usuario: repetido sobre la suya
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        Assert.Equal(EventoLead.ContactoRepetido, (await _api.EventosDb(propia.Id))[^1].Tipo);

        var (otro, otroId) = await _api.CompradorNuevo();
        await Enviar(otro);                                     // otro usuario: nueva
        Assert.Equal(3, await _api.LeadsDeTelefono(t));
        Assert.Equal(otroId, (await _api.LeadDeTelefono(t)).UsuarioId);
        Assert.Equal(id, (await _api.LeadDb(propia.Id)).UsuarioId);
    }

    [Fact(DisplayName = "Coincidencias de una búsqueda sin fila de criterios -> 409 'Esta búsqueda no tiene criterios guardados.' (nunca 500)")]
    public async Task Coincidencias_sin_criterios()
    {
        _api.Hora(T);
        var lead = new Lead
        {
            Origen = Lead.OrigenBusqueda, Nombre = "Prueba Sin Criterios", Telefono = "+57" + TelefonoUnico(),
            Mensaje = "Busco un vehículo", Estado = Lead.Abierto, CreadoEn = T, ActualizadoEn = T
        };
        await _api.Db(db =>
        {
            db.Leads.Add(lead);
            return Task.CompletedTask;
        });
        await Error(await _api.Coincidencias(_api.Admin.Token, lead.Id), HttpStatusCode.Conflict,
            "Esta búsqueda no tiene criterios guardados.");
    }
}
