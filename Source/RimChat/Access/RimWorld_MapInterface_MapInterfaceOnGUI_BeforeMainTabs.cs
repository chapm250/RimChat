using System;
using RimChat.Core;
using HarmonyLib;
using RimWorld;

namespace RimChat.Access;

[HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceOnGUI_BeforeMainTabs))]
public static class RimWorld_MapInterface_MapInterfaceOnGUI_BeforeMainTabs
{
  private static void Postfix()
  {
    try { Chatter.Talk(); }
    catch (Exception exception)
    {
      Mod.Error($"Deactivated because chat failed with error: [{exception.Source}: {exception.Message}]\n\nTrace:\n{exception.StackTrace}");
    }
  }
}
