using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;



namespace SharpTimer
{
    [MinimumApiVersion(116)]
    public class MapInfo
    {
        public string? MapStartTrigger { get; set; }
        public string? MapStartC1 { get; set; }
        public string? MapStartC2 { get; set; }
        public string? MapEndTrigger { get; set; }
        public string? MapEndC1 { get; set; }
        public string? MapEndC2 { get; set; }
        public string? RespawnPos { get; set; }
    }

    public class PlayerTimerInfo
    {
        public bool IsTimerRunning { get; set; }
        public int TimerTicks { get; set; }
        public string? TimerRank { get; set; }
        public string? PB { get; set; }
        public int CheckpointIndex { get; set; }
        public bool Azerty { get; set; }
        public bool HideTimerHud { get; set; }
        public int TicksSinceLastCmd { get; set; }
        public Dictionary<string, PlayerRecord>? SortedCachedRecords { get; set; }
        public CCSPlayer_MovementServices? MovementService { get; set; }
    }

    public class PlayerRecord
    {
        public string? PlayerName { get; set; }
        public int TimerTicks { get; set; }
    }

    public class PlayerCheckpoint
    {
        public string? PositionString { get; set; }
        public string? RotationString { get; set; }
        public string? SpeedString { get; set; }
    }

    public partial class SharpTimer : BasePlugin
    {
        private Dictionary<int, PlayerTimerInfo> playerTimers = new Dictionary<int, PlayerTimerInfo>();
        private Dictionary<int, List<PlayerCheckpoint>> playerCheckpoints = new Dictionary<int, List<PlayerCheckpoint>>();
        private Dictionary<int, CCSPlayerController> connectedPlayers = new Dictionary<int, CCSPlayerController>();

        public override string ModuleName => "SharpTimer";
        public override string ModuleVersion => "0.1.0";
        public override string ModuleAuthor => "DEAFPS https://github.com/DEAFPS/";
        public override string ModuleDescription => "A simple CSS Timer Plugin";
        public string msgPrefix = $" {ChatColors.Green} [SharpTimer] {ChatColors.White}";
        public string currentMapStartTrigger = "trigger_startzone";
        public string currentMapEndTrigger = "trigger_endzone";
        public Vector currentMapStartC1 = new Vector(0, 0, 0);
        public Vector currentMapStartC2 = new Vector(0, 0, 0);
        public Vector currentMapEndC1 = new Vector(0, 0, 0);
        public Vector currentMapEndC2 = new Vector(0, 0, 0);
        public Vector currentRespawnPos = new Vector(0, 0, 0);

        public bool useMySQL = false;

        public bool useTriggers = true;
        public bool respawnEnabled = true;
        public bool topEnabled = true;
        public bool rankEnabled = true;
        public bool pbComEnabled = true;
        public bool removeLegsEnabled = true;
        public bool cpEnabled = false;
        public bool removeCpRestrictEnabled = false;
        public bool connectMsgEnabled = true;
        public bool srEnabled = true;
        public int srTimer = 120;
        public int rankHUDTimer = 170;
        public bool resetTriggerTeleportSpeedEnabled = false;
        public bool maxStartingSpeedEnabled = true;
        public int maxStartingSpeed = 320;
        public bool isADTimerRunning = false;
        public bool isRankHUDTimerRunning = false;
        public bool removeCrouchFatigueEnabled = true;

        public string beepSound = "sounds/ui/csgo_ui_button_rollover_large.vsnd";
        public string respawnSound = "sounds/ui/menu_accept.vsnd";
        public string cpSound = "sounds/ui/counter_beep.vsnd";
        public string cpSoundAir = "sounds/ui/weapon_cant_buy.vsnd";
        public string tpSound = "sounds/ui/buttonclick.vsnd";
        public string? mySQLpath;
        public string? playerRecordsPath;
        public string? currentMapName;

