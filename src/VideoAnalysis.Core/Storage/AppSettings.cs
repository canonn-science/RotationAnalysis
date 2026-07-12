namespace VideoAnalysis.Core.Storage;

public sealed class AppSettings
{
    public string? CommanderName { get; set; }
    public bool MonitorJournals { get; set; }
    public bool OverrideUsername { get; set; }
}
