param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"

$sourceRootPath = (Resolve-Path $SourceRoot).Path
$patchRoot = Split-Path -Parent $PSScriptRoot
$mapperSource = Join-Path $patchRoot "patch\FModel\Views\LocresVoiceMapper.cs"
$mapperTarget = Join-Path $sourceRootPath "FModel\Views\LocresVoiceMapper.cs"
$mainWindowPath = Join-Path $sourceRootPath "FModel\MainWindow.xaml"
$menuCommandPath = Join-Path $sourceRootPath "FModel\ViewModels\Commands\MenuCommand.cs"

if (-not (Test-Path $mapperSource)) {
    throw "Mapper source not found: $mapperSource"
}

Copy-Item $mapperSource $mapperTarget -Force

# Resolve type-name collisions between AdonisUI and WPF, and use a token traversal
# that is valid for both JContainer and scalar JToken roots.
$mapper = Get-Content $mapperTarget -Raw
$mapper = $mapper.Replace("MessageBoxButton.", "System.Windows.MessageBoxButton.")
$mapper = $mapper.Replace("MessageBoxImage.", "System.Windows.MessageBoxImage.")
$oldTraversal = @'
            return root.DescendantsAndSelf()
                .OfType<JValue>()
'@
$newTraversal = @'
            IEnumerable<JToken> tokens = root is JContainer container
                ? container.DescendantsAndSelf()
                : new JToken[] { root };

            return tokens
                .OfType<JValue>()
'@
if (-not $mapper.Contains($oldTraversal)) {
    throw "JToken traversal replacement marker was not found."
}
$mapper = $mapper.Replace($oldTraversal, $newTraversal)
Set-Content $mapperTarget $mapper -Encoding utf8NoBOM

$mainWindow = Get-Content $mainWindowPath -Raw
$menuMarker = '                    <MenuItem Header="Image Merger" Command="{Binding MenuCommand}" CommandParameter="Views_ImageMerger">'
$menuInsertion = @'
                    <MenuItem Header="LOCRES Voice Mapper" Command="{Binding MenuCommand}" CommandParameter="Views_LocresVoiceMapper" IsEnabled="{Binding Status.IsReady}" />
                    <MenuItem Header="Image Merger" Command="{Binding MenuCommand}" CommandParameter="Views_ImageMerger">
'@

if (-not $mainWindow.Contains($menuMarker)) {
    throw "MainWindow menu insertion marker was not found."
}
$mainWindow = $mainWindow.Replace($menuMarker, $menuInsertion)
Set-Content $mainWindowPath $mainWindow -Encoding utf8NoBOM

$menuCommand = Get-Content $menuCommandPath -Raw
$commandMarker = @'
            case "Views_ImageMerger":
                Helper.OpenWindow<AdonisWindow>("Image Merger", () => new ImageMerger().Show());
                break;
'@
$commandInsertion = @'
            case "Views_LocresVoiceMapper":
                Helper.OpenWindow<AdonisWindow>("LOCRES Voice Mapper", () => new LocresVoiceMapper().Show());
                break;
            case "Views_ImageMerger":
                Helper.OpenWindow<AdonisWindow>("Image Merger", () => new ImageMerger().Show());
                break;
'@

if (-not $menuCommand.Contains($commandMarker)) {
    throw "MenuCommand insertion marker was not found."
}
$menuCommand = $menuCommand.Replace($commandMarker, $commandInsertion)
Set-Content $menuCommandPath $menuCommand -Encoding utf8NoBOM

Write-Host "LOCRES Voice Mapper patch applied successfully."
