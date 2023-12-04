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
        private void ServerRecordADtimer()
        {
            if (isADTimerRunning) return;

            var timer = AddTimer(srTimer, () =>
            {
                Dictionary<string, int> sortedRecords = GetSortedRecords();

                if (sortedRecords.Count == 0)
                {
                    return;
                }

                Server.PrintToChatAll($"{msgPrefix} Current Server Record on {ChatColors.Green}{Server.MapName}{ChatColors.White}: ");

                foreach (var record in sortedRecords.Take(1))
                {
                    string playerName = GetPlayerNameFromSavedSteamID(record.Key); // Get the player name using SteamID 
                    Server.PrintToChatAll(msgPrefix + $" {ChatColors.Green}{playerName} {ChatColors.White}- {ChatColors.Green}{FormatTime(record.Value)}");
                }
            }, TimerFlags.REPEAT);
            isADTimerRunning = true;
        }

        private static string FormatTime(int ticks)
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

        public static Dictionary<string, int> GetSortedRecords()
        {
            string currentMapName = Server.MapName;

            string recordsFileName = "SharpTimer/player_records.json";
            string recordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(recordsPath))
            {
                string json = File.ReadAllText(recordsPath);
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
                    .ToDictionary(record => record.Key, record => record.Value.TimerTicks);

                return sortedRecords;
            }

            return new Dictionary<string, int>();
        }

        private static string GetPlayerNameFromSavedSteamID(string steamId)
        {
            string currentMapName = Server.MapName;

            string recordsFileName = "SharpTimer/player_records.json";
            string recordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            if (File.Exists(recordsPath))
            {
                try
                {
                    string json = File.ReadAllText(recordsPath);
                    var records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json);

                    if (records != null && records.TryGetValue(currentMapName, out var mapRecords) && mapRecords.TryGetValue(steamId, out var playerRecord))
                    {
                        return playerRecord.PlayerName;
                    }
                }
                catch (JsonException ex)
                {
                    // Handle JSON deserialization errors
                    Console.WriteLine($"Error deserializing player records: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    Console.WriteLine($"Error reading player records: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Player records file not found: {recordsPath}");
            }

            return "Unknown"; // Return a default name if the player name is not found or an error occurs
        }

        private static int GetPreviousPlayerRecord(CCSPlayerController? player)
        {
            if (player == null) return 0;

            string currentMapName = Server.MapName;
            string steamId = player.SteamID.ToString();

            string recordsFileName = "SharpTimer/player_records.json";
            string recordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(recordsPath))
            {
                string json = File.ReadAllText(recordsPath);
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
            if (player == null || !playerTimers.ContainsKey(player.UserId ?? 0) || !playerTimers[player.UserId ?? 0].IsTimerRunning) return "";

            Dictionary<string, int> sortedRecords = GetSortedRecords();
            int currentPlayerTime = playerTimers[player.UserId ?? 0].TimerTicks;

            int placement = 1;

            foreach (var record in sortedRecords)
            {
                if (currentPlayerTime > record.Value)
                {
                    placement++;
                }
                else
                {
                    break;
                }
            }

            return "#" + placement;
        }

        public string GetPlayerPlacementWithTotal(CCSPlayerController? player)
        {
            if (player == null || !playerTimers.ContainsKey(player.UserId ?? 0))
            {
                return "Unranked";
            }

            string steamId = player.SteamID.ToString();

            int savedPlayerTime;
            if (useMySQL == true)
            {
                savedPlayerTime = GetPreviousPlayerRecordFromDatabase(player);
            }
            else
            {
                savedPlayerTime = GetPreviousPlayerRecord(player);
            }

            if (savedPlayerTime == 0)
            {
                return "Unranked";
            }

            Dictionary<string, int> sortedRecords;
            if (useMySQL == true)
            {
                sortedRecords = GetSortedRecordsFromDatabase();
            }
            else
            {
                sortedRecords = GetSortedRecords();
            }

            int placement = 1;

            foreach (var record in sortedRecords)
            {
                if (savedPlayerTime > record.Value)
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
            if (playerTimers[player.UserId ?? 0].IsTimerRunning == false) return;

            string currentMapName = Server.MapName;
            string steamId = player.SteamID.ToString();
            string playerName = player.PlayerName;

            string recordsFileName = "SharpTimer/player_records.json";
            string recordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(recordsPath))
            {
                string json = File.ReadAllText(recordsPath);
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

            if (!records[currentMapName].ContainsKey(steamId) || records[currentMapName][steamId].TimerTicks > playerTimers[player.UserId ?? 0].TimerTicks)
            {
                records[currentMapName][steamId] = new PlayerRecord
                {
                    PlayerName = playerName,
                    TimerTicks = playerTimers[player.UserId ?? 0].TimerTicks
                };

                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(recordsPath, updatedJson);
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
            Server.ExecuteCommand("execifexists SharpTimer/config.cfg");

            if (srEnabled == true) ServerRecordADtimer();

            string currentMapName = Server.MapName;

            string mapdataFileName = "SharpTimer/mapdata.json";
            string mapdataPath = Path.Join(Server.GameDirectory + "/csgo/cfg", mapdataFileName);

            if (File.Exists(mapdataPath))
            {
                string json = File.ReadAllText(mapdataPath);
                var mapData = JsonSerializer.Deserialize<Dictionary<string, MapInfo>>(json);

                if (mapData != null && mapData.TryGetValue(currentMapName, out var mapInfo))
                {
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
}
