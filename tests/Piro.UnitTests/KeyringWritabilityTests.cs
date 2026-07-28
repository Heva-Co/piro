using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Piro.Infrastructure.Extensions;

namespace Piro.UnitTests;

/// <summary>
/// Covers the startup check on the Data Protection key ring directory.
/// </summary>
/// <remarks>
/// The failure this guards against: Docker creates a missing volume target as root:root, so a
/// non-root image cannot write its key ring there. Directory.CreateDirectory succeeds anyway on an
/// existing directory, so the app starts clean and only fails on the first request that encrypts
/// something — surfacing as an opaque 500 with the cause buried in a keyring stack trace. This took
/// a live production debugging session to find, which is the whole argument for saying it at startup.
/// </remarks>
public class KeyringWritabilityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"piro-keyring-{Guid.NewGuid():N}");

    public KeyringWritabilityTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        // Restore write permission first, or the cleanup itself fails on the read-only case.
        try
        {
            if (!OperatingSystem.IsWindows() && Directory.Exists(_root))
                File.SetUnixFileMode(_root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static string Register(string keysDirectory)
    {
        var stderr = Console.Error;
        var captured = new StringWriter();
        Console.SetError(captured);

        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=piro;Username=x;Password=x",
                    ["DataProtection:KeysDirectory"] = keysDirectory,
                })
                .Build();

            services.AddInfrastructure(configuration);
            return captured.ToString();
        }
        finally
        {
            Console.SetError(stderr);
        }
    }

    [Fact]
    public void AWritableDirectorySaysNothing()
    {
        var keys = Path.Combine(_root, "keys");

        Register(keys).Should().NotContain("FATAL");
        Directory.Exists(keys).Should().BeTrue();
    }

    [Fact]
    public void AnUnwritableDirectoryIsReportedAtStartup()
    {
        if (OperatingSystem.IsWindows()) return;   // Unix file modes only.

        // Running as root defeats the point: root writes anywhere, so the probe would pass.
        if (Environment.GetEnvironmentVariable("USER") == "root") return;

        var keys = Path.Combine(_root, "readonly");
        Directory.CreateDirectory(keys);
        File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var output = Register(keys);

        output.Should().Contain("FATAL");
        output.Should().Contain("not writable");
        // The message has to carry the fix, not just the diagnosis — that is what makes it useful at
        // 3am to whoever is reading container logs.
        output.Should().Contain("chown");
    }
}
