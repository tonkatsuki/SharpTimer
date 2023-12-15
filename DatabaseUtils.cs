using System.Drawing;
using System.Reflection;
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
        private static string GetConnectionStringFromConfigFile(string mySQLpath)
        {
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

                return $"Server={host};Database={database};User ID={username};Password={password};Port={port};CharSet=utf8mb4;";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading MySQL config file: {ex.Message}");
                return "Server=localhost;Database=database;User ID=root;Password=root;Port=3306;CharSet=utf8mb4;";
            }
        }

        public async Task SavePlayerTimeToDatabase(CCSPlayerController? player, int timerTicks, string steamId, string playerName, int playerSlot)
        {
            if (player == null) return;
            if (playerTimers[playerSlot].IsTimerRunning == false) return;

            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                {
                    await connection.OpenAsync();

                    string formattedTime = FormatTime(timerTicks); // Assuming FormatTime method is available

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS PlayerRecords (
                    MapName VARCHAR(255),
                    SteamID VARCHAR(255),
                    PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
                    TimerTicks INT,
                    TimesCompleted INT,
                    FormattedTime VARCHAR(255),
                    PRIMARY KEY (MapName, SteamID)
                )";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    // Check if all columns from createTableQuery exist
                    var tableColumnsQuery = "SHOW COLUMNS FROM PlayerRecords";
                    using (var tableColumnsCommand = new MySqlCommand(tableColumnsQuery, connection))
                    {
                        using (var reader = await tableColumnsCommand.ExecuteReaderAsync())
                        {
                            var columnsToCheck = new List<string> { "MapName", "SteamID", "PlayerName", "TimerTicks", "TimesCompleted", "FormattedTime" };

                            while (await reader.ReadAsync())
                            {
                                string columnName = reader["Field"].ToString();
                                columnsToCheck.Remove(columnName); // Remove the checked columns

                                if (columnsToCheck.Count == 0)
                                {
                                    // All columns are present, break the loop
                                    break;
                                }
                            }

                            // If any columns are not present, add them
                            foreach (var missingColumn in columnsToCheck)
                            {
                                var addColumnQuery = $"ALTER TABLE PlayerRecords ADD COLUMN {missingColumn} VARCHAR(255) DEFAULT NULL";
                                using (var addColumnCommand = new MySqlCommand(addColumnQuery, connection))
                                {
                                    await addColumnCommand.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                    // Update or insert the record
                    string upsertQuery = "INSERT INTO PlayerRecords (MapName, SteamID, PlayerName, TimerTicks, TimesCompleted, FormattedTime) VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, 1, @FormattedTime) ON DUPLICATE KEY UPDATE TimesCompleted = TimesCompleted + 1, TimerTicks = IF(@TimerTicks < TimerTicks, @TimerTicks, TimerTicks), FormattedTime = IF(@TimerTicks < TimerTicks, @FormattedTime, FormattedTime)";
                    using (var upsertCommand = new MySqlCommand(upsertQuery, connection))
                    {
                        upsertCommand.Parameters.AddWithValue("@MapName", currentMapName);
                        upsertCommand.Parameters.AddWithValue("@SteamID", steamId);
                        upsertCommand.Parameters.AddWithValue("@PlayerName", playerName);
                        upsertCommand.Parameters.AddWithValue("@TimerTicks", timerTicks);
                        upsertCommand.Parameters.AddWithValue("@FormattedTime", formattedTime);

                        await upsertCommand.ExecuteNonQueryAsync();
                    }
                }

                if (useMySQL == true) _ = HandleSetPlayerPlacementWithTotal(player, steamId, playerSlot);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving player time to MySQL: {ex.Message}");
            }
        }

        public async Task<int> GetPreviousPlayerRecordFromDatabase(CCSPlayerController? player, string steamId, string currentMapName)
        {
            if (player == null)
            {
                return 0;
            }

            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                {
                    await connection.OpenAsync();

                    // Check if the table exists
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci, TimerTicks INT, TimesCompleated INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    // Retrieve the TimerTicks value for the specified player on the current map
                    string selectQuery = "SELECT TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("@MapName", currentMapName);
                        selectCommand.Parameters.AddWithValue("@SteamID", steamId);

                        var result = await selectCommand.ExecuteScalarAsync();

                        // Check for DBNull
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting previous player record from MySQL: {ex.Message}");
            }

            return 0;
        }

        public async Task<Dictionary<string, PlayerRecord>> GetSortedRecordsFromDatabase()
        {
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
                        selectCommand.Parameters.AddWithValue("@MapName", currentMapName);
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

                            return sortedRecords;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sorted records from MySQL: {ex.Message}");
            }

            return new Dictionary<string, PlayerRecord>();
        }

        public async Task SavePlayerStatToDatabase(string SteamID, string stat, string state)
        {
            try
            {
                await Task.Run(() =>
                {
                    using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                    {
                        connection.Open();

                        // Check if the table exists, and create it if necessary
                        string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerSettings (SteamID VARCHAR(255), TimesConnected INT, Azerty bool, HideTimerHud bool, SoundsEnabled bool, PRIMARY KEY (SteamID))";
                        using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                        {
                            createTableCommand.ExecuteNonQuery();
                        }

                        // Use SteamID and stat to locate a player in the table and then set stat to the state
                        string upsertQuery = $"INSERT INTO PlayerSettings (SteamID, {stat}) VALUES (@SteamID, @State) ON DUPLICATE KEY UPDATE {stat} = @State";
                        using (var upsertCommand = new MySqlCommand(upsertQuery, connection))
                        {
                            upsertCommand.Parameters.AddWithValue("@SteamID", SteamID);
                            upsertCommand.Parameters.AddWithValue("@State", state);

                            upsertCommand.ExecuteNonQuery();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving player stat to MySQL: {ex.Message}");
            }
        }

        public async Task GetPlayerSettingFromDatabase(CCSPlayerController? player, string setting)
        {
            // Ensure player is not null
            if (player != null)
            {
                try
                {
                    // Fetch the setting state from the database
                    bool settingState = await Task.Run(() =>
                    {
                        using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile(mySQLpath)))
                        {
                            connection.Open();

                            // Check if the table exists, and create it if necessary
                            string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerSettings (SteamID VARCHAR(255), TimesConnected INT, PlayerPoints INT, Azerty bool, HideTimerHud bool, SoundsEnabled bool, PRIMARY KEY (SteamID))";
                            using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                            {
                                createTableCommand.ExecuteNonQuery();
                            }

                            // Retrieve the setting state for the specified SteamID and setting
                            string selectQuery = "SELECT SettingState FROM PlayerSettings WHERE SteamID = @SteamID AND SettingName = @SettingName";
                            using (var selectCommand = new MySqlCommand(selectQuery, connection))
                            {
                                selectCommand.Parameters.AddWithValue("@SteamID", player.SteamID);
                                selectCommand.Parameters.AddWithValue("@SettingName", setting);

                                var result = selectCommand.ExecuteScalar();

                                // Check for DBNull
                                if (result != null && result != DBNull.Value)
                                {
                                    return Convert.ToBoolean(result);
                                }
                            }

                            // Default to false if the setting is not found
                            return false;
                        }
                    });

                    // Set the player setting
                    SetPlayerSetting(player.Slot, setting, settingState);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting player setting from database: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Player is null");
            }
        }

        public void SetPlayerSetting(int playerSlot, string setting, bool settingState)
        {
            // Use reflection to get the property with the specified setting name
            var property = typeof(PlayerTimerInfo).GetProperty(setting, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property != null && property.PropertyType == typeof(bool))
            {
                // Set the value of the property for the specified player slot
                property.SetValue(playerTimers[playerSlot], settingState);
            }
            else
            {
                Console.WriteLine($"Invalid setting: {setting}");
            }

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
                    Console.WriteLine($"Error: JSON file not found at {playerRecordsPath}");
                    return;
                }

                string json = await File.ReadAllTextAsync(playerRecordsPath);
                var records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json);

                if (records == null)
                {
                    Console.WriteLine("Error: Failed to deserialize JSON data.");
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

                    Console.WriteLine("JSON times successfully added to the database.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding JSON times to the database: {ex.Message}");
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

                            Console.WriteLine("Player records successfully exported to JSON.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting player records to JSON: {ex.Message}");
            }
        }
    }
}