using System.Text.Json;

namespace CompanyUsbFormatter;

public sealed class PowerShellDiskInventory : IDiskInventory
{
    private static readonly string PowerShellPath = Path.Combine(
        Environment.SystemDirectory,
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private readonly ICommandRunner _runner;

    public PowerShellDiskInventory(ICommandRunner runner)
    {
        _runner = runner;
    }

    public async Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            PowerShellPath,
            new[]
            {
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                BuildInventoryScript(),
            },
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (result.TimedOut)
        {
            throw new InvalidOperationException("Disk inventory timed out.");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Disk inventory failed: {CleanMessage(result.StandardError)}");
        }

        return ParseDiskJson(result.StandardOutput);
    }

    public async Task<DiskInfo?> GetDiskAsync(int diskNumber, CancellationToken cancellationToken)
    {
        var disks = await GetDisksAsync(cancellationToken);
        return disks.SingleOrDefault(disk => disk.Number == diskNumber);
    }

    public static IReadOnlyList<DiskInfo> ParseDiskJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<DiskInfo>();
        }

        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToArray()
            : new[] { document.RootElement };

        return elements.Select(ParseDisk).ToArray();
    }

    private static DiskInfo ParseDisk(JsonElement element)
    {
        var volumes = element.TryGetProperty("Volumes", out var volumeElement)
            && volumeElement.ValueKind == JsonValueKind.Array
            ? volumeElement.EnumerateArray().Select(ParseVolume).ToArray()
            : Array.Empty<VolumeInfo>();

        return new DiskInfo(
            element.GetProperty("Number").GetInt32(),
            ReadString(element, "FriendlyName"),
            ReadString(element, "SerialNumber"),
            ReadString(element, "UniqueId"),
            ReadString(element, "BusType"),
            ReadString(element, "OperationalStatus"),
            element.GetProperty("Size").GetInt64(),
            element.GetProperty("IsBoot").GetBoolean(),
            element.GetProperty("IsSystem").GetBoolean(),
            ReadString(element, "PartitionStyle"),
            volumes);
    }

    private static VolumeInfo ParseVolume(JsonElement element)
    {
        return new VolumeInfo(
            ReadString(element, "DriveLetter"),
            ReadString(element, "FileSystem"),
            ReadString(element, "Label"),
            element.GetProperty("Size").GetInt64());
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : string.Empty;
    }

    private static string BuildInventoryScript()
    {
        return """
            $ErrorActionPreference = 'Stop'
            $items = @(
              Get-Disk | ForEach-Object {
                $disk = $_
                $volumes = @(
                  Get-Partition -DiskNumber $disk.Number -ErrorAction SilentlyContinue |
                    Get-Volume -ErrorAction SilentlyContinue |
                    ForEach-Object {
                      [pscustomobject]@{
                        DriveLetter = if ($_.DriveLetter) { [string]$_.DriveLetter } else { '' }
                        FileSystem = [string]$_.FileSystem
                        Label = [string]$_.FileSystemLabel
                        Size = [int64]$_.Size
                      }
                    }
                )
                [pscustomobject]@{
                  Number = [int]$disk.Number
                  FriendlyName = [string]$disk.FriendlyName
                  SerialNumber = [string]$disk.SerialNumber
                  UniqueId = [string]$disk.UniqueId
                  BusType = [string]$disk.BusType
                  OperationalStatus = [string]$disk.OperationalStatus
                  Size = [int64]$disk.Size
                  IsBoot = [bool]$disk.IsBoot
                  IsSystem = [bool]$disk.IsSystem
                  PartitionStyle = [string]$disk.PartitionStyle
                  Volumes = $volumes
                }
              }
            )
            ConvertTo-Json -InputObject $items -Depth 5 -Compress
            """;
    }

    private static string CleanMessage(string value)
    {
        return string.Join(" ", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
