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
    OpenAI,
    Resemble
}

public enum LLMProvider
{
    OpenAI,
    Claude,
    Gemini
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
    public static readonly Setting<string> ClaudeAPIKey = new(nameof(ClaudeAPIKey), "");
    public static readonly Setting<string> GeminiAPIKey = new(nameof(GeminiAPIKey), "");
    public static readonly Setting<string> VoiceAPIKey = new(nameof(VoiceAPIKey), "");
    public static readonly Setting<string> ResembleAPIKey = new(nameof(ResembleAPIKey), "");
    public static readonly Setting<LLMProvider> LLMProviderSetting = new(nameof(LLMProviderSetting), LLMProvider.OpenAI);
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

    // Instructions template with keyword substitution
    // Available keywords: {PAWN_NAME}, {RECIPIENT_NAME}, {DAYS_PASSED}, {JOB}, {CHILDHOOD}, {ADULTHOOD}, {HISTORY}
    public static readonly Setting<string> InstructionsTemplate = new(nameof(InstructionsTemplate),
        @"You are a pawn in Rimworld named {PAWN_NAME} talking to {RECIPIENT_NAME} in english.
Respond to {RECIPIENT_NAME} in 1 - 3 sentences.
Do not reference objects as if they are nearby, just talk about them in the abstract or as memories.
Do not use emote or action texts
Do not speak for the other pawn {RECIPIENT_NAME}, only for yourself.
Here is some history, you crashed {DAYS_PASSED} days ago.
You are currently doing {JOB}
Your childhood background is {CHILDHOOD}
Your adulthood background is {ADULTHOOD}
The following are some recent events
{HISTORY}");


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