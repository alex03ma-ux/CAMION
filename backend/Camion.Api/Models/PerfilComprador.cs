namespace Camion.Api.Models;

public sealed class PerfilComprador
{
    public long Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
