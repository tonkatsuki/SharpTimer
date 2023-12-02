using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Nexd.MySQL;


namespace SharpTimer
{
    partial class SharpTimer
    {
        private MySqlDb db = new MySqlDb(GetConnectionStringFromConfigFile());

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

        public bool GetPlayerSettingFromDatabase(string SteamID, string setting)
        {
            
            return false;
        }

        public void SavePlayerTimeToDatabase(CCSPlayerController? player, int timerTicks)
        {
            
            return;
        }

        public static int GetPreviousPlayerRecordFromDatabase(CCSPlayerController? player)
        {
            
            return 0;
        }

        public static Dictionary<string, int> GetSortedRecordsFromDatabase()
        {
            
            return new Dictionary<string, int>();
        }

        public void SavePlayerSettingToDatabase(string SteamID, string setting, bool state)
        {
            
        }
    }
}