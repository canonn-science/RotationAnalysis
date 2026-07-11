namespace RotationAnalysis.Core.VideoAnalysis.SlitScan;

/// <summary>How overlapping slit placements combine, when <see cref="SlitScanParameters.ScanSpeedPixelsPerFrame"/>
/// is smaller than <see cref="SlitScanParameters.SlitWidthPixels"/> (each slit then overlaps the
/// next). With no overlap (speed >= width), the blend mode has no visible effect.</summary>
public enum SlitScanBlendMode
{
    /// <summary>Later slits overwrite earlier ones in the overlap region.</summary>
    Normal,

    /// <summary>Per-pixel brightest-of-the-overlap wins.</summary>
    Lighten,

    /// <summary>Per-pixel mean of every slit that covers that column.</summary>
    Average,
}

/// <summary>A quick preset that pre-fills <see cref="SlitScanParameters.SlitAngleDegrees"/> and
/// <see cref="SlitScanParameters.SamplingOrder"/> for a guessed subject-motion direction - a
/// convenience default, not a separate algorithm input (per spec, "if applicable").</summary>
public enum MotionDirectionHint
{
    None,
    LeftToRight,
    RightToLeft,
    TopToBottom,
    BottomToTop,
}

/// <summary>Where within each sampled frame the slit is taken from, and how that position
/// changes as the video plays.</summary>
public enum SlitScanMotionMode
{
    /// <summary>Same position for every frame - the classic time-slice.</summary>
    Static,

    /// <summary>Position interpolates from <see cref="SlitScanParameters.SlitPositionFraction"/>
    /// to <see cref="SlitScanParameters.SweepEndPositionFraction"/> over the trimmed range.</summary>
    Sweep,

    /// <summary>Position orbits a center point, producing a spiral/tunnel look.</summary>
    Rotational,
}

/// <summary>Standard easing curves, shared by Sweep motion and width animation.</summary>
public enum SlitScanEasing
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
}

/// <summary>Which sampled frames end up at which position in the output, independent of where
/// on-screen each sample was taken from.</summary>
public enum SlitScanSamplingOrder
{
    Forward,
    Reverse,

    /// <summary>Plays forward then back, mirrored around the midpoint.</summary>
    PingPong,

    /// <summary>Shuffled (deterministically, via <see cref="SlitScanParameters.RandomSeed"/>).</summary>
    Random,
}

public enum SlitScanRotationDirection
{
    Clockwise,
    CounterClockwise,
}

public enum SlitScanInterpolation
{
    Nearest,
    Linear,
    Cubic,
}

public sealed class SlitScanParameters
{
    // --- Geometry ---

    /// <summary>Degrees. 90 = a vertical slit (the classic left-right scan); 0 = a horizontal
    /// slit (top-bottom scan). Any angle in between rotates the sampled line accordingly.
    /// Ignored in <see cref="SlitScanMotionMode.Rotational"/>, where the angle is driven by the
    /// rotation instead.</summary>
    public double SlitAngleDegrees { get; init; } = 90.0;

    /// <summary>0.0-1.0, the slit's position across the frame (after rotating to the slit's own
    /// axis) - 0.5 samples the center. For <see cref="SlitScanMotionMode.Sweep"/>, this is the
    /// starting position.</summary>
    public double SlitPositionFraction { get; init; } = 0.5;

    public int SlitWidthPixels { get; init; } = 2;

    /// <summary>When true, slit width animates from <see cref="SlitWidthPixels"/> to
    /// <see cref="SlitWidthEndPixels"/> over the trimmed range, eased by <see cref="WidthEasing"/>.</summary>
    public bool WidthIsAnimated { get; init; } = false;

    public int SlitWidthEndPixels { get; init; } = 2;

    public SlitScanEasing WidthEasing { get; init; } = SlitScanEasing.Linear;

    // --- Motion ---

    public SlitScanMotionMode MotionMode { get; init; } = SlitScanMotionMode.Static;

    /// <summary>Only used when <see cref="MotionMode"/> is <see cref="SlitScanMotionMode.Sweep"/>.</summary>
    public double SweepEndPositionFraction { get; init; } = 0.5;

    public SlitScanEasing SweepEasing { get; init; } = SlitScanEasing.Linear;

    /// <summary>0.0-1.0 fraction of frame width/height. Only used in Rotational mode.</summary>
    public double RotationCenterXFraction { get; init; } = 0.5;

    public double RotationCenterYFraction { get; init; } = 0.5;

    /// <summary>Fraction of half the frame's shorter dimension - keeps the orbit within the
    /// frame at 1.0. Only used in Rotational mode.</summary>
    public double RotationRadiusFraction { get; init; } = 0.5;

    /// <summary>Full 360-degree turns completed over the trimmed range. Only used in Rotational mode.</summary>
    public double RotationRevolutions { get; init; } = 1.0;

    public SlitScanRotationDirection RotationDirection { get; init; } = SlitScanRotationDirection.Clockwise;

    // --- Sampling ---

    public SlitScanSamplingOrder SamplingOrder { get; init; } = SlitScanSamplingOrder.Forward;

    /// <summary>Only used when <see cref="SamplingOrder"/> is <see cref="SlitScanSamplingOrder.Random"/>.</summary>
    public int RandomSeed { get; init; } = 0;

    /// <summary>Only every Nth input frame is sampled - 1 samples every frame.</summary>
    public int FrameSamplingInterval { get; init; } = 1;

    /// <summary>0.0-1.0 fraction of the (post-sampling-interval) source range to start from.</summary>
    public double InPointFraction { get; init; } = 0.0;

    /// <summary>0.0-1.0 fraction of the source range to stop at.</summary>
    public double OutPointFraction { get; init; } = 1.0;

    // --- Compositing ---

    /// <summary>Output pixels advanced per sampled frame. Equal to <see cref="SlitWidthPixels"/>
    /// for edge-to-edge coverage with no overlap or gaps; smaller creates overlap (see
    /// <see cref="SlitScanBlendMode"/>), larger leaves gaps.</summary>
    public int ScanSpeedPixelsPerFrame { get; init; } = 2;

    public SlitScanBlendMode BlendMode { get; init; } = SlitScanBlendMode.Normal;

    // --- Output ---

    /// <summary>When true, the composited canvas is resized to <see cref="OutputWidth"/> x
    /// <see cref="OutputHeight"/>, independent of frame count or scan speed. When false, the
    /// output keeps its natural (scan-speed-driven) size.</summary>
    public bool CustomOutputSize { get; init; } = false;

    public int OutputWidth { get; init; } = 1920;

    public int OutputHeight { get; init; } = 1080;

    public SlitScanInterpolation Interpolation { get; init; } = SlitScanInterpolation.Cubic;
}

public sealed class SlitScanResult
{
    public required byte[] ImagePng { get; init; }
    public required int FramesSampled { get; init; }
}
