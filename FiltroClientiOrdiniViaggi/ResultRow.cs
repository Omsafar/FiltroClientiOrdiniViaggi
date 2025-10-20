namespace FiltroClientiOrdiniViaggi.Models;

public sealed class ResultRow
{
    public DateTime Data { get; init; }
    public string Viaggio { get; init; } = string.Empty;
    public int TotaleOrdini { get; init; }
    public int ClientiDistinti { get; init; }
    public string AltriClienti { get; init; } = string.Empty;
}
