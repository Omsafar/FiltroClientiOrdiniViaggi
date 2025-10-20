using FiltroClientiOrdiniViaggi.Models;

namespace FiltroClientiOrdiniViaggi.Services;

public sealed class AnalyzerService
{
    public List<ResultRow> BuildResultsForClient(
        string selectedClient,
        Dictionary<(DateTime Date, string Viaggio), CsvReaderService.Group> groups)
    {
        var results = new List<ResultRow>();

        foreach (var (key, group) in groups)
        {
            if (!group.Clients.Contains(selectedClient, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var altri = group.Clients
                .Where(c => !string.Equals(c, selectedClient, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

            results.Add(new ResultRow
            {
                Data = key.Date,
                Viaggio = key.Viaggio,
                TotaleOrdini = (int)group.OrderCount,
                ClientiDistinti = group.Clients.Count,
                AltriClienti = string.Join(", ", altri)
            });
        }

        return results
            .OrderBy(r => r.Data)
            .ThenBy(r => r.Viaggio, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
