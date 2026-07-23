using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace FModel.Views;

public sealed partial class LocresVoiceMapper
{
    private readonly ComboBox _voiceMode = CreateVoiceModeSelector();
    private readonly CheckBox _requireAudioCandidate = new()
    {
        Content = "Yalnızca ses adayı bulunanlar",
        IsChecked = true,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(12, 0, 16, 0),
        ToolTip = "Kapalı olduğunda, VO olduğu düşünülen fakat henüz fiziksel ses dosyası çözülemeyen paketler de gösterilir."
    };

    private static readonly HashSet<string> StrongVoiceTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "vo", "voice", "voices", "voiceover", "voiceovers", "voiceevent", "voiceline", "voicelines",
        "dialogue", "dialog", "dialogues", "dialoguewave", "dialoguevoice", "dialogueevent", "dialogueline",
        "speech", "spoken", "utterance", "utterances", "conversation", "conversations", "convo", "convos",
        "narration", "narrator", "narrativevoice", "monologue", "subtitle", "subtitles", "caption", "captions",
        "speaker", "speakers", "bark", "barks", "chatter", "announcer", "announcement", "announcements",
        "vocal", "vocals", "vocalization", "vocalizations", "vox", "dlg", "dubbing", "dubbed",
        "replik", "replikler", "diyalog", "konusma", "seslendirme"
    };

    private static readonly HashSet<string> ContextVoiceTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "quest", "quests", "story", "narrative", "cinematic", "cinematics", "cutscene", "cutscenes",
        "sequence", "character", "characters", "actor", "actors", "npc", "npcs", "localized", "localization",
        "culture", "language", "lipsync", "lip", "phoneme", "phonemes", "viseme", "visemes", "facial",
        "radio", "comms", "intercom", "phone", "call", "calls", "combatvo", "effort", "efforts", "grunt",
        "grunts", "exertion", "pain", "breath", "breaths", "breathing", "reaction", "reactions", "taunt",
        "taunts", "emote", "emotes", "localizedaudio", "localisedaudio", "storyline", "mission", "missions"
    };

    private static readonly HashSet<string> NonVoiceTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "music", "bgm", "ost", "score", "soundtrack", "stinger", "stingers", "jingle", "jingles", "song",
        "songs", "theme", "themes", "playlist", "musictrack", "sfx", "fx", "effect", "effects", "foley",
        "weapon", "weapons", "gun", "guns", "explosion", "explosions", "impact", "impacts", "footstep",
        "footsteps", "ambience", "ambient", "environment", "environmental", "weather", "wind", "rain",
        "ui", "hud", "menu", "menus", "notification", "notifications", "click", "clicks", "button", "buttons",
        "whoosh", "whooshes", "woosh", "rumble", "rumbles", "vehicle", "vehicles", "engine", "engines"
    };

    private static ComboBox CreateVoiceModeSelector()
    {
        var selector = new ComboBox
        {
            MinWidth = 165,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Dengeli: geniş VO yakalama. Katı: en az iki bağımsız VO kanıtı. Kesin: ses + doğrudan diyalog/event zinciri gerekir."
        };
        selector.Items.Add(new VoiceModeOption(VoiceFilterMode.All, "Tümü (tanılama)"));
        selector.Items.Add(new VoiceModeOption(VoiceFilterMode.Balanced, "Yalnızca VO — Dengeli"));
        selector.Items.Add(new VoiceModeOption(VoiceFilterMode.Strict, "Yalnızca VO — Katı"));
        selector.Items.Add(new VoiceModeOption(VoiceFilterMode.Certain, "Yalnızca VO — Kesin"));
        selector.SelectedIndex = 1;
        return selector;
    }

    private VoiceFilterMode SelectedVoiceMode =>
        (_voiceMode.SelectedItem as VoiceModeOption)?.Mode ?? VoiceFilterMode.Balanced;

    private static VoicePackageContext AnalyzePackageVoiceContext(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return VoicePackageContext.Empty;

        var lower = json.ToLowerInvariant();
        var evidence = new List<string>();
        var score = 0;
        var strong = 0;
        var negative = 0;
        var directDialogue = false;
        var hasAudioReference = false;

        void Positive(string needle, string label, int points, bool direct = false)
        {
            if (!lower.Contains(needle, StringComparison.Ordinal)) return;
            score += points;
            strong++;
            directDialogue |= direct;
            AddEvidence(evidence, label);
        }

        void Audio(string needle, string label)
        {
            if (!lower.Contains(needle, StringComparison.Ordinal)) return;
            hasAudioReference = true;
            AddEvidence(evidence, label);
        }

        void Negative(string needle, string label, int points)
        {
            if (!lower.Contains(needle, StringComparison.Ordinal)) return;
            score -= points;
            negative++;
            AddEvidence(evidence, label);
        }

        Positive("dialoguewave", "Paket: DialogueWave", 38, true);
        Positive("dialoguevoice", "Paket: DialogueVoice", 34, true);
        Positive("voiceevent", "Paket: VoiceEvent", 30, true);
        Positive("voiceline", "Paket: VoiceLine", 28, true);
        Positive("spokenText".ToLowerInvariant(), "Paket: SpokenText", 28, true);
        Positive("dialoguetext", "Paket: DialogueText", 22, true);
        Positive("dialogueid", "Paket: DialogueId", 18, true);
        Positive("play_vo", "Paket: Play_VO eventi", 28, true);
        Positive("event:/vo", "Paket: FMOD VO eventi", 28, true);
        Positive("/vo/", "Paket: /VO/ yolu", 22, true);
        Positive("/voice/", "Paket: /Voice/ yolu", 22, true);
        Positive("/dialogue/", "Paket: /Dialogue/ yolu", 20, true);
        Positive("/speech/", "Paket: /Speech/ yolu", 20, true);

        var hasSpeaker = lower.Contains("\"speaker\"", StringComparison.Ordinal) ||
                         lower.Contains("speakername", StringComparison.Ordinal);
        var hasSubtitle = lower.Contains("\"subtitle\"", StringComparison.Ordinal) ||
                          lower.Contains("subtitles", StringComparison.Ordinal);
        if (hasSpeaker)
        {
            score += 10;
            AddEvidence(evidence, "Paket: Speaker alanı");
        }
        if (hasSubtitle)
        {
            score += 10;
            AddEvidence(evidence, "Paket: Subtitle alanı");
        }
        if (hasSpeaker && hasSubtitle)
        {
            score += 12;
            strong++;
            directDialogue = true;
            AddEvidence(evidence, "Paket: Speaker + Subtitle zinciri");
        }

        Audio("soundwave", "Paket: SoundWave referansı");
        Audio("soundcue", "Paket: SoundCue referansı");
        Audio("akaudioevent", "Paket: Wwise event referansı");
        Audio("fmodevent", "Paket: FMOD event referansı");
        Audio(".wem", "Paket: WEM referansı");
        Audio(".wav", "Paket: WAV referansı");
        Audio(".ogg", "Paket: OGG referansı");

        Negative("/music/", "Karşı kanıt: Music yolu", 28);
        Negative("/bgm/", "Karşı kanıt: BGM yolu", 35);
        Negative("/sfx/", "Karşı kanıt: SFX yolu", 35);
        Negative("/ambience/", "Karşı kanıt: Ambience yolu", 30);
        Negative("/ui/", "Karşı kanıt: UI yolu", 30);

        return new VoicePackageContext(score, strong, negative, directDialogue, hasAudioReference, evidence);
    }

    private static VoiceAssessment AssessVoice(
        string nameSpace,
        string key,
        string localizedText,
        string packagePath,
        string audioPath,
        IReadOnlyCollection<string> hints,
        VoicePackageContext packageContext,
        bool deepConfirmed,
        int candidateScore)
    {
        var score = packageContext.Score;
        var strong = packageContext.StrongSignals;
        var negative = packageContext.NegativeSignals;
        var directLink = packageContext.DirectDialogue;
        var hasAudio = packageContext.HasAudioReference || !string.IsNullOrWhiteSpace(audioPath);
        var evidence = new List<string>(packageContext.Evidence);

        ApplySourceSignals(nameSpace, "Namespace", 18, 6, 24, ref score, ref strong, ref negative, ref directLink, evidence);
        ApplySourceSignals(key, "Key", 18, 6, 24, ref score, ref strong, ref negative, ref directLink, evidence);
        ApplySourceSignals(packagePath, "Paket yolu", 24, 8, 42, ref score, ref strong, ref negative, ref directLink, evidence);
        ApplySourceSignals(audioPath, "Ses yolu", 32, 8, 58, ref score, ref strong, ref negative, ref directLink, evidence);

        foreach (var hint in hints.Take(40))
        {
            var beforeStrong = strong;
            ApplySourceSignals(hint, "Referans", 25, 7, 48, ref score, ref strong, ref negative, ref directLink, evidence);
            if (strong > beforeStrong)
                hasAudio = true;
        }

        if (deepConfirmed)
        {
            score += 8;
            AddEvidence(evidence, "LOCRES anahtarı ayrıştırılmış pakette doğrulandı");
        }

        if (candidateScore >= 70)
        {
            score += 14;
            AddEvidence(evidence, "Ses adayı anahtarla çok güçlü eşleşiyor");
        }
        else if (candidateScore >= 35)
        {
            score += 8;
            AddEvidence(evidence, "Ses adayı anahtarla eşleşiyor");
        }

        if (!string.IsNullOrWhiteSpace(audioPath))
        {
            var extension = Path.GetExtension(audioPath).TrimStart('.').ToLowerInvariant();
            if (extension is "bnk" or "pck" or "bank" or "awb" or "acb")
            {
                score -= 8;
                AddEvidence(evidence, "Ses adayı tekil ses değil, banka/kapsayıcı");
            }

            var stem = Path.GetFileNameWithoutExtension(audioPath);
            if (!string.IsNullOrWhiteSpace(stem) && stem.All(char.IsDigit) && !directLink)
            {
                score -= 6;
                AddEvidence(evidence, "Sayısal ses adı; event zinciri doğrulanmadı");
            }
        }

        if (string.IsNullOrWhiteSpace(localizedText))
            score -= 3;

        score = Math.Clamp(score, -100, 100);
        var classification = score switch
        {
            >= 75 when directLink && hasAudio => "Kesin VO",
            >= 55 => "Güçlü VO",
            >= 28 => "Muhtemel VO",
            >= 10 => "Belirsiz",
            _ => "VO değil"
        };

        return new VoiceAssessment(
            score,
            strong,
            negative,
            hasAudio,
            directLink,
            classification,
            string.Join(" | ", evidence.Take(10)));
    }

    private static void ApplySourceSignals(
        string value,
        string sourceLabel,
        int strongWeight,
        int contextWeight,
        int negativeWeight,
        ref int score,
        ref int strong,
        ref int negative,
        ref bool directLink,
        List<string> evidence)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var tokens = ExtractVoiceTokens(value);
        var strongMatches = tokens.Where(StrongVoiceTokens.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        var contextMatches = tokens.Where(ContextVoiceTokens.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        var negativeMatches = tokens.Where(NonVoiceTokens.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();

        if (strongMatches.Count > 0)
        {
            score += strongWeight + Math.Min(12, (strongMatches.Count - 1) * 6);
            strong += strongMatches.Count;
            directLink |= sourceLabel is "Ses yolu" or "Referans" ||
                          strongMatches.Any(x => x is "dialoguewave" or "voiceevent" or "voiceline" or "dialogueevent");
            AddEvidence(evidence, $"{sourceLabel}: VO [{string.Join(", ", strongMatches)}]");
        }

        if (contextMatches.Count > 0)
        {
            score += contextWeight + Math.Min(6, (contextMatches.Count - 1) * 3);
            AddEvidence(evidence, $"{sourceLabel}: bağlam [{string.Join(", ", contextMatches)}]");
        }

        if (negativeMatches.Count > 0)
        {
            score -= negativeWeight + Math.Min(20, (negativeMatches.Count - 1) * 10);
            negative += negativeMatches.Count;
            AddEvidence(evidence, $"{sourceLabel}: VO dışı [{string.Join(", ", negativeMatches)}]");
        }
    }

    private static HashSet<string> ExtractVoiceTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var camelSplit = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2");
        var tokens = Regex.Matches(camelSplit, "[A-Za-z0-9ÇĞİÖŞÜçğıöşü]+")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedStem = Regex.Replace(Path.GetFileNameWithoutExtension(value) ?? string.Empty, "[^A-Za-z0-9]", string.Empty)
            .ToLowerInvariant();
        if (normalizedStem.Length >= 3)
            tokens.Add(normalizedStem);

        return tokens;
    }

    private static bool PassesVoiceFilter(VoiceAssessment assessment, VoiceFilterMode mode)
    {
        return mode switch
        {
            VoiceFilterMode.All => true,
            VoiceFilterMode.Balanced =>
                assessment.Score >= 28 &&
                assessment.StrongSignals >= 1 &&
                !(assessment.NegativeSignals >= 2 && assessment.StrongSignals < 3),
            VoiceFilterMode.Strict =>
                assessment.Score >= 52 &&
                assessment.StrongSignals >= 2 &&
                assessment.HasAudio &&
                assessment.DirectLink &&
                assessment.NegativeSignals <= 1,
            VoiceFilterMode.Certain =>
                assessment.Score >= 72 &&
                assessment.StrongSignals >= 3 &&
                assessment.HasAudio &&
                assessment.DirectLink &&
                assessment.NegativeSignals == 0,
            _ => false
        };
    }

    private static ScoredAudioCandidate FindBestVoiceAwareAudioCandidate(
        AudioIndex index,
        string key,
        IReadOnlyCollection<string> hints,
        VoiceFilterMode mode)
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
            if (!index.ByToken.TryGetValue(token, out var candidates)) continue;
            foreach (var candidate in candidates)
            {
                if (pool.Count >= 7500) break;
                pool.Add(candidate);
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
                    score += 75;
                else if (candidate.NormalizedStem.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
                         normalizedKey.Contains(candidate.NormalizedStem, StringComparison.OrdinalIgnoreCase))
                    score += 42;
            }

            var candidateTokens = Tokenize(candidate.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            score += Math.Min(40, candidateTokens.Count(token => queryTokens.Contains(token)) * 8);

            var stem = Path.GetFileNameWithoutExtension(candidate.Path);
            if (hints.Any(hint => hint.Contains(stem, StringComparison.OrdinalIgnoreCase)))
                score += 40;

            var voiceTokens = ExtractVoiceTokens(candidate.Path);
            if (voiceTokens.Any(StrongVoiceTokens.Contains))
                score += mode == VoiceFilterMode.All ? 8 : 28;
            if (voiceTokens.Any(ContextVoiceTokens.Contains))
                score += 6;
            if (voiceTokens.Any(NonVoiceTokens.Contains))
                score -= mode == VoiceFilterMode.All ? 15 : 75;

            var extension = Path.GetExtension(candidate.Path).TrimStart('.').ToLowerInvariant();
            if (extension is "bnk" or "pck" or "bank" or "awb" or "acb")
                score -= 10;

            if (best is null || score > best.Score)
                best = new ScoredAudioCandidate(candidate.Path, score);
        }

        return best is { Score: >= 12 } ? best : null;
    }

    private static void AddEvidence(List<string> evidence, string item)
    {
        if (evidence.Count >= 14 || evidence.Contains(item, StringComparer.OrdinalIgnoreCase)) return;
        evidence.Add(item);
    }

    private enum VoiceFilterMode
    {
        All,
        Balanced,
        Strict,
        Certain
    }

    private sealed record VoiceModeOption(VoiceFilterMode Mode, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record VoicePackageContext(
        int Score,
        int StrongSignals,
        int NegativeSignals,
        bool DirectDialogue,
        bool HasAudioReference,
        IReadOnlyList<string> Evidence)
    {
        public static VoicePackageContext Empty { get; } = new(0, 0, 0, false, false, Array.Empty<string>());
    }

    private sealed record VoiceAssessment(
        int Score,
        int StrongSignals,
        int NegativeSignals,
        bool HasAudio,
        bool DirectLink,
        string Classification,
        string Evidence);
}
