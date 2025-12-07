using HarmonyLib;
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

        listing.Label("Text API Key:");
        Settings.TextAPIKey.Value = listing.TextEntry(Settings.TextAPIKey.Value);

        listing.Gap();

        listing.Label("Voice API Key:");
        Settings.VoiceAPIKey.Value = listing.TextEntry(Settings.VoiceAPIKey.Value);

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