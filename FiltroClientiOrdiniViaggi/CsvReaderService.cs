using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO;

namespace FiltroClientiOrdiniViaggi.Services;

public sealed class CsvReaderService
{
    public sealed record Aggregates(
        Dictionary<(DateTime Date, string Viaggio), Group> Groups,
        HashSet<string> AllClients,
        long RowsRead,
        long RowsSkipped);

    public sealed class Group
    {
        public long OrderCount;
        public HashSet<string> Clients = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string CsvPath = @"C:\temp\elenco.csv";

    public Aggregates ReadAndAggregate(CancellationToken ct = default)
    {
        if (!File.Exists(CsvPath))
        {
            throw new FileNotFoundException($"File non trovato: {CsvPath}");
        }

        char DetectSeparator(string headerLine)
        {
            if (headerLine.Contains(';') && !headerLine.Contains(','))
            {
                return ';';
            }

            int countSemi = headerLine.Count(c => c == ';');
            int countComma = headerLine.Count(c => c == ',');
            return countSemi >= countComma ? ';' : ',';
        }

        using var sr = new StreamReader(CsvPath, detectEncodingFromByteOrderMarks: true);
        string? headerLine = sr.ReadLine() ?? throw new InvalidDataException("CSV vuoto (manca intestazione).");
        char separator = DetectSeparator(headerLine);

        sr.BaseStream.Seek(0, SeekOrigin.Begin);
        sr.DiscardBufferedData();

        using var parser = new TextFieldParser(sr);
        parser.SetDelimiters(separator.ToString());
        parser.HasFieldsEnclosedInQuotes = true;

        if (parser.EndOfData)
        {
            throw new InvalidDataException("CSV vuoto.");
        }

        string[]? header = parser.ReadFields();
        if (header == null)
        {
            throw new InvalidDataException("Intestazione non leggibile.");
        }

        int idxOrdine = IndexOf(header, "Ordine");
        int idxCliente = IndexOf(header, "Cliente");
        int idxViaggio = IndexOf(header, "Viaggio presa: codice");
        int idxData = IndexOf(header, "Data competenza");
        if (idxOrdine < 0 || idxCliente < 0 || idxViaggio < 0 || idxData < 0)
        {
            throw new InvalidDataException("Non trovo tutte le colonne richieste: 'Ordine', 'Cliente', 'Viaggio presa: codice', 'Data competenza'.");
        }

        var groups = new Dictionary<(DateTime, string), Group>();
        var allClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long rowsRead = 0, rowsSkipped = 0;

        var itCulture = new CultureInfo("it-IT");

        while (!parser.EndOfData)
        {
            ct.ThrowIfCancellationRequested();

            string[]? row = parser.ReadFields();
            if (row == null)
            {
                break;
            }

            string ordine = Safe(row, idxOrdine);
            string cliente = Safe(row, idxCliente);
            string viaggio = Safe(row, idxViaggio);
            string sData = Safe(row, idxData);

            if (string.IsNullOrWhiteSpace(ordine) || string.IsNullOrWhiteSpace(cliente) ||
                string.IsNullOrWhiteSpace(viaggio) || string.IsNullOrWhiteSpace(sData))
            {
                rowsSkipped++;
                continue;
            }

            if (!TryParseDate(sData, itCulture, out DateTime dataSolo))
            {
                rowsSkipped++;
                continue;
            }

            rowsRead++;
            allClients.Add(cliente);

            var key = (dataSolo, viaggio);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new Group();
                groups[key] = group;
            }

            group.OrderCount++;
            group.Clients.Add(cliente);
        }

        return new Aggregates(groups, allClients, rowsRead, rowsSkipped);

        static int IndexOf(string[] header, string name)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (string.Equals(Normalize(header[i]), Normalize(name), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;

            static string Normalize(string s) => s.Trim();
        }

        static string Safe(string[] arr, int i) => i >= 0 && i < arr.Length ? arr[i]?.Trim() ?? string.Empty : string.Empty;

        static bool TryParseDate(string s, CultureInfo it, out DateTime dateOnly)
        {
            string[] formats =
            {
                "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd",
                "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
            };
            if (DateTime.TryParseExact(s, formats, it, DateTimeStyles.AssumeLocal, out var dt) ||
                DateTime.TryParse(s, it, DateTimeStyles.AssumeLocal, out dt))
            {
                dateOnly = dt.Date;
                return true;
            }
            dateOnly = default;
            return false;
        }
    }
}
