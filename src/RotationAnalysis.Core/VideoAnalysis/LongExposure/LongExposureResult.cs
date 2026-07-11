namespace RotationAnalysis.Core.VideoAnalysis.LongExposure;

public enum LongExposureVariant
{
    Average,
    Maximum,
    Minimum,
    MaxMinusMin,
    MotionVariance,
    MotionBlur,
}

public sealed class LongExposureResult
{
    public required byte[] AveragePng { get; init; }
    public required byte[] MaximumPng { get; init; }
    public required byte[] MinimumPng { get; init; }
    public required byte[] MaxMinusMinPng { get; init; }
    public required byte[] MotionVariancePng { get; init; }
    public required byte[] MotionBlurPng { get; init; }
    public required int FrameCount { get; init; }

    /// <summary>All six generated variants paired with a display name, in the order the results
    /// view should show them.</summary>
    public IReadOnlyList<(LongExposureVariant Variant, string DisplayName, byte[] Png)> AllVariants => new[]
    {
        (LongExposureVariant.Average, "Average", AveragePng),
        (LongExposureVariant.Maximum, "Maximum", MaximumPng),
        (LongExposureVariant.Minimum, "Minimum", MinimumPng),
        (LongExposureVariant.MaxMinusMin, "Max Minus Min", MaxMinusMinPng),
        (LongExposureVariant.MotionVariance, "Motion Variance", MotionVariancePng),
        (LongExposureVariant.MotionBlur, "Motion Blur", MotionBlurPng),
    };
}
