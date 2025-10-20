namespace FiltroClientiOrdiniViaggi.Models;

public sealed class Record
{
    public string Ordine { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string Viaggio { get; init; } = string.Empty;
    public DateTime Data { get; init; }
}
