using System.Collections.Generic;
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
        
        // Series name to IMDb ID mapping for cases where Jellyfin provides episode IMDb instead of series IMDb
        // Format: "Series Name:tt1234567" (one per line)
        public string SeriesImdbMappings { get; set; } = "";
        
        public Dictionary<string, string> GetSeriesMappings()
        {
            var mappings = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(SeriesImdbMappings))
                return mappings;
                
            foreach (var line in SeriesImdbMappings.Split('\n'))
            {
                var parts = line.Trim().Split(':');
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    mappings[parts[0].Trim()] = parts[1].Trim();
                }
            }
            return mappings;
        }
    }
}
