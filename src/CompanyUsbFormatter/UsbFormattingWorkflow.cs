namespace CompanyUsbFormatter;

public sealed class UsbFormattingWorkflow
{
    public const int SuccessExitCode = 0;
    public const int NoUsbExitCode = 1;
    public const int InvalidSelectionExitCode = 2;
    public const int CancelledExitCode = 3;
    public const int SafetyRejectedExitCode = 4;
    public const int FormatFailedExitCode = 5;
    public const int VerificationFailedExitCode = 6;
    public const int UnexpectedErrorExitCode = 10;

    private readonly IDiskInventory _inventory;
    private readonly IDiskFormatter _formatter;
    private readonly IUserConsole _console;
    private readonly IAuditLogger _logger;

    public UsbFormattingWorkflow(
        IDiskInventory inventory,
        IDiskFormatter formatter,
        IUserConsole console,
        IAuditLogger logger)
    {
        _inventory = inventory;
        _formatter = formatter;
        _console = console;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var disks = (await _inventory.GetDisksAsync(cancellationToken))
            .Where(disk => string.Equals(disk.BusType, "USB", StringComparison.OrdinalIgnoreCase))
            .OrderBy(disk => disk.Number)
            .ToArray();

        if (disks.Length == 0)
        {
            _console.WriteLine("No USB disks were detected.");
            await LogAsync(null, "NO_USB", string.Empty, "No USB disks were detected.", cancellationToken);
            return NoUsbExitCode;
        }

        _console.WriteLine("Detected USB disks:");
        foreach (var disk in disks)
        {
            _console.WriteLine(Describe(disk));
        }

        _console.WriteLine("Enter the disk number to format, or press Enter to cancel:");
        var selectionText = _console.ReadLine();
        if (!int.TryParse(selectionText, out var diskNumber)
            || disks.All(disk => disk.Number != diskNumber))
        {
            _console.WriteLine("The disk selection is invalid.");
            await LogAsync(null, "INVALID_SELECTION", string.Empty, "The disk selection is invalid.", cancellationToken);
            return InvalidSelectionExitCode;
        }

        var selected = await _inventory.GetDiskAsync(diskNumber, cancellationToken);
        var rejection = selected is null ? "The selected disk is no longer present." : DiskSafetyValidator.GetRejectionReason(selected);
        if (rejection is not null)
        {
            _console.WriteLine(rejection);
            await LogAsync(selected, "SAFETY_REJECTED", string.Empty, rejection, cancellationToken);
            return SafetyRejectedExitCode;
        }

        _console.WriteLine(string.Empty);
        _console.WriteLine("WARNING: ALL DATA ON THIS USB DISK WILL BE DESTROYED.");
        _console.WriteLine(Describe(selected!));
        _console.WriteLine($"Enter disk number {diskNumber} again to continue:");
        var confirmation = _console.ReadLine();
        if (!string.Equals(confirmation?.Trim(), diskNumber.ToString(), StringComparison.Ordinal))
        {
            _console.WriteLine("Operation cancelled.");
            await LogAsync(selected, "CANCELLED", string.Empty, "Confirmation disk number did not match.", cancellationToken);
            return CancelledExitCode;
        }

        var confirmed = await _inventory.GetDiskAsync(diskNumber, cancellationToken);
        rejection = confirmed is null ? "The selected disk is no longer present." : DiskSafetyValidator.GetRejectionReason(confirmed);
        if (rejection is not null || !DiskSafetyValidator.HasSameIdentity(selected!, confirmed!))
        {
            var message = rejection ?? "The USB disk identity changed after confirmation.";
            _console.WriteLine(message);
            await LogAsync(confirmed ?? selected, "SAFETY_REJECTED", string.Empty, message, cancellationToken);
            return SafetyRejectedExitCode;
        }

        _console.WriteLine("Formatting USB disk as exFAT...");
        var formatResult = await _formatter.FormatAsExFatAsync(diskNumber, cancellationToken);
        if (!formatResult.Succeeded)
        {
            _console.WriteLine($"Formatting failed: {formatResult.Message}");
            await LogAsync(confirmed, "FORMAT_FAILED", string.Empty, formatResult.Message, cancellationToken);
            return FormatFailedExitCode;
        }

        var verified = await _inventory.GetDiskAsync(diskNumber, cancellationToken);
        var volume = verified?.Volumes.FirstOrDefault(item =>
            string.Equals(item.FileSystem, "exFAT", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Label, "COMPANY-USB", StringComparison.Ordinal));

        if (verified is null
            || !DiskSafetyValidator.HasSameIdentity(confirmed!, verified)
            || volume is null)
        {
            const string message = "The formatted exFAT volume could not be verified.";
            _console.WriteLine(message);
            await LogAsync(verified ?? confirmed, "VERIFICATION_FAILED", string.Empty, message, cancellationToken);
            return VerificationFailedExitCode;
        }

        _console.WriteLine($"Format completed successfully. Drive: {volume.DriveLetter}:");
        await LogAsync(verified, "SUCCESS", volume.DriveLetter, formatResult.Message, cancellationToken);
        return SuccessExitCode;
    }

    private static string Describe(DiskInfo disk)
    {
        var capacityGb = disk.Size / 1_000_000_000d;
        var volumes = disk.Volumes.Count == 0
            ? "none"
            : string.Join(", ", disk.Volumes.Select(volume => $"{volume.DriveLetter}: {volume.FileSystem} {volume.Label}".Trim()));

        return $"Disk {disk.Number} | {disk.FriendlyName} | {capacityGb:F1} GB | Serial: {Display(disk.SerialNumber)} | Volumes: {volumes}";
    }

    private async Task LogAsync(
        DiskInfo? disk,
        string result,
        string driveLetter,
        string message,
        CancellationToken cancellationToken)
    {
        var record = new AuditRecord(
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            Environment.UserName,
            disk?.Number ?? -1,
            disk?.FriendlyName ?? string.Empty,
            disk?.SerialNumber ?? string.Empty,
            disk?.Size ?? 0,
            result,
            driveLetter,
            message);

        await _logger.LogAsync(record, cancellationToken);
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;
    }
}
