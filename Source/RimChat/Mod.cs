using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimChat;

public sealed class Mod : Verse.Mod
{
    public const string Id = "RimChat";
    public const string Name = "RimChat";
    public const string Version = "1.0";

    public static Mod Instance = null!;
    public static Settings Settings = null!;
    private static Vector2 scrollPosition = Vector2.zero;

    public Mod(ModContentPack content) : base(content)
    {
        Instance = this;

        Settings = GetSettings<Settings>();

        new Harmony(Id).PatchAll();

        Log("Initialized");
    }

    public override string SettingsCategory() => Name;

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var viewRect = new Rect(0f, 0f, inRect.width - 16f, inRect.height);
        var scrollRect = new Rect(0f, 0f, inRect.width - 16f, 1200f); // Adjust height as needed

        Widgets.BeginScrollView(viewRect, ref scrollPosition, scrollRect);
        var listing = new Listing_Standard();
        listing.Begin(scrollRect);

        listing.Label("OpenAI API Key:");
        Settings.TextAPIKey.Value = listing.TextEntry(Settings.TextAPIKey.Value);

        listing.Gap();

        listing.Label("Eleven Labs API Key:");
        Settings.VoiceAPIKey.Value = listing.TextEntry(Settings.VoiceAPIKey.Value);

        listing.Gap();

        if (listing.ButtonText($"TTS Provider: {Settings.TTSProviderSetting.Value}"))
        {
            var options = new List<FloatMenuOption>();
            foreach (TTSProvider provider in System.Enum.GetValues(typeof(TTSProvider)))
            {
                options.Add(new FloatMenuOption(provider.ToString(), () => {
                    Settings.TTSProviderSetting.Value = provider;
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        listing.Gap();

        listing.Label($"Min Time Between Talk (minutes): {Settings.MinTimeBetweenTalkInMinutes.Value:F1}");
        Settings.MinTimeBetweenTalkInMinutes.Value = listing.Slider(Settings.MinTimeBetweenTalkInMinutes.Value, 0.1f, 10f);

        listing.Gap();
        listing.Label("Interaction Talk Chances (0-100%):");
        listing.Gap();

        listing.Label($"Chitchat: {Settings.ChitchatTalkChance.Value}%");
        Settings.ChitchatTalkChance.Value = (int)listing.Slider(Settings.ChitchatTalkChance.Value, 0, 100);

        listing.Label($"Deep Talk: {Settings.DeepTalkTalkChance.Value}%");
        Settings.DeepTalkTalkChance.Value = (int)listing.Slider(Settings.DeepTalkTalkChance.Value, 0, 100);

        listing.Label($"Slight: {Settings.SlightTalkChance.Value}%");
        Settings.SlightTalkChance.Value = (int)listing.Slider(Settings.SlightTalkChance.Value, 0, 100);

        listing.Label($"Insult: {Settings.InsultTalkChance.Value}%");
        Settings.InsultTalkChance.Value = (int)listing.Slider(Settings.InsultTalkChance.Value, 0, 100);

        listing.Label($"Kind Words: {Settings.KindWordsTalkChance.Value}%");
        Settings.KindWordsTalkChance.Value = (int)listing.Slider(Settings.KindWordsTalkChance.Value, 0, 100);

        listing.Label($"Animal Chat: {Settings.AnimalChatTalkChance.Value}%");
        Settings.AnimalChatTalkChance.Value = (int)listing.Slider(Settings.AnimalChatTalkChance.Value, 0, 100);

        listing.Label($"Tame Attempt: {Settings.TameAttemptTalkChance.Value}%");
        Settings.TameAttemptTalkChance.Value = (int)listing.Slider(Settings.TameAttemptTalkChance.Value, 0, 100);

        listing.Label($"Train Attempt: {Settings.TrainAttemptTalkChance.Value}%");
        Settings.TrainAttemptTalkChance.Value = (int)listing.Slider(Settings.TrainAttemptTalkChance.Value, 0, 100);

        listing.Label($"Nuzzle: {Settings.NuzzleTalkChance.Value}%");
        Settings.NuzzleTalkChance.Value = (int)listing.Slider(Settings.NuzzleTalkChance.Value, 0, 100);

        listing.Label($"Release To Wild: {Settings.ReleaseToWildTalkChance.Value}%");
        Settings.ReleaseToWildTalkChance.Value = (int)listing.Slider(Settings.ReleaseToWildTalkChance.Value, 0, 100);

        listing.Label($"Build Rapport: {Settings.BuildRapportTalkChance.Value}%");
        Settings.BuildRapportTalkChance.Value = (int)listing.Slider(Settings.BuildRapportTalkChance.Value, 0, 100);

        listing.Label($"Recruit Attempt: {Settings.RecruitAttemptTalkChance.Value}%");
        Settings.RecruitAttemptTalkChance.Value = (int)listing.Slider(Settings.RecruitAttemptTalkChance.Value, 0, 100);

        listing.Label($"Spark Jailbreak: {Settings.SparkJailbreakTalkChance.Value}%");
        Settings.SparkJailbreakTalkChance.Value = (int)listing.Slider(Settings.SparkJailbreakTalkChance.Value, 0, 100);

        listing.Label($"Romance Attempt: {Settings.RomanceAttemptTalkChance.Value}%");
        Settings.RomanceAttemptTalkChance.Value = (int)listing.Slider(Settings.RomanceAttemptTalkChance.Value, 0, 100);

        listing.Label($"Marriage Proposal: {Settings.MarriageProposalTalkChance.Value}%");
        Settings.MarriageProposalTalkChance.Value = (int)listing.Slider(Settings.MarriageProposalTalkChance.Value, 0, 100);

        listing.Label($"Breakup: {Settings.BreakupTalkChance.Value}%");
        Settings.BreakupTalkChance.Value = (int)listing.Slider(Settings.BreakupTalkChance.Value, 0, 100);

        listing.End();
        Widgets.EndScrollView();
        base.DoSettingsWindowContents(inRect);
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        Settings.Write();
    }

    public static void Log(string message) => Verse.Log.Message(PrefixMessage(message));
    public static void Warning(string message) => Verse.Log.Warning(PrefixMessage(message));
    public static void Error(string message) => Verse.Log.Error(PrefixMessage(message));
    private static string PrefixMessage(string message) => $"[{Name} v{Version}] {message}";


}