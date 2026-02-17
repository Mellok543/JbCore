namespace WebPanel.Services;

public sealed class ExcelSyncOptions
{
    public const string SectionName = "ExcelSync";

    public bool RunOnStartup { get; init; }
    public string TablesDirectory { get; init; } = "../";
    public string ApplicationsFileName { get; init; } = "applications.xlsx";
    public string RepairsFileName { get; init; } = "repairs.xlsx";
    public string ConsumablesFileName { get; init; } = "consumables.xlsx";
    public string AccessFileName { get; init; } = "access_users.xlsx";
}
