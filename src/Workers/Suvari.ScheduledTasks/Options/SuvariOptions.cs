using Suvari.ScheduledTasks.Core;

namespace Suvari.ScheduledTasks.Options;

public class SuvariOptions
{
    public const string SectionKey = "SuvariConfig";

    public Brand Brand { get; set; } = Brand.Suvari;

    /// <summary>
    /// true  → C:\Windows\Suvari\token.sek + settings.json üzerinden okur (Windows/legacy)
    /// false → IConfiguration (appsettings / env var) üzerinden okur (Docker/modern)
    /// </summary>
    public bool UseFileBasedSettings { get; set; } = false;
}
