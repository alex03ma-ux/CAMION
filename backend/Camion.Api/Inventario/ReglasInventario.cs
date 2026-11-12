using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Inventario;

// P4a: categorías de D-17 / D-19. Se reconocen por el nombre del tipo (tipos_vehiculo), sin distinguir mayúsculas ni tildes.
public enum Categoria { Carro, Camion, Carroceria, Maquinaria }

public enum Regla { NoAdmite, Opcional, Obligatorio }

// Valores ya validados y normalizados, listos para guardar. Los campos que la categoría no admite quedan en null
// (kilometraje en 0, como pide el contrato).
public sealed class DatosVehiculo
{
    public long TipoId { get; init; }
    public Categoria Categoria { get; init; }
    public long? MarcaId { get; init; }
    public long? LineaId { get; init; }
    public short? ModeloAnio { get; init; }
    public int Kilometraje { get; init; }
    public int? HorasUso { get; init; }
    public string? Placa { get; init; }
    public long? CarroceriaId { get; init; }
    public long? CombustibleId { get; init; }
    public int? CilindrajeCc { get; init; }
    public decimal? PesoBrutoKg { get; init; }
    public decimal? CapacidadCargaKg { get; init; }
    public decimal? LargoM { get; init; }
    public decimal? AnchoM { get; init; }
    public decimal? AltoM { get; init; }
    public long? MaterialId { get; init; }
    public long? TipoMaquinaId { get; init; }
    public long MunicipioId { get; init; }
    public decimal PrecioCop { get; init; }
    public string? DescripcionTecnica { get; init; }
}

// HU-013, HU-015 a HU-019, RN-VE-002, RN-VE-003, RN-VE-005, RN-VE-010, D-2, D-17, D-19: validación por categoría
// con la tabla del contrato P4a. Lógica de negocio separada del endpoint.
public static partial class ReglasInventario
{
    public const string MensajeRevisar = "Revisa los datos marcados.";
    public const string MensajeTipo = "Elige la categoría.";
    public const string MensajeTipoInvalido = "Categoría no válida.";
    public const string MensajeMarca = "Elige la marca.";
    public const string MensajeMarcaInvalida = "Marca no válida.";
    public const string MensajeLinea = "Elige el modelo.";
    public const string MensajeLineaInvalida = "Modelo no válido.";
    public const string MensajeLineaMarca = "El modelo no corresponde a la marca elegida.";
    public const string MensajeLineaCategoria = "Este modelo no corresponde a la categoría elegida.";
    public const string MensajeKilometraje = "Escribe el kilometraje (número entero mayor o igual a 0).";
    public const string MensajeHorasUso = "Escribe las horas de uso (número entero mayor o igual a 0).";
    public const string MensajePlaca = "Escribe una placa válida (letras y números).";
    public const string MensajePlacaDuplicada = "Esta placa ya está registrada en otro vehículo.";
    public const string MensajeCarroceria = "Elige el tipo de carrocería.";
    public const string MensajeCarroceriaInvalida = "Tipo de carrocería no válido.";
    public const string MensajeCombustibleInvalido = "Combustible no válido.";
    public const string MensajeCilindraje = "Escribe un cilindraje válido en cc (número entero mayor que 0).";
    public const string MensajePeso = "Escribe un peso válido en kg (mayor que 0, hasta 2 decimales).";
    public const string MensajeMedida = "Escribe una medida válida en metros (mayor que 0, hasta 2 decimales).";
    public const string MensajeMaterialInvalido = "Material no válido.";
    public const string MensajeTipoMaquina = "Elige el tipo de máquina.";
    public const string MensajeTipoMaquinaInvalido = "Tipo de máquina no válido.";
    public const string MensajeMunicipio = "Elige el municipio.";
    public const string MensajeMunicipioInvalido = "Municipio no válido.";
    public const string MensajePrecio = "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).";
    public const string MensajeDescripcion = "La descripción técnica no puede tener más de 4000 caracteres.";
    public const string MensajeAsesor = "Elige un asesor activo.";

    public const decimal PrecioMaximo = 10_000_000_000m;
    public const int AnioMinimo = 1950;
    public const int MaxDescripcion = 4000;
    public const int KilometrajeMaximo = 9_999_999;
    public const int HorasUsoMaximo = 999_999;
    public const int CilindrajeMaximo = 100_000;
    public const decimal PesoMaximo = 9_999_999_999.99m;   // DECIMAL(12,2)
    public const decimal MedidaMaxima = 9_999.99m;         // DECIMAL(6,2)

    private sealed record Reglas(
        Regla Marca, Regla Anio, Regla Kilometraje, Regla HorasUso, Regla Placa, Regla Carroceria,
        Regla Medidas, Regla Material, Regla TipoMaquina, Regla Mecanica, Regla Capacidad);

