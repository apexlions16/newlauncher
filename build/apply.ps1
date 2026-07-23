param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"

function Replace-Required {
    param(
        [Parameter(Mandatory = $true)][string]$Content,
        [Parameter(Mandatory = $true)][string]$Old,
        [Parameter(Mandatory = $true)][string]$New,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not $Content.Contains($Old)) {
        throw "Patch marker was not found: $Label"
    }

    return $Content.Replace($Old, $New)
}

$sourceRootPath = (Resolve-Path $SourceRoot).Path
$patchRoot = Split-Path -Parent $PSScriptRoot
$mapperSource = Join-Path $patchRoot "patch\FModel\Views\LocresVoiceMapper.cs"
$voFilterSource = Join-Path $patchRoot "patch\FModel\Views\LocresVoiceMapper.VoFilter.cs"
$mapperTarget = Join-Path $sourceRootPath "FModel\Views\LocresVoiceMapper.cs"
$voFilterTarget = Join-Path $sourceRootPath "FModel\Views\LocresVoiceMapper.VoFilter.cs"
$mainWindowPath = Join-Path $sourceRootPath "FModel\MainWindow.xaml"
$menuCommandPath = Join-Path $sourceRootPath "FModel\ViewModels\Commands\MenuCommand.cs"

foreach ($requiredFile in @($mapperSource, $voFilterSource)) {
    if (-not (Test-Path $requiredFile)) {
        throw "Patch source not found: $requiredFile"
    }
}

Copy-Item $mapperSource $mapperTarget -Force
Copy-Item $voFilterSource $voFilterTarget -Force

$mapper = Get-Content $mapperTarget -Raw
$mapper = Replace-Required $mapper `
    'public sealed class LocresVoiceMapper : AdonisWindow' `
    'public sealed partial class LocresVoiceMapper : AdonisWindow' `
    'partial mapper class'

$mapper = Replace-Required $mapper @'
        actions.Children.Add(_deepParse);
'@ @'
        actions.Children.Add(_deepParse);

        actions.Children.Add(new TextBlock
        {
            Text = "Ses türü:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        _voiceMode.Margin = new Thickness(0, 0, 0, 0);
        actions.Children.Add(_voiceMode);
        actions.Children.Add(_requireAudioCandidate);
'@ 'VO controls'

$mapper = Replace-Required $mapper @'
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Namespace",
'@ @'
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "VO Puanı",
            Binding = new Binding(nameof(VoiceMatchRow.VoiceScore)),
            Width = new DataGridLength(75)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "VO Sınıfı",
            Binding = new Binding(nameof(VoiceMatchRow.VoiceClassification)),
            Width = new DataGridLength(105)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "VO Kanıtı",
            Binding = new Binding(nameof(VoiceMatchRow.VoiceEvidence)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Namespace",
'@ 'VO result columns'

$mapper = Replace-Required $mapper @'
            var filter = _pathFilter.Text;
            var deepParse = _deepParse.IsChecked == true;
            var results = await Task.Run(
                () => Scan(locresPath, filter, maxMb * 1024L * 1024L, deepParse, _cancellation.Token),
'@ @'
            var filter = _pathFilter.Text;
            var deepParse = _deepParse.IsChecked == true;
            var voiceMode = SelectedVoiceMode;
            var requireAudioCandidate = _requireAudioCandidate.IsChecked == true;
            var results = await Task.Run(
                () => Scan(locresPath, filter, maxMb * 1024L * 1024L, deepParse, voiceMode,
                    requireAudioCandidate, _cancellation.Token),
'@ 'scan options'

$mapper = Replace-Required $mapper @'
    private List<VoiceMatchRow> Scan(string locresPath, string pathFilter, long maxBytes, bool deepParse,
        CancellationToken cancellationToken)
'@ @'
    private List<VoiceMatchRow> Scan(string locresPath, string pathFilter, long maxBytes, bool deepParse,
        VoiceFilterMode voiceMode, bool requireAudioCandidate, CancellationToken cancellationToken)
'@ 'scan signature'

$mapper = Replace-Required $mapper @'
            var hints = new List<string>();
            var jsonConfirmedKeys = new HashSet<string>(StringComparer.Ordinal);
'@ @'
            var hints = new List<string>();
            var packageContext = VoicePackageContext.Empty;
            var jsonConfirmedKeys = new HashSet<string>(StringComparer.Ordinal);
'@ 'package voice context initialization'

$mapper = Replace-Required $mapper @'
                    hints = ExtractAudioHints(json);
'@ @'
                    hints = ExtractAudioHints(json);
                    packageContext = AnalyzePackageVoiceContext(json);
'@ 'package voice context analysis'

$mapper = Replace-Required $mapper `
    '                var bestAudio = FindBestAudioCandidate(audioIndex, key, hints);' `
    '                var bestAudio = FindBestVoiceAwareAudioCandidate(audioIndex, key, hints, voiceMode);' `
    'voice-aware audio selection'

