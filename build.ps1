Write-Host "Building WizdomSubs Downloader plugin..."

dotnet restore .\WizdomSubsDownloader.csproj

$build = dotnet build .\WizdomSubsDownloader.csproj -c Release
if ($LASTEXITCODE -ne 0) {
	Write-Error "Build failed. Aborting packaging."
	exit 1
}

$pluginName = "WizdomSubsDownloader"
$bin = Join-Path $PSScriptRoot "bin/Release/net8.0"
$zip = Join-Path $PSScriptRoot "$pluginName.zip"

if (Test-Path $zip) { Remove-Item $zip }

Write-Host "Creating plugin package..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $bin) {
	[System.IO.Compression.ZipFile]::CreateFromDirectory($bin, $zip)
	Write-Host "Package created: $zip"
} else {
	Write-Error "Build output not found at $bin"
}
