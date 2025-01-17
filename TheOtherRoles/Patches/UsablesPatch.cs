using System;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static TheOtherRoles.MapOptions;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;


namespace TheOtherRoles.Patches
{
    public static class UsablesPatch
    {
        public static bool IsBlocked(PlayerTask task, PlayerControl pc)
        {
            if (task == null || pc == null || pc != CachedPlayer.LocalPlayer.PlayerControl) return false;

            bool isLights = task.TaskType == TaskTypes.FixLights;
            bool isComms = task.TaskType == TaskTypes.FixComms;
            bool isReactor = task.TaskType is TaskTypes.StopCharles or TaskTypes.ResetSeismic or TaskTypes.ResetReactor;
            bool isO2 = task.TaskType == TaskTypes.RestoreOxy;

            if (pc.isRole(RoleType.Swapper) && (isLights || isComms))
            {
                return true;
            }

            if (pc.hasModifier(ModifierType.Madmate) && (isLights || (isComms && !Madmate.canFixComm)))
            {
                return true;
            }

            if (pc.hasModifier(ModifierType.CreatedMadmate) && (isLights || (isComms && !CreatedMadmate.canFixComm)))
            {
                return true;
            }

            if (pc.isGM() && (isLights || isComms || isReactor || isO2))
            {
                return true;
            }

            if (pc.isRole(RoleType.Mafioso) && !Mafioso.canRepair && (isLights || isComms))
            {
                return true;
            }

            if (pc.isRole(RoleType.Janitor) && !Janitor.canRepair && (isLights || isComms))
            {
                return true;
            }

            if (pc.isRole(RoleType.Fox) && (isLights || isComms || isReactor || isO2))
            {
                if (isReactor)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public static bool IsBlocked(Console console, PlayerControl pc)
        {
            if (console == null || pc == null || pc != CachedPlayer.LocalPlayer.PlayerControl)
            {
                return false;
            }

            PlayerTask task = console.FindTask(pc);
            return IsBlocked(task, pc);
        }

        public static bool IsBlocked(SystemConsole console, PlayerControl pc)
        {
            if (console == null || pc == null || pc != CachedPlayer.LocalPlayer.PlayerControl)
            {
                return false;
            }

            string name = console.name;
            bool isSecurity = name is "task_cams" or "Surv_Panel" or "SurvLogConsole" or "SurvConsole";
            bool isVitals = name == "panel_vitals";
            bool isButton = name is "EmergencyButton" or "EmergencyConsole" or "task_emergency";

            if ((isSecurity && !MapOptions.canUseCameras) || (isVitals && !MapOptions.canUseVitals)) return true;
            return false;
        }

        public static bool IsBlocked(IUsable target, PlayerControl pc)
        {
            if (target == null) return false;

            Console targetConsole = target.TryCast<Console>();
            SystemConsole targetSysConsole = target.TryCast<SystemConsole>();
            MapConsole targetMapConsole = target.TryCast<MapConsole>();

            // Hydeの時にはタスクができない
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.JekyllAndHyde) && !JekyllAndHyde.isJekyll())
            {
                string name = targetSysConsole == null ? "" : targetSysConsole.name;
                bool isSecurity = name is "task_cams" or "Surv_Panel" or "SurvLogConsole" or "SurvConsole";
                bool isVitals = name == "panel_vitals";
                bool isButton = name is "EmergencyButton" or "EmergencyConsole" or "task_emergency";
                PlayerTask task = targetConsole.FindTask(pc);
                bool isLights = task?.TaskType == TaskTypes.FixLights;
                bool isComms = task?.TaskType == TaskTypes.FixComms;
                bool isReactor = task?.TaskType is TaskTypes.StopCharles or TaskTypes.ResetSeismic or TaskTypes.ResetReactor;
                bool isO2 = task?.TaskType == TaskTypes.RestoreOxy;
                if (!isSecurity || !isVitals || !isButton || !isLights || !isComms || !isReactor || !isO2)
                {
                    return true;
                }
            }

            if ((targetConsole != null && IsBlocked(targetConsole, pc)) ||
                (targetSysConsole != null && IsBlocked(targetSysConsole, pc)) ||
                (targetMapConsole != null && !MapOptions.canUseAdmin))
            {
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
        public static class VentCanUsePatch
        {
            public static bool Prefix(Vent __instance, ref float __result, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
            {
                float num = float.MaxValue;
                PlayerControl @object = pc.Object;

                if (MapOptions.disableVents)
                {
                    canUse = couldUse = false;
                    __result = num;
                    return false;
                }
                bool roleCouldUse = @object.roleCanUseVents();

                if (__instance.name.StartsWith("SealedVent_"))
                {
                    canUse = couldUse = false;
                    __result = num;
                    return false;
                }

                // Submerged Compatability if needed:
                if (SubmergedCompatibility.IsSubmerged)
                {
                    // as submerged does, only change stuff for vents 9 and 14 of submerged. Code partially provided by AlexejheroYTB
                    if (SubmergedCompatibility.getInTransition())
                    {
                        __result = float.MaxValue;
                        return canUse = couldUse = false;
                    }
                    switch (__instance.Id)
                    {
                        case 9:  // Cannot enter vent 9 (Engine Room Exit Only Vent)!
                            if (CachedPlayer.LocalPlayer.PlayerControl.inVent) break;
                            __result = float.MaxValue;
                            return canUse = couldUse = false;
                        case 14: // Lower Central
                            __result = float.MaxValue;
                            couldUse = roleCouldUse && !pc.IsDead && (@object.CanMove || @object.inVent);
                            canUse = couldUse;
                            if (canUse)
                            {
                                Vector3 center = @object.Collider.bounds.center;
                                Vector3 position = __instance.transform.position;
                                __result = Vector2.Distance(center, position);
                                canUse &= __result <= __instance.UsableDistance;
                            }
                            return false;
                    }
                }

                var usableDistance = __instance.UsableDistance;
                if (__instance.name.StartsWith("JackInTheBoxVent_"))
                {
                    if (Trickster.trickster != CachedPlayer.LocalPlayer.PlayerControl && !CachedPlayer.LocalPlayer.PlayerControl.isGM())
                    {
                        // Only the Trickster can use the Jack-In-The-Boxes!
                        canUse = false;
                        couldUse = false;
                        __result = num;
                        return false;
                    }
                    else
                    {
                        // Reduce the usable distance to reduce the risk of gettings stuck while trying to jump into the box if it's placed near objects
                        usableDistance = 0.4f;
                    }
                }
                else if (__instance.name.StartsWith("SealedVent_"))
                {
                    canUse = couldUse = false;
                    __result = num;
                    return false;
                }

                couldUse = (@object.inVent || roleCouldUse) && !pc.IsDead && (@object.CanMove || @object.inVent);
                canUse = couldUse;
                if (canUse)
                {
                    Vector2 truePosition = @object.GetTruePosition();
                    Vector3 position = __instance.transform.position;
                    num = Vector2.Distance(truePosition, position);

                    canUse &= num <= usableDistance && !PhysicsHelpers.AnythingBetween(truePosition, position, Constants.ShipOnlyMask, false);
                }
                __result = num;
                return false;
            }
        }

        [HarmonyPatch(typeof(VentButton), nameof(VentButton.DoClick))]
        class VentButtonDoClickPatch
        {
            static bool Prefix(VentButton __instance)
            {
                // Manually modifying the VentButton to use Vent.Use again in order to trigger the Vent.Use prefix patch
                if (__instance.currentTarget != null) __instance.currentTarget.Use();
                return false;
            }
        }

        [HarmonyPatch(typeof(Vent), nameof(Vent.Use))]
        public static class VentUsePatch
        {
            public static bool Prefix(Vent __instance)
            {
                __instance.CanUse(CachedPlayer.LocalPlayer.PlayerControl.Data, out bool canUse, out bool couldUse);
                bool canMoveInVents = CachedPlayer.LocalPlayer.PlayerControl != Spy.spy && !CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Madmate) && !CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.CreatedMadmate);
                if (!canUse) return false; // No need to execute the native method as using is disallowed anyways

                bool isEnter = !CachedPlayer.LocalPlayer.PlayerControl.inVent;

                if (__instance.name.StartsWith("JackInTheBoxVent_"))
                {
                    __instance.SetButtons(isEnter && canMoveInVents);
                    MessageWriter writer = AmongUsClient.Instance.StartRpc(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.UseUncheckedVent, Hazel.SendOption.Reliable);
                    writer.WritePacked(__instance.Id);
                    writer.Write(CachedPlayer.LocalPlayer.PlayerControl.PlayerId);
                    writer.Write(isEnter ? byte.MaxValue : (byte)0);
                    writer.EndMessage();
                    RPCProcedure.useUncheckedVent(__instance.Id, CachedPlayer.LocalPlayer.PlayerControl.PlayerId, isEnter ? byte.MaxValue : (byte)0);
                    return false;
                }

                if (isEnter)
                {
                    CachedPlayer.LocalPlayer.PlayerControl.MyPhysics.RpcEnterVent(__instance.Id);
                }
                else
                {
                    CachedPlayer.LocalPlayer.PlayerControl.MyPhysics.RpcExitVent(__instance.Id);
                }
                __instance.SetButtons(isEnter && canMoveInVents);
                return false;
            }
        }

        // disable vent animation
        [HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
        public static class EnterVentPatch
        {
            public static bool Prefix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
            {
                if (!CustomOptionHolder.disableVentAnimation.getBool()) return true;
                return pc.AmOwner;
            }
        }

        [HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
        public static class ExitVentPatch
        {
            public static bool Prefix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
            {
                if (!CustomOptionHolder.disableVentAnimation.getBool()) return true;
                return pc.AmOwner;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        class VentButtonVisibilityPatch
        {
            static void Postfix(PlayerControl __instance)
            {
                if (__instance.AmOwner && Helpers.ShowButtons)
                {
                    FastDestroyableSingleton<HudManager>.Instance.ImpostorVentButton.Hide();
                    FastDestroyableSingleton<HudManager>.Instance.SabotageButton.Hide();

                    if (Helpers.ShowButtons)
                    {
                        if (__instance.roleCanUseVents())
                            FastDestroyableSingleton<HudManager>.Instance.ImpostorVentButton.Show();

                        if (__instance.roleCanSabotage())
                        {
                            FastDestroyableSingleton<HudManager>.Instance.SabotageButton.Show();
                            FastDestroyableSingleton<HudManager>.Instance.SabotageButton.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(VentButton), nameof(VentButton.SetTarget))]
        class VentButtonSetTargetPatch
        {
            static Sprite defaultVentSprite = null;
            static void Postfix(VentButton __instance)
            {
                // Trickster render special vent button
                if (Trickster.trickster != null && Trickster.trickster == CachedPlayer.LocalPlayer.PlayerControl)
                {
                    if (defaultVentSprite == null) defaultVentSprite = __instance.graphic.sprite;
                    bool isSpecialVent = __instance.currentTarget != null && __instance.currentTarget.gameObject != null && __instance.currentTarget.gameObject.name.StartsWith("JackInTheBoxVent_");
                    __instance.graphic.sprite = isSpecialVent ? Trickster.getTricksterVentButtonSprite() : defaultVentSprite;
                    __instance.buttonLabelText.enabled = !isSpecialVent;
                }
            }
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
        class KillButtonDoClickPatch
        {
            public static bool Prefix(KillButton __instance)
            {
                if (__instance.isActiveAndEnabled && __instance.currentTarget && !__instance.isCoolingDown && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && CachedPlayer.LocalPlayer.PlayerControl.CanMove)
                {
                    bool showAnimation = true;
                    if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Ninja) && Ninja.isStealthed(CachedPlayer.LocalPlayer.PlayerControl))
                    {
                        showAnimation = false;
                    }

                    // Use an unchecked kill command, to allow shorter kill cooldowns etc. without getting kicked
                    MurderAttemptResult res = Helpers.checkMuderAttemptAndKill(CachedPlayer.LocalPlayer.PlayerControl, __instance.currentTarget, showAnimation: showAnimation);
                    // Handle blank kill
                    if (res == MurderAttemptResult.BlankKill)
                    {
                        CachedPlayer.LocalPlayer.PlayerControl.killTimer = PlayerControl.GameOptions.KillCooldown;
                        if (CachedPlayer.LocalPlayer.PlayerControl == Cleaner.cleaner)
                            Cleaner.cleaner.killTimer = HudManagerStartPatch.cleanerCleanButton.Timer = HudManagerStartPatch.cleanerCleanButton.MaxTimer;
                        else if (CachedPlayer.LocalPlayer.PlayerControl == Warlock.warlock)
                            Warlock.warlock.killTimer = HudManagerStartPatch.warlockCurseButton.Timer = HudManagerStartPatch.warlockCurseButton.MaxTimer;
                        else if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Mini) && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor)
                            CachedPlayer.LocalPlayer.PlayerControl.SetKillTimer(PlayerControl.GameOptions.KillCooldown * (Mini.isGrownUp(CachedPlayer.LocalPlayer.PlayerControl) ? 0.66f : 2f));
                        else if (CachedPlayer.LocalPlayer.PlayerControl == Witch.witch)
                            Witch.witch.killTimer = HudManagerStartPatch.witchSpellButton.Timer = HudManagerStartPatch.witchSpellButton.MaxTimer;
                        else if (CachedPlayer.LocalPlayer.PlayerControl == Assassin.assassin)
                            Assassin.assassin.killTimer = HudManagerStartPatch.assassinButton.Timer = HudManagerStartPatch.assassinButton.MaxTimer;
                    }

                    __instance.SetTarget(null);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.Refresh))]
        class SabotageButtonRefreshPatch
        {
            static void Postfix()
            {
                // Mafia disable sabotage button for Janitor and sometimes for Mafioso
                bool blockSabotageJanitor = (Janitor.janitor != null && Janitor.janitor == CachedPlayer.LocalPlayer.PlayerControl);
                bool blockSabotageMafioso = (Mafioso.mafioso != null && Mafioso.mafioso == CachedPlayer.LocalPlayer.PlayerControl && Godfather.godfather != null && !Godfather.godfather.Data.IsDead);
                if (blockSabotageJanitor || blockSabotageMafioso)
                {
                    FastDestroyableSingleton<HudManager>.Instance.SabotageButton.SetDisabled();
                }
            }
        }

        [HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.DoClick))]
        public static class SabotageButtonDoClickPatch
        {
            public static bool Prefix(SabotageButton __instance)
            {
                // The sabotage button behaves just fine if it's a regular impostor
                if (CachedPlayer.LocalPlayer.PlayerControl.Data.Role.TeamType == RoleTeamTypes.Impostor) return true;

                FastDestroyableSingleton<HudManager>.Instance.ShowMap((Il2CppSystem.Action<MapBehaviour>)((m) => { m.ShowSabotageMap(); }));
                return false;
            }
        }

        [HarmonyPatch(typeof(UseButton), nameof(UseButton.SetTarget))]
        class UseButtonSetTargetPatch
        {
            static bool Prefix(UseButton __instance, [HarmonyArgument(0)] IUsable target)
            {
                PlayerControl pc = CachedPlayer.LocalPlayer.PlayerControl;
                __instance.enabled = true;

                if (IsBlocked(target, pc))
                {
                    __instance.currentTarget = null;
                    __instance.buttonLabelText.text = ModTranslation.getString("buttonBlocked");
                    __instance.enabled = false;
                    __instance.graphic.color = Palette.DisabledClear;
                    __instance.graphic.material.SetFloat("_Desat", 0f);
                    return false;
                }

                __instance.currentTarget = target;
                return true;
            }
        }

        [HarmonyPatch(typeof(EmergencyMinigame), nameof(EmergencyMinigame.Update))]
        class EmergencyMinigameUpdatePatch
        {
            static void Postfix(EmergencyMinigame __instance)
            {
                var roleCanCallEmergency = true;
                var statusText = "";

                // Deactivate emergency button for GM
                if (CachedPlayer.LocalPlayer.PlayerControl.isGM())
                {
                    roleCanCallEmergency = false;
                    statusText = ModTranslation.getString("gmMeetingButton");
                }

                // Deactivate emergency button for FortuneTeller
                if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.FortuneTeller) && FortuneTeller.isCompletedNumTasks(CachedPlayer.LocalPlayer.PlayerControl))
                {
                    roleCanCallEmergency = false;
                    statusText = ModTranslation.getString("fortuneTellerMeetingButton");
                }

                // Deactivate emergency button for Swapper
                if (Swapper.swapper != null && Swapper.swapper == CachedPlayer.LocalPlayer.PlayerControl && !Swapper.canCallEmergency)
                {
                    roleCanCallEmergency = false;
                    statusText = ModTranslation.getString("swapperMeetingButton");
                }

                // Potentially deactivate emergency button for Jester
                if (Jester.jester != null && Jester.jester == CachedPlayer.LocalPlayer.PlayerControl && !Jester.canCallEmergency)
                {
                    roleCanCallEmergency = false;
                    statusText = ModTranslation.getString("jesterMeetingButton");
                }

                // Potentially deactivate emergency button for Lawyer
                if (Lawyer.lawyer != null && Lawyer.lawyer == CachedPlayer.LocalPlayer.PlayerControl && Lawyer.winsAfterMeetings)
                {
                    roleCanCallEmergency = false;
                    statusText = String.Format(ModTranslation.getString("lawyerMeetingButton"), Lawyer.neededMeetings - Lawyer.meetings);
                }

                if (!roleCanCallEmergency)
                {
                    __instance.StatusText.text = statusText;
                    __instance.NumberText.text = string.Empty;
                    __instance.ClosedLid.gameObject.SetActive(true);
                    __instance.OpenLid.gameObject.SetActive(false);
                    __instance.ButtonActive = false;
                    return;
                }

                // Handle max number of meetings
                if (__instance.state == 1)
                {
                    int localRemaining = CachedPlayer.LocalPlayer.PlayerControl.RemainingEmergencies;
                    int teamRemaining = Mathf.Max(0, maxNumberOfMeetings - meetingsCount);
                    int remaining = Mathf.Min(localRemaining, (Mayor.mayor != null && Mayor.mayor == CachedPlayer.LocalPlayer.PlayerControl) ? 1 : teamRemaining);

                    __instance.StatusText.text = "<size=100%>" + String.Format(ModTranslation.getString("meetingStatus"), CachedPlayer.LocalPlayer.PlayerControl.name) + "</size>";
                    __instance.NumberText.text = String.Format(ModTranslation.getString("meetingCount"), localRemaining.ToString(), teamRemaining.ToString());
                    __instance.ButtonActive = remaining > 0;
                    __instance.ClosedLid.gameObject.SetActive(!__instance.ButtonActive);
                    __instance.OpenLid.gameObject.SetActive(__instance.ButtonActive);
                    return;
                }
            }
        }


        public static class ConsolePatch
        {
            [HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
            public static class ConsoleCanUsePatch
            {

                public static bool Prefix(ref float __result, Console __instance, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
                {
                    canUse = couldUse = false;
                    __result = float.MaxValue;

                    //if (IsBlocked(__instance, pc.Object)) return false;
                    if (__instance.AllowImpostor) return true;
                    if (!pc.Object.hasFakeTasks()) return true;

                    return false;
                }
            }

            [HarmonyPatch(typeof(Console), nameof(Console.Use))]
            public static class ConsoleUsePatch
            {
                public static bool Prefix(Console __instance)
                {
                    return !IsBlocked(__instance, CachedPlayer.LocalPlayer.PlayerControl);
                }
            }
        }

        public class SystemConsolePatch
        {
            [HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.CanUse))]
            public static class SystemConsoleCanUsePatch
            {
                public static bool Prefix(ref float __result, SystemConsole __instance, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
                {
                    canUse = couldUse = false;
                    __result = float.MaxValue;
                    //if (IsBlocked(__instance, pc.Object)) return false;

                    return true;
                }
            }

            [HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.Use))]
            public static class SystemConsoleUsePatch
            {
                public static bool Prefix(SystemConsole __instance)
                {
                    return !IsBlocked(__instance, CachedPlayer.LocalPlayer.PlayerControl);
                }
            }
        }


    }

    [HarmonyPatch(typeof(MedScanMinigame), nameof(MedScanMinigame.FixedUpdate))]
    class MedScanMinigameFixedUpdatePatch
    {
        static void Prefix(MedScanMinigame __instance)
        {
            if (MapOptions.allowParallelMedBayScans)
            {
                __instance.medscan.CurrentUser = CachedPlayer.LocalPlayer.PlayerControl.PlayerId;
                __instance.medscan.UsersList.Clear();
            }
        }
    }

}
