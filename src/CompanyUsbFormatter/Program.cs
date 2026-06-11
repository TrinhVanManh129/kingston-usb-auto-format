namespace CompanyUsbFormatter;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var console = new SystemUserConsole();
        var pauseOnExit = !args.Contains("--no-pause", StringComparer.OrdinalIgnoreCase);

        console.WriteLine("Company USB Formatter 1.0");
        console.WriteLine("Formats one technician-selected USB disk as exFAT.");
        console.WriteLine("WARNING: Formatting permanently destroys all data on the selected USB.");
        console.WriteLine(string.Empty);

        int exitCode;
        if (!OperatingSystem.IsWindows())
        {
            console.WriteLine("This application supports Windows only.");
            exitCode = UsbFormattingWorkflow.UnexpectedErrorExitCode;
        }
        else
        {
            exitCode = await RunWorkflowAsync(console);
        }

        if (pauseOnExit)
        {
            console.WriteLine(string.Empty);
            console.WriteLine("Press Enter to close.");
            console.ReadLine();
        }

        return exitCode;
    }

    private static async Task<int> RunWorkflowAsync(IUserConsole console)
    {
        AuditLogger? logger = null;
        try
        {
            var runner = new SystemCommandRunner();
            logger = new AuditLogger();
            var workflow = new UsbFormattingWorkflow(
                new PowerShellDiskInventory(runner),
                new DiskPartFormatter(runner, new TemporaryFileStore()),
                console,
                logger);

            return await workflow.RunAsync();
        }
        catch (OperationCanceledException)
        {
            console.WriteLine("Operation cancelled.");
            return UsbFormattingWorkflow.CancelledExitCode;
        }
        catch (Exception exception)
        {
            var message = CleanMessage(exception.Message);
            console.WriteLine($"Unexpected error: {message}");

            if (logger is not null)
            {
                try
                {
                    await logger.LogAsync(
                        new AuditRecord(
                            DateTimeOffset.UtcNow,
                            Environment.MachineName,
                            Environment.UserName,
                            -1,
                            string.Empty,
                            string.Empty,
                            0,
                            "UNEXPECTED_ERROR",
                            string.Empty,
                            message),
                        CancellationToken.None);
                }
                catch
                {
                }
            }

            return UsbFormattingWorkflow.UnexpectedErrorExitCode;
        }
    }

    private static string CleanMessage(string value)
    {
        return string.Join(" ", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}

public sealed class SystemUserConsole : IUserConsole
{
    public void WriteLine(string text)
    {
        Console.WriteLine(text);
    }

    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}
