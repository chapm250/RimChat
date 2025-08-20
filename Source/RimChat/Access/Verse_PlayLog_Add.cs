using RimChat.Core;
using HarmonyLib;
using Verse;

namespace RimChat.Access;

[HarmonyPatch(typeof(PlayLog), nameof(PlayLog.Add))]
public static class Verse_PlayLog_Add
{
    private static void Postfix(LogEntry entry) => Chatter.Add(entry);
}
