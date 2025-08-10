using MediaBrowser.Model.Plugins;

namespace WizdomSubsDownloader
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string WizdomBaseUrl { get; set; } = "https://www.wizdom.xyz";
        public string PreferredLanguages { get; set; } = "he,he-IL"; // CSV of language codes
        public bool OverwriteExisting { get; set; } = false;
        public bool UseImdbId { get; set; } = true; // WizardSubs uses IMDB IDs
        public string UserAgent { get; set; } = "WizdomSubsDownloader/1.0";
    }
}