    // Tabla "Reglas por categoría" del contrato P4a (línea, municipio, precio y descripción valen igual para todas).
    private static readonly Dictionary<Categoria, Reglas> Tabla = new()
    {
        [Categoria.Carro] = new(Regla.Obligatorio, Regla.Obligatorio, Regla.Obligatorio, Regla.NoAdmite, Regla.Opcional,
            Regla.Opcional, Regla.NoAdmite, Regla.NoAdmite, Regla.NoAdmite, Regla.Opcional, Regla.NoAdmite),
        [Categoria.Camion] = new(Regla.Obligatorio, Regla.Obligatorio, Regla.Obligatorio, Regla.NoAdmite, Regla.Opcional,
            Regla.Opcional, Regla.NoAdmite, Regla.NoAdmite, Regla.NoAdmite, Regla.Opcional, Regla.Opcional),
        [Categoria.Carroceria] = new(Regla.Opcional, Regla.Opcional, Regla.NoAdmite, Regla.NoAdmite, Regla.NoAdmite,
            Regla.Obligatorio, Regla.Opcional, Regla.Opcional, Regla.NoAdmite, Regla.NoAdmite, Regla.Opcional),
        [Categoria.Maquinaria] = new(Regla.Obligatorio, Regla.Obligatorio, Regla.NoAdmite, Regla.Opcional, Regla.Opcional,
            Regla.NoAdmite, Regla.NoAdmite, Regla.NoAdmite, Regla.Obligatorio, Regla.Opcional, Regla.Opcional),
    };

    public static bool AdmiteKilometraje(Categoria c) => Tabla[c].Kilometraje != Regla.NoAdmite;
    public static bool AdmiteHorasUso(Categoria c) => Tabla[c].HorasUso != Regla.NoAdmite;

