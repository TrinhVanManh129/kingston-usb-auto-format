using CompanyUsbFormatter;

var tests = new (string Name, Action Run)[]
{
    ("accepts a healthy USB disk", AcceptsHealthyUsbDisk),
    ("rejects disk zero", RejectsDiskZero),
    ("rejects non USB disk", RejectsNonUsbDisk),
    ("rejects boot disk", RejectsBootDisk),
    ("rejects system disk", RejectsSystemDisk),
    ("rejects offline disk", RejectsOfflineDisk),
    ("rejects zero capacity disk", RejectsZeroCapacityDisk),
    ("matches unchanged disk identity", MatchesUnchangedIdentity),
    ("detects replaced disk identity", DetectsReplacedIdentity),
    ("detects changed unique disk identity", DetectsChangedUniqueIdentity),
    ("rejects an invalid disk selection", () => RejectsInvalidSelection().GetAwaiter().GetResult()),
    ("rejects a different confirmation disk number", () => RejectsDifferentConfirmationDiskNumber().GetAwaiter().GetResult()),
    ("accepts the repeated disk number confirmation", () => AcceptsRepeatedDiskNumberConfirmation().GetAwaiter().GetResult()),
    ("detects device replacement before formatting", () => DetectsReplacementBeforeFormatting().GetAwaiter().GetResult()),
    ("reports formatter failure", () => ReportsFormatterFailure().GetAwaiter().GetResult()),
    ("reports verification failure", () => ReportsVerificationFailure().GetAwaiter().GetResult()),
    ("completes a verified format", () => CompletesVerifiedFormat().GetAwaiter().GetResult()),
    ("parses PowerShell disk JSON", ParsesPowerShellDiskJson),
    ("generates constrained DiskPart script", GeneratesDiskPartScript),
    ("generates NTFS DiskPart script", GeneratesNtfsDiskPartScript),
    ("generates FAT32 DiskPart script", GeneratesFat32DiskPartScript),
    ("rejects unsupported file system", RejectsUnsupportedFileSystem),
    ("deletes DiskPart temporary script after failure", () => DeletesTemporaryScriptAfterFailure().GetAwaiter().GetResult()),
    ("escapes CSV audit fields", EscapesCsvAuditFields),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
return failures.Count == 0 ? 0 : 1;

static DiskInfo Disk(
    int number = 1,
    string busType = "USB",
    string status = "Online",
    long size = 64_000_000_000,
    bool isBoot = false,
    bool isSystem = false,
    string model = "Kingston DataTraveler",
    string serial = "ABC123",
    string uniqueId = "USB-UNIQUE-ABC123")
{
    return new DiskInfo(
        number,
        model,
        serial,
        uniqueId,
        busType,
        status,
        size,
        isBoot,
        isSystem,
        "MBR",
        Array.Empty<VolumeInfo>());
}

static void AcceptsHealthyUsbDisk()
{
    Equal(null, DiskSafetyValidator.GetRejectionReason(Disk()));
}

static void RejectsDiskZero()
{
    Contains("Disk 0", DiskSafetyValidator.GetRejectionReason(Disk(number: 0)));
}

static void RejectsNonUsbDisk()
{
    Contains("USB", DiskSafetyValidator.GetRejectionReason(Disk(busType: "NVMe")));
}

static void RejectsBootDisk()
{
    Contains("boot", DiskSafetyValidator.GetRejectionReason(Disk(isBoot: true)));
}

static void RejectsSystemDisk()
{
    Contains("system", DiskSafetyValidator.GetRejectionReason(Disk(isSystem: true)));
}

static void RejectsOfflineDisk()
{
    Contains("online", DiskSafetyValidator.GetRejectionReason(Disk(status: "Offline")));
}

static void RejectsZeroCapacityDisk()
{
    Contains("capacity", DiskSafetyValidator.GetRejectionReason(Disk(size: 0)));
}

static void MatchesUnchangedIdentity()
{
    True(DiskSafetyValidator.HasSameIdentity(Disk(), Disk()));
}

static void DetectsReplacedIdentity()
{
    False(DiskSafetyValidator.HasSameIdentity(Disk(), Disk(serial: "REPLACED")));
}

static void DetectsChangedUniqueIdentity()
{
    False(DiskSafetyValidator.HasSameIdentity(Disk(), Disk(uniqueId: "USB-UNIQUE-REPLACED")));
}

static async Task RejectsInvalidSelection()
{
    var formatter = new FakeFormatter();
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }),
        formatter,
        new FakeConsole("99"));

    Equal(UsbFormattingWorkflow.InvalidSelectionExitCode, await workflow.RunAsync());
    False(formatter.WasCalled);
}

