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
        private async void ServerRecordADtimer()
        {
            if (isADTimerRunning) return;

            var timer = AddTimer(srTimer, async () =>
            {
                SharpTimerDebug($"Running Server Record AD...");
                Dictionary<string, PlayerRecord> sortedRecords;
                if (useMySQL == false)
                {
                    SharpTimerDebug($"Getting Server Record AD using json");
                    sortedRecords = GetSortedRecords();
                }
                else
                {
                    SharpTimerDebug($"Getting Server Record AD using datanase");
                    sortedRecords = await GetSortedRecordsFromDatabase();
                }

                if (sortedRecords.Count == 0)
                {
                    SharpTimerDebug($"No Server Records for this map yet!");
                    return;
                }

                Server.NextFrame(() => Server.PrintToChatAll($"{msgPrefix} Current Server Record on {ParseColorToSymbol(primaryHUDcolor)}{currentMapName}{ChatColors.White}: "));

                foreach (var kvp in sortedRecords.Take(1))
                {
                    string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                    int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                    Server.NextFrame(() => Server.PrintToChatAll(msgPrefix + $" {ParseColorToSymbol(primaryHUDcolor)}{playerName} {ChatColors.White}- {ParseColorToSymbol(primaryHUDcolor)}{FormatTime(timerTicks)}"));
                }

            }, TimerFlags.REPEAT);
            isADTimerRunning = true;
        }

        public void SharpTimerDebug(string msg)
        {
            if (enableDebug == true) Console.WriteLine($"\u001b[33m[SharpTimerDebug] \u001b[37m{msg}");
        }

        public void SharpTimerError(string msg)
        {
            Console.WriteLine($"\u001b[31m[SharpTimerERROR] \u001b[37m{msg}");
        }

        public void SharpTimerConPrint(string msg)
        {
            Console.WriteLine($"\u001b[36m[SharpTimer] \u001b[37m{msg}");
        }

        public void PrintAllEnabledCommands(CCSPlayerController player)
        {
            SharpTimerDebug($"Printing Commands for {player.PlayerName}");
            player.PrintToChat($"{msgPrefix}Available Commands:");

            if (respawnEnabled) player.PrintToChat($"{msgPrefix}!r (css_r) - Respawns you");
            if (topEnabled) player.PrintToChat($"{msgPrefix}!top (css_top) - Lists top 10 records on this map");
            if (rankEnabled) player.PrintToChat($"{msgPrefix}!rank (css_rank) - Shows your current rank and pb");
            if (goToEnabled) player.PrintToChat($"{msgPrefix}!goto <name> (css_goto) - Teleports you to a player");

            if (cpEnabled)
            {
                player.PrintToChat($"{msgPrefix}!cp (css_cp) - Sets a Checkpoint");
                player.PrintToChat($"{msgPrefix}!tp (css_tp) - Teleports you to the last Checkpoint");
                player.PrintToChat($"{msgPrefix}!prevcp (css_prevcp) - Teleports you one Checkpoint back");
                player.PrintToChat($"{msgPrefix}!nextcp (css_nextcp) - Teleports you one Checkpoint forward");
            }
        }

        public void TimerOnTick(CCSPlayerController player, int playerSlot)
        {
            if (!IsAllowedPlayer(player))
            {
                playerTimers[playerSlot].IsTimerRunning = false;
                playerTimers[playerSlot].TimerTicks = 0;
                playerCheckpoints.Remove(playerSlot);
                playerTimers[playerSlot].TicksSinceLastCmd++;
                return;
            }

            var buttons = player.Buttons;
            var formattedPlayerVel = Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()).ToString().PadLeft(4, '0');
            var formattedPlayerPre = Math.Round(ParseVector(playerTimers[playerSlot].PreSpeed ?? "0 0 0").Length2D()).ToString().PadLeft(3, '0');
            var playerTime = FormatTime(playerTimers[playerSlot].TimerTicks);
            var playerBonusTime = FormatTime(playerTimers[playerSlot].BonusTimerTicks);
            var timerLine = playerTimers[playerSlot].IsBonusTimerRunning
                ? $" <font color='gray' class='fontSize-s'>Bonus: {playerTimers[playerSlot].BonusStage}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerBonusTime}</font> <br>"
                : playerTimers[playerSlot].IsTimerRunning
                    ? $" <font color='gray' class='fontSize-s'>{GetPlayerPlacement(player)}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerTime}</font>{(playerTimers[playerSlot].CurrentStage != 0 ? $"<font color='gray' class='fontSize-s'> {playerTimers[playerSlot].CurrentStage}/{stageTriggerCount}</font>" : "")} <br>"
                    : "";

            var veloLine = $" {(playerTimers[playerSlot].IsTester ? playerTimers[playerSlot].TesterSparkleGif : "")}<font class='fontSize-s' color='{tertiaryHUDcolor}'>Speed:</font> <font class='fontSize-l' color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font>{(playerTimers[playerSlot].IsTester ? playerTimers[playerSlot].TesterSparkleGif : "")} <br>";
            var veloLineAlt = $" {GetSpeedBar(Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()))} ";
            var infoLine = $"{playerTimers[playerSlot].RankHUDString}" +
                              $"{(currentMapTier != null ? $" | Tier: {currentMapTier}" : "")}" +
                              $"{(currentMapType != null ? $" | {currentMapType}" : "")} |</font> ";

            var forwardKey = playerTimers[playerSlot].Azerty ? "Z" : "W";
            var leftKey = playerTimers[playerSlot].Azerty ? "Q" : "A";
            var backKey = "S";
            var rightKey = "D";

            var keysLineNoHtml = $"{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
                                    $"{((buttons & PlayerButtons.Forward) != 0 ? forwardKey : "_")} " +
                                    $"{((buttons & PlayerButtons.Moveright) != 0 ? rightKey : "_")} " +
                                    $"{((buttons & PlayerButtons.Back) != 0 ? backKey : "_")} " +
                                    $"{((buttons & PlayerButtons.Jump) != 0 ? "J" : "_")} " +
                                    $"{((buttons & PlayerButtons.Duck) != 0 ? "C" : "_")}";

            var hudContentBuilder = new StringBuilder();
            hudContentBuilder.Append(timerLine);
            hudContentBuilder.Append(veloLine);
            if (alternativeSpeedometer) hudContentBuilder.Append(veloLineAlt);
            hudContentBuilder.Append(infoLine);
            if (playerTimers[playerSlot].IsTester && !playerTimers[playerSlot].IsTimerRunning && !playerTimers[playerSlot].IsBonusTimerRunning)
                hudContentBuilder.Append(playerTimers[playerSlot].TesterPausedGif);

            var hudContent = hudContentBuilder.ToString();

            if (playerTimers[playerSlot].HideTimerHud != true)
            {
                player.PrintToCenterHtml(hudContent);
            }

            if (playerTimers[playerSlot].HideKeys != true)
            {
                player.PrintToCenter(keysLineNoHtml);
            }

            if (playerTimers[playerSlot].IsTimerRunning)
            {
                playerTimers[playerSlot].TimerTicks++;
            }
            else if (playerTimers[playerSlot].IsBonusTimerRunning)
            {
                playerTimers[playerSlot].BonusTimerTicks++;
            }

            if (!useTriggers)
            {
                CheckPlayerCoords(player);
            }

            if (playerTimers[playerSlot].MovementService != null && removeCrouchFatigueEnabled == true)
            {
                if (playerTimers[playerSlot].MovementService.DuckSpeed != 7.0f)
                {
                    playerTimers[playerSlot].MovementService.DuckSpeed = 7.0f;
                }
            }

            //find a better solution for this by combining all into 1 string to store
            if (playerTimers[playerSlot].RankHUDString == null && playerTimers[playerSlot].IsRankPbCached == false)
            {
                SharpTimerDebug($"{player.PlayerName} has rank and pb null... calling handler");
                _ = RankCommandHandler(player, player.SteamID.ToString(), playerSlot, player.PlayerName, true);
                playerTimers[playerSlot].IsRankPbCached = true;
            }

            if (removeCollisionEnabled == true)
            {
                if (player.PlayerPawn.Value.Collision.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING || player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING)
                {
                    SharpTimerDebug($"{player.PlayerName} has wrong collision group... RemovePlayerCollision");
                    RemovePlayerCollision(player);
                }
            }

            if (!player.PlayerPawn.Value.OnGroundLastTick)
            {
                playerTimers[playerSlot].TicksInAir++;
                if (playerTimers[playerSlot].TicksInAir == 1)
                {
                    playerTimers[playerSlot].PreSpeed = $"{player.PlayerPawn.Value.AbsVelocity.X} {player.PlayerPawn.Value.AbsVelocity.Y} {player.PlayerPawn.Value.AbsVelocity.Z}";
                }
            }
            else
            {
                playerTimers[playerSlot].TicksInAir = 0;
            }

            playerTimers[playerSlot].TicksSinceLastCmd++;
        }

        private void CheckPlayerCoords(CCSPlayerController? player)
        {
            if (player == null || !IsAllowedPlayer(player))
            {
                return;
            }

            Vector incorrectVector = new Vector(0, 0, 0);
            Vector playerPos = player.Pawn?.Value.CBodyComponent?.SceneNode.AbsOrigin ?? incorrectVector;

            if (new[] { playerPos, currentMapStartC1, currentMapStartC2, currentMapEndC1, currentMapEndC2 }.All(v => v != incorrectVector))
            {
                bool isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
                bool isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);

                if (!isInsideStartBox && isInsideEndBox)
                {
                    OnTimerStop(player);
                }
                else if (isInsideStartBox)
                {
                    OnTimerStart(player);

                    if (maxStartingSpeedEnabled == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed)
                    {
                        AdjustPlayerVelocity(player, maxStartingSpeed, true);
                    }
                }
            }
        }

        private void HandlePlayerStageTimes(CCSPlayerController player, nint triggerHandle)
        {
            if (!IsAllowedPlayer(player)) return;

            if (playerTimers[player.Slot].CurrentStage == stageTriggers[triggerHandle])
            {
                playerTimers[player.Slot].CurrentStage = stageTriggers[triggerHandle];
                return;
            }

            SharpTimerDebug($"Player {player.PlayerName} has a stage trigger with handle {triggerHandle}");
            int previousStageTime = GetStageTime(player.SteamID.ToString(), stageTriggers[triggerHandle]);

            if (previousStageTime != 0)
            {
                player.PrintToChat(msgPrefix + $" {ParseColorToSymbol(primaryHUDcolor)}[{FormatTime(playerTimers[player.Slot].TimerTicks)}{ChatColors.White}] [{FormatTimeDifference(playerTimers[player.Slot].TimerTicks, previousStageTime)}{ChatColors.White}]");
            }

            if (playerTimers[player.Slot].StageRecords != null && playerTimers[player.Slot].IsTimerRunning == true)
            {
                playerTimers[player.Slot].StageRecords[stageTriggers[triggerHandle]] = playerTimers[player.Slot].TimerTicks;
                SharpTimerDebug($"Player {player.PlayerName} Entering stage {stageTriggers[triggerHandle]} Time {playerTimers[player.Slot].StageRecords[stageTriggers[triggerHandle]]}");
            }

            playerTimers[player.Slot].CurrentStage = stageTriggers[triggerHandle];
        }

        public int GetStageTime(string steamId, int stageIndex)
        {
            try
            {
                string fileName = $"{currentMapName.ToLower()}_stage_times.json";
                string playerStageRecordsPath = Path.Join(gameDir + "/csgo/cfg/SharpTimer/PlayerStageData", fileName);

                if (!File.Exists(playerStageRecordsPath))
                {
                    //file doesnt exist so create it
                    File.WriteAllText(playerStageRecordsPath, "{}");
                }

                using (var stream = File.OpenRead(playerStageRecordsPath))
                using (var document = JsonDocument.Parse(stream))
                {
                    var root = document.RootElement;
                    if (root.TryGetProperty(steamId, out var playerRecord))
                    {
                        if (playerRecord.TryGetProperty("StageRecords", out var stageRecords) &&
                            stageRecords.TryGetProperty(stageIndex.ToString(), out var stageTime))
                        {
                            return stageTime.GetInt32();
                        }
                        else
                        {
                            SharpTimerDebug($"Stage {stageIndex} not found for SteamID {steamId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"An error occurred in GetStageTime: {ex.Message}");
            }

            return 0;
        }

        private void DumpPlayerStageTimesToJson(CCSPlayerController player)
        {
            string fileName = $"{currentMapName.ToLower()}_stage_times.json";
            string playerStageRecordsPath = Path.Join(gameDir + "/csgo/cfg/SharpTimer/PlayerStageData", fileName);

            var stageTimes = playerTimers[player.Slot].StageRecords;

            var playerStageData = new Dictionary<string, object>
            {
                { "StageRecords", stageTimes }
            };

            if (File.Exists(playerStageRecordsPath))
            {
                var existingData = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(playerStageRecordsPath));

                existingData[player.SteamID.ToString()] = playerStageData;

                string jsonData = JsonSerializer.Serialize(existingData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(playerStageRecordsPath, jsonData);
            }
            else
            {
                var newData = new Dictionary<string, object>
                {
                    { player.SteamID.ToString(), playerStageData }
                };

                string jsonData = JsonSerializer.Serialize(newData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(playerStageRecordsPath, jsonData);
            }

            SharpTimerDebug($"Player {player.PlayerName} stage times for map {currentMapName} dumped to {playerStageRecordsPath}");
        }

        private string GetSpeedBar(double speed)
        {
            const int barLength = 80;

            int barProgress = (int)Math.Round((speed / altVeloMaxSpeed) * barLength);
            StringBuilder speedBar = new StringBuilder(barLength);

            for (int i = 0; i < barLength; i++)
            {
                if (i < barProgress)
                {
                    speedBar.Append($"<font class='fontSize-s' color='{(speed >= altVeloMaxSpeed ? GetRainbowColor() : primaryHUDcolor)}'>|</font>");
                }
                else
                {
                    speedBar.Append($"<font class='fontSize-s' color='{secondaryHUDcolor}'>|</font>");
                }
            }

            return $"{speedBar}<br>";
        }

        private string GetRainbowColor()
        {
            const double rainbowPeriod = 2.0;

            double percentage = (Server.EngineTime % rainbowPeriod) / rainbowPeriod;
            double red = Math.Sin(2 * Math.PI * (percentage)) * 127 + 128;
            double green = Math.Sin(2 * Math.PI * (percentage + 1.0 / 3.0)) * 127 + 128;
            double blue = Math.Sin(2 * Math.PI * (percentage + 2.0 / 3.0)) * 127 + 128;

            int intRed = (int)Math.Round(red);
            int intGreen = (int)Math.Round(green);
            int intBlue = (int)Math.Round(blue);

            return $"#{intRed:X2}{intGreen:X2}{intBlue:X2}";
        }

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
                    @"^map_cp([1-9][0-9]?)$",
                    @"^map_checkpoint([1-9][0-9]?)$",
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
                SharpTimerError($"Exception in IsValidStageTriggerName: {ex.Message}");
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

        private bool IsAllowedPlayer(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.Pawn == null || !player.PlayerPawn.IsValid || !player.PawnIsAlive || player.IsBot)
            {
                return false;
            }

            CsTeam teamNum = (CsTeam)player.TeamNum;
            bool isTeamValid = teamNum == CsTeam.CounterTerrorist || teamNum == CsTeam.Terrorist;

            bool isTeamSpectatorOrNone = teamNum != CsTeam.Spectator && teamNum != CsTeam.None;
            bool isConnected = connectedPlayers.ContainsKey(player.Slot) || playerTimers.ContainsKey(player.Slot);

            return isTeamValid && isTeamSpectatorOrNone && isConnected;
        }

        private bool IsPlayerATester(string steamId)
        {
            return testerPersonalGifs.ContainsKey(steamId);
        }

        public static Tuple<string, string> GetTesterPersonalGif(string steamId)
        {
            if (testerPersonalGifs.ContainsKey(steamId))
            {
                return testerPersonalGifs[steamId];
            }
            else
            {
                return Tuple.Create("Index not found", "");
            }
        }

        public void HandleTesterGifs(int playerSlot, string steamId)
        {
            Tuple<string, string> gifs = GetTesterPersonalGif(steamId);
            playerTimers[playerSlot].TesterSparkleGif = gifs.Item1;
            playerTimers[playerSlot].TesterPausedGif = gifs.Item2;
        }

        private static string FormatTime(int ticks)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(ticks / 64.0);

            string milliseconds = $"{(ticks % 64) * (1000.0 / 64.0):000}";

            int totalMinutes = (int)timeSpan.TotalMinutes;
            if (totalMinutes >= 60)
            {
                return $"{totalMinutes / 60:D1}:{totalMinutes % 60:D2}:{timeSpan.Seconds:D2}.{milliseconds}";
            }

            return $"{totalMinutes:D1}:{timeSpan.Seconds:D2}.{milliseconds}";
        }

        private static string FormatTimeDifference(int currentTicks, int previousTicks)
        {
            int differenceTicks = previousTicks - currentTicks;
            string sign = (differenceTicks > 0) ? "-" : "+";
            char signColor = (differenceTicks > 0) ? ChatColors.Green : ChatColors.Red;

            TimeSpan timeDifference = TimeSpan.FromSeconds(Math.Abs(differenceTicks) / 64.0);

            // Format seconds with three decimal points
            string secondsWithMilliseconds = $"{timeDifference.Seconds:D2}.{(Math.Abs(differenceTicks) % 64) * (1000.0 / 64.0):000}";

            int totalDifferenceMinutes = (int)timeDifference.TotalMinutes;
            if (totalDifferenceMinutes >= 60)
            {
                return $"{signColor}{sign}{totalDifferenceMinutes / 60:D1}:{totalDifferenceMinutes % 60:D2}:{secondsWithMilliseconds}";
            }

            return $"{signColor}{sign}{totalDifferenceMinutes:D1}:{secondsWithMilliseconds}";
        }

        string ParseColorToSymbol(string input)
        {
            Dictionary<string, string> colorNameSymbolMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
             {
                 { "white", "" },
                 { "darkred", "" },
                 { "purple", "" },
                 { "darkgreen", "" },
                 { "lightgreen", "" },
                 { "green", "" },
                 { "red", "" },
                 { "lightgray", "" },
                 { "orange", "" },
                 { "darkpurple", "" },
                 { "lightred", "" }
             };

            string lowerInput = input.ToLower();

            if (colorNameSymbolMap.TryGetValue(lowerInput, out var symbol))
            {
                return symbol;
            }

            if (IsHexColorCode(input))
            {
                return ParseHexToSymbol(input);
            }

            return "\u0010";
        }

        bool IsHexColorCode(string input)
        {
            if (input.StartsWith("#") && (input.Length == 7 || input.Length == 9))
            {
                try
                {
                    Color color = ColorTranslator.FromHtml(input);
                    return true;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error parsing hex color code: {ex.Message}");
                }
            }
            else
            {
                SharpTimerError("Invalid hex color code format. Please check SharpTimer/config.cfg");
            }

            return false;
        }

        static string ParseHexToSymbol(string hexColorCode)
        {
            Color color = ColorTranslator.FromHtml(hexColorCode);

            Dictionary<string, string> predefinedColors = new Dictionary<string, string>
            {
                { "#FFFFFF", "" },  // White
                { "#8B0000", "" },  // Dark Red
                { "#800080", "" },  // Purple
                { "#006400", "" },  // Dark Green
                { "#00FF00", "" },  // Light Green
                { "#008000", "" },  // Green
                { "#FF0000", "" },  // Red
                { "#D3D3D3", "" },  // Light Gray
                { "#FFA500", "" },  // Orange
                { "#780578", "" },  // Dark Purple
                { "#FF4500", "" }   // Light Red
            };

            hexColorCode = hexColorCode.ToUpper();

            if (predefinedColors.TryGetValue(hexColorCode, out var colorName))
            {
                return colorName;
            }

            Color targetColor = ColorTranslator.FromHtml(hexColorCode);
            string closestColor = FindClosestColor(targetColor, predefinedColors.Keys);

            if (predefinedColors.TryGetValue(closestColor, out var symbol))
            {
                return symbol;
            }

            return "";
        }

        static string FindClosestColor(Color targetColor, IEnumerable<string> colorHexCodes)
        {
            double minDistance = double.MaxValue;
            string closestColor = null;

            foreach (var hexCode in colorHexCodes)
            {
                Color color = ColorTranslator.FromHtml(hexCode);
                double distance = ColorDistance(targetColor, color);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestColor = hexCode;
                }
            }

            return closestColor;
        }

        static double ColorDistance(Color color1, Color color2)
        {
            int rDiff = color1.R - color2.R;
            int gDiff = color1.G - color2.G;
            int bDiff = color1.B - color2.B;

            return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        public void DrawLaserBetween(Vector startPos, Vector endPos)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null)
            {
                SharpTimerDebug($"Failed to create beam...");
                return;
            }

            if (IsHexColorCode(primaryHUDcolor))
            {
                beam.Render = ColorTranslator.FromHtml(primaryHUDcolor);
            }
            else
            {
                beam.Render = Color.FromName(primaryHUDcolor);
            }

            beam.Width = 1.5f;

            beam.Teleport(startPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));

            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;

            beam.DispatchSpawn();
            SharpTimerDebug($"Beam Spawned at S:{startPos} E:{beam.EndPos}");
        }

        public void DrawWireframe2D(Vector corner1, Vector corner2, float height = 50)
        {
            Vector corner3 = new Vector(corner2.X, corner1.Y, corner1.Z);
            Vector corner4 = new Vector(corner1.X, corner2.Y, corner1.Z);

            Vector corner1_top = new Vector(corner1.X, corner1.Y, corner1.Z + height);
            Vector corner2_top = new Vector(corner2.X, corner2.Y, corner2.Z + height);
            Vector corner3_top = new Vector(corner2.X, corner1.Y, corner1.Z + height);
            Vector corner4_top = new Vector(corner1.X, corner2.Y, corner1.Z + height);

            DrawLaserBetween(corner1, corner3);
            DrawLaserBetween(corner1, corner4);
            DrawLaserBetween(corner2, corner3);
            DrawLaserBetween(corner2, corner4);

            DrawLaserBetween(corner1_top, corner3_top);
            DrawLaserBetween(corner1_top, corner4_top);
            DrawLaserBetween(corner2_top, corner3_top);
            DrawLaserBetween(corner2_top, corner4_top);

            DrawLaserBetween(corner1, corner1_top);
            DrawLaserBetween(corner2, corner2_top);
            DrawLaserBetween(corner3, corner3_top);
            DrawLaserBetween(corner4, corner4_top);
        }

        public void DrawWireframe3D(Vector corner1, Vector corner8)
        {
            Vector corner2 = new Vector(corner1.X, corner8.Y, corner1.Z);
            Vector corner3 = new Vector(corner8.X, corner8.Y, corner1.Z);
            Vector corner4 = new Vector(corner8.X, corner1.Y, corner1.Z);

            Vector corner5 = new Vector(corner8.X, corner1.Y, corner8.Z);
            Vector corner6 = new Vector(corner1.X, corner1.Y, corner8.Z);
            Vector corner7 = new Vector(corner1.X, corner8.Y, corner8.Z);

            //top square
            DrawLaserBetween(corner1, corner2);
            DrawLaserBetween(corner2, corner3);
            DrawLaserBetween(corner3, corner4);
            DrawLaserBetween(corner4, corner1);

            //bottom square
            DrawLaserBetween(corner5, corner6);
            DrawLaserBetween(corner6, corner7);
            DrawLaserBetween(corner7, corner8);
            DrawLaserBetween(corner8, corner5);

            //connect them both to build a cube
            DrawLaserBetween(corner1, corner6);
            DrawLaserBetween(corner2, corner7);
            DrawLaserBetween(corner3, corner8);
            DrawLaserBetween(corner4, corner5);
        }

        private bool IsVectorInsideBox(Vector playerVector, Vector corner1, Vector corner2)
        {
            float minX = Math.Min(corner1.X, corner2.X);
            float minY = Math.Min(corner1.Y, corner2.Y);
            float minZ = Math.Min(corner1.Z, corner2.Z);

            float maxX = Math.Max(corner1.X, corner2.X);
            float maxY = Math.Max(corner1.Y, corner2.Y);
            float maxZ = Math.Max(corner1.Z, corner1.Z);

            return playerVector.X >= minX && playerVector.X <= maxX &&
                   playerVector.Y >= minY && playerVector.Y <= maxY &&
                   playerVector.Z >= minZ && playerVector.Z <= maxZ + fakeTriggerHeight;
        }

        private void AdjustPlayerVelocity(CCSPlayerController? player, float velocity, bool forceNoDebug = false)
        {
            if (!IsAllowedPlayer(player)) return;

            var currentX = player.PlayerPawn.Value.AbsVelocity.X;
            var currentY = player.PlayerPawn.Value.AbsVelocity.Y;
            var currentSpeed2D = Math.Sqrt(currentX * currentX + currentY * currentY);
            var normalizedX = currentX / currentSpeed2D;
            var normalizedY = currentY / currentSpeed2D;
            var adjustedX = normalizedX * velocity; // Adjusted speed limit
            var adjustedY = normalizedY * velocity; // Adjusted speed limit
            player.PlayerPawn.Value.AbsVelocity.X = (float)adjustedX;
            player.PlayerPawn.Value.AbsVelocity.Y = (float)adjustedY;
            if (!forceNoDebug) SharpTimerDebug($"Adjusted Velo for {player.PlayerName} to {player.PlayerPawn.Value.AbsVelocity}");
        }

        private void RemovePlayerCollision(CCSPlayerController? player)
        {
            if (removeCollisionEnabled == false || player == null) return;

            player.PlayerPawn.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
            player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
            VirtualFunctionVoid<nint> collisionRulesChanged = new VirtualFunctionVoid<nint>(player.PlayerPawn.Value.Handle, OnCollisionRulesChangedOffset.Get());
            collisionRulesChanged.Invoke(player.PlayerPawn.Value.Handle);
            SharpTimerDebug($"Removed Collison for {player.PlayerName}");
        }

        private Vector? FindStartTriggerPos()
        {
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                if (IsValidStartTriggerName(trigger.Entity.Name.ToString()))
                {
                    return trigger.CBodyComponent?.SceneNode?.AbsOrigin;
                }
            }
            return null;
        }

        private void FindStageTriggers()
        {
            stageTriggers.Clear();
            stageTriggerPoses.Clear();
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                var (validStage, X) = IsValidStageTriggerName(trigger.Entity.Name.ToString());
                if (validStage)
                {
                    stageTriggers[trigger.Handle] = X;
                    if (trigger.CBodyComponent?.SceneNode?.AbsOrigin == null) continue;
                    stageTriggerPoses[X] = trigger.CBodyComponent?.SceneNode?.AbsOrigin;
                    SharpTimerDebug($"Added Stage {X} Trigger {trigger.Handle}");
                }
            }
            stageTriggerCount = stageTriggers.Count;
            SharpTimerDebug($"Found a max of {stageTriggerCount} Stage triggers");
        }

        private void FindBonusStartTriggerPos()
        {
            bonusRespawnPoses.Clear();
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                var (validStartBonus, bonusX) = IsValidStartBonusTriggerName(trigger.Entity.Name.ToString());
                if (validStartBonus)
                {
                    if (trigger.CBodyComponent?.SceneNode?.AbsOrigin == null) continue;
                    bonusRespawnPoses[bonusX] = trigger.CBodyComponent?.SceneNode?.AbsOrigin;
                    SharpTimerDebug($"Added Bonus !rb {bonusX} pos {bonusRespawnPoses[bonusX]}");
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

        private static Vector ParseVector(string vectorString)
        {
            const char separator = ' ';

            var values = vectorString.Split(separator);

            if (values.Length == 3 &&
                float.TryParse(values[0], out float x) &&
                float.TryParse(values[1], out float y) &&
                float.TryParse(values[2], out float z))
            {
                return new Vector(x, y, z);
            }

            return new Vector(0, 0, 0);
        }

        private static QAngle ParseQAngle(string qAngleString)
        {
            const char separator = ' ';

            var values = qAngleString.Split(separator);

            if (values.Length == 3 &&
                float.TryParse(values[0], out float pitch) &&
                float.TryParse(values[1], out float yaw) &&
                float.TryParse(values[2], out float roll))
            {
                return new QAngle(pitch, yaw, roll);
            }

            return new QAngle(0, 0, 0);
        }

        public Dictionary<string, PlayerRecord> GetSortedRecords(int bonusX = 0)
        {
            string currentMapName = bonusX == 0 ? Server.MapName : $"{Server.MapName}_bonus{bonusX}";

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(playerRecordsPath))
            {
                string json = File.ReadAllText(playerRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json) ?? new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }
            else
            {
                records = new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }

            if (records.ContainsKey(currentMapName))
            {
                var sortedRecords = records[currentMapName]
                    .OrderBy(record => record.Value.TimerTicks)
                    .ToDictionary(record => record.Key, record => new PlayerRecord
                    {
                        PlayerName = record.Value.PlayerName,
                        TimerTicks = record.Value.TimerTicks
                    });

                return sortedRecords;
            }

            return new Dictionary<string, PlayerRecord>();
        }

        private int GetPreviousPlayerRecord(CCSPlayerController? player, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return 0;

            string currentMapName = bonusX == 0 ? Server.MapName : $"{Server.MapName}_bonus{bonusX}";

            string steamId = player.SteamID.ToString();

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(playerRecordsPath))
            {
                string json = File.ReadAllText(playerRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json) ?? new Dictionary<string, Dictionary<string, PlayerRecord>>();

                if (records.ContainsKey(currentMapName) && records[currentMapName].ContainsKey(steamId))
                {
                    return records[currentMapName][steamId].TimerTicks;
                }
            }

            return 0;
        }

        public string GetPlayerPlacement(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || !playerTimers[player.Slot].IsTimerRunning) return "";


            int currentPlayerTime = playerTimers[player.Slot].TimerTicks;

            int placement = 1;

            foreach (var kvp in playerTimers[player.Slot].SortedCachedRecords.Take(100))
            {
                int recordTimerTicks = kvp.Value.TimerTicks;

                if (currentPlayerTime > recordTimerTicks)
                {
                    placement++;
                }
                else
                {
                    break;
                }
            }
            if (placement > 100)
            {
                return "#100" + "+";
            }
            else
            {
                return "#" + placement;
            }
        }

        public async Task<string> GetPlayerPlacementWithTotal(CCSPlayerController? player, string steamId, string playerName, bool getRankImg = false)
        {
            if (!IsAllowedPlayer(player))
            {
                return "";
            }

            int savedPlayerTime;
            if (useMySQL == true)
            {
                savedPlayerTime = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName, playerName);
            }
            else
            {
                savedPlayerTime = GetPreviousPlayerRecord(player);
            }

            if (savedPlayerTime == 0)
            {
                return "";
            }

            Dictionary<string, PlayerRecord> sortedRecords;
            if (useMySQL == true)
            {
                sortedRecords = await GetSortedRecordsFromDatabase();
            }
            else
            {
                sortedRecords = GetSortedRecords();
            }

            int placement = 1;

            foreach (var kvp in sortedRecords)
            {
                int recordTimerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                if (savedPlayerTime > recordTimerTicks)
                {
                    placement++;
                }
                else
                {
                    break;
                }
            }

            int totalPlayers = sortedRecords.Count;

            string rank;

            if (getRankImg)
            {
                double percentage = (double)placement / totalPlayers * 100;

                if (percentage <= 6.4)
                {
                    rank = "<img src='https://i.imgur.com/IFAyj0y.png' class=''>";
                }
                else if (percentage <= 28)
                {
                    rank = "<img src='https://i.imgur.com/5O4OfR5.png' class=''>";
                }
                else if (percentage <= 75.6)
                {
                    rank = "<img src='https://i.imgur.com/pc0TN7z.png' class=''>";
                }
                else if (percentage <= 95.2)
                {
                    rank = "<img src='https://i.imgur.com/kKzdpqu.png' class=''>";
                }
                else
                {
                    rank = "<img src='https://i.imgur.com/gL6Ru8t.png' class=''>";
                }
            }
            else
            {
                rank = $"Rank: {placement}/{totalPlayers}";
            }

            return rank;
        }

        public void SavePlayerTime(CCSPlayerController? player, int timerTicks, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;
            if ((bonusX == 0 && playerTimers[player.Slot].IsTimerRunning == false) || (bonusX != 0 && playerTimers[player.Slot].IsBonusTimerRunning == false)) return;

            SharpTimerDebug($"Saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} of {timerTicks} ticks for {player.PlayerName} to json");

            string currentMapName = bonusX == 0 ? Server.MapName : $"{Server.MapName}_bonus{bonusX}";
            string steamId = player.SteamID.ToString();
            string playerName = player.PlayerName;

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(playerRecordsPath))
            {
                string json = File.ReadAllText(playerRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json) ?? new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }
            else
            {
                records = new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }

            if (!records.ContainsKey(currentMapName))
            {
                records[currentMapName] = new Dictionary<string, PlayerRecord>();
            }

            if (!records[currentMapName].ContainsKey(steamId) || records[currentMapName][steamId].TimerTicks > timerTicks)
            {
                records[currentMapName][steamId] = new PlayerRecord
                {
                    PlayerName = playerName,
                    TimerTicks = timerTicks
                };

                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(playerRecordsPath, updatedJson);
                if (stageTriggers.Any()) DumpPlayerStageTimesToJson(player);
            }
        }

        private async Task<(int? Tier, string? Type)> FineMapInfoFromHTTP(string url)
        {
            try
            {
                SharpTimerDebug($"Trying to fetch remote_data for {currentMapName} from {url}");
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetStringAsync(url);
                    var jsonDocument = JsonDocument.Parse(response);

                    if (jsonDocument.RootElement.TryGetProperty(currentMapName, out var mapInfo))
                    {
                        int? tier = null;
                        string? type = null;

                        if (mapInfo.TryGetProperty("Tier", out var tierElement))
                        {
                            tier = tierElement.GetInt32();
                        }

                        if (mapInfo.TryGetProperty("Type", out var typeElement))
                        {
                            type = typeElement.GetString();
                        }

                        SharpTimerDebug($"Fetched remote_data success! {tier} {type}");

                        return (tier, type);
                    }
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error Getting Remote Data for {currentMapName}: {ex.Message}");
                return (null, null);
            }
        }

        private async Task AddMapInfoToHostname()
        {
            string mapInfoSource = GetMapInfoSource();
            var (mapTier, mapType) = await FineMapInfoFromHTTP(mapInfoSource);
            currentMapTier = mapTier;
            currentMapType = mapType;
            string tierString = currentMapTier != null ? $" | Tier: {currentMapTier}" : "";
            string typeString = currentMapType != null ? $" | {currentMapType}" : "";

            if (!autosetHostname) return;

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"hostname {defaultServerHostname}{tierString}{typeString}");
                SharpTimerDebug($"SharpTimer Hostname Updated to: {ConVar.Find("hostname").StringValue}");
            });
        }

        private string GetMapInfoSource()
        {
            return currentMapName switch
            {
                var name when name.StartsWith("kz_") => remoteKZDataSource,
                var name when name.StartsWith("bhop_") => remoteBhopDataSource,
                var name when name.StartsWith("surf_") => remoteSurfDataSource,
                _ => null
            };
        }

        private void OnMapStartHandler(string mapName)
        {
            Server.NextFrame(() =>
            {
                SharpTimerDebug("OnMapStart:");
                SharpTimerDebug("Executing SharpTimer/config");
                Server.ExecuteCommand("sv_autoexec_mapname_cfg 0");
                Server.ExecuteCommand($"execifexists SharpTimer/config.cfg");

                //delay custom_exec so it executes after map exec
                SharpTimerDebug("Creating custom_exec 2sec delay");
                var custom_exec_delay = AddTimer(2.0f, () =>
                {
                    SharpTimerDebug("Executing SharpTimer/custom_exec");
                    Server.ExecuteCommand("execifexists SharpTimer/custom_exec.cfg");
                });

                if (removeCrouchFatigueEnabled == true) Server.ExecuteCommand("sv_timebetweenducks 0");
            });
        }

        private void LoadMapData()
        {
            Server.ExecuteCommand($"hostname {defaultServerHostname}");

            if (srEnabled == true) ServerRecordADtimer();

            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(gameDir + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(gameDir + "/csgo/cfg", mysqlConfigFileName);

            currentMapName = Server.MapName;

            string mapdataFileName = $"SharpTimer/MapData/{currentMapName}.json";
            string mapdataPath = Path.Join(gameDir + "/csgo/cfg", mapdataFileName);

            currentMapTier = null; //making sure previous map tier and type are wiped
            currentMapType = null;

            _ = AddMapInfoToHostname();

            if (triggerPushFixEnabled == true) FindTriggerPushData();

            if (File.Exists(mapdataPath))
            {
                string json = File.ReadAllText(mapdataPath);
                var mapInfo = JsonSerializer.Deserialize<MapInfo>(json);
                SharpTimerConPrint($"Map data json found for map: {currentMapName}!");

                if (!string.IsNullOrEmpty(mapInfo.MapStartC1) && !string.IsNullOrEmpty(mapInfo.MapStartC2) && !string.IsNullOrEmpty(mapInfo.MapEndC1) && !string.IsNullOrEmpty(mapInfo.MapEndC2))
                {
                    currentMapStartC1 = ParseVector(mapInfo.MapStartC1);
                    currentMapStartC2 = ParseVector(mapInfo.MapStartC2);
                    currentMapEndC1 = ParseVector(mapInfo.MapEndC1);
                    currentMapEndC2 = ParseVector(mapInfo.MapEndC2);
                    useTriggers = false;
                    SharpTimerConPrint($"Found Fake Trigger Corners: START {currentMapStartC1}, {currentMapStartC2} | END {currentMapEndC1}, {currentMapEndC2}");
                }

                if (!string.IsNullOrEmpty(mapInfo.MapStartTrigger) && !string.IsNullOrEmpty(mapInfo.MapEndTrigger))
                {
                    currentMapStartTrigger = mapInfo.MapStartTrigger;
                    currentMapEndTrigger = mapInfo.MapEndTrigger;
                    useTriggers = true;
                    SharpTimerConPrint($"Found Trigger Names: START {currentMapStartTrigger} | END {currentMapEndTrigger}");
                }

                if (!string.IsNullOrEmpty(mapInfo.RespawnPos))
                {
                    currentRespawnPos = ParseVector(mapInfo.RespawnPos);
                    SharpTimerConPrint($"Found RespawnPos: {currentRespawnPos}");
                }
                else
                {
                    currentRespawnPos = FindStartTriggerPos();
                    FindBonusStartTriggerPos();
                    FindStageTriggers();
                    SharpTimerConPrint($"RespawnPos not found, trying to hook trigger pos instead");
                    if (currentRespawnPos == null)
                    {
                        SharpTimerConPrint($"Hooking Trigger RespawnPos Failed!");
                    }
                    else
                    {
                        SharpTimerConPrint($"Hooking Trigger RespawnPos Success! {currentRespawnPos}");
                    }
                }

                if (!string.IsNullOrEmpty(mapInfo.OverrideDisableTelehop))
                {
                    try
                    {
                        currentMapOverrideDisableTelehop = bool.Parse(mapInfo.OverrideDisableTelehop);
                        SharpTimerConPrint($"Overriding Telehop...");
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Invalid boolean string format for OverrideDisableTelehop");
                    }
                }
                else
                {
                    currentMapOverrideDisableTelehop = false;
                }
            }
            else
            {
                SharpTimerConPrint($"Map data json not found for map: {currentMapName}!");
                SharpTimerConPrint($"Trying to hook Triggers supported by default!");
                currentRespawnPos = FindStartTriggerPos();
                FindBonusStartTriggerPos();
                FindStageTriggers();
                if (currentRespawnPos == null)
                {
                    SharpTimerConPrint($"Hooking Trigger RespawnPos Failed!");
                }
                else
                {
                    SharpTimerConPrint($"Hooking Trigger RespawnPos Success! {currentRespawnPos}");
                }
                useTriggers = true;
            }

            if (useTriggers == false)
            {
                DrawWireframe2D(currentMapStartC1, currentMapStartC2, fakeTriggerHeight);
                DrawWireframe2D(currentMapEndC1, currentMapEndC2, fakeTriggerHeight);
            }
            else
            {
                var (startRight, startLeft, endRight, endLeft) = FindTriggerCorners();

                if (startRight == null || startLeft == null || endRight == null || endLeft == null) return;

                DrawWireframe3D(startRight, startLeft);
                DrawWireframe3D(endRight, endLeft);
            }
        }
    }
}