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
        var listing = new Listing_Standard();
        listing.Begin(inRect);

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

        listing.End();
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