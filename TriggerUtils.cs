using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        private bool IsValidStartTriggerName(string triggerName)
        {
            try
            {
                if (string.IsNullOrEmpty(triggerName)) return false;
                string[] validStartTriggers = { "map_start", "s1_start", "stage1_start", "timer_startzone", "zone_start", currentMapStartTrigger };
                return validStartTriggers.Contains(triggerName);
            }
            catch (NullReferenceException ex)
            {
                SharpTimerError($"Null ref in IsValidStartTriggerName: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in IsValidStartTriggerName: {ex.Message}");
                return false;
            }
        }

        private (bool valid, int X) IsValidStartBonusTriggerName(string triggerName)
        {
            try
            {
                if (string.IsNullOrEmpty(triggerName)) return (false, 0);

                string[] patterns = {
                    @"^b([1-9][0-9]?|onus[1-9][0-9]?)_start$",
                    @"^timer_bonus([1-9][0-9]?)_startzone$"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(triggerName, pattern);
                    if (match.Success)
                    {
                        int X = int.Parse(match.Groups[1].Value);
                        return (true, X);
                    }
                }

                return (false, 0);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in IsValidStartBonusTriggerName: {ex.Message}");
                return (false, 0);
            }
        }

        private (bool valid, int X) IsValidStageTriggerName(string triggerName)
        {
            try
            {
                if (string.IsNullOrEmpty(triggerName)) return (false, 0);

                string[] patterns = {
                    @"^s([1-9][0-9]?|tage[1-9][0-9]?)_start$",
                    @"^map_start$",
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(triggerName, pattern);
                    if (match.Success)
                    {
                        if (pattern == @"^map_start$")
                        {
                            // If pattern is "^map_start$", set X to 1
                            return (true, 1);
                        }
                        else
                        {
                            int X = int.Parse(match.Groups[1].Value);
                            return (true, X);
                        }
                    }
                }

                return (false, 0);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in IsValidStageTriggerName: {ex.Message}");
                return (false, 0);
            }
        }

        private (bool valid, int X) IsValidCheckpointTriggerName(string triggerName)
        {
            try
            {
                if (string.IsNullOrEmpty(triggerName)) return (false, 0);

                string[] patterns = {
                    @"^map_cp([1-9][0-9]?)$",
                    @"^map_checkpoint([1-9][0-9]?)$"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(triggerName, pattern);
                    if (match.Success)
                    {
                        useStageTriggers = false;
                        int X = int.Parse(match.Groups[1].Value);
                        return (true, X);
                    }
                }

                return (false, 0);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in IsValidCheckpointTriggerName: {ex.Message}");
                return (false, 0);
            }
        }

        private bool IsValidEndTriggerName(string triggerName)
        {
            try
            {
                if (string.IsNullOrEmpty(triggerName)) return false;
                string[] validEndTriggers = { "map_end", "timer_endzone", "zone_end", currentMapEndTrigger };
                return validEndTriggers.Contains(triggerName);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in IsValidEndTriggerName: {ex.Message}");
                return false;
            }
        }

        private (bool valid, int X) IsValidEndBonusTriggerName(string triggerName, int playerSlot)
        {
            try
            {
                if (string.IsNullOrEmpty(triggerName)) return (false, 0);
                string[] patterns = {
                    @"^b([1-9][0-9]?|onus[1-9][0-9]?)_end$",
                    @"^timer_bonus([1-9][0-9]?)_endzone$"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(triggerName, pattern);
                    if (match.Success)
                    {
                        int X = int.Parse(match.Groups[1].Value);
                        if (X != playerTimers[playerSlot].BonusStage) return (false, 0);
                        return (true, X);
                    }
                }

                return (false, 0);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in IsValidStartTriggerName: {ex.Message}");
                return (false, 0);
            }
        }

        private (Vector?, QAngle?) FindStartTriggerPos()
        {
            currentRespawnPos = null;
            currentRespawnAng = null;
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");
            var info_tps = Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                if (IsValidStartTriggerName(trigger.Entity.Name.ToString()))
                {
                    foreach (CBaseEntity info_tp in info_tps)
                    {
                        if (info_tp.Entity!.Name != null && IsInsideTrigger(trigger.AbsOrigin!, trigger.Collision.BoundingRadius, info_tp.AbsOrigin!))
                        {
                            if (info_tp.CBodyComponent.SceneNode.AbsOrigin == null || info_tp.AbsRotation == null) continue;
                            return (info_tp.CBodyComponent.SceneNode.AbsOrigin, info_tp.AbsRotation);
                        }
                    }
                    if (trigger.CBodyComponent.SceneNode.AbsOrigin == null) continue;
                    return (trigger.CBodyComponent.SceneNode.AbsOrigin, null);
                }
            }
            return (null, null);
        }

        private void FindStageTriggers()
        {
            useStageTriggers = true;
            stageTriggers.Clear();
            stageTriggerPoses.Clear();
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");
            var info_tps = Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                var (validStage, X) = IsValidStageTriggerName(trigger.Entity.Name.ToString());
                if (validStage)
                {
                    foreach (CBaseEntity info_tp in info_tps)
                    {
                        if (info_tp.Entity!.Name != null && IsInsideTrigger(trigger.AbsOrigin!, trigger.Collision.BoundingRadius, info_tp.AbsOrigin!))
                        {
                            if (info_tp.CBodyComponent.SceneNode.AbsOrigin == null || info_tp.AbsRotation == null) continue;
                            stageTriggerPoses[X] = info_tp.CBodyComponent?.SceneNode?.AbsOrigin;
                            stageTriggerAngs[X] = info_tp.AbsRotation;
                            SharpTimerDebug($"Added !stage {X} pos {stageTriggerPoses[X]} ang {stageTriggerAngs[X]}");
                        }
                    }
                    stageTriggers[trigger.Handle] = X;
                    SharpTimerDebug($"Added Stage {X} Trigger {trigger.Handle}");
                }
            }

            stageTriggerCount = stageTriggers.Count;

            if (stageTriggerCount == 1) // if theres only one stage strigger the map is liniear
            {
                stageTriggerCount = 0;
                useStageTriggers = false;
                stageTriggers.Clear();
                stageTriggerPoses.Clear();
                stageTriggerAngs.Clear();
                SharpTimerDebug($"Only one Stage Trigger found. Not enough. Cancelling...");
            }

            SharpTimerDebug($"Found a max of {stageTriggerCount} Stage triggers");
            SharpTimerDebug($"Use stageTriggers is set to {useStageTriggers}");
        }

        private void FindCheckpointTriggers()
        {
            cpTriggers.Clear();
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                var (validCp, X) = IsValidCheckpointTriggerName(trigger.Entity.Name.ToString());
                if (validCp)
                {
                    cpTriggers[trigger.Handle] = X;
                    SharpTimerDebug($"Added Checkpoint {X} Trigger {trigger.Handle}");
                }

            }

            cpTriggerCount = cpTriggers.Count;

            SharpTimerDebug($"Found a max of {cpTriggerCount} Checkpoint triggers");
        }

        private void FindBonusStartTriggerPos()
        {
            bonusRespawnPoses.Clear();
            bonusRespawnAngs.Clear();

            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");
            var info_tps = Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                var (validStartBonus, bonusX) = IsValidStartBonusTriggerName(trigger.Entity.Name.ToString());
                if (validStartBonus)
                {
                    bool bonusPosAndAngSet = false;

                    foreach (CBaseEntity info_tp in info_tps)
                    {
                        if (info_tp.Entity!.Name != null && IsInsideTrigger(trigger.AbsOrigin!, trigger.Collision.BoundingRadius, info_tp.AbsOrigin!))
                        {
                            if (info_tp.CBodyComponent.SceneNode.AbsOrigin == null || info_tp.AbsRotation == null) continue;
                            bonusRespawnPoses[bonusX] = info_tp.CBodyComponent?.SceneNode?.AbsOrigin;
                            bonusRespawnAngs[bonusX] = info_tp.AbsRotation;
                            SharpTimerDebug($"Added Bonus !rb {bonusX} pos {bonusRespawnPoses[bonusX]} ang {bonusRespawnAngs[bonusX]}");
                            bonusPosAndAngSet = true;
                        }
                    }

                    if (!bonusPosAndAngSet && trigger.CBodyComponent.SceneNode.AbsOrigin != null)
                    {
                        bonusRespawnPoses[bonusX] = trigger.CBodyComponent?.SceneNode?.AbsOrigin;
                        SharpTimerDebug($"Added Bonus !rb {bonusX} pos {bonusRespawnPoses[bonusX]}");
                    }
                }
            }
        }

        private void FindTriggerPushData()
        {
            if (triggerPushFixEnabled)
            {
                triggerPushData.Clear();
                var trigger_pushers = Utilities.FindAllEntitiesByDesignerName<CTriggerPush>("trigger_push");

                foreach (var trigger_push in trigger_pushers)
                {
                    if (trigger_push == null)
                    {
                        SharpTimerDebug("Trigger_push was null");
                        continue;
                    }

                    nint handle = trigger_push.Handle;

                    triggerPushData[handle] = new TriggerPushData(
                        trigger_push.PushSpeed,
                        trigger_push.PushEntitySpace,
                        trigger_push.PushDirEntitySpace
                    );

                    trigger_push.PushSpeed = 0.0f;

                    SharpTimerDebug($"Trigger_push {trigger_push.Entity.Name} Speed set to {trigger_push.PushSpeed}");
                }
            }
        }

        private (Vector? startRight, Vector? startLeft, Vector? endRight, Vector? endLeft) FindTriggerCorners()
        {
            var targets = Utilities.FindAllEntitiesByDesignerName<CPointEntity>("info_target");

            Vector? startRight = null;
            Vector? startLeft = null;
            Vector? endRight = null;
            Vector? endLeft = null;

            foreach (var target in targets)
            {
                if (target == null || target.Entity.Name == null) continue;

                switch (target.Entity.Name)
                {
                    case "start_right":
                        startRight = target.AbsOrigin;
                        break;
                    case "start_left":
                        startLeft = target.AbsOrigin;
                        break;
                    case "end_right":
                        endRight = target.AbsOrigin;
                        break;
                    case "end_left":
                        endLeft = target.AbsOrigin;
                        break;
                }
            }

            return (startRight, startLeft, endRight, endLeft);
        }

        public bool IsInsideTrigger(Vector triggerPos, float triggerCollisionRadius, Vector info_tpPos)
        {
            return info_tpPos.X >= triggerPos.X - triggerCollisionRadius && info_tpPos.X <= triggerPos.X + triggerCollisionRadius &&
                   info_tpPos.Y >= triggerPos.Y - triggerCollisionRadius && info_tpPos.Y <= triggerPos.Y + triggerCollisionRadius &&
                   info_tpPos.Z >= triggerPos.Z - triggerCollisionRadius && info_tpPos.Z <= triggerPos.Z + triggerCollisionRadius;
        }
    }
}