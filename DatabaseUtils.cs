using System.Drawing;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using Nexd.MySQL;


namespace SharpTimer
{
    partial class SharpTimer
    {
        private static string GetConnectionStringFromConfigFile()
        {
            try
            {
                string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
                string mysqlConfigPath = Path.Join(Server.GameDirectory + "/csgo/cfg", mysqlConfigFileName);

                string jsonString = File.ReadAllText(mysqlConfigPath);
                JsonDocument jsonConfig = JsonDocument.Parse(jsonString);

                JsonElement root = jsonConfig.RootElement;

                string host = root.GetProperty("MySqlHost").GetString();
                string database = root.GetProperty("MySqlDatabase").GetString();
                string username = root.GetProperty("MySqlUsername").GetString();
                string password = root.GetProperty("MySqlPassword").GetString();
                int port = root.GetProperty("MySqlPort").GetInt32();

                return $"Server={host};Database={database};User ID={username};Password={password};Port={port};";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading MySQL config file: {ex.Message}");
                return "Server=localhost;Database=database;User ID=root;Password=root;Port=3306;";
            }
        }

        public async Task SavePlayerTimeToDatabase(CCSPlayerController? player, int timerTicks)
        {
            if (player == null) return;
            if (playerTimers[player.Slot].IsTimerRunning == false) return;

            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile()))
                {
                    await connection.OpenAsync();

                    string currentMapName = Server.MapName;
                    string steamId = player.SteamID.ToString();
                    string playerName = player.PlayerName;
                    string formattedTime = FormatTime(timerTicks); // Assuming FormatTime method is available

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255), TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    // Check if the record already exists or has a higher timer value
                    string selectQuery = "SELECT TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("@MapName", currentMapName);
                        selectCommand.Parameters.AddWithValue("@SteamID", steamId);

                        var existingTimerTicks = await selectCommand.ExecuteScalarAsync();
                        if (existingTimerTicks == null || (int)existingTimerTicks > timerTicks)
                        {
                            // Update or insert the record
                            string upsertQuery = "REPLACE INTO PlayerRecords (MapName, SteamID, PlayerName, TimerTicks, FormattedTime) VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, @FormattedTime)";
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
                    }
                }
                if (useMySQL == true) _ = HandleSetPlayerPlacementWithTotal(player);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving player time to MySQL: {ex.Message}");
            }
        }


        public async Task<int> GetPreviousPlayerRecordFromDatabase(CCSPlayerController? player)
        {
            if (player == null)
            {
                return 0;
            }

            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile()))
                {
                    await connection.OpenAsync();

                    string currentMapName = Server.MapName;
                    string steamId = player.SteamID.ToString();

                    // Check if the table exists
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255), TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
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
                        if (result != null)
                        {
                            return (int)result;
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

        public static async Task<Dictionary<string, PlayerRecord>> GetSortedRecordsFromDatabase()
        {
            string currentMapName = Server.MapName;

            try
            {
                using (var connection = new MySqlConnection(GetConnectionStringFromConfigFile()))
                {
                    await connection.OpenAsync();

                    // Check if the table exists
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255), TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
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
                                string playerName = reader.GetString(1);
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

        public void SavePlayerSettingToDatabase(string SteamID, string setting, bool state)
        {

        }

        public bool GetPlayerSettingFromDatabase(string SteamID, string setting)
        {

            return false;
        }

        [ConsoleCommand("css_jsontodatabase", " ")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void AddJsonTimesToDatabase(CCSPlayerController? player, CommandInfo command)
        {
            try
            {
                string recordsFileName = "SharpTimer/player_records.json";
                string recordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

                if (!File.Exists(recordsPath))
                {
                    Console.WriteLine($"Error: JSON file not found at {recordsPath}");
                    return;
                }

                string json = File.ReadAllText(recordsPath);
                var records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json);

                if (records == null)
                {
                    Console.WriteLine("Error: Failed to deserialize JSON data.");
                    return;
                }

                string connectionString = GetConnectionStringFromConfigFile();

                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = "CREATE TABLE IF NOT EXISTS PlayerRecords (MapName VARCHAR(255), SteamID VARCHAR(255), PlayerName VARCHAR(255), TimerTicks INT, FormattedTime VARCHAR(255), PRIMARY KEY (MapName, SteamID))";
                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        createTableCommand.ExecuteNonQuery();
                    }

                    foreach (var mapEntry in records)
                    {
                        string currentMapName = mapEntry.Key;

                        foreach (var recordEntry in mapEntry.Value)
                        {
                            string steamId = recordEntry.Key;
                            PlayerRecord playerRecord = recordEntry.Value;

                            // Check if the player is already in the database for the map
                            string selectQuery = "SELECT TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID";
                            using (var selectCommand = new MySqlCommand(selectQuery, connection))
                            {
                                selectCommand.Parameters.AddWithValue("@MapName", currentMapName);
                                selectCommand.Parameters.AddWithValue("@SteamID", steamId);

                                var existingTimerTicks = selectCommand.ExecuteScalar();
                                if (existingTimerTicks == null || (int)existingTimerTicks > playerRecord.TimerTicks)
                                {
                                    // Insert the record into the database
                                    string insertQuery = "INSERT INTO PlayerRecords (MapName, SteamID, PlayerName, TimerTicks, FormattedTime) VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, @FormattedTime)";
                                    using (var insertCommand = new MySqlCommand(insertQuery, connection))
                                    {
                                        insertCommand.Parameters.AddWithValue("@MapName", currentMapName);
                                        insertCommand.Parameters.AddWithValue("@SteamID", steamId);
                                        insertCommand.Parameters.AddWithValue("@PlayerName", playerRecord.PlayerName);
                                        insertCommand.Parameters.AddWithValue("@TimerTicks", playerRecord.TimerTicks);
                                        insertCommand.Parameters.AddWithValue("@FormattedTime", FormatTime(playerRecord.TimerTicks));

                                        insertCommand.ExecuteNonQuery();
                                    }
                                }
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
    }
}