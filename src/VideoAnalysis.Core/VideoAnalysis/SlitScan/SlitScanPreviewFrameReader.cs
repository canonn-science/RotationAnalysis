using OpenCvSharp;

namespace VideoAnalysis.Core.VideoAnalysis.SlitScan;

/// <summary>Grabs a single representative frame from a video, outside the full sampling/compositing
/// pipeline - cheap enough to run once on upload so the UI can show a geometry-guide preview without
/// re-decoding the video on every slider drag.</summary>
public static class SlitScanPreviewFrameReader
{
    public static Task<byte[]?> ReadRepresentativeFrameAsync(string videoPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var cap = new VideoCapture(videoPath);
            if (!cap.IsOpened())
            {
                return null;
            }

            // A frame a little into the video, rather than frame 0, avoids black/fade-in frames
            // some captures start with, while staying cheap (no scan needed - direct seek).
            if (cap.FrameCount > 1)
            {
                cap.Set(VideoCaptureProperties.PosFrames, Math.Min(cap.FrameCount / 10, cap.FrameCount - 1));
            }

            using var frame = new Mat();
            ct.ThrowIfCancellationRequested();
            if (!cap.Read(frame) || frame.Empty())
            {
                return null;
            }

            Cv2.ImEncode(".png", frame, out var bytes);
            return (byte[]?)bytes;
        }, ct);
    }
}
