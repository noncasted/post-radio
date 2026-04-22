using System.Text;
using Infrastructure;

namespace Console.Infrastructure.Monitoring;

public static class HeapReportFormatter
{
    public static string FormatReport(DateTime timestamp, IReadOnlyList<HeapSnapshotResponse> snapshots)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"========== HEAP REPORT {timestamp:yyyy-MM-ddTHH:mm:ss.fffZ} ==========");
        sb.AppendLine();

        foreach (var s in snapshots)
        {
            sb.AppendLine($"------ SERVICE {s.ServiceName} ({s.ServiceId}) ------");
            sb.AppendLine($"Mode:             {(s.Deep ? "deep (ClrMD heap walk)" : "quick (GC stats only)")}");
            sb.AppendLine($"Timestamp:        {s.Timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}");
            sb.AppendLine($"WorkingSet:       {FormatBytes(s.WorkingSetBytes)}");
            sb.AppendLine($"GC Total:         {FormatBytes(s.GcTotalBytes)}");
            sb.AppendLine($"Gen0:             {FormatBytes(s.Gen0SizeBytes)}");
            sb.AppendLine($"Gen1:             {FormatBytes(s.Gen1SizeBytes)}");
            sb.AppendLine($"Gen2:             {FormatBytes(s.Gen2SizeBytes)}");
            sb.AppendLine($"LOH:              {FormatBytes(s.LohSizeBytes)}");
            sb.AppendLine($"POH:              {FormatBytes(s.PohSizeBytes)}");
            sb.AppendLine($"Collect duration: {s.CollectDurationMs} ms");

            if (s.Error != null)
                sb.AppendLine($"Error:            {s.Error}");

            if (s.TopTypes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Top types (sorted by bytes):");
                sb.AppendLine($"| {"Rank",4} | {"Bytes",14} | {"Count",9} | {"Type",-70} |");
                sb.AppendLine($"|{new string('-', 6)}|{new string('-', 16)}|{new string('-', 11)}|{new string('-', 72)}|");

                for (var i = 0; i < s.TopTypes.Count; i++)
                {
                    var t = s.TopTypes[i];
                    sb.AppendLine($"| {i + 1,4} | {t.TotalBytes,14:N0} | {t.Count,9:N0} | {t.TypeName,-70} |");
                }
            }
            else if (s.Deep)
            {
                sb.AppendLine("Top types: ClrMD heap walk produced no data (see Error above).");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
