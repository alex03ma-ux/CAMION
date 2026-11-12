namespace Camion.Api.Models;

// P3: catálogos existentes del script 002 (marcas, lineas, tipos_vehiculo, tipos_carroceria, combustibles).
// P3 solo los lee; P4p (D-37) permite al administrador crear, renombrar y activar o desactivar (nunca borrar).
// D-17 (PROPUESTO): "modelo" = línea; "categoría" (Carro / Camión) = tipo de vehículo.
public sealed class Marca : IElementoCatalogo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
    public List<Linea> Lineas { get; set; } = [];
}

public sealed class Linea : IElementoCatalogo
{
    public long Id { get; set; }
    public long MarcaId { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
    public Marca? Marca { get; set; }
}

// D-32: categorías (tipos_vehiculo) a las que pertenece cada modelo. Tabla lineas_tipos (migración 009).
public sealed class LineaTipo
{
    public long LineaId { get; set; }
    public long TipoVehiculoId { get; set; }
}

public sealed class TipoVehiculo : IElementoCatalogo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public sealed class TipoCarroceria : IElementoCatalogo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public sealed class Combustible : IElementoCatalogo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

// P4a (D-19): tablas nuevas de la migración 004. La gestión de estos catálogos es P7.
public sealed class MaterialCarroceria : IElementoCatalogo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public sealed class TipoMaquina : IElementoCatalogo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

// P4p: forma común (id, nombre, activo) de los catálogos administrables.
public interface IElementoCatalogo
{
    long Id { get; }
    string Nombre { get; set; }
    bool Activo { get; set; }
}
