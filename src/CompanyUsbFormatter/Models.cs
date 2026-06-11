namespace CompanyUsbFormatter;

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