    [GeneratedRegex("^[A-Z0-9]{5,7}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FormatoPlaca();

    [GeneratedRegex("^[A-Z0-9]{5,8}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FormatoPlacaMaquinaria();

    /// <summary>Carro, Camión, Carrocería o Maquinaria (sin distinguir mayúsculas ni tildes); null si es otro nombre.</summary>
    public static Categoria? CategoriaDe(string? nombreTipo) => SinTildes(nombreTipo ?? "").Trim().ToUpperInvariant() switch
    {
        "CARRO" => Categoria.Carro,
        "CAMION" => Categoria.Camion,
        "CARROCERIA" => Categoria.Carroceria,
        "MAQUINARIA" => Categoria.Maquinaria,
        _ => null
    };

    public static string SinTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>D-2: mayúsculas, sin espacios ni guiones. Vacío = null.</summary>
    public static string? NormalizarPlaca(string? placa)
    {
        if (placa is null) return null;
        var limpia = new string(placa.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        return limpia.Length == 0 ? null : limpia;
    }

    /// <summary>
    /// Valida el cuerpo con la tabla de la categoría. <paramref name="tipoActual"/> es el tipo del vehículo al editar
    /// (no se puede cambiar); null al crear. Devuelve los errores por campo y, si no hay ninguno, los datos normalizados.
    /// </summary>
    public static async Task<(Dictionary<string, string[]> Errores, DatosVehiculo? Datos)> ValidarAsync(
        CamionDbContext db, VehiculoInventarioRequest r, long? tipoActual, int anioActual, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        void Error(string campo, string mensaje)
        {
            if (!errores.ContainsKey(campo)) errores[campo] = [mensaje];
        }

        // Categoría.
        var tipoId = r.TipoId ?? tipoActual;
        Categoria? categoria = null;
        string nombreTipo = "";
        // D-33: el dueño (y el administrador) puede cambiar la categoría; se valida todo con la nueva.
        // El bloqueo por publicación activa lo hace el endpoint de edición.
        if (tipoId is null)
            Error("tipoId", MensajeTipo);
        else
        {
            var tipo = await db.TiposVehiculo.AsNoTracking().Where(t => t.Id == tipoId && t.Activo)
                .Select(t => t.Nombre).FirstOrDefaultAsync(ct);
            categoria = CategoriaDe(tipo);
            if (categoria is null) Error("tipoId", MensajeTipoInvalido);
            else nombreTipo = tipo!;
        }

        // Comunes: municipio, precio y descripción.
        if (r.MunicipioId is null) Error("municipioId", MensajeMunicipio);
        else if (!await db.Municipios.AsNoTracking().AnyAsync(m => m.Id == r.MunicipioId, ct))
            Error("municipioId", MensajeMunicipioInvalido);

        if (r.PrecioCop is not { } precio || precio <= 0 || precio > PrecioMaximo || precio != decimal.Truncate(precio))
            Error("precioCop", MensajePrecio);

        var descripcion = (r.DescripcionTecnica ?? "").Trim();
        if (descripcion.Length > MaxDescripcion) Error("descripcionTecnica", MensajeDescripcion);

        if (categoria is not { } cat)
            return (errores, null);

        var reglas = Tabla[cat];
        var noAplica = $"Este dato no aplica a {nombreTipo.Trim().ToLower(CultureInfo.GetCultureInfo("es-CO"))}.";
        var placa = NormalizarPlaca(r.Placa);

        // Un campo que la categoría no admite y llega con valor da 400 en ese campo.
        bool Admite(Regla regla, string campo, bool tieneValor)
        {
            if (regla != Regla.NoAdmite) return true;
            if (tieneValor) Error(campo, noAplica);
            return false;
        }

        // Marca y línea (modelo).
        if (Admite(reglas.Marca, "marcaId", r.MarcaId is not null))
        {
            if (r.MarcaId is null)
            {
                if (reglas.Marca == Regla.Obligatorio) Error("marcaId", MensajeMarca);
            }
            else if (!await db.Marcas.AsNoTracking().AnyAsync(m => m.Id == r.MarcaId && m.Activo, ct))
                Error("marcaId", MensajeMarcaInvalida);
        }
        // D-25: el modelo (línea) es obligatorio en Carro, Camión y Maquinaria; opcional en Carrocería.
        if (r.LineaId is null && cat != Categoria.Carroceria)
            Error("lineaId", MensajeLinea);
        if (r.LineaId is not null)
        {
            var linea = await db.Lineas.AsNoTracking().Where(l => l.Id == r.LineaId && l.Activo)
                .Select(l => new { l.MarcaId }).FirstOrDefaultAsync(ct);
            if (linea is null) Error("lineaId", MensajeLineaInvalida);
            else if (linea.MarcaId != r.MarcaId) Error("lineaId", MensajeLineaMarca);
            // D-32: si el modelo está clasificado, debe pertenecer a la categoría del vehículo.
            else if (tipoId is not null &&
                     await db.LineasTipos.AsNoTracking().AnyAsync(lt => lt.LineaId == r.LineaId, ct) &&
                     !await db.LineasTipos.AsNoTracking().AnyAsync(lt => lt.LineaId == r.LineaId && lt.TipoVehiculoId == tipoId, ct))
                Error("lineaId", MensajeLineaCategoria);
        }

        // Año.
        var mensajeAnio = $"Escribe un año entre {AnioMinimo} y {anioActual + 1}.";
        if (Admite(reglas.Anio, "modeloAnio", r.ModeloAnio is not null))
        {
            if (r.ModeloAnio is null)
            {
                if (reglas.Anio == Regla.Obligatorio) Error("modeloAnio", mensajeAnio);
            }
            else if (r.ModeloAnio < AnioMinimo || r.ModeloAnio > anioActual + 1)
                Error("modeloAnio", mensajeAnio);
        }

        // Kilometraje y horas de uso.
        if (Admite(reglas.Kilometraje, "kilometraje", r.Kilometraje is not null))
        {
            if (r.Kilometraje is null || r.Kilometraje < 0 || r.Kilometraje > KilometrajeMaximo)
                Error("kilometraje", MensajeKilometraje);
        }
        if (Admite(reglas.HorasUso, "horasUso", r.HorasUso is not null) &&
            r.HorasUso is not null && (r.HorasUso < 0 || r.HorasUso > HorasUsoMaximo))
            Error("horasUso", MensajeHorasUso);

        // Placa (D-2).
        // D-25: la placa de maquinaria admite hasta 8 caracteres.
        var formatoPlaca = cat == Categoria.Maquinaria ? FormatoPlacaMaquinaria() : FormatoPlaca();
        if (Admite(reglas.Placa, "placa", placa is not null) && placa is not null && !formatoPlaca.IsMatch(placa))
            Error("placa", MensajePlaca);

        // Carrocería.
        if (Admite(reglas.Carroceria, "carroceriaId", r.CarroceriaId is not null))
        {
            if (r.CarroceriaId is null)
            {
                if (reglas.Carroceria == Regla.Obligatorio) Error("carroceriaId", MensajeCarroceria);
            }
            else if (!await db.TiposCarroceria.AsNoTracking().AnyAsync(c => c.Id == r.CarroceriaId && c.Activo, ct))
                Error("carroceriaId", MensajeCarroceriaInvalida);
        }

        // Medidas y material (carrocería).
        foreach (var (campo, valor) in new[] { ("largoM", r.LargoM), ("anchoM", r.AnchoM), ("altoM", r.AltoM) })
            if (Admite(reglas.Medidas, campo, valor is not null) && valor is not null && !DecimalValido(valor.Value, MedidaMaxima))
                Error(campo, MensajeMedida);
        if (Admite(reglas.Material, "materialId", r.MaterialId is not null) && r.MaterialId is not null &&
            !await db.MaterialesCarroceria.AsNoTracking().AnyAsync(m => m.Id == r.MaterialId && m.Activo, ct))
            Error("materialId", MensajeMaterialInvalido);

        // Tipo de máquina.
        if (Admite(reglas.TipoMaquina, "tipoMaquinaId", r.TipoMaquinaId is not null))
        {
            if (r.TipoMaquinaId is null)
            {
                if (reglas.TipoMaquina == Regla.Obligatorio) Error("tipoMaquinaId", MensajeTipoMaquina);
            }
            else if (!await db.TiposMaquina.AsNoTracking().AnyAsync(t => t.Id == r.TipoMaquinaId && t.Activo, ct))
                Error("tipoMaquinaId", MensajeTipoMaquinaInvalido);
        }

        // Combustible, cilindraje y peso bruto.
        if (Admite(reglas.Mecanica, "combustibleId", r.CombustibleId is not null) && r.CombustibleId is not null &&
            !await db.Combustibles.AsNoTracking().AnyAsync(c => c.Id == r.CombustibleId && c.Activo, ct))
            Error("combustibleId", MensajeCombustibleInvalido);
        if (Admite(reglas.Mecanica, "cilindrajeCc", r.CilindrajeCc is not null) && r.CilindrajeCc is not null &&
            (r.CilindrajeCc < 1 || r.CilindrajeCc > CilindrajeMaximo))
            Error("cilindrajeCc", MensajeCilindraje);
        if (Admite(reglas.Mecanica, "pesoBrutoKg", r.PesoBrutoKg is not null) && r.PesoBrutoKg is not null &&
            !DecimalValido(r.PesoBrutoKg.Value, PesoMaximo))
            Error("pesoBrutoKg", MensajePeso);

        // Capacidad de carga (en maquinaria es la capacidad de la máquina, en kg; PROPUESTO).
        if (Admite(reglas.Capacidad, "capacidadCargaKg", r.CapacidadCargaKg is not null) && r.CapacidadCargaKg is not null &&
            !DecimalValido(r.CapacidadCargaKg.Value, PesoMaximo))
            Error("capacidadCargaKg", MensajePeso);

        if (errores.Count > 0)
            return (errores, null);

        T? Si<T>(Regla regla, T? valor) where T : struct => regla == Regla.NoAdmite ? null : valor;
        return (errores, new DatosVehiculo
        {
            TipoId = tipoId!.Value,
            Categoria = cat,
            MarcaId = Si(reglas.Marca, r.MarcaId),
            LineaId = r.LineaId,
            ModeloAnio = reglas.Anio == Regla.NoAdmite || r.ModeloAnio is null ? null : (short)r.ModeloAnio.Value,
            Kilometraje = reglas.Kilometraje == Regla.NoAdmite ? 0 : (int)r.Kilometraje!.Value,
            HorasUso = reglas.HorasUso == Regla.NoAdmite || r.HorasUso is null ? null : (int)r.HorasUso.Value,
            Placa = reglas.Placa == Regla.NoAdmite ? null : placa,
            CarroceriaId = Si(reglas.Carroceria, r.CarroceriaId),
            CombustibleId = Si(reglas.Mecanica, r.CombustibleId),
            CilindrajeCc = reglas.Mecanica == Regla.NoAdmite || r.CilindrajeCc is null ? null : (int)r.CilindrajeCc.Value,
            PesoBrutoKg = Si(reglas.Mecanica, r.PesoBrutoKg),
            CapacidadCargaKg = Si(reglas.Capacidad, r.CapacidadCargaKg),
            LargoM = Si(reglas.Medidas, r.LargoM),
            AnchoM = Si(reglas.Medidas, r.AnchoM),
            AltoM = Si(reglas.Medidas, r.AltoM),
            MaterialId = Si(reglas.Material, r.MaterialId),
            TipoMaquinaId = Si(reglas.TipoMaquina, r.TipoMaquinaId),
            MunicipioId = r.MunicipioId!.Value,
            PrecioCop = r.PrecioCop!.Value,
            DescripcionTecnica = descripcion.Length == 0 ? null : descripcion
        });
    }

    // Mayor que 0, hasta el máximo de la columna y con 2 decimales como mucho.
    private static bool DecimalValido(decimal valor, decimal maximo) =>
        valor > 0 && valor <= maximo && decimal.Round(valor, 2) == valor;
}
