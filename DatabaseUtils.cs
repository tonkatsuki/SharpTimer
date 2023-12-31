using System.Drawing;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using Nexd.MySQL;


namespace SharpTimer
{
    partial class SharpTimer
    {
        private string GetConnectionStringFromConfigFile(string mySQLpath)
        {
            SharpTimerDebug($"Trying to get a MySQL Connection string from {mySQLpath}");
            try
            {
                string jsonString = File.ReadAllText(mySQLpath);
                JsonDocument jsonConfig = JsonDocument.Parse(jsonString);

                JsonElement root = jsonConfig.RootElement;

                string host = root.GetProperty("MySqlHost").GetString();
                string database = root.GetProperty("MySqlDatabase").GetString();
                string username = root.GetProperty("MySqlUsername").GetString();
                string password = root.GetProperty("MySqlPassword").GetString();
                int port = root.GetProperty("MySqlPort").GetInt32();

                SharpTimerDebug($"Got Connection String!");
                return $"Server={host};Database={database};User ID={username};Password={password};Port={port};CharSet=utf8mb4;";
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Error GetConnectionString: {ex.Message}");
                return "Server=localhost;Database=database;User ID=root;Password=root;Port=3306;CharSet=utf8mb4;";
            }
        }

        public async Task SavePlayerTimeToDatabase(CCSPlayerController? player, int timerTicks, string steamId, string playerName, int playerSlot, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;
            if ((bonusX == 0 && playerTimers[playerSlot].IsTimerRunning == false) || (bonusX != 0 && playerTimers[playerSlot].IsBonusTimerRunning == false)) return;

            string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";

            SharpTimerDebug($"Trying to save player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to MySQL for {playerName} {timerTicks}");
            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                {
                    await connection.OpenAsync();

                    string formattedTime = FormatTime(timerTicks); // Assuming FormatTime method is available

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    // Check if the record already exists or has a higher timer value
                    string selectQuery = "SELECT TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("@MapName", currentMapNamee);
                        selectCommand.Parameters.AddWithValue("@SteamID", steamId);

                        var existingTimerTicks = await selectCommand.ExecuteScalarAsync();
                        if (existingTimerTicks == null || (int)existingTimerTicks > timerTicks)
                        {
                            // Update or insert the record
                            string upsertQuery = "REPLACE INTO PlayerRecords (MapName, SteamID, PlayerName, TimerTicks, FormattedTime) VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, @FormattedTime)";
                            using (var upsertCommand = new MySqlCommand(upsertQuery, connection))
                            {
                                upsertCommand.Parameters.AddWithValue("@MapName", currentMapNamee);
                                upsertCommand.Parameters.AddWithValue("@SteamID", steamId);
                                upsertCommand.Parameters.AddWithValue("@PlayerName", playerName);
                                upsertCommand.Parameters.AddWithValue("@TimerTicks", timerTicks);
                                upsertCommand.Parameters.AddWithValue("@FormattedTime", formattedTime);

                                await upsertCommand.ExecuteNonQueryAsync();
                                Server.NextFrame(() => SharpTimerDebug($"Saved player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to MySQL for {playerName} {timerTicks}"));
                            }
                        }
                    }
                }
                if (useMySQL == true) _ = RankCommandHandler(player, steamId, playerSlot, playerName, true);
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerDebug($"Error saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to MySQL: {ex.Message}"));
            }
        }

        public async Task<int> GetPreviousPlayerRecordFromDatabase(CCSPlayerController? player, string steamId, string currentMapName, string playerName, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player))
            {
                return 0;
            }

            string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";

            SharpTimerDebug($"Trying to get Previous {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} from MySQL for {playerName}");
            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                {
                    await connection.OpenAsync();

                    // Check if the table exists
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    // Retrieve the TimerTicks value for the specified player on the current map
                    string selectQuery = "SELECT TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("@MapName", currentMapNamee);
                        selectCommand.Parameters.AddWithValue("@SteamID", steamId);

                        var result = await selectCommand.ExecuteScalarAsync();

                        // Check for DBNull
                        if (result != null && result != DBNull.Value)
                        {
                            SharpTimerDebug($"Got Previous Time from MySQL for {playerName}");
                            return Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Error getting previous player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} from MySQL: {ex.Message}");
            }

            return 0;
        }

        public async Task<Dictionary<string, PlayerRecord>> GetSortedRecordsFromDatabase(int bonusX = 0)
        {
            string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";
            SharpTimerDebug($"Trying GetSortedRecords {(bonusX != 0 ? $"bonus {bonusX}" : "")} from MySQL");
            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                {
                    await connection.OpenAsync();

                    // Check if the table exists
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    // Retrieve and sort records for the current map
                    string selectQuery = "SELECT SteamID, PlayerName, TimerTicks FROM PlayerRecords WHERE MapName = @MapName";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("@MapName", currentMapNamee);
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            var sortedRecords = new Dictionary<string, PlayerRecord>();
                            while (await reader.ReadAsync())
                            {
                                string steamId = reader.GetString(0);
                                string playerName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                                int timerTicks = reader.GetInt32(2);
                                sortedRecords.Add(steamId, new PlayerRecord
                                {
                                    PlayerName = playerName,
                                    TimerTicks = timerTicks
                                });
                            }

                            // Sort the records by TimerTicks
                            sortedRecords = sortedRecords.OrderBy(record => record.Value.TimerTicks)
                                                         .ToDictionary(record => record.Key, record => record.Value);

                            SharpTimerDebug($"Got GetSortedRecords {(bonusX != 0 ? $"bonus {bonusX}" : "")} from MySQL");
                            return sortedRecords;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Error getting sorted records from MySQL: {ex.Message}");
            }

            return new Dictionary<string, PlayerRecord>();
        }

        public void SavePlayerSettingToDatabase(string SteamID, string setting, bool state)
        {

        }

        public bool GetPlayerSettingFromDatabase(string SteamID, string setting)
        {

            return false;
        }

        [ConsoleCommand("css_jsontodatabase", " ")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void AddJsonTimesToDatabaseCommand(CCSPlayerController? player, CommandInfo command)
        {
            _ = AddJsonTimesToDatabaseAsync(player);
        }

        public async Task AddJsonTimesToDatabaseAsync(CCSPlayerController? player)
        {
            try
            {
                if (!File.Exists(playerRecordsPath))
                {
                    SharpTimerDebug($"Error: JSON file not found at {playerRecordsPath}");
                    return;
                }

                string json = await File.ReadAllTextAsync(playerRecordsPath);
                var records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json);

                if (records == null)
                {
                    SharpTimerDebug("Error: Failed to deserialize JSON data.");
                    return;
                }

                string connectionString = GetConnectionStringFromConfigFile(mySQLpath);

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var mapEntry in records)
                    {
                        string currentMapName = mapEntry.Key;

                        foreach (var recordEntry in mapEntry.Value)
                        {
                            string steamId = recordEntry.Key;
                            PlayerRecord playerRecord = recordEntry.Value;

                            // Check if the player is already in the database for the map
                            string insertOrUpdateQuery = @"
                                INSERT INTO PlayerRecords (MapName, SteamID, PlayerName, TimerTicks, FormattedTime)
                                VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, @FormattedTime)
                                ON DUPLICATE KEY UPDATE
                                TimerTicks = IF(@TimerTicks < TimerTicks, @TimerTicks, TimerTicks),
                                FormattedTime = IF(@TimerTicks < TimerTicks, @FormattedTime, FormattedTime)";

                            using (var insertOrUpdateCommand = new MySqlCommand(insertOrUpdateQuery, connection))
                            {
                                insertOrUpdateCommand.Parameters.AddWithValue("@MapName", currentMapName);
                                insertOrUpdateCommand.Parameters.AddWithValue("@SteamID", steamId);
                                insertOrUpdateCommand.Parameters.AddWithValue("@PlayerName", playerRecord.PlayerName);
                                insertOrUpdateCommand.Parameters.AddWithValue("@TimerTicks", playerRecord.TimerTicks);
                                insertOrUpdateCommand.Parameters.AddWithValue("@FormattedTime", FormatTime(playerRecord.TimerTicks));

                                await insertOrUpdateCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    SharpTimerDebug("JSON times successfully added to the database.");
                }
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Error adding JSON times to the database: {ex.Message}");
            }
        }

        [ConsoleCommand("css_databasetojson", " ")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void ExportDatabaseToJsonCommand(CCSPlayerController? player, CommandInfo command)
        {
            _ = ExportDatabaseToJsonAsync(player);
        }

        public async Task ExportDatabaseToJsonAsync(CCSPlayerController? player)
        {
            try
            {
                string connectionString = GetConnectionStringFromConfigFile(mySQLpath);

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT MapName, SteamID, PlayerName, TimerTicks FROM PlayerRecords";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            var records = new Dictionary<string, Dictionary<string, PlayerRecord>>();

                            while (await reader.ReadAsync())
                            {
                                string mapName = reader.GetString(0);
                                string steamId = reader.GetString(1);
                                string playerName = reader.GetString(2);
                                int timerTicks = reader.GetInt32(3);

                                if (!records.ContainsKey(mapName))
                                {
                                    records[mapName] = new Dictionary<string, PlayerRecord>();
                                }

                                records[mapName][steamId] = new PlayerRecord
                                {
                                    PlayerName = playerName,
                                    TimerTicks = timerTicks
                                };
                            }

                            // Convert records to JSON
                            string json = JsonSerializer.Serialize(records, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });

                            // Save JSON to file
                            await File.WriteAllTextAsync(playerRecordsPath, json);

                            SharpTimerDebug("Player records successfully exported to JSON.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Error exporting player records to JSON: {ex.Message}");
            }
        }
    }
}