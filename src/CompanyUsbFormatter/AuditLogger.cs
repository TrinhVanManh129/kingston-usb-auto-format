using System.Text;

namespace CompanyUsbFormatter;

public sealed class AuditLogger : IAuditLogger
{
    private const string Header =
        "TimestampUtc,ComputerName,UserName,DiskNumber,Model,SerialNumber,Size,Result,DriveLetter,Message";

    private readonly string _logDirectory;

    public AuditLogger(string? preferredDirectory = null, string? fallbackDirectory = null)
    {
        var primary = preferredDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "CompanyUsbFormatter",
                "Logs");
        var fallback = fallbackDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Logs");

        _logDirectory = EnsureDirectory(primary) ? primary : fallback;
        Directory.CreateDirectory(_logDirectory);
    }

    public async Task LogAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_logDirectory, $"{record.TimestampUtc:yyyy-MM}.csv");
        var needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
        var line = string.Join(
            ",",
            EscapeCsv(record.TimestampUtc.ToString("O")),
            EscapeCsv(record.ComputerName),
            EscapeCsv(record.UserName),
            record.DiskNumber.ToString(),
            EscapeCsv(record.Model),
            EscapeCsv(record.SerialNumber),
            record.Size.ToString(),
            EscapeCsv(record.Result),
            EscapeCsv(record.DriveLetter),
            EscapeCsv(Sanitize(record.Message)));

        await using var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            4_096,
            useAsync: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (needsHeader)
        {
            await writer.WriteLineAsync(Header.AsMemory(), cancellationToken);
        }

        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    public static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool EnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Sanitize(string value)
    {
        return string.Join(" ", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