        public override void Load(bool hotReload)
        {
            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(Server.GameDirectory + "/csgo/cfg", mysqlConfigFileName);

            currentMapName = Server.MapName;

            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {

                    connectedPlayers[player.Slot] = player;

                    Console.WriteLine($"Added player {player.PlayerName} with UserID {player.UserId} to connectedPlayers");
                    playerTimers[player.Slot] = new PlayerTimerInfo();

                    if (connectMsgEnabled == true) Server.PrintToChatAll($"{msgPrefix}Player {ChatColors.Red}{player.PlayerName} {ChatColors.White}connected!");

                    player.PrintToChat($"{msgPrefix}Welcome {ChatColors.Red}{player.PlayerName} {ChatColors.White}to the server!");

                    player.PrintToChat($"{msgPrefix}Avalible Commands:");

                    if (respawnEnabled) player.PrintToChat($"{msgPrefix}!r (css_r) - Respawns you");
                    if (topEnabled) player.PrintToChat($"{msgPrefix}!top (css_top) - Lists top 10 records on this map");
                    if (rankEnabled) player.PrintToChat($"{msgPrefix}!rank (css_rank) - Shows your current rank");
                    if (pbComEnabled) player.PrintToChat($"{msgPrefix}!pb (css_pb) - Shows your current PB");

                    if (cpEnabled)
                    {
                        player.PrintToChat($"{msgPrefix}!cp (css_cp) - Sets a Checkpoint");
                        player.PrintToChat($"{msgPrefix}!tp (css_tp) - Teleports you to the last Checkpoint");
                        player.PrintToChat($"{msgPrefix}!prevcp (css_prevcp) - Teleports you to the previous Checkpoint");
                        player.PrintToChat($"{msgPrefix}!nextcp (css_nextcp) - Teleports you to the next Checkpoint");
                    }

                    _ = HandleSetPlayerPlacementWithTotal(player, player.SteamID.ToString(), player.Slot);

                    playerTimers[player.Slot].MovementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle);
                    playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();

                    //_ = PBCommandHandler(player, player.SteamID.ToString(), player.Slot);

                    if (removeLegsEnabled == true) player.PlayerPawn.Value.Render = Color.FromArgb(254, 254, 254, 254);

                    string SteamID = player.SteamID.ToString();
                    if (GetPlayerSettingFromDatabase(SteamID, "azerty") == true)
                    {
                        playerTimers[player.Slot].Azerty = true;
                    }
                    else
                    {
                        playerTimers[player.Slot].Azerty = false;
                    }

                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                LoadConfig();
                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {
                    if (connectedPlayers.TryGetValue(player.Slot, out var connectedPlayer))
                    {
                        connectedPlayers.Remove(player.Slot);
                        playerTimers.Remove(player.Slot);
                        Console.WriteLine($"Removed player {connectedPlayer.PlayerName} with UserID {connectedPlayer.UserId} from connectedPlayers");
                        Console.WriteLine(string.Join(", ", connectedPlayers.Values));

                        if (connectMsgEnabled == true) Server.PrintToChatAll($"{msgPrefix}Player {ChatColors.Red}{connectedPlayer.PlayerName} {ChatColors.White}disconnected!");
                    }

                    return HookResult.Continue;
                }
            });

