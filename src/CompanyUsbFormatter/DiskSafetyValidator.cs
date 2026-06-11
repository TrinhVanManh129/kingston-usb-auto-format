namespace CompanyUsbFormatter;

public static class DiskSafetyValidator
{
    public static string? GetRejectionReason(DiskInfo disk)
    {
        if (disk.Number == 0)
        {
            return "Disk 0 is blocked by company safety policy.";
        }

        if (!string.Equals(disk.BusType, "USB", StringComparison.OrdinalIgnoreCase))
        {
            return "Only disks with BusType USB are allowed.";
        }

        if (disk.IsBoot)
        {
            return "A boot disk cannot be formatted.";
        }

        if (disk.IsSystem)
        {
            return "A system disk cannot be formatted.";
        }

        if (!string.Equals(disk.OperationalStatus, "Online", StringComparison.OrdinalIgnoreCase))
        {
            return "The disk must be online.";
        }

        if (disk.Size <= 0)
        {
            return "The disk capacity is invalid.";
        }

        return null;
    }

    public static bool HasSameIdentity(DiskInfo expected, DiskInfo actual)
    {
        return expected.Number == actual.Number
            && expected.Size == actual.Size
            && string.Equals(expected.FriendlyName, actual.FriendlyName, StringComparison.Ordinal)
            && string.Equals(expected.SerialNumber, actual.SerialNumber, StringComparison.Ordinal)
            && string.Equals(expected.UniqueId, actual.UniqueId, StringComparison.Ordinal);
    }
}
