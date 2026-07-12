namespace VideoAnalysis.Core.Storage;

/// <summary>Resolves the app's %LocalAppData% storage folder. On first access after the
/// Rotation Analysis Lab -> Video Analysis Lab rename, transparently moves an existing
/// "RotationAnalysisLab" folder to "VideoAnalysisLab" so upgrading users keep their settings,
/// measurement history, and logs instead of the app appearing to have reset.</summary>
public static class StoragePaths
{
    private const string OldFolderName = "RotationAnalysisLab";
    private const string CurrentFolderName = "VideoAnalysisLab";

    public static string Root { get; } = ResolveRoot();

    private static string ResolveRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var currentPath = Path.Combine(appData, CurrentFolderName);
        var oldPath = Path.Combine(appData, OldFolderName);

        if (!Directory.Exists(currentPath) && Directory.Exists(oldPath))
        {
            try
            {
                Directory.Move(oldPath, currentPath);
            }
            catch
            {
                // Best-effort: if the move fails (e.g. a file is locked), fall through and let
                // the app create a fresh folder rather than failing to start.
            }
        }

        return currentPath;
    }
}
