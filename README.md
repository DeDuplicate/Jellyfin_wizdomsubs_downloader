# WizdomSubs Downloader (Jellyfin Plugin)

A Jellyfin plugin that downloads Hebrew subtitles from WizdomSubs (https://www.wizdom.xyz/) by IMDB ID and attaches them to your media so Jellyfin can use them.

Status: Preview release 0.1.0. Core subtitle provider implemented; manual search & download working.

## Features (planned)
- Search WizdomSubs by IMDB ID for movies and TV episodes
- Download SRT subtitles (Hebrew) and save to the media folder with proper naming
- Auto-refresh item in Jellyfin to register the new subtitles
- Configurable language preferences and overwrite behavior

## Building
Use .NET 8.0 SDK. Reference Jellyfin 10.10.7 packages.

## Configuration
In Jellyfin Dashboard -> Plugins -> WizdomSubs Downloader.

## Notes
- This initial scaffold includes a placeholder provider class. Implementation will be added next.
