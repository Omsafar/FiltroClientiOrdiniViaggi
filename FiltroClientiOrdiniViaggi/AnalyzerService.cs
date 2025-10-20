using ElencoAnalyzer.Models;

namespace ElencoAnalyzer.Services
{
    public sealed class AnalyzerService
    {
        public List<ResultRow> BuildResultsForClient(
            string selectedClient,
            Dictionary<(DateTime Date, string Viaggio), CsvReaderService.Group> groups)
        {
            var results = new List<ResultRow>();

            foreach (var kvp in groups)
            {
                var key = kvp.Key;
                var g = kvp.Value;

                // Considera solo i gruppi dove il cliente selezionato è presente
                if (!g.Clients.Contains(selectedClient, StringComparer.OrdinalIgnoreCase)) continue;

                var altri = g.Clients
                    .Where(c => !string.Equals(c, selectedClient, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

                results.Add(new ResultRow
                {
                    Data = key.Date,
                    Viaggio = key.Viaggio,
                    TotaleOrdini = (int)g.OrderCount,
                    ClientiDistinti = g.Clients.Count,
                    AltriClienti = string.Join(", ", altri)
                });
            }

            return results
                .OrderBy(r => r.Data)
                .ThenBy(r => r.Viaggio, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
