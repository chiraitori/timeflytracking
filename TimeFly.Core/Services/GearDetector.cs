using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TimeFly.Core.Models;

namespace TimeFly.Core.Services;

[SupportedOSPlatform("windows")]
public sealed partial class GearDetector
{
    private static readonly IReadOnlyDictionary<string, string[]> TabletProcesses = new Dictionary<string, string[]>
    {
        ["XP-Pen"] = ["xppentablet", "pentablet", "pentabletservice", "xppen"],
        ["Wacom"] = ["wacom_tablet", "wacom_tabletuser", "wacomhost", "wacom_touchuser", "tablet"],
        ["Huion"] = ["huiontablet", "huionservice", "huioncore", "tabletcore"],
        ["Gaomon"] = ["gaomontablet", "gaomonservice"],
        ["Veikk"] = ["veikktablet", "vktablet"],
        ["Xencelabs"] = ["xencelabs"],
        ["Ugee"] = ["ugeetablet"],
        ["Windows Ink"] = ["tabtip", "wisptis"]
    };

    private static readonly IReadOnlyDictionary<string, TabletDefinition> SupplementaryModels = new Dictionary<string, TabletDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["28BD_2904"] = new TabletDefinition { Name = "XP-Pen Deco 640", Manufacturer = "XP-Pen", MaxPressure = 16384 },
        ["LDN1302F-A"] = new TabletDefinition { Name = "XP-Pen Deco 640", Manufacturer = "XP-Pen", MaxPressure = 16384 },
        ["LDN1816U"] = new TabletDefinition { Name = "XP-Pen Deco Pro (Gen 2)", Manufacturer = "XP-Pen", MaxPressure = 16384 },
        ["LDN2215Q-A"] = new TabletDefinition { Name = "XP-Pen Artist Pro 16 (Gen 2)", Manufacturer = "XP-Pen", MaxPressure = 16384 },
    };

    private string databasePath;
    private Dictionary<string, TabletDefinition>? definitions;
    private int supportedCount;

    public GearDetector(string? databasePath = null) =>
        this.databasePath = databasePath ?? ResolveActiveDatabasePath();

    private static string ResolveActiveDatabasePath()
    {
        var userCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".timefly", "otd_tablets.json");
        if (File.Exists(userCache)) return userCache;
        return Path.Combine(AppContext.BaseDirectory, "Assets", "otd_tablets.json");
    }

    public async Task<(bool Success, int Count, string Message)> SyncOnlineAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = "https://raw.githubusercontent.com/chiraitori/timeflytracking/master/TimeFly.App/Assets/otd_tablets.json";
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("vid_pid_map", out _))
            {
                return (false, supportedCount, "Invalid database format received from repository.");
            }

            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".timefly");
            Directory.CreateDirectory(appDir);
            var cachePath = Path.Combine(appDir, "otd_tablets.json");
            await File.WriteAllTextAsync(cachePath, json, System.Text.Encoding.UTF8);

            this.databasePath = cachePath;
            definitions = null;
            EnsureDefinitions();
            return (true, supportedCount, $"Successfully synced {supportedCount:N0} tablet definitions from OpenTabletDriver!");
        }
        catch (Exception ex)
        {
            return (false, supportedCount, $"Sync failed: {ex.Message}");
        }
    }

    public GearInfo Scan()
    {
        EnsureDefinitions();
        var driver = FindDriver();
        var hasStylus = false;
        TabletDefinition? match = null;

        foreach (var device in EnumeratePresentDevices())
        {
            if (!IsPhysicalTabletBus(device.InstanceId)) continue;
            var idMatch = VidPidRegex().Match(device.InstanceId);
            if (idMatch.Success)
            {
                var vidPid = $"{idMatch.Groups[1].Value}_{idMatch.Groups[2].Value}";
                if (definitions!.TryGetValue(vidPid, out match) || SupplementaryModels.TryGetValue(vidPid, out match))
                {
                    hasStylus = true;
                    break;
                }
            }
            var label = $"{device.InstanceId} {device.Label}";
            if (label.Contains("pen", StringComparison.OrdinalIgnoreCase) || label.Contains("digitizer", StringComparison.OrdinalIgnoreCase) || label.Contains("tablet", StringComparison.OrdinalIgnoreCase)) hasStylus = true;
        }

        if (match is null && driver.IsRunning && string.Equals(driver.Brand, "XP-Pen", StringComparison.OrdinalIgnoreCase))
        {
            match = ReadXpPenDriverConfig();
            if (match is not null) hasStylus = true;
        }

        if (match is not null)
        {
            return new GearInfo(match.Name ?? "Drawing Tablet", match.Manufacturer ?? "Graphics Tablet", match.MaxPressure ?? 8192, true, true, driver, supportedCount);
        }

        if (driver.IsRunning && !string.Equals(driver.Brand, "None", StringComparison.OrdinalIgnoreCase) && !string.Equals(driver.Brand, "Generic", StringComparison.OrdinalIgnoreCase))
        {
            return new GearInfo($"{driver.Brand} Tablet", driver.Brand, 8192, true, hasStylus, driver, supportedCount);
        }

        return hasStylus
            ? new GearInfo("Stylus Digitizer Tablet", "Generic", 8192, true, true, driver, supportedCount)
            : new GearInfo("No drawing tablet detected", "None", 0, false, false, driver, supportedCount);
    }

    private static TabletDefinition? ReadXpPenDriverConfig()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var xmlPath = Path.Combine(appData, "XPPen", "config.xml");
            if (File.Exists(xmlPath))
            {
                var doc = new System.Xml.XmlDocument();
                doc.Load(xmlPath);
                if (doc.DocumentElement != null)
                {
                    foreach (System.Xml.XmlNode node in doc.DocumentElement.ChildNodes)
                    {
                        var tag = node.Name;
                        if (SupplementaryModels.TryGetValue(tag, out var supp)) return supp;
                        if (tag.Contains("Deco", StringComparison.OrdinalIgnoreCase) || tag.Contains("Artist", StringComparison.OrdinalIgnoreCase) || tag.Contains("Star", StringComparison.OrdinalIgnoreCase))
                        {
                            var cleaned = Regex.Replace(tag, @"([a-zA-Z]+)(\d+)", "$1 $2");
                            return new TabletDefinition { Name = $"XP-Pen {cleaned}", Manufacturer = "XP-Pen", MaxPressure = 16384 };
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static IReadOnlyList<PresentDevice> EnumeratePresentDevices()
    {
        const uint presentAndAllClasses = 0x00000002 | 0x00000004;
        var deviceSet = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, presentAndAllClasses);
        if (deviceSet == new IntPtr(-1)) return [];
        var devices = new List<PresentDevice>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInfoData { Size = Marshal.SizeOf<DeviceInfoData>() };
                if (!SetupDiEnumDeviceInfo(deviceSet, index, ref data)) break;
                var instanceId = new System.Text.StringBuilder(512);
                if (!SetupDiGetDeviceInstanceId(deviceSet, ref data, instanceId, instanceId.Capacity, out _)) continue;
                var label = GetDeviceProperty(deviceSet, ref data, 0x0000000C);
                if (string.IsNullOrWhiteSpace(label)) label = GetDeviceProperty(deviceSet, ref data, 0x00000000);
                devices.Add(new PresentDevice(instanceId.ToString(), label));
            }
        }
        finally { _ = SetupDiDestroyDeviceInfoList(deviceSet); }
        return devices;
    }

    private static string GetDeviceProperty(IntPtr deviceSet, ref DeviceInfoData data, uint property)
    {
        var buffer = new byte[2048];
        if (!SetupDiGetDeviceRegistryProperty(deviceSet, ref data, property, out _, buffer, (uint)buffer.Length, out _)) return string.Empty;
        return System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    private static bool IsPhysicalTabletBus(string instanceId) =>
        instanceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
        instanceId.StartsWith("HID\\VID_", StringComparison.OrdinalIgnoreCase) ||
        instanceId.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase) ||
        instanceId.StartsWith("BTHLEDEVICE\\", StringComparison.OrdinalIgnoreCase);

    private static TabletDriver FindDriver()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                foreach (var group in TabletProcesses)
                {
                    if (group.Value.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)) return new TabletDriver(group.Key, process.ProcessName, process.Id, true);
                }
            }
            catch { }
            finally { process.Dispose(); }
        }
        return new TabletDriver("None", "Not running", 0, false);
    }

    private void EnsureDefinitions()
    {
        if (definitions is not null) return;
        definitions = new Dictionary<string, TabletDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(databasePath));
            if (document.RootElement.TryGetProperty("vid_pid_map", out var map))
            {
                foreach (var item in map.EnumerateObject())
                {
                    definitions[item.Name] = JsonSerializer.Deserialize<TabletDefinition>(item.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TabletDefinition();
                }
            }
            supportedCount = document.RootElement.TryGetProperty("tablet_count", out var count) ? count.GetInt32() : definitions.Count;
        }
        catch { supportedCount = 0; }
    }

    private sealed class TabletDefinition
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }
        [JsonPropertyName("max_pressure")]
        public int? MaxPressure { get; set; }
    }

    private sealed record PresentDevice(string InstanceId, string Label);

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoData
    {
        public int Size;
        public Guid ClassGuid;
        public uint DeviceInstance;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceSet, uint index, ref DeviceInfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInstanceIdW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceSet, ref DeviceInfoData deviceInfoData, System.Text.StringBuilder instanceId, int instanceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceRegistryPropertyW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceSet, ref DeviceInfoData deviceInfoData, uint property, out uint propertyType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceSet);

    [GeneratedRegex(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();
}
