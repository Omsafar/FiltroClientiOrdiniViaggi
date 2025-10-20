using ElencoAnalyzer.Models;
using ElencoAnalyzer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ElencoAnalyzer.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        // Stato caricamento/aggregati
        private readonly CsvReaderService.Aggregates _agg;
        private readonly AnalyzerService _analyzer = new();

        // UI-bindable
        public ObservableCollection<string> FilteredClients { get; } = new();
        public ObservableCollection<ResultRow> Results { get; } = new();

        private string _queryText = "";
        public string QueryText
        {
            get => _queryText;
            set
            {
                if (Set(ref _queryText, value))
                    RefreshFilteredClients();
            }
        }

        private string? _selectedClient;
        public string? SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (Set(ref _selectedClient, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        QueryText = value;
                }
            }
        }

        private double _progress;
        public double Progress { get => _progress; set => Set(ref _progress, value); }

        private string _statusMessage = "Pronto";
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

        public ICommand AnalyzeCommand { get; }
        public ICommand ExportCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel()
        {
            StatusMessage = "Carico e aggrego CSV...";
            try
            {
                // Carica/aggrego all'avvio (streaming)
                var reader = new CsvReaderService();
                _agg = reader.ReadAndAggregate();
                StatusMessage = $"Righe valide: {_agg.RowsRead:n0}, scartate: {_agg.RowsSkipped:n0}. Clienti: {_agg.AllClients.Count:n0}";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                _agg = new CsvReaderService.Aggregates(new(), new(StringComparer.OrdinalIgnoreCase), 0, 0);
            }

            // Precarica suggerimenti (non li mostriamo tutti subito per non essere pesanti)
            RefreshFilteredClients();

            AnalyzeCommand = new RelayCommand(_ => Analyze(), _ => !string.IsNullOrWhiteSpace(QueryText));
            ExportCommand = new RelayCommand(_ => ExportResults(), _ => Results.Any());
        }

        private void RefreshFilteredClients()
        {
            FilteredClients.Clear();
            if (string.IsNullOrWhiteSpace(QueryText))
            {
                // Niente query → mostra primi 50 in ordine alfabetico
                foreach (var c in _agg.AllClients
                             .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                             .Take(50))
                    FilteredClients.Add(c);
                return;
            }

            string q = QueryText.Trim();
            // Match parziale + semplice fuzzy (distanza di Levenshtein <= 2 su stringhe >= 5)
            var candidates = _agg.AllClients.Where(c =>
                c.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (q.Length >= 5 && Levenshtein(c.ToLowerInvariant(), q.ToLowerInvariant()) <= 2));

            foreach (var c in candidates
                         .OrderBy(c => c.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                         .Take(200))
                FilteredClients.Add(c);
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
            foreach (var r in rows) Results.Add(r);

            Progress = 100;
            StatusMessage = $"Righe risultato: {Results.Count:n0}";
        }

        private void ExportResults()
        {
            try
            {
                Directory.CreateDirectory(@"C:\Temp");
                var safeClient = MakeSafeFilename(QueryText ?? "cliente");
                var path = $@"C:\Temp\output_{safeClient}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);
                sw.WriteLine("Data;Viaggio;Totale ordini;Clienti distinti;Altri clienti");
                foreach (var r in Results)
                {
                    sw.WriteLine($"{r.Data:yyyy-MM-dd};\"{r.Viaggio}\";{r.TotaleOrdini};{r.ClientiDistinti};\"{r.AltriClienti}\"");
                }

                StatusMessage = $"Esportato: {path}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

        // Utility
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
                raw = raw.Replace(c, '_');
            return raw;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
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
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }
    }
}
