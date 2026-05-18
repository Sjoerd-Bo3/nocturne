using System.Text;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Legacy;

/// <summary>
/// Static helpers for formatting <see cref="Entry"/> and <see cref="Treatment"/> collections into
/// plain-text export formats (CSV, TSV, TXT). All methods are pure functions with no external dependencies.
/// </summary>
public static class DataFormatService
{
    public static string FormatEntries(Entry[] entries, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "csv" => FormatEntriesAsCsv(entries),
            "tsv" => FormatEntriesAsTsv(entries),
            "txt" => FormatEntriesAsText(entries),
            _ => throw new ArgumentException($"Unsupported format: {format}"),
        };
    }

    /// <inheritdoc />
    public static string FormatTreatments(Treatment[] treatments, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "csv" => FormatTreatmentsAsCsv(treatments),
            "tsv" => FormatTreatmentsAsTsv(treatments),
            "txt" => FormatTreatmentsAsText(treatments),
            _ => throw new ArgumentException($"Unsupported format: {format}"),
        };
    }

    /// <inheritdoc />
    public static string FormatDeviceStatus(DeviceStatus[] deviceStatus, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "csv" => FormatDeviceStatusAsCsv(deviceStatus),
            "tsv" => FormatDeviceStatusAsTsv(deviceStatus),
            "txt" => FormatDeviceStatusAsText(deviceStatus),
            _ => throw new ArgumentException($"Unsupported format: {format}"),
        };
    }

    /// <inheritdoc />
    public static string GetContentType(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "csv" => "text/csv",
            "tsv" => "text/tab-separated-values",
            "txt" => "text/plain",
            _ => "application/json",
        };
    }

    private static string FormatEntriesAsCsv(Entry[] entries)
    {
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine(
            "_id,mills,date,dateString,sgv,mbg,type,direction,device,filtered,unfiltered,rssi,noise"
        );

        foreach (var entry in entries)
        {
            sb.AppendLine(FormatEntryAsCsvRow(entry));
        }

        return sb.ToString();
    }

    private static string FormatEntriesAsTsv(Entry[] entries)
    {
        var sb = new StringBuilder();

        // TSV Header
        sb.AppendLine(
            "_id\tmills\tdate\tdateString\tsgv\tmbg\ttype\tdirection\tdevice\tfiltered\tunfiltered\trssi\tnoise"
        );

        foreach (var entry in entries)
        {
            sb.AppendLine(FormatEntryAsTsvRow(entry));
        }

        return sb.ToString();
    }

    private static string FormatEntriesAsText(Entry[] entries)
    {
        var sb = new StringBuilder();

        foreach (var entry in entries)
        {
            sb.AppendLine(
                $"Entry {entry.Id}: {entry.Sgv ?? entry.Mgdl} mg/dL at {entry.DateString} ({entry.Type})"
            );
        }

        return sb.ToString();
    }

    private static string FormatEntryAsCsvRow(Entry entry)
    {
        return $"{EscapeCsvField(entry.Id ?? "")},{entry.Mills},{entry.Date?.ToString("o") ?? ""},{EscapeCsvField(entry.DateString ?? "")},{entry.Sgv?.ToString() ?? ""},{entry.Mgdl.ToString()},{EscapeCsvField(entry.Type)},{EscapeCsvField(entry.Direction ?? "")},{EscapeCsvField(entry.Device ?? "")},{entry.Filtered?.ToString() ?? ""},{entry.Unfiltered?.ToString() ?? ""},{entry.Rssi?.ToString() ?? ""},{entry.Noise?.ToString() ?? ""}";
    }

    private static string FormatEntryAsTsvRow(Entry entry)
    {
        return $"{EscapeTsvField(entry.Id ?? "")}\t{entry.Mills}\t{entry.Date?.ToString("o") ?? ""}\t{EscapeTsvField(entry.DateString ?? "")}\t{entry.Sgv?.ToString() ?? ""}\t{entry.Mgdl.ToString()}\t{EscapeTsvField(entry.Type)}\t{EscapeTsvField(entry.Direction ?? "")}\t{EscapeTsvField(entry.Device ?? "")}\t{entry.Filtered?.ToString() ?? ""}\t{entry.Unfiltered?.ToString() ?? ""}\t{entry.Rssi?.ToString() ?? ""}\t{entry.Noise?.ToString() ?? ""}";
    }

    private static string FormatTreatmentsAsCsv(Treatment[] treatments)
    {
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine(
            "_id,timestamp,created_at,eventType,insulin,carbs,glucose,glucoseType,notes,enteredBy"
        );

        foreach (var treatment in treatments)
        {
            sb.AppendLine(FormatTreatmentAsCsvRow(treatment));
        }

        return sb.ToString();
    }

    private static string FormatTreatmentsAsTsv(Treatment[] treatments)
    {
        var sb = new StringBuilder();

        // TSV Header
        sb.AppendLine(
            "_id\ttimestamp\tcreated_at\teventType\tinsulin\tcarbs\tglucose\tglucoseType\tnotes\tenteredBy"
        );

        foreach (var treatment in treatments)
        {
            sb.AppendLine(FormatTreatmentAsTsvRow(treatment));
        }

        return sb.ToString();
    }

    private static string FormatTreatmentsAsText(Treatment[] treatments)
    {
        var sb = new StringBuilder();

        foreach (var treatment in treatments)
        {
            var parts = new List<string>();
            if (treatment.Insulin.HasValue)
                parts.Add($"{treatment.Insulin}U insulin");
            if (treatment.Carbs.HasValue)
                parts.Add($"{treatment.Carbs}g carbs");

            sb.AppendLine(
                $"Treatment {treatment.Id}: {string.Join(", ", parts)} at {treatment.CreatedAt} ({treatment.EventType})"
            );
        }

        return sb.ToString();
    }

    private static string FormatTreatmentAsCsvRow(Treatment treatment)
    {
        return $"{EscapeCsvField(treatment.Id ?? "")},{treatment.Timestamp?.ToString() ?? ""},{EscapeCsvField(treatment.CreatedAt ?? "")},{EscapeCsvField(treatment.EventType ?? "")},{treatment.Insulin?.ToString() ?? ""},{treatment.Carbs?.ToString() ?? ""},{treatment.Glucose?.ToString() ?? ""},{EscapeCsvField(treatment.GlucoseType ?? "")},{EscapeCsvField(treatment.Notes ?? "")},{EscapeCsvField(treatment.EnteredBy ?? "")}";
    }

    private static string FormatTreatmentAsTsvRow(Treatment treatment)
    {
        return $"{EscapeTsvField(treatment.Id ?? "")}\t{treatment.Timestamp?.ToString() ?? ""}\t{EscapeTsvField(treatment.CreatedAt ?? "")}\t{EscapeTsvField(treatment.EventType ?? "")}\t{treatment.Insulin?.ToString() ?? ""}\t{treatment.Carbs?.ToString() ?? ""}\t{treatment.Glucose?.ToString() ?? ""}\t{EscapeTsvField(treatment.GlucoseType ?? "")}\t{EscapeTsvField(treatment.Notes ?? "")}\t{EscapeTsvField(treatment.EnteredBy ?? "")}";
    }

    private static string FormatDeviceStatusAsCsv(DeviceStatus[] deviceStatuses)
    {
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine(
            "_id,mills,created_at,device,uploader_battery,pump_battery_percent,iob_timestamp,iob_bolusiob,iob_basaliob"
        );

        foreach (var status in deviceStatuses)
        {
            sb.AppendLine(FormatDeviceStatusAsCsvRow(status));
        }

        return sb.ToString();
    }

    private static string FormatDeviceStatusAsTsv(DeviceStatus[] deviceStatuses)
    {
        var sb = new StringBuilder();

        // TSV Header
        sb.AppendLine(
            "_id\tmills\tcreated_at\tdevice\tuploader_battery\tpump_battery_percent\tiob_timestamp\tiob_bolusiob\tiob_basaliob"
        );

        foreach (var status in deviceStatuses)
        {
            sb.AppendLine(FormatDeviceStatusAsTsvRow(status));
        }

        return sb.ToString();
    }

    private static string FormatDeviceStatusAsText(DeviceStatus[] deviceStatuses)
    {
        var sb = new StringBuilder();

        foreach (var status in deviceStatuses)
        {
            var batteryInfo = status.Uploader?.Battery?.ToString() ?? "unknown";
            sb.AppendLine(
                $"Device Status {status.Id}: {status.Device} at {status.CreatedAt} (Battery: {batteryInfo}%)"
            );
        }

        return sb.ToString();
    }

    private static string FormatDeviceStatusAsCsvRow(DeviceStatus status)
    {
        return $"{EscapeCsvField(status.Id ?? "")},{status.Mills},{EscapeCsvField(status.CreatedAt ?? "")},{EscapeCsvField(status.Device ?? "")},{status.Uploader?.Battery?.ToString() ?? ""},{status.Pump?.Battery?.Percent?.ToString() ?? ""},{EscapeCsvField(status.Pump?.Iob?.Timestamp ?? "")},{status.Pump?.Iob?.BolusIob?.ToString() ?? ""},{status.Pump?.Iob?.BasalIob?.ToString() ?? ""}";
    }

    private static string FormatDeviceStatusAsTsvRow(DeviceStatus status)
    {
        return $"{EscapeTsvField(status.Id ?? "")}\t{status.Mills}\t{EscapeTsvField(status.CreatedAt ?? "")}\t{EscapeTsvField(status.Device ?? "")}\t{status.Uploader?.Battery?.ToString() ?? ""}\t{status.Pump?.Battery?.Percent?.ToString() ?? ""}\t{EscapeTsvField(status.Pump?.Iob?.Timestamp ?? "")}\t{status.Pump?.Iob?.BolusIob?.ToString() ?? ""}\t{status.Pump?.Iob?.BasalIob?.ToString() ?? ""}";
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // Escape double quotes by doubling them and wrap in quotes if needed
        if (
            field.Contains('"')
            || field.Contains(',')
            || field.Contains('\n')
            || field.Contains('\r')
        )
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string EscapeTsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // Replace tabs, newlines, and carriage returns with spaces
        return field.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }
}
