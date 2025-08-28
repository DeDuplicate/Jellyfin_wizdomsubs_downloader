using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace WizdomSubsDownloader.Providers
{
    // Mirrors OpenSubtitles shape: expose provider to Jellyfin subtitle UI and tasks
    public class WizdomSubtitleProvider : ISubtitleProvider
    {
        private const string WizdomApiBase = "https://wizdom.xyz/api";
        private readonly ILogger<WizdomSubtitleProvider> _logger;
        private readonly HttpClient _http;

        public WizdomSubtitleProvider(ILogger<WizdomSubtitleProvider> logger)
        {
            _logger = logger;
            _http = new HttpClient();
        }

    public string Name => "WizdomSubs";

    public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode, VideoContentType.Movie };

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            _logger.LogInformation("WizdomSubs: Search request for {ContentType}, Path: {Path}, Language: {Lang}", 
                request.ContentType, request.MediaPath, request.Language);

            var imdb = request.GetProviderId(MediaBrowser.Model.Entities.MetadataProvider.Imdb);
            if (string.IsNullOrWhiteSpace(imdb))
            {
                _logger.LogWarning("WizdomSubs: Missing IMDb ID for {ContentType} at {Path}", 
                    request.ContentType, request.MediaPath);
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }

            _logger.LogDebug("WizdomSubs: Found IMDb ID: {ImdbId}", imdb);

            int? season = null, episode = null;
            if (request.ContentType == VideoContentType.Episode)
            {
                season = request.ParentIndexNumber;
                episode = request.IndexNumber;
                
                _logger.LogDebug("WizdomSubs: Episode request - Season: {Season}, Episode: {Episode}", 
                    season, episode);
                
                if (!season.HasValue || !episode.HasValue)
                {
                    _logger.LogWarning("WizdomSubs: Missing season/episode numbers for episode {Path} (S:{Season} E:{Episode})", 
                        request.MediaPath, season?.ToString() ?? "null", episode?.ToString() ?? "null");
                    return Enumerable.Empty<RemoteSubtitleInfo>();
                }
            }

            // Wizdom API always expects season and episode parameters
            // For movies: use season=0&episode=0
            // For TV episodes: use actual season and episode numbers
            string url;
            if (request.ContentType == VideoContentType.Episode && season.HasValue && episode.HasValue)
            {
                url = $"{WizdomApiBase}/search?action=by_id&imdb={imdb}&season={season.Value}&episode={episode.Value}";
            }
            else
            {
                // For movies, use season=0 and episode=0 as per Wizdom API convention
                url = $"{WizdomApiBase}/search?action=by_id&imdb={imdb}&season=0&episode=0";
            }
            
            _logger.LogInformation("WizdomSubs: API URL: {Url}", url);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var json = await _http.GetStringAsync(url, cts.Token).ConfigureAwait(false);
                var subs = System.Text.Json.JsonSerializer.Deserialize<List<WizdomSub>>(json) ?? new List<WizdomSub>();

                _logger.LogDebug("WizdomSubs: Found {Count} subtitles from API", subs.Count);

                // Optional filename similarity sort
                if (!string.IsNullOrWhiteSpace(request.MediaPath))
                {
                    var baseName = Path.GetFileNameWithoutExtension(request.MediaPath);
                    subs = SortByFilename(subs, baseName);
                }

                var lang3 = string.IsNullOrWhiteSpace(request.Language) ? "heb" : request.Language; // Jellyfin uses 3-letter

                var results = subs.Select(s => new RemoteSubtitleInfo
                {
                    Id = $"srt-{lang3}-{s.id}",
                    Name = s.versioname ?? $"Wizdom Subtitle #{s.id}",
                    ProviderName = Name,
                    ThreeLetterISOLanguageName = lang3,
                    Format = "srt",
                    IsHashMatch = false,
                    Comment = $"Wizdom ID: {s.id}"
                }).ToList();

                _logger.LogInformation("WizdomSubs: Returning {Count} results for {ContentType}", 
                    results.Count, request.ContentType);
                
                return results;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "WizdomSubs: HTTP error searching for {Imdb} S{S}E{E}", imdb, season, episode);
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WizdomSubs: Unexpected error searching for {Imdb} S{S}E{E}", imdb, season, episode);
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Missing id", nameof(id));
            }

            // Expect id format: srt-heb-<wizdomId>
            var parts = id.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                throw new FormatException($"Invalid subtitle id format: {id}");
            }

            var format = parts[0];
            var lang = parts[1];
            if (!int.TryParse(parts[2], out var wizId))
            {
                throw new FormatException($"Invalid wizdom id in: {id}");
            }

            var zipUrl = $"{WizdomApiBase}/files/sub/{wizId}";
            try
            {
                await using var stream = await _http.GetStreamAsync(zipUrl, cancellationToken).ConfigureAwait(false);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                          ?? zip.Entries.FirstOrDefault();
                if (entry is null)
                {
                    throw new FileNotFoundException("No SRT found in archive");
                }

                var ms = new MemoryStream();
                await using (var es = entry.Open())
                {
                    await es.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
                }
                ms.Position = 0;

                return new SubtitleResponse
                {
                    Format = format,
                    Language = lang,
                    Stream = ms,
                    IsForced = false,
                    IsHearingImpaired = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wizdom subtitle download failed for {Id}", id);
                throw;
            }
        }

        private static List<WizdomSub> SortByFilename(List<WizdomSub> subs, string baseName)
        {
            int Dist(string a, string b)
            {
                var n = a.Length; var m = b.Length;
                var d = new int[n + 1, m + 1];
                for (int i = 0; i <= n; i++) d[i, 0] = i;
                for (int j = 0; j <= m; j++) d[0, j] = j;
                for (int i = 1; i <= n; i++)
                    for (int j = 1; j <= m; j++)
                    {
                        int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    }
                return d[n, m];
            }

            return subs.OrderBy(s => Dist(baseName, s.versioname ?? string.Empty)).ToList();
        }

        private class WizdomSub
        {
            public int id { get; set; }
            public string? versioname { get; set; }
        }
    }
}
