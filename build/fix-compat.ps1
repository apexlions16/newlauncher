param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"
$sourceRootPath = (Resolve-Path $SourceRoot).Path
$mapperPath = Join-Path $sourceRootPath "FModel\Views\LocresVoiceMapper.cs"
$voFilterPath = Join-Path $sourceRootPath "FModel\Views\LocresVoiceMapper.VoFilter.cs"

foreach ($requiredPath in @($mapperPath, $voFilterPath)) {
    if (-not (Test-Path $requiredPath)) {
        throw "Mapper target not found: $requiredPath"
    }
}

$content = Get-Content $mapperPath -Raw
$content = [regex]::Replace(
    $content,
    '(?<!System\.Windows\.)MessageBoxButton\.',
    'System.Windows.MessageBoxButton.')
$content = [regex]::Replace(
    $content,
    '(?<!System\.Windows\.)MessageBoxImage\.',
    'System.Windows.MessageBoxImage.')

$oldTraversal = @'
            return root.DescendantsAndSelf()
                .OfType<JValue>()
'@
$newTraversal = @'
            var values = root is JContainer container
                ? container.DescendantsAndSelf().OfType<JValue>()
                : root is JValue rootValue
                    ? new[] { rootValue }
                    : Enumerable.Empty<JValue>();

            return values
'@

if (-not $content.Contains($oldTraversal)) {
    throw "Newtonsoft traversal compatibility marker was not found."
}
$content = $content.Replace($oldTraversal, $newTraversal)
Set-Content $mapperPath $content -Encoding utf8NoBOM

$voFilter = Get-Content $voFilterPath -Raw
$engineMarker = '"vehicle", "vehicles", "engine", "engines"'
if (-not $voFilter.Contains($engineMarker)) {
    throw "VO negative-token compatibility marker was not found."
}
$voFilter = $voFilter.Replace($engineMarker, '"vehicle", "vehicles"')
Set-Content $voFilterPath $voFilter -Encoding utf8NoBOM

Write-Host "WPF, Newtonsoft, and VO classifier compatibility fixes applied successfully."
