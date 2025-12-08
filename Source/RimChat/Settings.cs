using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using RimChat.Configuration;
using Verse;

namespace RimChat;

public enum TTSProvider
{
    ElevenLabs,
    OpenAI
}

public class Settings : ModSettings
{
    public const int AutoHideSpeedDisabled = 1;

    private static readonly string[] SameConfigVersions =
    [
      "4.0"
    ];

    private static bool _resetRequired;

    public static bool Activated = true;

    public static readonly Setting<string> TextAPIKey = new(nameof(TextAPIKey), "");
    public static readonly Setting<string> VoiceAPIKey = new(nameof(VoiceAPIKey), "");
    public static readonly Setting<TTSProvider> TTSProviderSetting = new(nameof(TTSProviderSetting), TTSProvider.ElevenLabs);
    public static readonly Setting<float> MinTimeBetweenTalkInMinutes = new(nameof(MinTimeBetweenTalkInMinutes), 1f);

    // Interaction talk chance percentages (0-100, how likely the AI will vocalize this interaction)
    public static readonly Setting<int> ChitchatTalkChance = new(nameof(ChitchatTalkChance), 20);
    public static readonly Setting<int> DeepTalkTalkChance = new(nameof(DeepTalkTalkChance), 30);
    public static readonly Setting<int> SlightTalkChance = new(nameof(SlightTalkChance), 50);
    public static readonly Setting<int> InsultTalkChance = new(nameof(InsultTalkChance), 100);
    public static readonly Setting<int> KindWordsTalkChance = new(nameof(KindWordsTalkChance), 50);
    public static readonly Setting<int> AnimalChatTalkChance = new(nameof(AnimalChatTalkChance), 50);
    public static readonly Setting<int> TameAttemptTalkChance = new(nameof(TameAttemptTalkChance), 30);
    public static readonly Setting<int> TrainAttemptTalkChance = new(nameof(TrainAttemptTalkChance), 30);
    public static readonly Setting<int> NuzzleTalkChance = new(nameof(NuzzleTalkChance), 30);
    public static readonly Setting<int> ReleaseToWildTalkChance = new(nameof(ReleaseToWildTalkChance), 100);
    public static readonly Setting<int> BuildRapportTalkChance = new(nameof(BuildRapportTalkChance), 90);
    public static readonly Setting<int> RecruitAttemptTalkChance = new(nameof(RecruitAttemptTalkChance), 90);
    public static readonly Setting<int> SparkJailbreakTalkChance = new(nameof(SparkJailbreakTalkChance), 100);
    public static readonly Setting<int> RomanceAttemptTalkChance = new(nameof(RomanceAttemptTalkChance), 100);
    public static readonly Setting<int> MarriageProposalTalkChance = new(nameof(MarriageProposalTalkChance), 100);
    public static readonly Setting<int> BreakupTalkChance = new(nameof(BreakupTalkChance), 100);


    private static IEnumerable<Setting> AllSettings => typeof(Settings).GetFields().Select(static field => field.GetValue(null) as Setting).Where(static setting => setting is not null)!;

    public static void Reset() => AllSettings.Do(static setting => setting.ToDefault());

    public void CheckResetRequired()
    {
        if (!_resetRequired) { return; }
        _resetRequired = false;

        Write();

        RimChat.Mod.Warning("Settings were reset with new update");
    }

    public override void ExposeData()
    {
        if (_resetRequired) { return; }

        var version = Scribe.mode is LoadSaveMode.Saving ? RimChat.Mod.Version : null;
        Scribe_Values.Look(ref version, "Version");
        if (Scribe.mode is LoadSaveMode.LoadingVars && (version is null || (version is not RimChat.Mod.Version && !SameConfigVersions.Contains(Regex.Match(version, @"^\d+\.\d+").Value))))
        {
            _resetRequired = true;
            return;
        }

        AllSettings.Do(static setting => setting.Scribe());
    }
}