static async Task RejectsDifferentConfirmationDiskNumber()
{
    var formatter = new FakeFormatter();
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }, Disk()),
        formatter,
        new FakeConsole("1", "2"));

    Equal(UsbFormattingWorkflow.CancelledExitCode, await workflow.RunAsync());
    False(formatter.WasCalled);
}

static async Task AcceptsRepeatedDiskNumberConfirmation()
{
    var formatted = Disk() with
    {
        PartitionStyle = "MBR",
        Volumes = new[] { new VolumeInfo("F", "exFAT", "COMPANY-USB", 63_000_000_000) },
    };
    var formatter = new FakeFormatter();
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }, Disk(), Disk(), formatted),
        formatter,
        new FakeConsole("1", "1"));

    Equal(UsbFormattingWorkflow.SuccessExitCode, await workflow.RunAsync());
    True(formatter.WasCalled);
}

static async Task DetectsReplacementBeforeFormatting()
{
    var formatter = new FakeFormatter();
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }, Disk(), Disk(serial: "REPLACED")),
        formatter,
        new FakeConsole("1", "1"));

    Equal(UsbFormattingWorkflow.SafetyRejectedExitCode, await workflow.RunAsync());
    False(formatter.WasCalled);
}

static async Task ReportsFormatterFailure()
{
    var formatter = new FakeFormatter(new FormatResult(false, "diskpart failed"));
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }, Disk(), Disk()),
        formatter,
        new FakeConsole("1", "1"));

    Equal(UsbFormattingWorkflow.FormatFailedExitCode, await workflow.RunAsync());
    True(formatter.WasCalled);
}

static async Task ReportsVerificationFailure()
{
    var formatter = new FakeFormatter();
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }, Disk(), Disk(), Disk()),
        formatter,
        new FakeConsole("1", "1"));

    Equal(UsbFormattingWorkflow.VerificationFailedExitCode, await workflow.RunAsync());
}

static async Task CompletesVerifiedFormat()
{
    var formatted = Disk() with
    {
        PartitionStyle = "MBR",
        Volumes = new[] { new VolumeInfo("F", "exFAT", "COMPANY-USB", 63_000_000_000) },
    };
    var formatter = new FakeFormatter();
    var logger = new FakeLogger();
    var workflow = Workflow(
        new FakeInventory(new[] { Disk() }, Disk(), Disk(), formatted),
        formatter,
        new FakeConsole("1", "1"),
        logger);

    Equal(UsbFormattingWorkflow.SuccessExitCode, await workflow.RunAsync());
    True(formatter.WasCalled);
    Equal("SUCCESS", logger.Records.Single().Result);
    Equal("F", logger.Records.Single().DriveLetter);
}

static void ParsesPowerShellDiskJson()
{
    const string json = """
        [
          {
            "Number": 1,
            "FriendlyName": "Kingston DataTraveler",
            "SerialNumber": "ABC123",
            "BusType": "USB",
            "OperationalStatus": "Online",
            "Size": 61907927040,
            "IsBoot": false,
            "IsSystem": false,
            "PartitionStyle": "MBR",
            "Volumes": [
              {
                "DriveLetter": "F",
                "FileSystem": "exFAT",
                "Label": "COMPANY-USB",
                "Size": 61903732736
              }
            ]
          }
        ]
        """;

    var disk = PowerShellDiskInventory.ParseDiskJson(json).Single();
    Equal(1, disk.Number);
    Equal("USB", disk.BusType);
    Equal("F", disk.Volumes.Single().DriveLetter);
    Equal("exFAT", disk.Volumes.Single().FileSystem);
}

static void GeneratesDiskPartScript()
{
    var expected = string.Join(
        Environment.NewLine,
        "select disk 3",
        "attributes disk clear readonly",
        "clean",
        "convert mbr",
        "create partition primary",
        "format fs=exfat quick label=COMPANY-USB",
        "assign",
        "exit",
        string.Empty);

    Equal(expected, DiskPartFormatter.BuildScript(3, UsbFileSystem.ExFat));
}

