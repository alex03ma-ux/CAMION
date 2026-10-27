namespace Camion.Api.Models;

// P3: tablas existentes departamentos y municipios (script 002). Solo lectura.
public sealed class Departamento
{
    public long Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public sealed class Municipio
{
    public long Id { get; set; }
    public long DepartamentoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public Departamento? Departamento { get; set; }
}
