using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AdonisUI.Controls;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Localization;
using FModel.Extensions;
using FModel.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FModel.Views;

public sealed class LocresVoiceMapper : AdonisWindow
{
    private readonly ComboBox _locresSelector = new();
    private readonly TextBox _pathFilter = new();
    private readonly TextBox _maxFileSize = new();
    private readonly CheckBox _deepParse = new();
    private readonly Button _scanButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _exportButton = new();
    private readonly Button _openPackageButton = new();
    private readonly Button _openAudioButton = new();
    private readonly ProgressBar _progress = new();
    private readonly TextBlock _status = new();
    private readonly DataGrid _resultsGrid = new();
    private readonly ObservableCollection<VoiceMatchRow> _rows = [];
    private CancellationTokenSource _cancellation;

    public LocresVoiceMapper()
    {
        Title = "LOCRES Voice Mapper";
        Width = 1500;
        Height = 850;
        MinWidth = 1050;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildLayout();
        LoadLocresFiles();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "LOCRES anahtarlarını Unreal paketlerinde arar ve olası ses dosyalarıyla eşleştirir.",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var settings = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

        settings.Children.Add(new TextBlock
        {
            Text = "LOCRES:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        _locresSelector.MinWidth = 380;
        _locresSelector.Margin = new Thickness(0, 0, 14, 0);
        Grid.SetColumn(_locresSelector, 1);
        settings.Children.Add(_locresSelector);

        var filterLabel = new TextBlock
        {
            Text = "Yol filtresi:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(filterLabel, 2);
        settings.Children.Add(filterLabel);

        _pathFilter.ToolTip = "İsteğe bağlı. Birden fazla terimi | ile ayırın. Örnek: Dialogue|VO|Quest";
        _pathFilter.Margin = new Thickness(0, 0, 14, 0);
        Grid.SetColumn(_pathFilter, 3);
        settings.Children.Add(_pathFilter);

        var sizeLabel = new TextBlock
        {
            Text = "Azami MB:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(sizeLabel, 4);
        settings.Children.Add(sizeLabel);

        _maxFileSize.Text = "96";
        _maxFileSize.ToolTip = "Ham ön taramada tek dosya için azami boyut. Çok büyük paketleri atlamak belleği korur.";
        Grid.SetColumn(_maxFileSize, 5);
        settings.Children.Add(_maxFileSize);

        Grid.SetRow(settings, 1);
        root.Children.Add(settings);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };

        _deepParse.Content = "Eşleşen paketleri ayrıştır ve referansları çöz";
        _deepParse.IsChecked = true;
        _deepParse.VerticalAlignment = VerticalAlignment.Center;
        _deepParse.Margin = new Thickness(0, 0, 16, 0);
        actions.Children.Add(_deepParse);

        _scanButton.Content = "Taramayı Başlat";
        _scanButton.Padding = new Thickness(14, 5, 14, 5);
        _scanButton.Margin = new Thickness(0, 0, 8, 0);
        _scanButton.Click += ScanButton_Click;
        actions.Children.Add(_scanButton);

        _cancelButton.Content = "İptal";
        _cancelButton.Padding = new Thickness(14, 5, 14, 5);
        _cancelButton.Margin = new Thickness(0, 0, 8, 0);
        _cancelButton.IsEnabled = false;
        _cancelButton.Click += (_, _) => _cancellation?.Cancel();
        actions.Children.Add(_cancelButton);

        _exportButton.Content = "CSV Dışa Aktar";
        _exportButton.Padding = new Thickness(14, 5, 14, 5);
        _exportButton.Margin = new Thickness(0, 0, 8, 0);
        _exportButton.IsEnabled = false;
        _exportButton.Click += ExportButton_Click;
        actions.Children.Add(_exportButton);

        _openPackageButton.Content = "Paketi FModel'de Aç";
        _openPackageButton.Padding = new Thickness(14, 5, 14, 5);
        _openPackageButton.Margin = new Thickness(0, 0, 8, 0);
        _openPackageButton.IsEnabled = false;
        _openPackageButton.Click += (_, _) => OpenSelectedPath(false);
        actions.Children.Add(_openPackageButton);

        _openAudioButton.Content = "Sesi FModel'de Aç";
        _openAudioButton.Padding = new Thickness(14, 5, 14, 5);
        _openAudioButton.IsEnabled = false;
        _openAudioButton.Click += (_, _) => OpenSelectedPath(true);
        actions.Children.Add(_openAudioButton);

        Grid.SetRow(actions, 2);
        root.Children.Add(actions);

        ConfigureResultsGrid();
        Grid.SetRow(_resultsGrid, 3);
        root.Children.Add(_resultsGrid);

        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        _status.Text = "Hazır.";
        _status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_status);

        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Height = 18;
        Grid.SetColumn(_progress, 1);
        footer.Children.Add(_progress);

        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        return root;
    }

    private void ConfigureResultsGrid()
    {
        _resultsGrid.AutoGenerateColumns = false;
        _resultsGrid.IsReadOnly = true;
        _resultsGrid.SelectionMode = DataGridSelectionMode.Single;
        _resultsGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        _resultsGrid.ItemsSource = _rows;
        _resultsGrid.SelectionChanged += (_, _) => UpdateSelectionButtons();
        _resultsGrid.MouseDoubleClick += (_, _) => OpenSelectedPath(false);

        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Güven",
            Binding = new Binding(nameof(VoiceMatchRow.Confidence)),
            Width = new DataGridLength(70)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Namespace",
            Binding = new Binding(nameof(VoiceMatchRow.Namespace)),
            Width = new DataGridLength(160)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Key",
            Binding = new Binding(nameof(VoiceMatchRow.Key)),
            Width = new DataGridLength(230)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Yerelleştirilmiş Metin",
            Binding = new Binding(nameof(VoiceMatchRow.LocalizedText)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Paket",
            Binding = new Binding(nameof(VoiceMatchRow.PackagePath)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Ses Adayı",
            Binding = new Binding(nameof(VoiceMatchRow.AudioPath)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Referans İpucu",
            Binding = new Binding(nameof(VoiceMatchRow.ReferenceHint)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        _resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Eşleşme",
            Binding = new Binding(nameof(VoiceMatchRow.MatchType)),
            Width = new DataGridLength(170)
        });
    }

    private void LoadLocresFiles()
    {
        var provider = ApplicationService.ApplicationView.CUE4Parse?.Provider;
        if (provider is null)
        {
            _status.Text = "FModel sağlayıcısı hazır değil.";
            _scanButton.IsEnabled = false;
            return;
        }

        var paths = provider.Files.Values
            .Where(file => file.Extension.Equals("locres", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _locresSelector.ItemsSource = paths;
        if (paths.Count > 0)
        {
            _locresSelector.SelectedIndex = 0;
            _status.Text = $"{paths.Count:N0} LOCRES bulundu.";
        }
        else
        {
            _scanButton.IsEnabled = false;
            _status.Text = "Yüklü arşivlerde LOCRES bulunamadı.";
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_locresSelector.SelectedItem is not string locresPath)
        {
            System.Windows.MessageBox.Show("Önce bir LOCRES seçin.", "LOCRES Voice Mapper",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(_maxFileSize.Text, out var maxMb) || maxMb < 1 || maxMb > 4096)
        {
            System.Windows.MessageBox.Show("Azami dosya boyutu 1 ile 4096 MB arasında olmalıdır.",
                "LOCRES Voice Mapper", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cancellation = new CancellationTokenSource();
        SetBusy(true);
        _rows.Clear();
        _progress.Value = 0;

        try
        {
            var filter = _pathFilter.Text;
            var deepParse = _deepParse.IsChecked == true;
            var results = await Task.Run(
                () => Scan(locresPath, filter, maxMb * 1024L * 1024L, deepParse, _cancellation.Token),
                _cancellation.Token);

            foreach (var row in results.OrderByDescending(x => x.Confidence)
                         .ThenBy(x => x.Namespace, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                _rows.Add(row);
            }

            _status.Text = $"Tamamlandı: {_rows.Count:N0} eşleşme.";
            _progress.Value = 100;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Tarama iptal edildi.";
        }
        catch (Exception ex)
        {
            _status.Text = "Tarama başarısız.";
            System.Windows.MessageBox.Show(ex.ToString(), "LOCRES Voice Mapper - Hata",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private List<VoiceMatchRow> Scan(string locresPath, string pathFilter, long maxBytes, bool deepParse,
        CancellationToken cancellationToken)
    {
        var provider = ApplicationService.ApplicationView.CUE4Parse.Provider;
        if (!provider.Files.TryGetValue(locresPath, out var locresFile))
            throw new FileNotFoundException("Seçili LOCRES sağlayıcıda bulunamadı.", locresPath);

        List<LocresLine> lines;
        using (var archive = locresFile.CreateReader())
        {
            var locres = new FTextLocalizationResource(archive);
            lines = locres.Entries
                .SelectMany(namespaceEntry => namespaceEntry.Value.Select(keyEntry =>
                    new LocresLine(namespaceEntry.Key.Str, keyEntry.Key.Str, keyEntry.Value.LocalizedString)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToList();
        }

        var keyMap = lines
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var knownKeys = keyMap.Keys.Where(x => x.Length >= 4).ToHashSet(StringComparer.Ordinal);

        var filterTerms = SplitFilter(pathFilter);
        var scanFiles = provider.Files.Values
            .Where(IsScannablePackagePart)
            .Where(file => file.Size <= maxBytes)
            .Where(file => filterTerms.Count == 0 ||
                           filterTerms.Any(term => file.Path.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var audioIndex = BuildAudioIndex(provider.Files.Values);
        var results = new List<VoiceMatchRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        UpdateProgress(0, $"LOCRES: {lines.Count:N0} satır. Taranacak dosya: {scanFiles.Count:N0}.");

        for (var i = 0; i < scanFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scannedFile = scanFiles[i];

            byte[] data;
            try
            {
                data = provider.SaveAsset(scannedFile);
            }
            catch
            {
                continue;
            }

            var foundKeys = FindKnownKeys(data, knownKeys);
            if (foundKeys.Count == 0)
            {
                ReportLoopProgress(i, scanFiles.Count, scannedFile.Path);
                continue;
            }

            var packageFile = ResolvePackageFile(provider.Files, scannedFile);
            var packagePath = packageFile?.Path ?? scannedFile.Path;
            var hints = new List<string>();
            var jsonConfirmedKeys = new HashSet<string>(StringComparer.Ordinal);

            if (deepParse && packageFile is not null)
            {
                try
                {
                    var displayData = provider.GetLoadPackageResult(packageFile).GetDisplayData(true);
                    var json = JsonConvert.SerializeObject(displayData, Formatting.None);

                    foreach (var key in foundKeys)
                    {
                        if (json.Contains(key, StringComparison.Ordinal))
                            jsonConfirmedKeys.Add(key);
                    }

                    hints = ExtractAudioHints(json);
                }
                catch
                {
                    // Cooked/unversioned packages can fail without mappings. Raw-key results remain useful.
                }
            }

            foreach (var key in foundKeys)
            {
                if (!keyMap.TryGetValue(key, out var locresLines))
                    continue;

                var bestAudio = FindBestAudioCandidate(audioIndex, key, hints);
                foreach (var line in locresLines)
                {
                    var rowId = $"{line.Namespace}\u001f{line.Key}\u001f{packagePath}\u001f{bestAudio?.Path}";
                    if (!seen.Add(rowId))
                        continue;

                    var confidence = 55;
                    var matchParts = new List<string> { "Ham anahtar" };
                    if (jsonConfirmedKeys.Contains(key))
                    {
                        confidence += 15;
                        matchParts.Add("Ayrıştırılmış paket");
                    }

                    if (hints.Count > 0)
                    {
                        confidence += 10;
                        matchParts.Add("Ses referansı");
                    }

                    if (bestAudio is not null)
                    {
                        confidence += bestAudio.Score >= 60 ? 19 : bestAudio.Score >= 30 ? 10 : 5;
                        matchParts.Add("Ses adayı");
                    }

                    results.Add(new VoiceMatchRow
                    {
                        Namespace = line.Namespace,
                        Key = line.Key,
                        LocalizedText = line.LocalizedText,
                        PackagePath = packagePath,
                        AudioPath = bestAudio?.Path ?? string.Empty,
                        ReferenceHint = string.Join(" | ", hints.Take(4)),
                        MatchType = string.Join(" + ", matchParts),
                        Confidence = Math.Min(confidence, 99)
                    });
                }
            }

            ReportLoopProgress(i, scanFiles.Count, scannedFile.Path);
        }

        return results;
    }

    private void ReportLoopProgress(int index, int total, string path)
    {
        if (index % 20 != 0 && index + 1 != total)
            return;

        var percent = total == 0 ? 100 : (index + 1) * 100d / total;
        UpdateProgress(percent, $"Taranıyor {index + 1:N0}/{total:N0}: {path}");
    }

    private void UpdateProgress(double percent, string text)
    {
        Dispatcher.Invoke(() =>
        {
            _progress.Value = Math.Clamp(percent, 0, 100);
            _status.Text = text;
        });
    }

    private static List<string> SplitFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return [];

        return filter.Split(['|', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsScannablePackagePart(GameFile file)
    {
        return file.Extension.Equals("uasset", StringComparison.OrdinalIgnoreCase) ||
               file.Extension.Equals("umap", StringComparison.OrdinalIgnoreCase) ||
               file.Extension.Equals("uexp", StringComparison.OrdinalIgnoreCase);
    }

    private static GameFile ResolvePackageFile(IReadOnlyDictionary<string, GameFile> files, GameFile scannedFile)
    {
        if (scannedFile.Extension.Equals("uasset", StringComparison.OrdinalIgnoreCase) ||
            scannedFile.Extension.Equals("umap", StringComparison.OrdinalIgnoreCase))
            return scannedFile;

        var basePath = scannedFile.Path[..^(scannedFile.Extension.Length + 1)];
        if (files.TryGetValue(basePath + ".uasset", out var uasset))
            return uasset;
        if (files.TryGetValue(basePath + ".umap", out var umap))
            return umap;

        return null;
    }

    private static HashSet<string> FindKnownKeys(byte[] data, HashSet<string> knownKeys)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        ScanAsciiRuns(data, knownKeys, found);
        ScanUtf16Runs(data, knownKeys, found);
        return found;
    }

    private static void ScanAsciiRuns(byte[] data, HashSet<string> knownKeys, HashSet<string> found)
    {
        var start = -1;
        for (var i = 0; i <= data.Length; i++)
        {
            var printable = i < data.Length && data[i] is >= 0x20 and <= 0x7E;
            if (printable)
            {
                if (start < 0) start = i;
                continue;
            }

            if (start >= 0)
            {
                var length = i - start;
                if (length is >= 4 and <= 1024)
                {
                    var value = Encoding.ASCII.GetString(data, start, length);
                    CheckCandidate(value, knownKeys, found);
                }

                start = -1;
            }
        }
    }

    private static void ScanUtf16Runs(byte[] data, HashSet<string> knownKeys, HashSet<string> found)
    {
        for (var offset = 0; offset < 2; offset++)
        {
            var builder = new StringBuilder();
            for (var i = offset; i + 1 < data.Length; i += 2)
            {
                var low = data[i];
                var high = data[i + 1];
                if (high == 0 && low is >= 0x20 and <= 0x7E)
                {
                    if (builder.Length < 1024)
                        builder.Append((char)low);
                    continue;
                }

                if (builder.Length >= 4)
                    CheckCandidate(builder.ToString(), knownKeys, found);
                builder.Clear();
            }

            if (builder.Length >= 4)
                CheckCandidate(builder.ToString(), knownKeys, found);
        }
    }

    private static void CheckCandidate(string value, HashSet<string> knownKeys, HashSet<string> found)
    {
        if (knownKeys.Contains(value))
            found.Add(value);

        foreach (var segment in value.Split(['/', '\\', '.', ':', ';', ',', '"', '\'', '[', ']', '(', ')', '{', '}', '<', '>', '='],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment.Length >= 4 && knownKeys.Contains(segment))
                found.Add(segment);
        }
    }

    private static List<string> ExtractAudioHints(string json)
    {
        try
        {
            var root = JToken.Parse(json);
            return root.DescendantsAndSelf()
                .OfType<JValue>()
                .Where(value => value.Type == JTokenType.String)
                .Select(value => value.Value<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value) && LooksLikeAudioReference(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(40)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool LooksLikeAudioReference(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower.Contains("soundwave") ||
               lower.Contains("soundcue") ||
               lower.Contains("akaudio") ||
               lower.Contains("fmodevent") ||
               lower.Contains("wwise") ||
               lower.Contains("/audio/") ||
               lower.Contains("/vo/") ||
               lower.Contains("/voice/") ||
               lower.Contains("/dialogue/") ||
               lower.Contains("/dialog/") ||
               lower.EndsWith(".wem") ||
               lower.EndsWith(".wav") ||
               lower.EndsWith(".ogg") ||
               lower.EndsWith(".bnk") ||
               lower.StartsWith("play_vo") ||
               lower.StartsWith("vo_");
    }

    private static AudioIndex BuildAudioIndex(IEnumerable<GameFile> files)
    {
        var index = new AudioIndex();
        foreach (var file in files.Where(IsLikelyAudioFile).DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
        {
            var candidate = new AudioFileCandidate(file.Path, Normalize(Path.GetFileNameWithoutExtension(file.Path)));
            index.Items.Add(candidate);

            foreach (var token in Tokenize(file.Path))
            {
                if (!index.ByToken.TryGetValue(token, out var list))
                {
                    list = [];
                    index.ByToken[token] = list;
                }

                if (list.Count < 500)
                    list.Add(candidate);
            }
        }

        return index;
    }

    private static bool IsLikelyAudioFile(GameFile file)
    {
        var extension = file.Extension.ToLowerInvariant();
        if (extension is "wem" or "wav" or "ogg" or "flac" or "bnk" or "pck" or "bank" or "awb" or "acb")
            return true;

        if (extension is not ("uasset" or "umap"))
            return false;

        var lower = file.Path.ToLowerInvariant();
        return lower.Contains("/audio/") ||
               lower.Contains("/vo/") ||
               lower.Contains("/voice/") ||
               lower.Contains("/dialogue/") ||
               lower.Contains("/dialog/") ||
               lower.Contains("akaudio") ||
               lower.Contains("fmod") ||
               lower.Contains("soundwave") ||
               lower.Contains("soundcue");
    }

    private static ScoredAudioCandidate FindBestAudioCandidate(AudioIndex index, string key, IReadOnlyCollection<string> hints)
    {
        var queryTokens = new HashSet<string>(Tokenize(key), StringComparer.OrdinalIgnoreCase);
        foreach (var hint in hints)
        {
            foreach (var token in Tokenize(hint))
                queryTokens.Add(token);
        }

        var pool = new HashSet<AudioFileCandidate>();
        foreach (var token in queryTokens)
        {
            if (index.ByToken.TryGetValue(token, out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    if (pool.Count >= 5000) break;
                    pool.Add(candidate);
                }
            }
        }

        if (pool.Count == 0)
            return null;

        var normalizedKey = Normalize(key);
        ScoredAudioCandidate best = null;
        foreach (var candidate in pool)
        {
            var score = 0;
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                if (candidate.NormalizedStem.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase))
                    score += 70;
                else if (candidate.NormalizedStem.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
                         normalizedKey.Contains(candidate.NormalizedStem, StringComparison.OrdinalIgnoreCase))
                    score += 40;
            }

            var candidateTokens = Tokenize(candidate.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            score += Math.Min(35, candidateTokens.Count(token => queryTokens.Contains(token)) * 7);

            if (hints.Any(hint =>
                    hint.Contains(Path.GetFileNameWithoutExtension(candidate.Path), StringComparison.OrdinalIgnoreCase)))
                score += 35;

            if (candidate.Path.Contains("/VO/", StringComparison.OrdinalIgnoreCase) ||
                candidate.Path.Contains("/Voice/", StringComparison.OrdinalIgnoreCase))
                score += 5;

            if (best is null || score > best.Score)
                best = new ScoredAudioCandidate(candidate.Path, score);
        }

        return best is { Score: >= 12 } ? best : null;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        var pieces = value.Split(
            value.Where(ch => !char.IsLetterOrDigit(ch)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var piece in pieces)
        {
            if (piece.Length >= 3)
                yield return piece.ToLowerInvariant();
        }

        var normalized = Normalize(Path.GetFileNameWithoutExtension(value));
        if (normalized.Length >= 4)
            yield return normalized;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "LOCRES Voice Mapper sonuçlarını kaydet",
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = "locres_voice_matches.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        var csv = new StringBuilder();
        csv.AppendLine("Confidence,Namespace,Key,LocalizedText,PackagePath,AudioPath,ReferenceHint,MatchType");
        foreach (var row in _rows)
        {
            csv.Append(row.Confidence).Append(',')
                .Append(Csv(row.Namespace)).Append(',')
                .Append(Csv(row.Key)).Append(',')
                .Append(Csv(row.LocalizedText)).Append(',')
                .Append(Csv(row.PackagePath)).Append(',')
                .Append(Csv(row.AudioPath)).Append(',')
                .Append(Csv(row.ReferenceHint)).Append(',')
                .Append(Csv(row.MatchType)).AppendLine();
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
        _status.Text = $"CSV kaydedildi: {dialog.FileName}";
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private void OpenSelectedPath(bool audio)
    {
        if (_resultsGrid.SelectedItem is not VoiceMatchRow selected)
            return;

        var path = audio ? selected.AudioPath : selected.PackagePath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var application = ApplicationService.ApplicationView;
        if (!application.CUE4Parse.Provider.Files.TryGetValue(path, out var file))
        {
            System.Windows.MessageBox.Show($"Dosya sağlayıcıda bulunamadı:\n{path}",
                "LOCRES Voice Mapper", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        application.CUE4Parse.Extract(CancellationToken.None, file, true);
    }

    private void UpdateSelectionButtons()
    {
        var selected = _resultsGrid.SelectedItem as VoiceMatchRow;
        _openPackageButton.IsEnabled = selected is not null && !string.IsNullOrWhiteSpace(selected.PackagePath);
        _openAudioButton.IsEnabled = selected is not null && !string.IsNullOrWhiteSpace(selected.AudioPath);
    }

    private void SetBusy(bool busy)
    {
        _scanButton.IsEnabled = !busy && _locresSelector.Items.Count > 0;
        _cancelButton.IsEnabled = busy;
        _locresSelector.IsEnabled = !busy;
        _pathFilter.IsEnabled = !busy;
        _maxFileSize.IsEnabled = !busy;
        _deepParse.IsEnabled = !busy;
        _exportButton.IsEnabled = !busy && _rows.Count > 0;
        if (busy)
        {
            _openPackageButton.IsEnabled = false;
            _openAudioButton.IsEnabled = false;
        }
        else
        {
            UpdateSelectionButtons();
            _exportButton.IsEnabled = _rows.Count > 0;
        }
    }

    private sealed record LocresLine(string Namespace, string Key, string LocalizedText);

    private sealed record AudioFileCandidate(string Path, string NormalizedStem);

    private sealed record ScoredAudioCandidate(string Path, int Score);

    private sealed class AudioIndex
    {
        public List<AudioFileCandidate> Items { get; } = [];
        public Dictionary<string, List<AudioFileCandidate>> ByToken { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class VoiceMatchRow
    {
        public int Confidence { get; init; }
        public string Namespace { get; init; }
        public string Key { get; init; }
        public string LocalizedText { get; init; }
        public string PackagePath { get; init; }
        public string AudioPath { get; init; }
        public string ReferenceHint { get; init; }
        public string MatchType { get; init; }
    }
}
