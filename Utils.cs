using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
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
                Dictionary<string, PlayerRecord> sortedRecords;
                if (useMySQL == false)
                {
                    sortedRecords = GetSortedRecords();
                }
                else
                {
                    sortedRecords = await GetSortedRecordsFromDatabase();
                }

                if (sortedRecords.Count == 0)
                {
                    return;
                }

                Server.NextFrame(() => Server.PrintToChatAll($"{msgPrefix} Current Server Record on {ChatColors.Green}{currentMapName}{ChatColors.White}: "));

                foreach (var kvp in sortedRecords.Take(1))
                {
                    string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                    int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                    Server.NextFrame(() => Server.PrintToChatAll(msgPrefix + $" {ChatColors.Green}{playerName} {ChatColors.White}- {ChatColors.Green}{FormatTime(timerTicks)}"));
                }
            }, TimerFlags.REPEAT);
            isADTimerRunning = true;
        }


        private static string FormatTime(int ticks)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(ticks / 64.0);

            // Format seconds with three decimal points
            string secondsWithMilliseconds = $"{timeSpan.Seconds:D2}.{(ticks % 64) * (1000.0 / 64.0):000}";

            return $"{timeSpan.Minutes:D1}:{secondsWithMilliseconds}";
        }

        private static string FormatTimeold(int ticks)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(ticks / 64.0);
            int centiseconds = (int)((ticks % 64) * (100.0 / 64.0));

            return $"{timeSpan.Minutes:D1}:{timeSpan.Seconds:D2}.{centiseconds:D2}";
        }

        private static string FormatTimeDifference(int currentTicks, int previousTicks)
        {
            int differenceTicks = previousTicks - currentTicks;
            string sign = (differenceTicks > 0) ? "-" : "+";

            TimeSpan timeDifference = TimeSpan.FromSeconds(Math.Abs(differenceTicks) / 64.0);

            // Format seconds with three decimal points
            string secondsWithMilliseconds = $"{timeDifference.Seconds:D2}.{(Math.Abs(differenceTicks) % 64) * (1000.0 / 64.0):000}";

            return $"{sign}{timeDifference.Minutes:D1}:{secondsWithMilliseconds}";
        }

        private static string FormatTimeDifferenceold(int currentTicks, int previousTicks)
        {
            int differenceTicks = previousTicks - currentTicks;
            string sign = (differenceTicks > 0) ? "-" : "+";

            TimeSpan timeDifference = TimeSpan.FromSeconds(Math.Abs(differenceTicks) / 64.0);
            int centiseconds = (int)((Math.Abs(differenceTicks) % 64) * (100.0 / 64.0));

            return $"{sign}{timeDifference.Minutes:D1}:{timeDifference.Seconds:D2}.{centiseconds:D2}";
        }

        static bool IsVectorInsideBox(Vector playerVector, Vector corner1, Vector corner2, float height = 50)
        {
            float minX = Math.Min(corner1.X, corner2.X);
            float minY = Math.Min(corner1.Y, corner2.Y);
            float minZ = Math.Min(corner1.Z, corner2.Z);

            float maxX = Math.Max(corner1.X, corner2.X);
            float maxY = Math.Max(corner1.Y, corner2.Y);
            float maxZ = Math.Max(corner1.Z, corner1.Z);

            return playerVector.X >= minX && playerVector.X <= maxX &&
                   playerVector.Y >= minY && playerVector.Y <= maxY &&
                   playerVector.Z >= minZ && playerVector.Z <= maxZ + height;
        }

        private static void AdjustPlayerVelocity(CCSPlayerController? player, float velocity)
        {
            var currentX = player.PlayerPawn.Value.AbsVelocity.X;
            var currentY = player.PlayerPawn.Value.AbsVelocity.Y;
            var currentSpeed2D = Math.Sqrt(currentX * currentX + currentY * currentY);
            var normalizedX = currentX / currentSpeed2D;
            var normalizedY = currentY / currentSpeed2D;
            var adjustedX = normalizedX * velocity; // Adjusted speed limit
            var adjustedY = normalizedY * velocity; // Adjusted speed limit
            player.PlayerPawn.Value.AbsVelocity.X = (float)adjustedX;
            player.PlayerPawn.Value.AbsVelocity.Y = (float)adjustedY;
        }

        private Vector FindStartTriggerPos()
        {
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");

            foreach (var trigger in triggers)
            {
                if (trigger.Entity.Name == currentMapStartTrigger)
                {
                    return trigger.CBodyComponent?.SceneNode?.AbsOrigin;
                }
            }
            return new Vector(0, 0, 0);
        }

        private static Vector ParseVector(string vectorString)
        {
            var values = vectorString.Split(' ');
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
            var values = qAngleString.Split(' ');
            if (values.Length == 3 &&
                float.TryParse(values[0], out float pitch) &&
                float.TryParse(values[1], out float yaw) &&
                float.TryParse(values[2], out float roll))
            {
                return new QAngle(pitch, yaw, roll);
            }

            return new QAngle(0, 0, 0);
        }

        public Dictionary<string, PlayerRecord> GetSortedRecords()
        {
            string currentMapName = Server.MapName;

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

        private int GetPreviousPlayerRecord(CCSPlayerController? player)
        {
            if (player == null) return 0;

            string currentMapName = Server.MapName;
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
            if (player == null || !playerTimers.ContainsKey(player.Slot) || !playerTimers[player.Slot].IsTimerRunning) return "";


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

        public async Task<string> GetPlayerPlacementWithTotal(CCSPlayerController? player, string steamId, int playerSlot)
        {
            if (player == null || !playerTimers.ContainsKey(playerSlot))
            {
                return "Unranked";
            }

            int savedPlayerTime;
            if (useMySQL == true)
            {
                savedPlayerTime = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName);
            }
            else
            {
                savedPlayerTime = GetPreviousPlayerRecord(player);
            }

            if (savedPlayerTime == 0)
            {
                return "Unranked";
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

            return $"Rank: {placement}/{totalPlayers}";
        }

        public void SavePlayerTime(CCSPlayerController? player, int timerTicks)
        {
            if (player == null) return;
            if (playerTimers[player.Slot].IsTimerRunning == false) return;

            string currentMapName = Server.MapName;
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
                    TimerTicks = playerTimers[player.Slot].TimerTicks
                };

                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(playerRecordsPath, updatedJson);
            }
        }

        private void OnMapStartHandler(string mapName)
        {
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("execifexists SharpTimer/custom_exec.cfg");
                if (removeCrouchFatigueEnabled == true) Server.ExecuteCommand("sv_timebetweenducks 0");
            });
        }

        private void LoadConfig()
        {
            Server.ExecuteCommand($"execifexists SharpTimer/config.cfg");

            if (srEnabled == true) ServerRecordADtimer();

            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(Server.GameDirectory + "/csgo/cfg", mysqlConfigFileName);

            currentMapName = Server.MapName;

            string mapdataFileName = $"SharpTimer/MapData/{currentMapName}.json"; // Assuming the map JSON files are in the SharpTimer folder
            string mapdataPath = Path.Join(Server.GameDirectory + "/csgo/cfg", mapdataFileName);

            if (File.Exists(mapdataPath))
            {
                string json = File.ReadAllText(mapdataPath);
                var mapInfo = JsonSerializer.Deserialize<MapInfo>(json);

                if (!string.IsNullOrEmpty(mapInfo.RespawnPos))
                {
                    currentRespawnPos = ParseVector(mapInfo.RespawnPos);
                }

                if (!string.IsNullOrEmpty(mapInfo.MapStartC1) && !string.IsNullOrEmpty(mapInfo.MapStartC2) && !string.IsNullOrEmpty(mapInfo.MapEndC1) && !string.IsNullOrEmpty(mapInfo.MapEndC2))
                {
                    currentMapStartC1 = ParseVector(mapInfo.MapStartC1);
                    currentMapStartC2 = ParseVector(mapInfo.MapStartC2);
                    currentMapEndC1 = ParseVector(mapInfo.MapEndC1);
                    currentMapEndC2 = ParseVector(mapInfo.MapEndC2);
                    useTriggers = false;
                }

                if (!string.IsNullOrEmpty(mapInfo.MapStartTrigger) && !string.IsNullOrEmpty(mapInfo.MapEndTrigger))
                {
                    currentMapStartTrigger = mapInfo.MapStartTrigger;
                    currentMapEndTrigger = mapInfo.MapEndTrigger;
                    useTriggers = true;
                }
            }
            else
            {
                Console.WriteLine($"Map data not found for map: {currentMapName}! Using default trigger names instead!");
                currentMapStartTrigger = "timer_startzone";
                currentMapEndTrigger = "timer_endzone";
                useTriggers = true;
            }
        }
    }
}