$mapper = Replace-Required $mapper @'
                foreach (var line in locresLines)
                {
                    var rowId = $"{line.Namespace}\u001f{line.Key}\u001f{packagePath}\u001f{bestAudio?.Path}";
'@ @'
                foreach (var line in locresLines)
                {
                    var assessment = AssessVoice(
                        line.Namespace,
                        line.Key,
                        line.LocalizedText,
                        packagePath,
                        bestAudio?.Path,
                        hints,
                        packageContext,
                        jsonConfirmedKeys.Contains(key),
                        bestAudio?.Score ?? 0);

                    if (requireAudioCandidate && bestAudio is null)
                        continue;
                    if (!PassesVoiceFilter(assessment, voiceMode))
                        continue;

                    var rowId = $"{line.Namespace}\u001f{line.Key}\u001f{packagePath}\u001f{bestAudio?.Path}";
'@ 'VO assessment and filtering'

$mapper = Replace-Required $mapper @'
                        MatchType = string.Join(" + ", matchParts),
                        Confidence = Math.Min(confidence, 99)
'@ @'
                        MatchType = string.Join(" + ", matchParts),
                        VoiceScore = assessment.Score,
                        VoiceClassification = assessment.Classification,
                        VoiceEvidence = assessment.Evidence,
                        Confidence = Math.Min(confidence, 99)
'@ 'VO row fields'

$mapper = Replace-Required $mapper `
    '        csv.AppendLine("Confidence,Namespace,Key,LocalizedText,PackagePath,AudioPath,ReferenceHint,MatchType");' `
    '        csv.AppendLine("Confidence,VoiceScore,VoiceClassification,VoiceEvidence,Namespace,Key,LocalizedText,PackagePath,AudioPath,ReferenceHint,MatchType");' `
    'CSV VO header'

$mapper = Replace-Required $mapper @'
            csv.Append(row.Confidence).Append(',')
                .Append(Csv(row.Namespace)).Append(',')
'@ @'
            csv.Append(row.Confidence).Append(',')
                .Append(row.VoiceScore).Append(',')
                .Append(Csv(row.VoiceClassification)).Append(',')
                .Append(Csv(row.VoiceEvidence)).Append(',')
                .Append(Csv(row.Namespace)).Append(',')
'@ 'CSV VO values'

$mapper = Replace-Required $mapper @'
        _deepParse.IsEnabled = !busy;
        _exportButton.IsEnabled = !busy && _rows.Count > 0;
'@ @'
        _deepParse.IsEnabled = !busy;
        _voiceMode.IsEnabled = !busy;
        _requireAudioCandidate.IsEnabled = !busy;
        _exportButton.IsEnabled = !busy && _rows.Count > 0;
'@ 'VO controls busy state'

$mapper = Replace-Required $mapper @'
        public int Confidence { get; init; }
        public string Namespace { get; init; }
'@ @'
        public int Confidence { get; init; }
        public int VoiceScore { get; init; }
        public string VoiceClassification { get; init; }
        public string VoiceEvidence { get; init; }
        public string Namespace { get; init; }
'@ 'VO result properties'

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

Write-Host "LOCRES Voice Mapper with multi-signal VO filtering applied successfully."
