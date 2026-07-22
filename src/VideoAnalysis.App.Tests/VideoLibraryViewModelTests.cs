using System.IO;
using VideoAnalysis.App.ViewModels;
using VideoAnalysis.Core.Storage;
using Xunit;

namespace VideoAnalysis.App.Tests;

/// <summary>Covers <see cref="VideoLibraryViewModel.EntryDataChanged"/> (GitHub issue #52): tagging
/// or renaming the active entry in place should notify every subscribed tab immediately, not only
/// the next time the entry is (re-)selected.</summary>
public class VideoLibraryViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "VideoLibraryViewModelTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private VideoLibraryViewModel CreateViewModel() => new(new VideoLibraryStore(Path.Combine(_directory, "video_library.json")));

    [Fact]
    public void UpdateSystemBodyRing_RaisesEntryDataChanged_WithTheTaggedEntry()
    {
        var viewModel = CreateViewModel();
        var entry = viewModel.AddFromUpload(new VideoLibraryEntry { FilePath = @"C:\videos\a.mp4" });

        VideoLibraryEntryViewModel? notified = null;
        viewModel.EntryDataChanged += e => notified = e;

        viewModel.UpdateSystemBodyRing(entry, "Sol", 10477373803, 0, 0, 0, "Sol A", "Sol A 1");

        Assert.Same(entry, notified);
        Assert.Equal("Sol", entry.Entry.SystemName);
        Assert.Equal("Sol A", entry.Entry.BodyName);
        Assert.Equal("Sol A 1", entry.Entry.RingName);
    }

    [Fact]
    public void UpdatePath_RaisesEntryDataChanged_WithTheRenamedEntry()
    {
        var viewModel = CreateViewModel();
        var entry = viewModel.AddFromUpload(new VideoLibraryEntry { FilePath = @"C:\videos\a.mp4" });

        VideoLibraryEntryViewModel? notified = null;
        viewModel.EntryDataChanged += e => notified = e;

        viewModel.UpdatePath(entry, @"C:\videos\Sol.mp4");

        Assert.Same(entry, notified);
        Assert.Equal(@"C:\videos\Sol.mp4", entry.FilePath);
    }

    [Fact]
    public void EntrySelected_DoesNotFireEntryDataChanged()
    {
        var viewModel = CreateViewModel();
        var first = viewModel.AddFromUpload(new VideoLibraryEntry { FilePath = @"C:\videos\a.mp4" });
        var second = viewModel.AddFromUpload(new VideoLibraryEntry { FilePath = @"C:\videos\b.mp4" });

        var dataChangedCount = 0;
        viewModel.EntryDataChanged += _ => dataChangedCount++;

        viewModel.Select(first);

        Assert.Equal(0, dataChangedCount);
    }
}
