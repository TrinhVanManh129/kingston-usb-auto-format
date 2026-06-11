namespace CompanyUsbFormatter;

public interface IDiskInventory
{
    Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken);

    Task<DiskInfo?> GetDiskAsync(int diskNumber, CancellationToken cancellationToken);
}

public interface IDiskFormatter
{
    Task<FormatResult> FormatAsync(
        int diskNumber,
        UsbFileSystem fileSystem,
        CancellationToken cancellationToken);
}

public interface IUserConsole
{
    void WriteLine(string text);

    string? ReadLine();
}

public interface IAuditLogger
{
    Task LogAsync(AuditRecord record, CancellationToken cancellationToken);
}

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public interface ITemporaryFileStore
{
    string Create(string content);

    void Delete(string path);
}