static void GeneratesNtfsDiskPartScript()
{
    Contains(
        "format fs=ntfs quick label=COMPANY-USB",
        DiskPartFormatter.BuildScript(3, UsbFileSystem.Ntfs));
}

static void GeneratesFat32DiskPartScript()
{
    Contains(
        "format fs=fat32 quick label=COMPANY-USB",
        DiskPartFormatter.BuildScript(3, UsbFileSystem.Fat32));
}

static void RejectsUnsupportedFileSystem()
{
    Throws<ArgumentOutOfRangeException>(() =>
        DiskPartFormatter.BuildScript(3, (UsbFileSystem)999));
}

static async Task DeletesTemporaryScriptAfterFailure()
{
    var runner = new FakeCommandRunner(new CommandResult(1, string.Empty, "failed", false));
    var files = new FakeTemporaryFileStore();
    var formatter = new DiskPartFormatter(runner, files);

    var result = await formatter.FormatAsync(1, UsbFileSystem.ExFat, CancellationToken.None);

    False(result.Succeeded);
    Equal(files.CreatedPath, files.DeletedPath);
    Equal(Path.Combine(Environment.SystemDirectory, "diskpart.exe"), runner.FileName);
}

static void EscapesCsvAuditFields()
{
    Equal("\"value,with \"\"quotes\"\"\"", AuditLogger.EscapeCsv("value,with \"quotes\""));
    Equal("plain", AuditLogger.EscapeCsv("plain"));
}

static UsbFormattingWorkflow Workflow(
    IDiskInventory inventory,
    IDiskFormatter formatter,
    IUserConsole console,
    IAuditLogger? logger = null)
{
    return new UsbFormattingWorkflow(inventory, formatter, console, logger ?? new FakeLogger());
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void Contains(string expected, string? actual)
{
    if (actual is null || !actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected text containing '{expected}', got '{actual}'.");
    }
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void False(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("Expected false.");
    }
}

static void Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

sealed class FakeInventory : IDiskInventory
{
    private readonly IReadOnlyList<DiskInfo> _initial;
    private readonly Queue<DiskInfo> _refreshes;

    public FakeInventory(IReadOnlyList<DiskInfo> initial, params DiskInfo[] refreshes)
    {
        _initial = initial;
        _refreshes = new Queue<DiskInfo>(refreshes);
    }

    public Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_initial);
    }

    public Task<DiskInfo?> GetDiskAsync(int diskNumber, CancellationToken cancellationToken)
    {
        return Task.FromResult<DiskInfo?>(_refreshes.Count == 0 ? null : _refreshes.Dequeue());
    }
}

sealed class FakeFormatter : IDiskFormatter
{
    private readonly FormatResult _result;

    public FakeFormatter(FormatResult? result = null)
    {
        _result = result ?? new FormatResult(true, "formatted");
    }

    public bool WasCalled { get; private set; }

    public Task<FormatResult> FormatAsync(
        int diskNumber,
        UsbFileSystem fileSystem,
        CancellationToken cancellationToken)
    {
        WasCalled = true;
        return Task.FromResult(_result);
    }
}

sealed class FakeConsole : IUserConsole
{
    private readonly Queue<string?> _inputs;

    public FakeConsole(params string?[] inputs)
    {
        _inputs = new Queue<string?>(inputs);
    }

    public List<string> Output { get; } = new();

    public void WriteLine(string text)
    {
        Output.Add(text);
    }

    public string? ReadLine()
    {
        return _inputs.Count == 0 ? null : _inputs.Dequeue();
    }
}

sealed class FakeLogger : IAuditLogger
{
    public List<AuditRecord> Records { get; } = new();

    public Task LogAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }
}

sealed class FakeCommandRunner : ICommandRunner
{
    private readonly CommandResult _result;

    public FakeCommandRunner(CommandResult result)
    {
        _result = result;
    }

    public string? FileName { get; private set; }

    public Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        FileName = fileName;
        return Task.FromResult(_result);
    }
}

sealed class FakeTemporaryFileStore : ITemporaryFileStore
{
    public string CreatedPath { get; } = @"C:\Temp\company-usb-formatter-test.txt";

    public string? DeletedPath { get; private set; }

    public string Create(string content)
    {
        return CreatedPath;
    }

    public void Delete(string path)
    {
        DeletedPath = path;
    }
}
