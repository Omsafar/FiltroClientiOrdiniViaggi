using FiltroClientiOrdiniViaggi.Models;
using FiltroClientiOrdiniViaggi.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace FiltroClientiOrdiniViaggi.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly CsvReaderService.Aggregates _agg;
    private readonly AnalyzerService _analyzer = new();

    public ObservableCollection<string> FilteredClients { get; } = new();
    public ObservableCollection<ResultRow> Results { get; } = new();

    private string _queryText = string.Empty;
    public string QueryText
    {
        get => _queryText;
        set
        {
            if (Set(ref _queryText, value))
            {
                RefreshFilteredClients();
            }
        }
    }

    private string? _selectedClient;
    public string? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (Set(ref _selectedClient, value) && !string.IsNullOrWhiteSpace(value))
            {
                QueryText = value;
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    private string _statusMessage = "Pronto";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => Set(ref _selectedTabIndex, value);
    }

    public ICommand AnalyzeCommand { get; }
    public ICommand ExportCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        StatusMessage = "Carico e aggrego CSV...";
        try
        {
            var reader = new CsvReaderService();
            _agg = reader.ReadAndAggregate();
            StatusMessage = $"Righe valide: {_agg.RowsRead:n0}, scartate: {_agg.RowsSkipped:n0}. Clienti: {_agg.AllClients.Count:n0}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _agg = new CsvReaderService.Aggregates(new(), new(StringComparer.OrdinalIgnoreCase), 0, 0);
        }

        RefreshFilteredClients();

        AnalyzeCommand = new RelayCommand(_ => Analyze(), _ => !string.IsNullOrWhiteSpace(QueryText));
        ExportCommand = new RelayCommand(_ => ExportResults(), _ => Results.Any());
    }

    private void RefreshFilteredClients()
    {
        FilteredClients.Clear();
        if (string.IsNullOrWhiteSpace(QueryText))
        {
            foreach (var c in _agg.AllClients
                         .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                         .Take(50))
            {
                FilteredClients.Add(c);
            }
            return;
        }

        string q = QueryText.Trim();
        var candidates = _agg.AllClients.Where(c =>
            c.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (q.Length >= 5 && Levenshtein(c.ToLowerInvariant(), q.ToLowerInvariant()) <= 2));

        foreach (var c in candidates
                     .OrderBy(c => c.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                     .Take(200))
        {
            FilteredClients.Add(c);
        }
    }

    private void Analyze()
    {
        Results.Clear();

        var client = QueryText?.Trim();
        if (string.IsNullOrWhiteSpace(client))
        {
            StatusMessage = "Inserisci/Seleziona un cliente.";
            return;
        }

        StatusMessage = $"Analizzo per cliente: {client}...";
        Progress = 30;

        var rows = _analyzer.BuildResultsForClient(client, _agg.Groups);
        foreach (var r in rows)
        {
            Results.Add(r);
        }

        Progress = 100;
        StatusMessage = $"Righe risultato: {Results.Count:n0}";
        SelectedTabIndex = 1;
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExportResults()
    {
        try
        {
            Directory.CreateDirectory(@"C:\Temp");
            var safeClient = MakeSafeFilename(QueryText ?? "cliente");
            var path = $@"C:\Temp\output_{safeClient}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Risultati");
                worksheet.Cells[1, 1].Value = "Data";
                worksheet.Cells[1, 2].Value = "Viaggio";
                worksheet.Cells[1, 3].Value = "Totale ordini";
                worksheet.Cells[1, 4].Value = "Clienti distinti";
                worksheet.Cells[1, 5].Value = "Altri clienti";

                int row = 2;
                foreach (var r in Results)
                {
                    worksheet.Cells[row, 1].Value = r.Data;
                    worksheet.Cells[row, 1].Style.Numberformat.Format = "yyyy-mm-dd";
                    worksheet.Cells[row, 2].Value = r.Viaggio;
                    worksheet.Cells[row, 3].Value = r.TotaleOrdini;
                    worksheet.Cells[row, 4].Value = r.ClientiDistinti;
                    worksheet.Cells[row, 5].Value = r.AltriClienti;
                    row++;
                }

                using (var headerRange = worksheet.Cells[1, 1, 1, 5])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xF1, 0xF3, 0xF4));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                worksheet.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(path));
            }

            StatusMessage = $"Esportato: {path}";
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore export: {ex.Message}";
        }
    }

    private static int Levenshtein(string s, string t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static string MakeSafeFilename(string raw)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            raw = raw.Replace(c, '_');
        }
        return raw;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        return true;
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
