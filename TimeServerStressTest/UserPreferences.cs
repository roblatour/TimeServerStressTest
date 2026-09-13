using System.Text.Json;

namespace TimeServerStressTest;

internal static class UserPreferences
{
    private const int MaximumServerAddresses = 15;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TimeServerStressTest",
        "settings.json");

    public static string LoadServerAddress() => LoadSettings().ServerAddress;

    public static IReadOnlyList<string> LoadServerAddresses() => LoadSettings().ServerAddresses;

    public static int LoadNtpPort() => LoadSettings().NtpPort;

    public static int LoadConcurrentTests() => LoadSettings().ConcurrentTests;

    public static int LoadTestDurationSeconds() => LoadSettings().TestDurationSeconds;

    public static int LoadMaximumRequestsPerSecond() => LoadSettings().MaximumRequestsPerSecond;

    public static StressTestMode LoadStressTestMode() => LoadSettings().StressTestMode;

    public static ReportSettings LoadReportSettings()
    {
        var settings = LoadSettings();
        return new ReportSettings(settings.ReportNotes, settings.CreatePdfReport, settings.CreateCsvReport, settings.ViewPdfReportAfterCreation, settings.ViewCsvReportAfterCreation, settings.PdfReportPath, settings.CsvReportPath);
    }

    public static void Save(string serverAddress, IEnumerable<string> serverAddresses, int ntpPort, int concurrentTests, int testDurationSeconds, int maximumRequestsPerSecond, StressTestMode stressTestMode)
    {
        var settings = LoadSettings();
        SaveSettings(settings with
        {
            ServerAddress = serverAddress,
            ServerAddresses = NormalizeServerAddresses(serverAddresses),
            NtpPort = ntpPort,
            ConcurrentTests = concurrentTests,
            TestDurationSeconds = testDurationSeconds,
            MaximumRequestsPerSecond = maximumRequestsPerSecond,
            StressTestMode = stressTestMode
        });
    }

    public static void SaveReportSettings(ReportSettings reportSettings)
    {
        var settings = LoadSettings();
        SaveSettings(settings with
        {
            ReportNotes = reportSettings.Notes,
            CreatePdfReport = reportSettings.CreatePdfReport,
            CreateCsvReport = reportSettings.CreateCsvReport,
            ViewPdfReportAfterCreation = reportSettings.ViewPdfReportAfterCreation,
            ViewCsvReportAfterCreation = reportSettings.ViewCsvReportAfterCreation,
            PdfReportPath = reportSettings.PdfReportPath,
            CsvReportPath = reportSettings.CsvReportPath
        });
    }

    private static void SaveSettings(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch (IOException)
        {
        }
    }

    private static Settings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return CreateDefaultSettings();
            }

            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            return new Settings(
                settings?.ServerAddress ?? string.Empty,
                NormalizeServerAddresses(settings?.ServerAddresses ?? []),
                Math.Clamp(settings?.NtpPort ?? NtpEndpoint.DefaultPort, 1, 65535),
                Math.Clamp(settings?.ConcurrentTests ?? NtpStressRunner.DefaultConcurrentWorkers, 0, NtpStressRunner.MaximumConcurrentWorkers),
                Math.Clamp(settings?.TestDurationSeconds ?? 15, 1, 300),
                Math.Clamp(settings?.MaximumRequestsPerSecond ?? 1, 1, 20000),
                settings?.ReportNotes ?? string.Empty,
                settings?.CreatePdfReport ?? true,
                settings?.CreateCsvReport ?? true,
                settings?.ViewPdfReportAfterCreation ?? true,
                settings?.ViewCsvReportAfterCreation ?? true,
                GetValidDirectory(settings?.PdfReportPath),
                GetValidDirectory(settings?.CsvReportPath),
                settings?.StressTestMode is StressTestMode.Saturation ? StressTestMode.Saturation : StressTestMode.Paced);
        }
        catch (IOException)
        {
            return CreateDefaultSettings();
        }
        catch (JsonException)
        {
            return CreateDefaultSettings();
        }
    }

    private static string[] NormalizeServerAddresses(IEnumerable<string> serverAddresses)
    {
        return serverAddresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumServerAddresses)
            .ToArray();
    }

    private static string GetValidDirectory(string? value) => Directory.Exists(value) ? value : GetDownloadsDirectory();

    private static string GetDownloadsDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.Exists(Path.Combine(userProfile, "Downloads")) ? Path.Combine(userProfile, "Downloads") : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static Settings CreateDefaultSettings() => new(
        string.Empty,
        [],
        NtpEndpoint.DefaultPort,
        NtpStressRunner.DefaultConcurrentWorkers,
        15,
        1,
        string.Empty,
        true,
        true,
        true,
        true,
        GetDownloadsDirectory(),
        GetDownloadsDirectory());

    internal sealed record ReportSettings(string Notes, bool CreatePdfReport, bool CreateCsvReport, bool ViewPdfReportAfterCreation, bool ViewCsvReportAfterCreation, string PdfReportPath, string CsvReportPath);

    private sealed record Settings(
        string ServerAddress,
        string[] ServerAddresses,
        int NtpPort = NtpEndpoint.DefaultPort,
        int ConcurrentTests = NtpStressRunner.DefaultConcurrentWorkers,
        int TestDurationSeconds = 15,
        int MaximumRequestsPerSecond = 1,
        string ReportNotes = "",
        bool CreatePdfReport = true,
        bool CreateCsvReport = true,
        bool ViewPdfReportAfterCreation = true,
        bool ViewCsvReportAfterCreation = true,
        string PdfReportPath = "",
        string CsvReportPath = "",
        StressTestMode StressTestMode = StressTestMode.Paced);
}
