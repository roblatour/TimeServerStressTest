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

    public static void Save(string serverAddress, IEnumerable<string> serverAddresses, int ntpPort, int concurrentTests, int testDurationSeconds)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Settings(
                serverAddress,
                NormalizeServerAddresses(serverAddresses),
                ntpPort,
                concurrentTests,
                testDurationSeconds)));
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
                Math.Clamp(settings?.TestDurationSeconds ?? 15, 1, 300));
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

    private static Settings CreateDefaultSettings() => new(
        string.Empty,
        [],
        NtpEndpoint.DefaultPort,
        NtpStressRunner.DefaultConcurrentWorkers,
        15);

    private sealed record Settings(
        string ServerAddress,
        string[] ServerAddresses,
        int NtpPort = NtpEndpoint.DefaultPort,
        int ConcurrentTests = NtpStressRunner.DefaultConcurrentWorkers,
        int TestDurationSeconds = 15);
}
