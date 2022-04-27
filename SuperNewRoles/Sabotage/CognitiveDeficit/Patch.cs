﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace SuperNewRoles.Sabotage.CognitiveDeficit
{
    public static class TaskBar
    {
        public static ProgressTracker Instance;
        [HarmonyPatch(typeof(ProgressTracker),nameof(ProgressTracker.FixedUpdate))]
        class TaskBarPatch
        {
            public static void Postfix(ProgressTracker __instance)
            {
                Instance = __instance;
                if (AmongUsClient.Instance.AmHost)
                {
                    if (SuperNewRoles.Patch.DebugMode.DebugManager.IsHide)
                    {
                        __instance.gameObject.SetActive(false);
                        return;
                    }
                }
                if (PlayerControl.GameOptions.TaskBarMode != TaskBarMode.Invisible)
                {
                    if (SabotageManager.thisSabotage == SabotageManager.CustomSabotage.CognitiveDeficit)
                    {
                        __instance.gameObject.SetActive(main.IsLocalEnd);
                    }
                }
            }
        }
    }
}