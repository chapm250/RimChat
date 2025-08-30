using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using RimChat.Configuration;
using Verse;

namespace RimChat;

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