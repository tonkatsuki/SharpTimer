using System.Text.Json;
using MySqlConnector;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace SharpTimer
{
    partial class SharpTimer
    {
        private async Task<MySqlConnection> OpenDatabaseConnectionAsync()
        {
            var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath));
            await connection.OpenAsync();
            return connection;
        }

        private async Task CreatePlayerRecordsTableAsync(MySqlConnection connection)
        {
            string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
            using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
            {
                await createTableCommand.ExecuteNonQueryAsync();
            }
        }

        private string GetConnectionStringFromConfigFile(string mySQLpath)
        {
            try
            {
                using (JsonDocument jsonConfig = LoadJson(mySQLpath))
                {
                    if (jsonConfig != null)
                    {
                        JsonElement root = jsonConfig.RootElement;

                        string host = root.GetProperty("MySqlHost").GetString();
                        string database = root.GetProperty("MySqlDatabase").GetString();
                        string username = root.GetProperty("MySqlUsername").GetString();
                        string password = root.GetProperty("MySqlPassword").GetString();
                        int port = root.GetProperty("MySqlPort").GetInt32();

                        return $"Server={host};Database={database};User ID={username};Password={password};Port={port};CharSet=utf8mb4;";
                    }
                    else
                    {
                        SharpTimerError($"mySQL json was null");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in GetConnectionString: {ex.Message}");
            }

            return "Server=localhost;Database=database;User ID=root;Password=root;Port=3306;CharSet=utf8mb4;";
        }

        public async Task SavePlayerTimeToDatabase(CCSPlayerController? player, int timerTicks, string steamId, string playerName, int playerSlot, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;
            if ((bonusX == 0 && !playerTimers[playerSlot].IsTimerRunning) || (bonusX != 0 && !playerTimers[playerSlot].IsBonusTimerRunning)) return;

            string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";

            SharpTimerDebug($"Trying to save player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to MySQL for {playerName} {timerTicks}");
            try
            {
                using (var connection = await OpenDatabaseConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);

                    string formattedTime = FormatTime(timerTicks); // Assuming FormatTime method is available

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
                Server.NextFrame(() => SharpTimerError($"Error saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to MySQL: {ex.Message}"));
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
                using (var connection = await OpenDatabaseConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);

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
                SharpTimerError($"Error getting previous player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} from MySQL: {ex.Message}");
            }

            return 0;
        }

        public async Task<Dictionary<string, PlayerRecord>> GetSortedRecordsFromDatabase(int bonusX = 0)
        {
            string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";
            SharpTimerDebug($"Trying GetSortedRecords {(bonusX != 0 ? $"bonus {bonusX}" : "")} from MySQL");
            try
            {
                using (var connection = await OpenDatabaseConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);

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
                SharpTimerError($"Error getting sorted records from MySQL: {ex.Message}");
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
            _ = AddJsonTimesToDatabaseAsync();
        }

        public async Task AddJsonTimesToDatabaseAsync()
        {
            try
            {
                string recordsDirectoryNamee = "SharpTimer/PlayerRecords";
                string playerRecordsPathh = Path.Combine(gameDir, "csgo", "cfg", recordsDirectoryNamee);

                if (!Directory.Exists(playerRecordsPathh))
                {
                    SharpTimerDebug($"Error: Directory not found at {playerRecordsPathh}");
                    return;
                }

                string connectionString = GetConnectionStringFromConfigFile(mySQLpath);

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, FormattedTime VARCHAR(255), MapName VARCHAR(255), PRIMARY KEY (SteamID, MapName))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var filePath in Directory.EnumerateFiles(playerRecordsPathh, "*.json"))
                    {
                        string json = await File.ReadAllTextAsync(filePath);
                        var records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(json);

                        if (records == null)
                        {
                            SharpTimerDebug($"Error: Failed to deserialize JSON data from {filePath}");
                            continue;
                        }

                        foreach (var recordEntry in records)
                        {
                            string steamId = recordEntry.Key;
                            PlayerRecord playerRecord = recordEntry.Value;

                            // Extract MapName from the filename (remove extension)
                            string mapName = Path.GetFileNameWithoutExtension(filePath);

                            // Check if the player is already in the database
                            string insertOrUpdateQuery = @"
                                INSERT INTO PlayerRecords (SteamID, PlayerName, TimerTicks, FormattedTime, MapName)
                                VALUES (@SteamID, @PlayerName, @TimerTicks, @FormattedTime, @MapName)
                                ON DUPLICATE KEY UPDATE
                                TimerTicks = IF(@TimerTicks < TimerTicks, @TimerTicks, TimerTicks),
                                FormattedTime = IF(@TimerTicks < TimerTicks, @FormattedTime, FormattedTime)";

                            using (var insertOrUpdateCommand = new MySqlCommand(insertOrUpdateQuery, connection))
                            {
                                insertOrUpdateCommand.Parameters.AddWithValue("@SteamID", steamId);
                                insertOrUpdateCommand.Parameters.AddWithValue("@PlayerName", playerRecord.PlayerName);
                                insertOrUpdateCommand.Parameters.AddWithValue("@TimerTicks", playerRecord.TimerTicks);
                                insertOrUpdateCommand.Parameters.AddWithValue("@FormattedTime", FormatTime(playerRecord.TimerTicks));
                                insertOrUpdateCommand.Parameters.AddWithValue("@MapName", mapName);

                                await insertOrUpdateCommand.ExecuteNonQueryAsync();
                            }
                        }

                        SharpTimerDebug($"JSON times from {Path.GetFileName(filePath)} successfully added to the database.");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error adding JSON times to the database: {ex.Message}");
            }
        }

        [ConsoleCommand("css_databasetojson", " ")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void ExportDatabaseToJsonCommand(CCSPlayerController? player, CommandInfo command)
        {
            _ = ExportDatabaseToJsonAsync();
        }

        public async Task ExportDatabaseToJsonAsync()
        {
            string recordsDirectoryNamee = "SharpTimer/PlayerRecords";
            string playerRecordsPathh = Path.Combine(gameDir, "csgo", "cfg", recordsDirectoryNamee);

            try
            {
                string connectionString = GetConnectionStringFromConfigFile(mySQLpath);

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT SteamID, PlayerName, TimerTicks, MapName FROM PlayerRecords";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string steamId = reader.GetString(0);
                                string playerName = reader.GetString(1);
                                int timerTicks = reader.GetInt32(2);
                                string mapName = reader.GetString(3);

                                Directory.CreateDirectory(playerRecordsPathh);

                                Dictionary<string, PlayerRecord> records;
                                string filePath = Path.Combine(playerRecordsPathh, $"{mapName}.json");
                                if (File.Exists(filePath))
                                {
                                    string existingJson = await File.ReadAllTextAsync(filePath);
                                    records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(existingJson) ?? new Dictionary<string, PlayerRecord>();
                                }
                                else
                                {
                                    records = new Dictionary<string, PlayerRecord>();
                                }

                                records[steamId] = new PlayerRecord
                                {
                                    PlayerName = playerName,
                                    TimerTicks = timerTicks
                                };

                                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions
                                {
                                    WriteIndented = true
                                });

                                await File.WriteAllTextAsync(filePath, updatedJson);

                                SharpTimerDebug($"Player records for map {mapName} successfully exported to JSON.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error exporting player records to JSON: {ex.Message}");
            }
        }
    }
}