            RegisterListener<Listeners.OnTick>(() =>
            {
                foreach (var playerEntry in connectedPlayers)
                {
                    var player = playerEntry.Value;

                    if (player.IsValid && !player.IsBot && player.PawnIsAlive)
                    {
                        var buttons = player.Buttons;
                        string formattedPlayerVel = Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()).ToString().PadLeft(4, '0');
                        string playerTime = FormatTime(playerTimers[player.Slot].TimerTicks);
                        string forwardKey = "W";
                        string leftKey = "A";
                        string backKey = "S";
                        string rightKey = "D";

                        if (playerTimers[player.Slot].Azerty == true)
                        {
                            forwardKey = "Z";
                            leftKey = "Q";
                            backKey = "S";
                            rightKey = "D";
                        }

                        if (playerTimers[player.Slot].IsTimerRunning)
                        {
                            if (playerTimers[player.Slot].HideTimerHud != true) player.PrintToCenterHtml(
                                $"<font color='gray'>{GetPlayerPlacement(player)}</font> <font class='fontSize-l' color='green'>{playerTime}</font><br>" +
                                $"<font color='white'>Speed:</font> <font color='orange'>{formattedPlayerVel}</font><br>" +
                                $"<font class='fontSize-s' color='gray'>{playerTimers[player.Slot].TimerRank}</font><br>" +
                                $"<font color='white'>{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
                                $"{((buttons & PlayerButtons.Forward) != 0 ? forwardKey : "_")} " +
                                $"{((buttons & PlayerButtons.Moveright) != 0 ? rightKey : "_")} " +
                                $"{((buttons & PlayerButtons.Back) != 0 ? backKey : "_")} " +
                                $"{((buttons & PlayerButtons.Jump) != 0 ? "J" : "_")} " +
                                $"{((buttons & PlayerButtons.Duck) != 0 ? "C" : "_")}</font>");

                            playerTimers[player.Slot].TimerTicks++;
                        }
                        else
                        {
                            if (playerTimers[player.Slot].HideTimerHud != true) player.PrintToCenterHtml(
                                $"<font color='white'>Speed:</font> <font color='orange'>{formattedPlayerVel}</font><br>" +
                                $"<font class='fontSize-s' color='gray'>{playerTimers[player.Slot].TimerRank}</font><br>" +
                                $"<font color='white'>{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
                                $"{((buttons & PlayerButtons.Forward) != 0 ? forwardKey : "_")} " +
                                $"{((buttons & PlayerButtons.Moveright) != 0 ? rightKey : "_")} " +
                                $"{((buttons & PlayerButtons.Back) != 0 ? backKey : "_")} " +
                                $"{((buttons & PlayerButtons.Jump) != 0 ? "J" : "_")} " +
                                $"{((buttons & PlayerButtons.Duck) != 0 ? "C" : "_")}</font>");
                        }

                        if (!useTriggers)
                        {
                            CheckPlayerActions(player);
                        }

                        if (playerTimers[player.Slot].MovementService != null && removeCrouchFatigueEnabled == true)
                        {
                            if (playerTimers[player.Slot].MovementService.DuckSpeed != 7.0f) playerTimers[player.Slot].MovementService.DuckSpeed = 7.0f;
                        }

                        playerTimers[player.Slot].TicksSinceLastCmd++;
                    }
                }
            });

            /* VirtualFunctions.CBaseTrigger_StartTouchFunc.Hook(h =>
            {
                var trigger = h.GetParam<CBaseTrigger>(0);
                var entity = h.GetParam<CBaseEntity>(1);

                if (trigger.DesignerName != "trigger_multiple" || entity.DesignerName != "player" || useTriggers == false)
                    return HookResult.Continue;

                var player = new CCSPlayerController(new CCSPlayerPawn(entity.Handle).Controller.Value.Handle);
                if (player == null) return HookResult.Continue;
                if (!connectedPlayers.ContainsKey(player.Slot))
                    return HookResult.Continue;  // Player not in connectedPlayers, do nothing

                if (trigger.DesignerName == "trigger_multiple" && trigger.Entity.Name == currentMapEndTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot) && playerTimers[player.Slot].IsTimerRunning)
                {
                    OnTimerStop(player);
                    return HookResult.Continue;
                }

                if (trigger.DesignerName == "trigger_multiple" && trigger.Entity.Name == currentMapStartTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot))
                {
                    OnTimerStart(player);
                    return HookResult.Continue;
                }

                return HookResult.Continue;
            }, HookMode.Post);

            VirtualFunctions.CBaseTrigger_EndTouchFunc.Hook(h =>
            {
                var trigger = h.GetParam<CBaseTrigger>(0);
                var entity = h.GetParam<CBaseEntity>(1);

                if (resetTriggerTeleportSpeedEnabled == true)
                {
                    if (!(trigger.DesignerName == "trigger_multiple" || trigger.DesignerName == "trigger_teleport") || entity.DesignerName != "player" || useTriggers == false)
                        return HookResult.Continue;
                }
                else
                {
                    if (trigger.DesignerName != "trigger_multiple" || entity.DesignerName != "player" || useTriggers == false)
                        return HookResult.Continue;
                }

                var player = new CCSPlayerController(new CCSPlayerPawn(entity.Handle).Controller.Value.Handle);
                if (player == null) return HookResult.Continue;
                if (!connectedPlayers.ContainsKey(player.Slot))
                    return HookResult.Continue;  // Player not in connectedPlayers, do nothing

                if (trigger.Entity.Name == currentMapStartTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot))
                {
                    OnTimerStart(player);

                    if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                    {
                        AdjustPlayerVelocity(player, maxStartingSpeed);
                    }
                    return HookResult.Continue;
                }

                if (trigger.DesignerName == "trigger_teleport" && player.IsValid)
                {
                    if (resetTriggerTeleportSpeedEnabled == true) AdjustPlayerVelocity(player, 0);
                    return HookResult.Continue;
                }

                return HookResult.Continue;
            }, HookMode.Post); */

            HookEntityOutput("trigger_multiple", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        //Console.WriteLine($"[EntityOutputHook Attribute] Trigger touched! ({name}, {activator}, {caller.Entity.Name}, {delay})");

                        if (activator.DesignerName != "player" || useTriggers == false)
                            return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);
                        if (player == null) return HookResult.Continue;
                        if (!connectedPlayers.ContainsKey(player.Slot))
                            return HookResult.Continue;  // Player not in connectedPlayers, do nothing

                        if (caller.Entity.Name == currentMapEndTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot) && playerTimers[player.Slot].IsTimerRunning)
                        {
                            OnTimerStop(player);
                            return HookResult.Continue;
                        }

                        if (caller.Entity.Name == currentMapStartTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot))
                        {
                            OnTimerStart(player);
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_multiple", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        //Console.WriteLine($"[EntityOutputHook Attribute] Trigger touched ended! ({name}, {activator}, {caller.Entity.Name}, {delay})");

                        if (activator.DesignerName != "player" || useTriggers == false)
                            return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);
                        if (player == null) return HookResult.Continue;
                        if (!connectedPlayers.ContainsKey(player.Slot))
                            return HookResult.Continue;  // Player not in connectedPlayers, do nothing

                        if (caller.Entity.Name == currentMapEndTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot) && playerTimers[player.Slot].IsTimerRunning)
                        {
                            OnTimerStop(player);
                            if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        if (caller.Entity.Name == currentMapStartTrigger && player.IsValid && playerTimers.ContainsKey(player.Slot))
                        {
                            OnTimerStart(player);
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_teleport", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        //Console.WriteLine($"[EntityOutputHook Attribute] Trigger teleport touched ended! ({name}, {activator}, {caller.Entity.Name}, {delay})");

                        if (activator.DesignerName != "player" || resetTriggerTeleportSpeedEnabled == false)
                            return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);
                        if (player == null) return HookResult.Continue;
                        if (!connectedPlayers.ContainsKey(player.Slot))
                            return HookResult.Continue;  // Player not in connectedPlayers, do nothing

                        if (player.IsValid && resetTriggerTeleportSpeedEnabled == true)
                        {
                            AdjustPlayerVelocity(player, 0);
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            Console.WriteLine("[SharpTimer] Plugin Loaded");
        }

        private void CheckPlayerActions(CCSPlayerController? player)
        {
            if (player == null) return;

            Vector incorrectVector = new Vector(0, 0, 0);

            Vector playerPos = player.Pawn.Value.CBodyComponent!.SceneNode.AbsOrigin;

            if (IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2) && currentMapStartC1 != incorrectVector && currentMapStartC2 != incorrectVector && currentMapEndC1 != incorrectVector && currentMapEndC2 != incorrectVector)
            {
                OnTimerStart(player);

                if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                {
                    AdjustPlayerVelocity(player, maxStartingSpeed);
                }
            }

            if (IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2) && currentMapStartC1 != incorrectVector && currentMapStartC2 != incorrectVector && currentMapEndC1 != incorrectVector && currentMapEndC2 != incorrectVector)
            {
                OnTimerStop(player);
            }
        }

        public void OnTimerStart(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid) return;

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            playerTimers[player.Slot].IsTimerRunning = true;
            playerTimers[player.Slot].TimerTicks = 0;
        }

        public void OnTimerStop(CCSPlayerController? player)
        {
            if (player == null || playerTimers[player.Slot].IsTimerRunning == false || !player.IsValid) return;

            int currentTicks = playerTimers[player.Slot].TimerTicks;
            int previousRecordTicks = GetPreviousPlayerRecord(player);

            SavePlayerTime(player, currentTicks);
            if (useMySQL == true) _ = SavePlayerTimeToDatabase(player, currentTicks, player.SteamID.ToString(), player.PlayerName, player.Slot);
            playerTimers[player.Slot].IsTimerRunning = false;

            string timeDifference = "";
            char ifFirstTimeColor;
            if (previousRecordTicks != 0)
            {
                timeDifference = FormatTimeDifference(currentTicks, previousRecordTicks);
                ifFirstTimeColor = ChatColors.Red;
            }
            else
            {
                ifFirstTimeColor = ChatColors.Yellow;
            }

            if (currentTicks < previousRecordTicks)
            {
                Server.PrintToChatAll(msgPrefix + $"{ChatColors.Green}{player.PlayerName} {ChatColors.White}just finished the map in: {ChatColors.Green}[{FormatTime(currentTicks)}]! {timeDifference}");
            }
            else if (currentTicks > previousRecordTicks)
            {
                Server.PrintToChatAll(msgPrefix + $"{ChatColors.Green}{player.PlayerName} {ChatColors.White}just finished the map in: {ifFirstTimeColor}[{FormatTime(currentTicks)}]! {timeDifference}");
            }
            else
            {
                Server.PrintToChatAll(msgPrefix + $"{ChatColors.Green}{player.PlayerName} {ChatColors.White}just finished the map in: {ChatColors.Yellow}[{FormatTime(currentTicks)}]! (No change in time)");
            }

            if (useMySQL == false) _ = HandleSetPlayerPlacementWithTotal(player, player.SteamID.ToString(), player.Slot);

            player.ExecuteClientCommand($"play {beepSound}");
        }

        public async Task HandleSetPlayerPlacementWithTotal(CCSPlayerController? player, string steamId, int playerSlot)
        {
            playerTimers[playerSlot].TimerRank = await GetPlayerPlacementWithTotal(player, steamId, playerSlot);
        }

        [ConsoleCommand("css_azerty", "Switches layout to AZERTY")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AzertySwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (playerTimers[player.Slot].Azerty == true)
            {
                playerTimers[player.Slot].Azerty = false;
                SavePlayerSettingToDatabase(player.SteamID.ToString(), "azerty", false);
            }
            else
            {
                playerTimers[player.Slot].Azerty = true;
                SavePlayerSettingToDatabase(player.SteamID.ToString(), "azerty", true);
            }
        }

        [ConsoleCommand("css_hud", "Draws/Hides The timer HUD")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void HUDSwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (playerTimers[player.Slot].HideTimerHud == true)
            {
                playerTimers[player.Slot].HideTimerHud = false;
                player.PrintToChat($"Hide Timer HUD set to: {ChatColors.Green}{playerTimers[player.Slot].HideTimerHud}");
                SavePlayerSettingToDatabase(player.SteamID.ToString(), "DrawTimerHud", false);
                return;
            }
            else
            {
                playerTimers[player.Slot].HideTimerHud = true;
                player.PrintToChat($"Hide Timer HUD set to: {ChatColors.Green}{playerTimers[player.Slot].HideTimerHud}");
                SavePlayerSettingToDatabase(player.SteamID.ToString(), "DrawTimerHud", true);
                return;
            }
        }

        [ConsoleCommand("css_top", "Prints top players of this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void PrintTopRecords(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || topEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            _ = PrintTopRecordsHandler(player);
        }

        public async Task PrintTopRecordsHandler(CCSPlayerController? player)
        {
            Dictionary<string, PlayerRecord> sortedRecords;
            if (useMySQL == true)
            {
                sortedRecords = await GetSortedRecordsFromDatabase();
            }
            else
            {
                sortedRecords = GetSortedRecords();
            }

            if (sortedRecords.Count == 0)
            {
                Server.NextFrame(() => player.PrintToChat(msgPrefix + $" No records available for {currentMapName}."));
                //ReplyToPlayer(player, msgPrefix + $" No records available for {currentMapName}.");
                return;
            }


            Server.NextFrame(() => player.PrintToChat(msgPrefix + $" Top 10 Records for {currentMapName}:"));
            //ReplyToPlayer(player, msgPrefix + $" Top 10 Records for {currentMapName}:");
            int rank = 1;

            foreach (var kvp in sortedRecords.Take(10))
            {
                string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                Server.NextFrame(() =>
                {
                    player.PrintToChat(msgPrefix + $" #{rank}: {ChatColors.Green}{playerName} {ChatColors.White}- {ChatColors.Green}{FormatTime(timerTicks)}");
                    rank++;
                });
                //ReplyToPlayer(player, msgPrefix + $" #{rank}: {ChatColors.Green}{playerName} {ChatColors.White}- {ChatColors.Green}{FormatTime(timerTicks)}");
            }
        }

        [ConsoleCommand("css_rank", "Tells you your rank on this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RankCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || rankEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot);
        }

        public async Task RankCommandHandler(CCSPlayerController? player, string steamId, int playerSlot)
        {
            string ranking = await GetPlayerPlacementWithTotal(player, steamId, playerSlot);

            playerTimers[playerSlot].TimerRank = ranking;

            Server.NextFrame(() => player.PrintToChat(msgPrefix + $" You are currently {ChatColors.Green}{ranking}"));
        }

        [ConsoleCommand("css_pb", "Tells you your pb on this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void PBCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || rankEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            _ = PBCommandHandler(player, player.SteamID.ToString(), player.Slot);
        }

        public async Task PBCommandHandler(CCSPlayerController? player, string steamId, int playerSlot)
        {
            int pbTicks;
            if (useMySQL == false)
            {
                pbTicks = GetPreviousPlayerRecord(player);
            }
            else
            {
                pbTicks = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName);
            }

            playerTimers[playerSlot].PB = FormatTime(pbTicks);

            Server.NextFrame(() => player.PrintToChat(msgPrefix + $" You are currently {ChatColors.Green}{FormatTime(pbTicks)}"));
        }

        [ConsoleCommand("css_pb", "Tells you the Server record on this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SRCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || rankEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            _ = SRCommandHandler(player);
        }

        public async Task SRCommandHandler(CCSPlayerController? player)
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
        }

        [ConsoleCommand("css_r", "Teleports you to start")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RespawnPlayer(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || respawnEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (currentRespawnPos == new Vector(0, 0, 0))
            {
                player.PrintToChat(msgPrefix + $" {ChatColors.LightRed} No RespawnPos found for current map!");
                return;
            }

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            if (useTriggers == true)
            {
                player.PlayerPawn.Value.Teleport(FindStartTriggerPos(), new QAngle(0, 90, 0), new Vector(0, 0, 0));
            }
            else
            {
                player.PlayerPawn.Value.Teleport(currentRespawnPos, new QAngle(0, 90, 0), new Vector(0, 0, 0));
            }
            playerTimers[player.Slot].IsTimerRunning = false;
            playerTimers[player.Slot].TimerTicks = 0;
            playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();
            player.ExecuteClientCommand($"play {respawnSound}");
        }

        [ConsoleCommand("css_cp", "Sets a checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SetPlayerCP(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || cpEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            if (!player.PlayerPawn.Value.OnGroundLastTick && removeCpRestrictEnabled == false)
            {
                player.PrintToChat(msgPrefix + $"{ChatColors.LightRed}Cant set checkpoint while in air");
                player.ExecuteClientCommand($"play {cpSoundAir}");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            // Get the player's current position and rotation
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);
            Vector currentSpeed = player.PlayerPawn.Value.AbsVelocity ?? new Vector(0, 0, 0);
            QAngle currentRotation = player.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0);

            // Convert position and rotation to strings
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";
            string rotationString = $"{currentRotation.X} {currentRotation.Y} {currentRotation.Z}";
            string speedString = $"{currentSpeed.X} {currentSpeed.Y} {currentSpeed.Z}";

            // Add the current position and rotation strings to the player's checkpoint list
            if (!playerCheckpoints.ContainsKey(player.Slot))
            {
                playerCheckpoints[player.Slot] = new List<PlayerCheckpoint>();
            }

            playerCheckpoints[player.Slot].Add(new PlayerCheckpoint
            {
                PositionString = positionString,
                RotationString = rotationString,
                SpeedString = speedString
            });

            // Get the count of checkpoints for this player
            int checkpointCount = playerCheckpoints[player.Slot].Count;

            // Print the chat message with the checkpoint count
            player.PrintToChat(msgPrefix + $"Checkpoint set! {ChatColors.Green}#{checkpointCount}");
            player.ExecuteClientCommand($"play {cpSound}");
        }

        [ConsoleCommand("css_tp", "Tp to the most recent checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpPlayerCP(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || cpEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            // Check if the player has any checkpoints
            if (!playerCheckpoints.ContainsKey(player.Slot) || playerCheckpoints[player.Slot].Count == 0)
            {
                player.PrintToChat(msgPrefix + "No checkpoints set!");
                return;
            }

            // Get the most recent checkpoint from the player's list
            PlayerCheckpoint lastCheckpoint = playerCheckpoints[player.Slot].Last();

            // Convert position and rotation strings to Vector and QAngle
            Vector position = ParseVector(lastCheckpoint.PositionString ?? "0 0 0");
            QAngle rotation = ParseQAngle(lastCheckpoint.RotationString ?? "0 0 0");
            Vector speed = ParseVector(lastCheckpoint.SpeedString ?? "0 0 0");

            // Teleport the player to the most recent checkpoint, including the saved rotation
            if (removeCpRestrictEnabled == true)
            {
                player.PlayerPawn.Value.Teleport(position, rotation, speed);
            }
            else
            {
                player.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));
            }

            // Play a sound or provide feedback to the player
            player.ExecuteClientCommand($"play {tpSound}");
            player.PrintToChat(msgPrefix + "Teleported to most recent checkpoint!");
        }

        [ConsoleCommand("css_prevcp", "Tp to the previous checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpPreviousCP(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !cpEnabled) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (!playerCheckpoints.TryGetValue(player.Slot, out List<PlayerCheckpoint> checkpoints) || checkpoints.Count == 0)
            {
                player.PrintToChat(msgPrefix + "No checkpoints set!");
                return;
            }

            int index = playerTimers.TryGetValue(player.Slot, out var timer) ? timer.CheckpointIndex : 0;

            if (checkpoints.Count == 1)
            {
                TpPlayerCP(player, command);
            }
            else
            {
                // Calculate the index of the previous checkpoint, circling back if necessary
                index = (index - 1 + checkpoints.Count) % checkpoints.Count;

                PlayerCheckpoint previousCheckpoint = checkpoints[index];

                // Update the player's checkpoint index
                playerTimers[player.Slot].CheckpointIndex = index;

                // Convert position and rotation strings to Vector and QAngle
                Vector position = ParseVector(previousCheckpoint.PositionString ?? "0 0 0");
                QAngle rotation = ParseQAngle(previousCheckpoint.RotationString ?? "0 0 0");

                // Teleport the player to the previous checkpoint, including the saved rotation
                player.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));

                // Play a sound or provide feedback to the player
                player.ExecuteClientCommand($"play {tpSound}");
                player.PrintToChat(msgPrefix + "Teleported to the previous checkpoint!");
            }
        }

        [ConsoleCommand("css_nextcp", "Tp to the next checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpNextCP(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !cpEnabled) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < 64)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (!playerCheckpoints.TryGetValue(player.Slot, out List<PlayerCheckpoint> checkpoints) || checkpoints.Count == 0)
            {
                player.PrintToChat(msgPrefix + "No checkpoints set!");
                return;
            }

            int index = playerTimers.TryGetValue(player.Slot, out var timer) ? timer.CheckpointIndex : 0;

            if (checkpoints.Count == 1)
            {
                TpPlayerCP(player, command);
            }
            else
            {
                // Calculate the index of the next checkpoint, circling back if necessary
                index = (index + 1) % checkpoints.Count;

                PlayerCheckpoint nextCheckpoint = checkpoints[index];

                // Update the player's checkpoint index
                playerTimers[player.Slot].CheckpointIndex = index;

                // Convert position and rotation strings to Vector and QAngle
                Vector position = ParseVector(nextCheckpoint.PositionString ?? "0 0 0");
                QAngle rotation = ParseQAngle(nextCheckpoint.RotationString ?? "0 0 0");

                // Teleport the player to the next checkpoint, including the saved rotation
                player.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));

                // Play a sound or provide feedback to the player
                player.ExecuteClientCommand($"play {tpSound}");
                player.PrintToChat(msgPrefix + "Teleported to the next checkpoint!");
            }
        }
    }
}
