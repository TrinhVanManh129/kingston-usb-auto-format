namespace CompanyUsbFormatter;

public enum UsbFileSystem
{
    ExFat,
    Ntfs,
    Fat32,
}

public static class UsbFileSystemInfo
{
    public static string DisplayName(this UsbFileSystem fileSystem)
    {
        return fileSystem switch
        {
            UsbFileSystem.ExFat => "exFAT",
            UsbFileSystem.Ntfs => "NTFS",
            UsbFileSystem.Fat32 => "FAT32",
            _ => throw new ArgumentOutOfRangeException(nameof(fileSystem)),
        };
    }

    public static string DiskPartName(this UsbFileSystem fileSystem)
    {
        return fileSystem switch
        {
            UsbFileSystem.ExFat => "exfat",
            UsbFileSystem.Ntfs => "ntfs",
            UsbFileSystem.Fat32 => "fat32",
            _ => throw new ArgumentOutOfRangeException(nameof(fileSystem)),
        };
    }
}

public sealed record VolumeInfo(
    string DriveLetter,
    string FileSystem,
    string Label,
    long Size);

public sealed record DiskInfo(
    int Number,
    string FriendlyName,
    string SerialNumber,
    string UniqueId,
    string BusType,
    string OperationalStatus,
    long Size,
    bool IsBoot,
    bool IsSystem,
    string PartitionStyle,
    IReadOnlyList<VolumeInfo> Volumes);

public sealed record FormatResult(
    bool Succeeded,
    string Message);

public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);

public sealed record AuditRecord(
    DateTimeOffset TimestampUtc,
    string ComputerName,
    string UserName,
    int DiskNumber,
    string Model,
    string SerialNumber,
    long Size,
    string Result,
    string DriveLetter,
    string Message);
