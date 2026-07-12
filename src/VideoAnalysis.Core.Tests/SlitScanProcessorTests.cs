using VideoAnalysis.Core.VideoAnalysis.SlitScan;
using Xunit;

namespace VideoAnalysis.Core.Tests;

/// <summary>Regression/sanity fixture against real footage, mirroring
/// <see cref="LongExposureProcessorTests"/> - skips (rather than fails) on machines without the
/// sample footage checked out locally.</summary>
public class SlitScanProcessorTests
{
    private const string SampleDirectory = @"S:\Canonn\NeutronJet\Output";

    private static string? FindSampleVideo()
        => Directory.Exists(SampleDirectory)
            ? Directory.EnumerateFiles(SampleDirectory, "*.mp4").OrderBy(p => p).FirstOrDefault()
            : null;

    [Fact]
    public async Task GenerateAsync_Normal_ProducesValidPng()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var result = await SlitScanProcessor.GenerateAsync(videoPath, new SlitScanParameters());

        Assert.True(result.FramesSampled > 1);
        AssertValidPng(result.ImagePng);
    }

    [Fact]
    public async Task GenerateAsync_AverageBlendWithOverlap_ProducesValidPng()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        // Overlapping placements (speed < width) is the only case where blend mode matters.
        var parameters = new SlitScanParameters
        {
            SlitWidthPixels = 6,
            ScanSpeedPixelsPerFrame = 2,
            BlendMode = SlitScanBlendMode.Average,
        };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
    }

    [Fact]
    public async Task GenerateAsync_RotatedSlitAngle_ProducesValidPng()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var parameters = new SlitScanParameters { SlitAngleDegrees = 45.0 };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
    }

    [Fact]
    public async Task GenerateAsync_CustomOutputSize_ProducesRequestedDimensions()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var parameters = new SlitScanParameters
        {
            CustomOutputSize = true,
            OutputWidth = 200,
            OutputHeight = 150,
            FrameSamplingInterval = 2,
        };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
        using var decoded = OpenCvSharp.Cv2.ImDecode(result.ImagePng, OpenCvSharp.ImreadModes.Color);
        Assert.Equal(200, decoded.Width);
        Assert.Equal(150, decoded.Height);
    }

    [Fact]
    public async Task GenerateAsync_SweepMotion_ProducesValidPng()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var parameters = new SlitScanParameters
        {
            MotionMode = SlitScanMotionMode.Sweep,
            SlitPositionFraction = 0.1,
            SweepEndPositionFraction = 0.9,
            SweepEasing = SlitScanEasing.EaseInOut,
            FrameSamplingInterval = 2,
        };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
    }

    [Fact]
    public async Task GenerateAsync_RotationalMotion_ProducesValidPng()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var parameters = new SlitScanParameters
        {
            MotionMode = SlitScanMotionMode.Rotational,
            RotationCenterXFraction = 0.5,
            RotationCenterYFraction = 0.5,
            RotationRadiusFraction = 0.6,
            RotationRevolutions = 1.5,
            RotationDirection = SlitScanRotationDirection.CounterClockwise,
            FrameSamplingInterval = 2,
        };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
    }

    [Fact]
    public async Task GenerateAsync_AnimatedWidth_ProducesValidPng()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var parameters = new SlitScanParameters
        {
            WidthIsAnimated = true,
            SlitWidthPixels = 1,
            SlitWidthEndPixels = 15,
            WidthEasing = SlitScanEasing.EaseIn,
            FrameSamplingInterval = 2,
        };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
    }

    [Theory]
    [InlineData(SlitScanSamplingOrder.Reverse)]
    [InlineData(SlitScanSamplingOrder.PingPong)]
    [InlineData(SlitScanSamplingOrder.Random)]
    public async Task GenerateAsync_SamplingOrders_ProduceValidPng(SlitScanSamplingOrder order)
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var parameters = new SlitScanParameters
        {
            SamplingOrder = order,
            RandomSeed = 42,
            FrameSamplingInterval = 3,
        };

        var result = await SlitScanProcessor.GenerateAsync(videoPath, parameters);

        AssertValidPng(result.ImagePng);
    }

    [Fact]
    public async Task GenerateAsync_InOutTrim_SamplesFewerFramesThanFullRange()
    {
        var videoPath = FindSampleVideo();
        if (videoPath is null)
        {
            return;
        }

        var fullResult = await SlitScanProcessor.GenerateAsync(videoPath, new SlitScanParameters { FrameSamplingInterval = 2 });
        var trimmedResult = await SlitScanProcessor.GenerateAsync(videoPath, new SlitScanParameters
        {
            FrameSamplingInterval = 2,
            InPointFraction = 0.25,
            OutPointFraction = 0.5,
        });

        AssertValidPng(trimmedResult.ImagePng);
        Assert.True(trimmedResult.FramesSampled < fullResult.FramesSampled);
    }

    private static void AssertValidPng(byte[] png)
    {
        Assert.True(png.Length > 0);
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
    }
}
