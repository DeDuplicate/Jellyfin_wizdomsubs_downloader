# WizdomSubs Downloader (Jellyfin Plugin)

A Jellyfin plugin that downloads Hebrew subtitles from WizdomSubs (https://www.wizdom.xyz/) by IMDb ID and attaches them to your media so Jellyfin can use them.

**Current Version:** 0.2.0.0
**Status:** Stable - Fully compatible with Jellyfin 10.11.2+

## Features

✅ **Implemented:**
- Search WizdomSubs by IMDb ID for movies and TV episodes
- Download SRT subtitles (Hebrew) and save to the media folder with proper naming
- Automatic series IMDb resolution for TV episodes
- Subtitle ranking by filename similarity (Levenshtein distance)
- Support for both movies and TV series
- Jellyfin 10.11.2+ compatibility

🔄 **Planned:**
- Configurable language preferences and overwrite behavior
- Additional subtitle provider options

## Requirements

- **Jellyfin:** 10.11.0 or higher
- **.NET Runtime:** 9.0

## Installation

### Method 1: Plugin Catalog (Recommended)

Add this catalog URL to Jellyfin:
```
https://raw.githubusercontent.com/DeDuplicate/Jellyfin_wizdomsubs_downloader/refs/heads/main/manifest.json
```

1. Go to **Dashboard → Plugins → Repositories**
2. Add the catalog URL above
3. Go to **Dashboard → Plugins → Catalog**
4. Find "WizdomSubs Downloader" and click Install
5. Restart Jellyfin

### Method 2: Manual Installation

1. Download `WizdomSubsDownloader.zip` from [Latest Release](https://github.com/DeDuplicate/Jellyfin_wizdomsubs_downloader/releases/latest)
2. Extract the DLL to your Jellyfin plugins folder:
   - **Windows:** `C:\ProgramData\Jellyfin\Server\plugins\WizdomSubsDownloader\`
   - **Linux:** `/var/lib/jellyfin/plugins/WizdomSubsDownloader/`
3. Restart Jellyfin server

## Configuration

Access configuration via **Jellyfin Dashboard → Plugins → WizdomSubs Downloader**.

### Series IMDb Mapping (Optional)

For TV series where automatic IMDb resolution fails, you can add manual mappings:

1. Go to plugin configuration
2. Add mappings in format: `Series Name:tt1234567` (one per line)
3. Example:
   ```
   Breaking Bad:tt0903747
   The Office:tt0386676
   ```

## Building from Source

Requirements:
- .NET 9.0 SDK
- Jellyfin 10.11.* packages

Build commands:
```bash
dotnet restore jellyfin_WizdomSubs_downloader.sln
dotnet build jellyfin_WizdomSubs_downloader.sln -c Release
```

Output: `bin/Release/net9.0/WizdomSubsDownloader.dll`

## Changelog

### v0.2.0.0 (2025-11-16)
- **Jellyfin 10.11.2 Compatibility:** Updated to support Jellyfin 10.11.x API
- **Upgraded to .NET 9.0** from .NET 8.0
- **Fixed API Breaking Changes:** Updated ILibraryManager calls (GetItemList → GetItemsResult)
- **Improved HttpClient Management:** Now uses IHttpClientFactory pattern
- **Enhanced TV Episode Support:** Automatic series IMDb resolution now works reliably
- **Bug Fix:** Resolved MissingMethodException error when searching for TV episode subtitles

### v0.1.0.0 (2025-08-10)
- Initial preview release
- Core subtitle provider implemented
- Manual search & download working

## License

This plugin is provided as-is for use with Jellyfin media server.

## Support

- **Issues:** [GitHub Issues](https://github.com/DeDuplicate/Jellyfin_wizdomsubs_downloader/issues)
- **WizdomSubs:** https://www.wizdom.xyz/
