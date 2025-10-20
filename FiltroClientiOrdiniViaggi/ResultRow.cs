using System;

namespace ElencoAnalyzer.Models
{
    public sealed class Record
    {
        public string Ordine { get; init; } = "";
        public string Cliente { get; init; } = "";
        public string Viaggio { get; init; } = "";
        public DateTime Data { get; init; } // solo data (00:00)
    }
}
