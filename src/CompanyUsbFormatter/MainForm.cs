using System.Drawing.Drawing2D;
using System.Security.Principal;

namespace CompanyUsbFormatter;

public sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(18, 31, 52);
    private static readonly Color Blue = Color.FromArgb(35, 99, 235);
    private static readonly Color Red = Color.FromArgb(204, 45, 45);
    private static readonly Color Surface = Color.White;
    private static readonly Color Muted = Color.FromArgb(99, 112, 132);
    private static readonly Color Border = Color.FromArgb(220, 226, 235);

    private readonly IDiskInventory _inventory;
    private readonly IDiskFormatter _formatter;
    private readonly IAuditLogger _logger;
    private readonly DataGridView _diskGrid = new();
    private readonly Button _refreshButton = new();
    private readonly Button _formatButton = new();
    private readonly TextBox _confirmationBox = new();
    private readonly Label _selectedTitle = new();
    private readonly Label _selectedDetails = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progress = new();
    private DiskInfo? _selectedDisk;

    public MainForm()
    {
        var runner = new SystemCommandRunner();
        _inventory = new PowerShellDiskInventory(runner);
        _formatter = new DiskPartFormatter(runner, new TemporaryFileStore());
        _logger = new AuditLogger();

        Text = "Kingston USB Formatter";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 650);
        Size = new Size(980, 720);
        BackColor = Color.FromArgb(243, 246, 251);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildInterface();
        Shown += async (_, _) => await RefreshDisksAsync();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(28),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildDiskPanel(), 0, 1);
        root.Controls.Add(BuildActionPanel(), 0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            AutoSize = true,
            Text = "Kingston USB Formatter",
            Font = new Font("Segoe UI Semibold", 23F),
            ForeColor = Navy,
            Location = new Point(0, 5),
        };
        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Safely prepare removable USB drives as exFAT",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Muted,
            Location = new Point(3, 54),
        };
        var elevated = IsAdministrator();
        var badge = new Label
        {
            AutoSize = true,
            Text = elevated ? "ADMINISTRATOR MODE" : "PREVIEW MODE",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = elevated ? Blue : Muted,
            BackColor = elevated ? Color.FromArgb(226, 235, 255) : Color.FromArgb(232, 236, 242),
            Padding = new Padding(12, 7, 12, 7),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(badge);
        panel.Resize += (_, _) => badge.Location = new Point(panel.ClientSize.Width - badge.Width, 15);
        return panel;
    }

    private Control BuildDiskPanel()
    {
        var panel = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(20),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label
        {
            Text = "Detected USB drives",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F),
            ForeColor = Navy,
            Location = new Point(0, 4),
        });
        ConfigureButton(_refreshButton, "Refresh", Blue);
        _refreshButton.Size = new Size(105, 34);
        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _refreshButton.Click += async (_, _) => await RefreshDisksAsync();
        heading.Controls.Add(_refreshButton);
        heading.Resize += (_, _) => _refreshButton.Location = new Point(heading.ClientSize.Width - _refreshButton.Width, 0);
        layout.Controls.Add(heading, 0, 0);

        _diskGrid.Dock = DockStyle.Fill;
        _diskGrid.BackgroundColor = Surface;
        _diskGrid.BorderStyle = BorderStyle.None;
        _diskGrid.AllowUserToAddRows = false;
        _diskGrid.AllowUserToDeleteRows = false;
        _diskGrid.AllowUserToResizeRows = false;
        _diskGrid.ReadOnly = true;
        _diskGrid.MultiSelect = false;
        _diskGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _diskGrid.AutoGenerateColumns = false;
        _diskGrid.RowHeadersVisible = false;
        _diskGrid.RowTemplate.Height = 42;
        _diskGrid.EnableHeadersVisualStyles = false;
        _diskGrid.ColumnHeadersHeight = 40;
        _diskGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 243, 249);
        _diskGrid.ColumnHeadersDefaultCellStyle.ForeColor = Navy;
        _diskGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
        _diskGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 235, 255);
        _diskGrid.DefaultCellStyle.SelectionForeColor = Navy;
        _diskGrid.GridColor = Border;
        _diskGrid.Columns.Add(Column("Disk", "DiskNumber", 70));
        _diskGrid.Columns.Add(Column("Device", "Device", 280));
        _diskGrid.Columns.Add(Column("Capacity", "Capacity", 105));
        _diskGrid.Columns.Add(Column("Serial", "Serial", 180));
        _diskGrid.Columns.Add(Column("Current volume", "Volume", 190));
        _diskGrid.SelectionChanged += (_, _) => SelectCurrentDisk();
        layout.Controls.Add(_diskGrid, 0, 1);
        return panel;
    }

    private Control BuildActionPanel()
    {
        var panel = CreateCard();
        panel.Margin = new Padding(0, 16, 0, 0);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(22),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        panel.Controls.Add(layout);

        var details = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 25, 0) };
        _selectedTitle.Text = "Select a USB drive";
        _selectedTitle.AutoSize = true;
        _selectedTitle.Font = new Font("Segoe UI Semibold", 14F);
        _selectedTitle.ForeColor = Navy;
        _selectedTitle.Location = new Point(0, 0);
        _selectedDetails.Text = "Choose a device from the table above to review its identity.";
        _selectedDetails.AutoSize = false;
        _selectedDetails.Size = new Size(430, 55);
        _selectedDetails.Font = new Font("Segoe UI", 9.5F);
        _selectedDetails.ForeColor = Muted;
        _selectedDetails.Location = new Point(2, 42);
        var warning = new Label
        {
            AutoSize = false,
            Text = "WARNING: Formatting permanently deletes all data.",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = Color.FromArgb(145, 42, 42),
            BackColor = Color.FromArgb(255, 239, 239),
            Padding = new Padding(14, 11, 14, 10),
            Location = new Point(0, 103),
            Size = new Size(430, 58),
        };
        details.Controls.Add(_selectedTitle);
        details.Controls.Add(_selectedDetails);
        details.Controls.Add(warning);
        layout.Controls.Add(details, 0, 0);

        var action = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 2, 0, 0) };
        action.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Confirm disk number",
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Navy,
            Location = new Point(0, 0),
        });
        action.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Enter the selected Disk number again.",
            ForeColor = Muted,
            Location = new Point(0, 27),
        });
        _confirmationBox.Location = new Point(0, 60);
        _confirmationBox.Size = new Size(315, 32);
        _confirmationBox.Font = new Font("Segoe UI Semibold", 12F);
        _confirmationBox.TextAlign = HorizontalAlignment.Center;
        _confirmationBox.Enabled = false;
        _confirmationBox.TextChanged += (_, _) => UpdateFormatButton();
        action.Controls.Add(_confirmationBox);

        ConfigureButton(_formatButton, "FORMAT USB", Red);
        _formatButton.Location = new Point(0, 110);
        _formatButton.Size = new Size(315, 48);
        _formatButton.Enabled = false;
        _formatButton.Click += async (_, _) => await FormatSelectedDiskAsync();
        action.Controls.Add(_formatButton);
        layout.Controls.Add(action, 1, 0);
        return panel;
    }

    private Control BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Ready";
        _statusLabel.ForeColor = Muted;
        _statusLabel.Location = new Point(4, 16);
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 25;
        _progress.Visible = false;
        _progress.Size = new Size(180, 5);
        _progress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(_statusLabel);
        panel.Controls.Add(_progress);
        panel.Resize += (_, _) => _progress.Location = new Point(panel.ClientSize.Width - _progress.Width, 18);
        return panel;
    }

    private async Task RefreshDisksAsync()
    {
        SetBusy(true, "Scanning Windows storage devices...");
        try
        {
            var disks = (await _inventory.GetDisksAsync(CancellationToken.None))
                .Where(disk => string.Equals(disk.BusType, "USB", StringComparison.OrdinalIgnoreCase))
                .OrderBy(disk => disk.Number)
                .ToArray();

            _selectedDisk = null;
            _confirmationBox.Clear();
            _confirmationBox.Enabled = false;
            _diskGrid.DataSource = disks.Select(disk => new DiskRow(disk)).ToArray();
            _selectedTitle.Text = disks.Length == 0 ? "No USB drive detected" : "Select a USB drive";
            _selectedDetails.Text = disks.Length == 0
                ? "Connect a removable USB drive, then select Refresh."
                : "Choose a device from the table above to review its identity.";
            _statusLabel.Text = disks.Length == 0
                ? "No USB drives detected"
                : $"{disks.Length} USB drive{(disks.Length == 1 ? string.Empty : "s")} detected";

            if (disks.Length > 0 && _diskGrid.Rows.Count > 0)
            {
                _diskGrid.CurrentCell = _diskGrid.Rows[0].Cells[0];
                _diskGrid.Rows[0].Selected = true;
                SelectCurrentDisk();
                BeginInvoke(() => _confirmationBox.Focus());
            }
        }
        catch (Exception exception)
        {
            ShowError("Unable to scan USB drives", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SelectCurrentDisk()
    {
        if (_diskGrid.CurrentRow?.DataBoundItem is not DiskRow row)
        {
            return;
        }

        _selectedDisk = row.Disk;
        _selectedTitle.Text = $"Disk {row.Disk.Number} · {row.Disk.FriendlyName}";
        _selectedDetails.Text =
            $"{row.Capacity}  |  Serial: {Display(row.Disk.SerialNumber)}\r\n" +
            $"Current volume: {row.Volume}";
        _confirmationBox.Clear();
        _confirmationBox.Enabled = true;
        _confirmationBox.Focus();
        UpdateFormatButton();
    }

    private async Task FormatSelectedDiskAsync()
    {
        if (_selectedDisk is null
            || !string.Equals(_confirmationBox.Text.Trim(), _selectedDisk.Number.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        var expected = _selectedDisk;
        var warning = MessageBox.Show(
            $"All data on Disk {expected.Number} ({expected.FriendlyName}) will be permanently deleted.\n\nContinue?",
            "Final confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (warning != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, $"Verifying Disk {expected.Number}...");
        try
        {
            var current = await _inventory.GetDiskAsync(expected.Number, CancellationToken.None);
            var rejection = current is null
                ? "The selected USB drive is no longer connected."
                : DiskSafetyValidator.GetRejectionReason(current);
            if (rejection is not null || !DiskSafetyValidator.HasSameIdentity(expected, current!))
            {
                throw new InvalidOperationException(rejection ?? "The selected USB drive changed after confirmation.");
            }

            _statusLabel.Text = $"Formatting Disk {expected.Number} as exFAT...";
            var result = await _formatter.FormatAsExFatAsync(expected.Number, CancellationToken.None);
            if (!result.Succeeded)
            {
                await LogAsync(current!, "FORMAT_FAILED", string.Empty, result.Message);
                throw new InvalidOperationException(result.Message);
            }

            var verified = await _inventory.GetDiskAsync(expected.Number, CancellationToken.None);
            var volume = verified?.Volumes.FirstOrDefault(item =>
                string.Equals(item.FileSystem, "exFAT", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Label, "COMPANY-USB", StringComparison.Ordinal));
            if (verified is null
                || !DiskSafetyValidator.HasSameIdentity(current!, verified)
                || volume is null)
            {
                const string message = "The exFAT volume could not be verified after formatting.";
                await LogAsync(verified ?? current!, "VERIFICATION_FAILED", string.Empty, message);
                throw new InvalidOperationException(message);
            }

            await LogAsync(verified, "SUCCESS", volume.DriveLetter, result.Message);
            MessageBox.Show(
                $"Disk {expected.Number} was formatted successfully.\nDrive: {volume.DriveLetter}:\\\nFile system: exFAT",
                "Format complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await RefreshDisksAsync();
        }
        catch (Exception exception)
        {
            ShowError("Formatting failed", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LogAsync(DiskInfo disk, string result, string driveLetter, string message)
    {
        await _logger.LogAsync(
            new AuditRecord(
                DateTimeOffset.UtcNow,
                Environment.MachineName,
                Environment.UserName,
                disk.Number,
                disk.FriendlyName,
                disk.SerialNumber,
                disk.Size,
                result,
                driveLetter,
                message),
            CancellationToken.None);
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _diskGrid.Enabled = !busy;
        _refreshButton.Enabled = !busy;
        _confirmationBox.Enabled = !busy && _selectedDisk is not null;
        _progress.Visible = busy;
        UseWaitCursor = busy;
        if (status is not null)
        {
            _statusLabel.Text = status;
        }
        UpdateFormatButton();
    }

    private void UpdateFormatButton()
    {
        var enabled = !UseWaitCursor
            && _selectedDisk is not null
            && string.Equals(
                _confirmationBox.Text.Trim(),
                _selectedDisk.Number.ToString(),
                StringComparison.Ordinal);
        _formatButton.Enabled = enabled;
        _formatButton.BackColor = enabled ? Red : Color.FromArgb(225, 229, 236);
        _formatButton.ForeColor = enabled ? Color.White : Color.FromArgb(112, 121, 135);
    }

    private static Panel CreateCard()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderColor = Border,
            CornerRadius = 12,
        };
        return panel;
    }

    private static DataGridViewTextBoxColumn Column(string title, string property, int width)
    {
        return new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = title == "Device"
                ? DataGridViewAutoSizeColumnMode.Fill
                : DataGridViewAutoSizeColumnMode.None,
        };
    }

    private static void ConfigureButton(Button button, string text, Color color)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Cursor = Cursors.Hand;
    }

    private static void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private sealed class DiskRow
    {
        public DiskRow(DiskInfo disk)
        {
            Disk = disk;
        }

        public DiskInfo Disk { get; }

        public string DiskNumber => $"Disk {Disk.Number}";

        public string Device => Disk.FriendlyName;

        public string Capacity => $"{Disk.Size / 1_000_000_000d:F1} GB";

        public string Serial => Display(Disk.SerialNumber);

        public string Volume => Disk.Volumes.Count == 0
            ? "No volume"
            : string.Join(", ", Disk.Volumes.Select(volume =>
                $"{volume.DriveLetter}: {volume.FileSystem} {volume.Label}".Trim()));
    }
}

internal sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 12;

    public Color BorderColor { get; set; } = Color.LightGray;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rectangle = ClientRectangle;
        rectangle.Width -= 1;
        rectangle.Height -= 1;
        using var path = CreateRoundedRectangle(rectangle, CornerRadius);
        using var pen = new Pen(BorderColor);
        Region = new Region(path);
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
