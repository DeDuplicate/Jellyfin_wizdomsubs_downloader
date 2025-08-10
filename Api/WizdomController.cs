using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WizdomSubsDownloader.Api
{
    [ApiController]
    [Route("WizdomSubs")]
    public class WizdomController : ControllerBase
    {
        private const string WizdomApiBase = "https://wizdom.xyz/api";

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<WizdomController> _logger;

        public WizdomController(ILibraryManager libraryManager, ILogger<WizdomController> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        [HttpPost("Search")]
        public async Task<IActionResult> Search([FromBody] SearchRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ImdbId))
                return BadRequest("ImdbId is required");

            var url = $"{WizdomApiBase}/search?action=by_id&imdb={req.ImdbId}&season={req.Season}&episode={req.Episode}";
            using var http = new HttpClient();
            try
            {
                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var subs = JsonSerializer.Deserialize<List<WizdomSub>>(json) ?? new List<WizdomSub>();

                if (!string.IsNullOrWhiteSpace(req.Filename))
                {
                    subs = SortByFilename(subs, req.Filename);
                }

                return Ok(new SearchResponse { Subtitles = subs.Select(s => new SubInfo { Id = s.id, Name = s.versioname }).ToList() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wizdom search failed for {Imdb} S{Season}E{Episode}", req.ImdbId, req.Season, req.Episode);
                return StatusCode(502, "Wizdom search failed");
            }
        }

        [HttpPost("DownloadToItemFolder")]
        public async Task<IActionResult> DownloadToItemFolder([FromBody] DownloadRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ItemId))
                return BadRequest("ItemId is required");
            if (string.IsNullOrWhiteSpace(req.ImdbId))
                return BadRequest("ImdbId is required");

            var item = _libraryManager.GetItemById(req.ItemId);
            if (item == null)
                return NotFound($"Item {req.ItemId} not found");

            int? season = req.Season;
            int? episode = req.Episode;
            if (item is Episode ep)
            {
                season ??= ep.ParentIndexNumber;
                episode ??= ep.IndexNumber;
            }

            var searchResp = await Search(new SearchRequest
            {
                ImdbId = req.ImdbId,
                Season = season,
                Episode = episode,
                Filename = req.Filename
            }) as ObjectResult;

            if (searchResp == null || searchResp.Value is not SearchResponse sr)
                return StatusCode(502, "Search failed");

            var first = sr.Subtitles.FirstOrDefault();
            if (first == null)
                return NotFound("No subtitles found");

            var srtBytes = await DownloadSrtZipAndExtract(first.Id).ConfigureAwait(false);
            if (srtBytes == null)
                return StatusCode(502, "Failed to download SRT");

            var savedPath = SaveSrtNextToItem(item, srtBytes, req.LanguageCode ?? "he", req.OverwriteExisting);

            _logger.LogInformation("Saved Wizdom SRT for item {ItemId} at {Path}", req.ItemId, savedPath);
            return Ok(new { Path = savedPath });
        }

        private static List<WizdomSub> SortByFilename(List<WizdomSub> subs, string filename)
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

            var name = Path.GetFileNameWithoutExtension(filename);
            return subs.OrderBy(s => Dist(name, s.versioname ?? string.Empty)).ToList();
        }

        private async Task<byte[]?> DownloadSrtZipAndExtract(int subId)
        {
            var url = $"{WizdomApiBase}/files/sub/{subId}";
            using var http = new HttpClient();
            try
            {
                using var stream = await http.GetStreamAsync(url).ConfigureAwait(false);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                         ?? zip.Entries.FirstOrDefault();
                if (entry == null) return null;
                using var es = entry.Open();
                using var ms = new MemoryStream();
                await es.CopyToAsync(ms).ConfigureAwait(false);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download SRT zip {Id}", subId);
                return null;
            }
        }

        private string SaveSrtNextToItem(BaseItem item, byte[] srt, string lang, bool overwrite)
        {
            var itemPath = item.Path;
            var dir = Path.GetDirectoryName(itemPath!)!;
            var stem = Path.GetFileNameWithoutExtension(itemPath);
            var srtPath = Path.Combine(dir, $"{stem}.{lang}.srt");
            if (System.IO.File.Exists(srtPath) && !overwrite)
            {
                int i = 2;
                while (System.IO.File.Exists(Path.Combine(dir, $"{stem}.{lang}.{i}.srt"))) i++;
                srtPath = Path.Combine(dir, $"{stem}.{lang}.{i}.srt");
            }
            System.IO.File.WriteAllBytes(srtPath, srt);
            return srtPath;
        }

        public class SearchRequest
        {
            public string ImdbId { get; set; } = string.Empty;
            public int? Season { get; set; }
            public int? Episode { get; set; }
            public string? Filename { get; set; }
        }

        public class SearchResponse
        {
            public List<SubInfo> Subtitles { get; set; } = new();
        }

        public class SubInfo
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        public class DownloadRequest : SearchRequest
        {
            public string ItemId { get; set; } = string.Empty;
            public string? LanguageCode { get; set; }
            public bool OverwriteExisting { get; set; }
        }

        private class WizdomSub
        {
            public int id { get; set; }
            public string? versioname { get; set; }
        }
    }
}
