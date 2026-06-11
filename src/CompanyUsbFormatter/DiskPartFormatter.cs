using System.Text;

namespace CompanyUsbFormatter;

public sealed class DiskPartFormatter : IDiskFormatter
{
    private readonly ICommandRunner _runner;
    private readonly ITemporaryFileStore _temporaryFiles;

    public DiskPartFormatter(ICommandRunner runner, ITemporaryFileStore temporaryFiles)
    {
        _runner = runner;
        _temporaryFiles = temporaryFiles;
    }

    public async Task<FormatResult> FormatAsync(
        int diskNumber,
        UsbFileSystem fileSystem,
        CancellationToken cancellationToken)
    {
        if (diskNumber <= 0)
        {
            return new FormatResult(false, "The disk number is not allowed.");
        }

        var scriptPath = _temporaryFiles.Create(BuildScript(diskNumber, fileSystem));
        try
        {
            var result = await _runner.RunAsync(
                Path.Combine(Environment.SystemDirectory, "diskpart.exe"),
                new[] { "/s", scriptPath },
                TimeSpan.FromMinutes(5),
                cancellationToken);

            if (result.TimedOut)
            {
                return new FormatResult(false, "DiskPart timed out.");
            }

            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError;
                return new FormatResult(false, $"DiskPart failed: {CleanMessage(detail)}");
            }

            return new FormatResult(true, "DiskPart completed and the resulting volume was verified.");
        }
        finally
        {
            _temporaryFiles.Delete(scriptPath);
        }
    }

    public static string BuildScript(int diskNumber, UsbFileSystem fileSystem)
    {
        if (diskNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diskNumber));
        }

        var diskPartName = fileSystem.DiskPartName();
        return string.Join(
            Environment.NewLine,
            $"select disk {diskNumber}",
            "attributes disk clear readonly",
            "clean",
            "convert mbr",
            "create partition primary",
            $"format fs={diskPartName} quick label=COMPANY-USB",
            "assign",
            "exit",
            string.Empty);
    }

    private static string CleanMessage(string value)
    {
        return string.Join(" ", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}

public sealed class TemporaryFileStore : ITemporaryFileStore
{
    public string Create(string content)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"company-usb-formatter-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content, Encoding.ASCII);
        return path;
    }

    public void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
