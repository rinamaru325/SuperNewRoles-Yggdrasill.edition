using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperNewRoles.Patch
{
    [Harmony]
    public class AdminPatch
    {
        public static float RestrictAdminTime = 600f;
        public static float RestrictAdminTimeMax = 600f;
        public static bool canUseAdmin
        {
            get
            {
                return MapOptions.MapOption.RestrictAdmin.getBool() || RestrictAdminTime > 0f;
            }
        }
        static Dictionary<SystemTypes, List<Color>> playerColors = new Dictionary<SystemTypes, List<Color>>();
        static float adminTimer = 0f;
        static TMPro.TextMeshPro OutOfTime;
        static TMPro.TextMeshPro TimeRemaining;
        static bool clearedIcons = false;

        public static void ResetData()
        {
            adminTimer = 0f;
            if (TimeRemaining != null)
            {
                UnityEngine.Object.Destroy(TimeRemaining);
                TimeRemaining = null;
            }

            if (OutOfTime != null)
            {
                UnityEngine.Object.Destroy(OutOfTime);
                OutOfTime = null;
            }
        }

        static void UseAdminTime()
        {
            // Don't waste network traffic if we're out of time.
            if (MapOptions.MapOption.RestrictAdmin.getBool() && PlayerControl.LocalPlayer.isAlive())
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.CustomRPC.UseAdminTime, Hazel.SendOption.Reliable, -1);
                writer.Write(adminTimer);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                CustomRPC.RPCProcedure.UseAdminTime(adminTimer);
            }
            adminTimer = 0f;
        }

        [HarmonyPatch(typeof(MapConsole), nameof(MapConsole.CanUse))]
        public static class MapConsoleCanUsePatch
        {
            public static bool Prefix(ref float __result, MapConsole __instance, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
            {
                canUse = couldUse = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(MapConsole), nameof(MapConsole.Use))]
        public static class MapConsoleUsePatch
        {
            public static bool Prefix(MapConsole __instance)
            {
                return canUseAdmin;
            }
        }

        [HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.OnEnable))]
        class MapCountOverlayOnEnablePatch
        {
            static void Prefix(MapCountOverlay __instance)
            {
                adminTimer = 0f;
            }
        }


        [HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.OnDisable))]
        class MapCountOverlayOnDisablePatch
        {
            static void Prefix(MapCountOverlay __instance)
            {
                UseAdminTime();
            }
        }

        [HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.Update))]
        class MapCountOverlayUpdatePatch
        {

            static bool Prefix(MapCountOverlay __instance)
            {
                adminTimer += Time.deltaTime;
                if (adminTimer > 0.1f)
                    UseAdminTime();

                // Save colors for the Hacker
                __instance.timer += Time.deltaTime;
                if (__instance.timer < 0.1f)
                {
                    return false;
                }
                __instance.timer = 0f;

                playerColors = new Dictionary<SystemTypes, List<Color>>();

                if (MapOptions.MapOption.RestrictAdmin.getBool())
                {
                    if (OutOfTime == null)
                    {
                        OutOfTime = UnityEngine.Object.Instantiate(__instance.SabotageText, __instance.SabotageText.transform.parent);
                        OutOfTime.text = ModTranslation.getString("restrictOutOfTime");
                    }

                    if (TimeRemaining == null)
                    {
                        TimeRemaining = UnityEngine.Object.Instantiate(HudManager.Instance.TaskText, __instance.transform);
                        TimeRemaining.alignment = TMPro.TextAlignmentOptions.BottomRight;
                        TimeRemaining.transform.position = Vector3.zero;
                        TimeRemaining.transform.localPosition = new Vector3(3.25f, 5.25f);
                        TimeRemaining.transform.localScale *= 2f;
                        TimeRemaining.color = Palette.White;
                    }

                    if (RestrictAdminTime <= 0f)
                    {
                        __instance.BackgroundColor.SetColor(Palette.DisabledGrey);
                        OutOfTime.gameObject.SetActive(true);
                        TimeRemaining.gameObject.SetActive(false);
                        if (clearedIcons == false)
                        {
                            foreach (CounterArea ca in __instance.CountAreas) ca.UpdateCount(0);
                            clearedIcons = true;
                        }
                        return false;
                    }

                    clearedIcons = false;
                    OutOfTime.gameObject.SetActive(false);
                    string timeString = TimeSpan.FromSeconds(RestrictAdminTime).ToString(@"mm\:ss\.ff");
                    TimeRemaining.text = String.Format(ModTranslation.getString("timeRemaining"), timeString);
                    //TimeRemaining.color = MapOptions.restrictAdminTime > 10f ? Palette.AcceptedGreen : Palette.ImpostorRed;
                    TimeRemaining.gameObject.SetActive(true);
                }

                bool commsActive = false;
                foreach (PlayerTask task in PlayerControl.LocalPlayer.myTasks)
                    if (task.TaskType == TaskTypes.FixComms) commsActive = true;

                if (!__instance.isSab && commsActive)
                {
                    __instance.isSab = true;
                    __instance.BackgroundColor.SetColor(Palette.DisabledGrey);
                    __instance.SabotageText.gameObject.SetActive(true);
                    OutOfTime.gameObject.SetActive(false);
                    return false;
                }

                if (__instance.isSab && !commsActive)
                {
                    __instance.isSab = false;
                    __instance.BackgroundColor.SetColor(Color.green);
                    __instance.SabotageText.gameObject.SetActive(false);
                    OutOfTime.gameObject.SetActive(false);
                }

                for (int i = 0; i < __instance.CountAreas.Length; i++)
                {
                    CounterArea counterArea = __instance.CountAreas[i];
                    List<Color> roomColors = new List<Color>();
                    playerColors.Add(counterArea.RoomType, roomColors);

                    if (!commsActive)
                    {
                        PlainShipRoom plainShipRoom = ShipStatus.Instance.FastRooms[counterArea.RoomType];

                        if (plainShipRoom != null && plainShipRoom.roomArea)
                        {
                            int num = plainShipRoom.roomArea.OverlapCollider(__instance.filter, __instance.buffer);
                            int num2 = num;
                            for (int j = 0; j < num; j++)
                            {
                                Collider2D collider2D = __instance.buffer[j];
                                if (!(collider2D.tag == "DeadBody"))
                                {
                                    PlayerControl component = collider2D.GetComponent<PlayerControl>();
                                    if (!component || component.Data == null || component.Data.Disconnected || component.Data.IsDead)
                                    {
                                        num2--;
                                    }
                                    else if (component?.MyRend?.material != null)
                                    {
                                        Color color = component.MyRend.material.GetColor("_BodyColor");

                                        roomColors.Add(color);
                                    }
                                }
                                else
                                {
                                    DeadBody component = collider2D.GetComponent<DeadBody>();
                                    if (component)
                                    {
                                        GameData.PlayerInfo playerInfo = GameData.Instance.GetPlayerById(component.ParentId);
                                        if (playerInfo != null)
                                        {
                                            var color = Palette.PlayerColors[playerInfo.Object.CurrentOutfit.ColorId];
                                            //  if (Hacker.onlyColorType)
                                            //    color = Helpers.isLighterColor(playerInfo.Object.CurrentOutfit.ColorId) ? Palette.PlayerColors[7] : Palette.PlayerColors[6];
                                            roomColors.Add(color);
                                        }
                                    }
                                }
                            }
                            counterArea.UpdateCount(num2);
                        }
                        else
                        {
                            Debug.LogWarning("Couldn't find counter for:" + counterArea.RoomType);
                        }
                    }
                    else
                    {
                        counterArea.UpdateCount(0);
                    }
                }
                return false;
            }
        }
      /*  public static bool IsBlocked(IUsable target, PlayerControl pc)
        {
            if (target == null) return false;

            Console targetConsole = target.TryCast<Console>();
            SystemConsole targetSysConsole = target.TryCast<SystemConsole>();
            MapConsole targetMapConsole = target.TryCast<MapConsole>();
            if ((targetConsole != null && IsBlocked(targetConsole, pc)) ||
                (targetSysConsole != null && IsBlocked(targetSysConsole, pc)) ||
                (targetMapConsole != null && !MapOptions.canUseAdmin))
            {
                return true;
            }
            return false;
        }*/
    }
}