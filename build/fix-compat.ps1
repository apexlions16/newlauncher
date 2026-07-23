param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"
$sourceRootPath = (Resolve-Path $SourceRoot).Path
$mapperPath = Join-Path $sourceRootPath "FModel\Views\LocresVoiceMapper.cs"

if (-not (Test-Path $mapperPath)) {
    throw "Mapper target not found: $mapperPath"
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
            return new[] { root }.Concat(root.Descendants())
                .OfType<JValue>()
'@

if (-not $content.Contains($oldTraversal)) {
    throw "Newtonsoft traversal compatibility marker was not found."
}
$content = $content.Replace($oldTraversal, $newTraversal)

Set-Content $mapperPath $content -Encoding utf8NoBOM
Write-Host "WPF and Newtonsoft compatibility fixes applied successfully."
