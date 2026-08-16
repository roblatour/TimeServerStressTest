using System.Text.Json;

namespace TimeServerStressTest;

internal static class UserPreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TimeServerStressTest",
        "settings.json");

    public static string LoadServerAddress() => LoadSettings().ServerAddress;

    public static int LoadNtpPort() => LoadSettings().NtpPort;

    public static int LoadConcurrentTests() => LoadSettings().ConcurrentTests;

    public static int LoadTestDurationSeconds() => LoadSettings().TestDurationSeconds;

    public static void Save(string serverAddress, int ntpPort, int concurrentTests, int testDurationSeconds)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Settings(serverAddress, ntpPort, concurrentTests, testDurationSeconds)));
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
                return new Settings(string.Empty, NtpEndpoint.DefaultPort, NtpStressRunner.DefaultConcurrentWorkers, 15);
            }

            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            return new Settings(
                settings?.ServerAddress ?? string.Empty,
                Math.Clamp(settings?.NtpPort ?? NtpEndpoint.DefaultPort, 1, 65535),
                Math.Clamp(settings?.ConcurrentTests ?? NtpStressRunner.DefaultConcurrentWorkers, 1, NtpStressRunner.MaximumConcurrentWorkers),
                Math.Clamp(settings?.TestDurationSeconds ?? 15, 1, 300));
        }
        catch (IOException)
        {
            return new Settings(string.Empty, NtpEndpoint.DefaultPort, NtpStressRunner.DefaultConcurrentWorkers, 15);
        }
        catch (JsonException)
        {
            return new Settings(string.Empty, NtpEndpoint.DefaultPort, NtpStressRunner.DefaultConcurrentWorkers, 15);
        }
    }

    private sealed record Settings(
        string ServerAddress,
        int NtpPort = NtpEndpoint.DefaultPort,
        int ConcurrentTests = NtpStressRunner.DefaultConcurrentWorkers,
        int TestDurationSeconds = 15);
}
