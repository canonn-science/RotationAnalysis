using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Vanara.PInvoke;

namespace VideoAnalysis.App.Infrastructure;

public sealed class QuickVideoMetadata
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public double? FrameRate { get; init; }
    public TimeSpan? Duration { get; init; }
    public BitmapSource? Thumbnail { get; init; }
}

/// <summary>
/// Reads video metadata and a thumbnail the same way Windows Explorer's Properties dialog does -
/// via the Shell Property System and thumbnail cache - instead of opening the file with
/// OpenCV/FFmpeg. This is metadata-only (no frame decode), so it's usually available almost
/// instantly even for large or poorly-indexed capture files where VideoCapture's own open can be
/// slow. Every field is independently best-effort: a missing/unsupported property, or a failure
/// anywhere in here, must never block the real (OpenCV-based) analysis pipeline that follows.
/// </summary>
public static class QuickVideoMetadataReader
{
    // Confirmed against learn.microsoft.com/windows/win32/properties/props-system-video-*
    // and props-system-media-duration - these formatID/propID pairs are stable Win32 constants.
    private static readonly Ole32.PROPERTYKEY PKEY_Video_FrameWidth = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 3);
    private static readonly Ole32.PROPERTYKEY PKEY_Video_FrameHeight = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 4);
    private static readonly Ole32.PROPERTYKEY PKEY_Video_FrameRate = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 6);
    private static readonly Ole32.PROPERTYKEY PKEY_Media_Duration = new(new Guid("64440490-4C8B-11D1-8B70-080036B11A03"), 3);

    public static QuickVideoMetadata Read(string path, int thumbnailSize = 480)
    {
        try
        {
            var item = Shell32.SHCreateItemFromParsingName<Shell32.IShellItem2>(path, null);
            if (item is null)
            {
                return new QuickVideoMetadata();
            }

            // System.Video.FrameRate is expressed in frames per 1000 seconds, not fps.
            var frameRateX1000 = TryGetUInt32(item, PKEY_Video_FrameRate);

            return new QuickVideoMetadata
            {
                Width = (int?)TryGetUInt32(item, PKEY_Video_FrameWidth),
                Height = (int?)TryGetUInt32(item, PKEY_Video_FrameHeight),
                FrameRate = frameRateX1000 is uint fr ? fr / 1000.0 : null,
                // System.Media.Duration is in 100ns units - the same unit as TimeSpan.Ticks.
                Duration = TryGetUInt64(item, PKEY_Media_Duration) is ulong ticks ? TimeSpan.FromTicks((long)ticks) : null,
                Thumbnail = TryGetThumbnail(item, thumbnailSize),
            };
        }
        catch
        {
            return new QuickVideoMetadata();
        }
    }

    private static uint? TryGetUInt32(Shell32.IShellItem2 item, Ole32.PROPERTYKEY key)
    {
        try
        {
            return item.GetUInt32(key);
        }
        catch
        {
            return null;
        }
    }

    private static ulong? TryGetUInt64(Shell32.IShellItem2 item, Ole32.PROPERTYKEY key)
    {
        try
        {
            return item.GetUInt64(key);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? TryGetThumbnail(Shell32.IShellItem2 item, int size)
    {
        try
        {
            var factory = (Shell32.IShellItemImageFactory)item;

            // SIIGBF_INCACHEONLY caps this at a cache lookup - without it, GetImage falls back to
            // generating a thumbnail from the video itself when nothing is cached, which for a
            // large/poorly-indexed file can be exactly as slow as the OpenCV open this is meant to
            // route around.
            var hr = factory.GetImage(
                new SIZE(size, size),
                Shell32.SIIGBF.SIIGBF_RESIZETOFIT | Shell32.SIIGBF.SIIGBF_BIGGERSIZEOK | Shell32.SIIGBF.SIIGBF_INCACHEONLY,
                out var hbitmap);
            if (hr.Failed)
            {
                return null;
            }

            using (hbitmap)
            {
                var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap.DangerousGetHandle(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch
        {
            return null;
        }
    }
}
