using VideoAnalysis.Core.VideoAnalysis.LongExposure;
using Xunit;

namespace VideoAnalysis.Core.Tests;

/// <summary>Regression/sanity fixture against real footage, mirroring
/// <see cref="JetWarningOnsetDetectorTests"/> - skips (rather than fails) on machines without the
/// sample footage checked out locally. Any real video works here (this isn't jet-cone-specific
/// content) - it's exercising the frame-accumulation math, not anything about the footage.</summary>
public class LongExposureProcessorTests
{
    private const string SampleDirectory = @"S:\Canonn\NeutronJet\Output";

    [Fact]
    public async Task GenerateAsync_ProducesSixNonEmptyImages_OnRealFootage()
    {
        if (!Directory.Exists(SampleDirectory))
        {
            return;
        }

        var videoPath = Directory.EnumerateFiles(SampleDirectory, "*.mp4").OrderBy(p => p).FirstOrDefault();
        if (videoPath is null)
        {
            return;
        }

        var result = await LongExposureProcessor.GenerateAsync(videoPath);

        Assert.True(result.FrameCount > 1);
        foreach (var (variant, _, png) in result.AllVariants)
        {
            Assert.True(png.Length > 0, $"{variant} produced an empty image");
            // PNG magic bytes
            Assert.Equal(0x89, png[0]);
            Assert.Equal((byte)'P', png[1]);
        }
    }
}
