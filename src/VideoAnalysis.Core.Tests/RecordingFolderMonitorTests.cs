using VideoAnalysis.Core.Recording;
using Xunit;

namespace VideoAnalysis.Core.Tests;

public class RecordingFolderMonitorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RecordingFolderMonitorTests_" + Guid.NewGuid());

    public RecordingFolderMonitorTests()
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

    [Theory]
    [InlineData(@"C:\videos\clip.mp4", new[] { "mp4", "mkv" }, true)]
    [InlineData(@"C:\videos\clip.MP4", new[] { ".mp4" }, true)]
    [InlineData(@"C:\videos\clip.avi", new[] { "mp4", "mkv" }, false)]
    [InlineData(@"C:\videos\clip", new[] { "mp4" }, false)]
    public void MatchesExtension_IsCaseInsensitiveAndDotAgnostic(string path, string[] extensions, bool expected)
    {
        Assert.Equal(expected, RecordingFolderMonitor.MatchesExtension(path, extensions));
    }

    [Fact]
    public void SetWatchedFolders_NewFileWithMatchingExtension_RaisesRecordingDetected()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50));
        var detected = new List<string>();
        monitor.RecordingDetected += path => { lock (detected) { detected.Add(path); } };

        monitor.SetWatchedFolders(new[] { _directory });

        var filePath = Path.Combine(_directory, "recording.mp4");
        File.WriteAllText(filePath, "partial");

        WaitUntil(() => detected.Count > 0);

        Assert.Contains(filePath, detected);
    }

    [Fact]
    public void SetWatchedFolders_NewFileWithNonMatchingExtension_DoesNotRaiseRecordingDetected()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50));
        var detected = new List<string>();
        monitor.RecordingDetected += path => { lock (detected) { detected.Add(path); } };

        monitor.SetWatchedFolders(new[] { _directory });

        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "hello");

        Thread.Sleep(200);
        Assert.Empty(detected);
    }

    [Fact]
    public void PollTrackedFiles_FileSizeStabilizes_RaisesRecordingCompleted()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50), stableCyclesRequired: 2);
        var completed = new List<string>();
        monitor.RecordingCompleted += path => { lock (completed) { completed.Add(path); } };

        monitor.SetWatchedFolders(new[] { _directory });

        var filePath = Path.Combine(_directory, "recording.mp4");
        File.WriteAllText(filePath, "some initial bytes");

        WaitUntil(() => completed.Count > 0, timeoutMs: 3000);

        Assert.Contains(filePath, completed);
    }

    [Fact]
    public void TrackExistingFile_DrivesCompletionWithoutPriorCreatedEvent()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50), stableCyclesRequired: 2);
        var completed = new List<string>();
        monitor.RecordingCompleted += path => { lock (completed) { completed.Add(path); } };

        var filePath = Path.Combine(_directory, "resumed.mp4");
        File.WriteAllText(filePath, "already recording when the app relaunched");

        monitor.TrackExistingFile(filePath);

        WaitUntil(() => completed.Count > 0, timeoutMs: 3000);

        Assert.Contains(filePath, completed);
    }

    [Fact]
    public void IsTracking_TrueWhileTracked_FalseAfterCompletion()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50), stableCyclesRequired: 2);
        var completed = new List<string>();
        monitor.RecordingCompleted += path => { lock (completed) { completed.Add(path); } };

        monitor.SetWatchedFolders(new[] { _directory });

        var filePath = Path.Combine(_directory, "recording.mp4");
        File.WriteAllText(filePath, "some initial bytes");

        WaitUntil(() => monitor.IsTracking(filePath));
        Assert.True(monitor.IsTracking(filePath));

        WaitUntil(() => completed.Count > 0, timeoutMs: 3000);
        Assert.False(monitor.IsTracking(filePath));
    }

    [Fact]
    public void IsTracking_FalseForUnknownPath()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50));
        Assert.False(monitor.IsTracking(Path.Combine(_directory, "never-seen.mp4")));
    }

    [Fact]
    public void RenameTrackedFile_CompletionStillFires_UnderTheNewPath()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50), stableCyclesRequired: 2);
        var completed = new List<string>();
        monitor.RecordingCompleted += path => { lock (completed) { completed.Add(path); } };

        monitor.SetWatchedFolders(new[] { _directory });

        var oldPath = Path.Combine(_directory, "recording.mp4");
        File.WriteAllText(oldPath, "some initial bytes");
        WaitUntil(() => monitor.IsTracking(oldPath));

        var newPath = Path.Combine(_directory, "Sol.mp4");
        File.Move(oldPath, newPath);
        monitor.RenameTrackedFile(oldPath, newPath);

        Assert.False(monitor.IsTracking(oldPath));
        Assert.True(monitor.IsTracking(newPath));

        WaitUntil(() => completed.Count > 0, timeoutMs: 3000);

        Assert.Contains(newPath, completed);
        Assert.DoesNotContain(oldPath, completed);
    }

    [Fact]
    public void RenameTrackedFile_UnknownOldPath_IsNoOp()
    {
        using var monitor = new RecordingFolderMonitor(new[] { ".mp4" }, TimeSpan.FromMilliseconds(50));

        monitor.RenameTrackedFile(
            Path.Combine(_directory, "never-tracked.mp4"),
            Path.Combine(_directory, "renamed.mp4"));

        Assert.False(monitor.IsTracking(Path.Combine(_directory, "renamed.mp4")));
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }
    }
}
