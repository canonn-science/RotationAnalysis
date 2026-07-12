using VideoAnalysis.Core.Journal;
using Xunit;

namespace VideoAnalysis.Core.Tests;

public class JournalMonitorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "JournalMonitorTests_" + Guid.NewGuid());

    public JournalMonitorTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string JournalPath(string name) => Path.Combine(_directory, name);

    [Theory]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"Commander\",\"FID\":\"F123\",\"Name\":\"HARDCASE\"}", "HARDCASE")]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"HARDCASE\",\"Ship\":\"Anaconda\"}", "HARDCASE")]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"Fileheader\",\"part\":1}", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    public void TryExtractCommanderName_ParsesExpectedEvents(string line, string? expected)
    {
        Assert.Equal(expected, JournalMonitor.TryExtractCommanderName(line));
    }

    [Theory]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"Location\",\"StarSystem\":\"Shinrarta Dezhra\"}", "Shinrarta Dezhra")]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"JumpDist\":10.5}", "Sol")]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"CarrierJump\",\"StarSystem\":\"Deciat\"}", "Deciat")]
    [InlineData("{\"timestamp\":\"2026-07-01T00:00:00Z\",\"event\":\"Commander\",\"Name\":\"HARDCASE\"}", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    public void TryExtractSystemName_ParsesExpectedEvents(string line, string? expected)
    {
        Assert.Equal(expected, JournalMonitor.TryExtractSystemName(line));
    }

    [Fact]
    public void Start_ReadsCommanderNameFromExistingJournalFile()
    {
        File.WriteAllText(JournalPath("Journal.2601010000.01.log"),
            "{\"event\":\"Fileheader\",\"part\":1}\n" +
            "{\"event\":\"Commander\",\"FID\":\"F1\",\"Name\":\"HARDCASE\"}\n");

        var monitor = new JournalMonitor(_directory);
        string? observed = null;
        using var signal = new ManualResetEventSlim(false);
        monitor.CommanderNameChanged += name => { observed = name; signal.Set(); };

        try
        {
            monitor.Start();
            Assert.True(signal.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal("HARDCASE", observed);
        }
        finally
        {
            monitor.Dispose();
        }
    }

    [Fact]
    public void AppendingToJournalFile_RaisesCommanderNameChanged_ForNewName()
    {
        var path = JournalPath("Journal.2601010000.01.log");
        File.WriteAllText(path, "{\"event\":\"Commander\",\"FID\":\"F1\",\"Name\":\"HARDCASE\"}\n");

        var monitor = new JournalMonitor(_directory);
        using var signal = new ManualResetEventSlim(false);
        monitor.CommanderNameChanged += name =>
        {
            if (name == "NEWNAME")
            {
                signal.Set();
            }
        };

        try
        {
            monitor.Start();

            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("{\"event\":\"Commander\",\"FID\":\"F1\",\"Name\":\"NEWNAME\"}\n");
            }

            Assert.True(signal.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            monitor.Dispose();
        }
    }

    [Fact]
    public void Start_PopulatesLastKnownSystemName_FromExistingJournalFile()
    {
        File.WriteAllText(JournalPath("Journal.2601010000.01.log"),
            "{\"event\":\"Fileheader\",\"part\":1}\n" +
            "{\"event\":\"Location\",\"StarSystem\":\"Shinrarta Dezhra\"}\n" +
            "{\"event\":\"FSDJump\",\"StarSystem\":\"Sol\"}\n");

        var monitor = new JournalMonitor(_directory);
        using var signal = new ManualResetEventSlim(false);
        monitor.SystemLocationChanged += _ => signal.Set();

        try
        {
            monitor.Start();
            Assert.True(signal.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal("Sol", monitor.LastKnownSystemName);
        }
        finally
        {
            monitor.Dispose();
        }
    }

    [Fact]
    public void Start_WhenDirectoryDoesNotExist_DoesNotThrow()
    {
        var monitor = new JournalMonitor(Path.Combine(_directory, "missing"));
        monitor.Start();
        monitor.Dispose();
    }
}
