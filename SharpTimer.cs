using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;



namespace SharpTimer
{
    [MinimumApiVersion(125)]
    public partial class SharpTimer : BasePlugin
    {
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

                    player.PrintToChat($"{msgPrefix}Available Commands:");

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

                    _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, true);

                    playerTimers[player.Slot].MovementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle);
                    playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();

                    player.Pawn.Value.Glow.Glowing = true;
                    player.Pawn.Value.Glow.GlowColorOverride = Color.OrangeRed;

                    //_ = PBCommandHandler(player, player.SteamID.ToString(), player.Slot);

                    if (removeLegsEnabled == true) player.PlayerPawn.Value.Render = Color.FromArgb(254, 254, 254, 254);

                    //PlayerSettings
                    if(useMySQL == true)
                    {
                        //_ = GetPlayerSettingFromDatabase(player, "Azerty");
                        //_ = GetPlayerSettingFromDatabase(player, "HideTimerHud");
                        //_ = GetPlayerSettingFromDatabase(player, "TimesConnected");
                        //_ = GetPlayerSettingFromDatabase(player, "SoundsEnabled");
                        //_ = SavePlayerStatToDatabase(player.SteamID.ToString(), "MouseSens", player.GetConVarValue("sensitivity").ToString());
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
                        playerCheckpoints.Remove(player.Slot);
                        Console.WriteLine($"Removed player {connectedPlayer.PlayerName} with UserID {connectedPlayer.UserId} from connectedPlayers");

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
                        string formattedPlayerPre = Math.Round(ParseVector(playerTimers[player.Slot].PreSpeed ?? "0 0 0").Length2D()).ToString();
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
                                $"<font color='gray'>{GetPlayerPlacement(player)}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerTime}</font><br>" +
                                $"<font color='{tertiaryHUDcolor}'>Speed:</font> <font color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font><br>" +
                                $"<font class='fontSize-s' color='gray'>{playerTimers[player.Slot].TimerRank} | PB: {playerTimers[player.Slot].PB}</font><br>" +
                                $"<font color='{tertiaryHUDcolor}'>{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
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
                                $"<font color='{tertiaryHUDcolor}'>Speed:</font> <font color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font><br>" +
                                $"<font class='fontSize-s' color='gray'>{playerTimers[player.Slot].TimerRank} | PB: {playerTimers[player.Slot].PB}</font><br>" +
                                $"<font color='{tertiaryHUDcolor}'>{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
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

                        if(!player.PlayerPawn.Value.OnGroundLastTick)
                        {
                            playerTimers[player.Slot].TicksInAir++;
                            if(playerTimers[player.Slot].TicksInAir == 1)
                            {
                                playerTimers[player.Slot].PreSpeed = $"{player.PlayerPawn.Value.AbsVelocity.X} {player.PlayerPawn.Value.AbsVelocity.Y} {player.PlayerPawn.Value.AbsVelocity.Z}";
                                //playerTimers[player.Slot].JumpPos = $"{player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin.X} {player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin.Y} {player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin.Z}";
                            }
                        }
                        else
                        {
                            playerTimers[player.Slot].TicksInAir = 0;
                        }

                        playerTimers[player.Slot].TicksSinceLastCmd++;
                    }
                }
            });

            HookEntityOutput("trigger_multiple", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator.DesignerName != "player" || useTriggers == false || activator == null || caller == null)
                            return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (!player.PawnIsAlive || player == null || !connectedPlayers.ContainsKey(player.Slot) || caller.Entity.Name == null) return HookResult.Continue;

                        if (IsValidEndTriggerName(caller.Entity.Name.ToString()) && player.IsValid && playerTimers.ContainsKey(player.Slot) && playerTimers[player.Slot].IsTimerRunning)
                        {
                            OnTimerStop(player);
                            return HookResult.Continue;
                        }

                        if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && player.IsValid && playerTimers.ContainsKey(player.Slot))
                        {
                            playerTimers[player.Slot].IsTimerRunning = false;
                            playerTimers[player.Slot].TimerTicks = 0;
                            playerCheckpoints.Remove(player.Slot);
                            if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_multiple", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator.DesignerName != "player" || useTriggers == false || activator == null || caller == null)
                            return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (!player.PawnIsAlive || player == null || !connectedPlayers.ContainsKey(player.Slot) || caller.Entity.Name == null) return HookResult.Continue;

                        if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && player.IsValid && playerTimers.ContainsKey(player.Slot))
                        {
                            OnTimerStart(player);
                            if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_teleport", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator.DesignerName != "player" || resetTriggerTeleportSpeedEnabled == false)
                            return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (!player.PawnIsAlive || player == null || !connectedPlayers.ContainsKey(player.Slot)) return HookResult.Continue;

                        if (player.IsValid && resetTriggerTeleportSpeedEnabled == true)
                        {
                            AdjustPlayerVelocity(player, 0);
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            /* VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre); */

            Console.WriteLine("[SharpTimer] Plugin Loaded");
        }

        /* private HookResult OnTakeDamage(DynamicHook hook)
            {
                if (disableDamage == false) return HookResult.Continue;

                var entity = hook.GetParam<CEntityInstance>(0);
                var player = new CCSPlayerController(new CCSPlayerPawn(entity.Handle).Controller.Value.Handle);

                if(!player.PawnIsAlive || !player.IsValid || player == null || !connectedPlayers.ContainsKey(player.Slot)) return HookResult.Continue;

                if (disableDamage == true)
                {
                    hook.GetParam<CTakeDamageInfo>(1).Damage = 0;
                }

                return HookResult.Continue;
            } */

        private void CheckPlayerActions(CCSPlayerController? player)
        {
            if (!player.PawnIsAlive || player == null) return;

            Vector incorrectVector = new Vector(0, 0, 0);

            Vector playerPos = player.Pawn.Value.CBodyComponent!.SceneNode.AbsOrigin;

            if (!IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2) && IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2) && currentMapStartC1 != incorrectVector && currentMapStartC2 != incorrectVector && currentMapEndC1 != incorrectVector && currentMapEndC2 != incorrectVector)
            {
                OnTimerStart(player);

                if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                {
                    AdjustPlayerVelocity(player, maxStartingSpeed);
                }
            }

            if (IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2) && !IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2) && currentMapStartC1 != incorrectVector && currentMapStartC2 != incorrectVector && currentMapEndC1 != incorrectVector && currentMapEndC2 != incorrectVector)
            {
                OnTimerStop(player);
            }
        }

        public void OnTimerStart(CCSPlayerController? player)
        {
            if (!player.PawnIsAlive || player == null || !player.IsValid) return;

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            playerTimers[player.Slot].IsTimerRunning = true;
            playerTimers[player.Slot].TimerTicks = 0;
        }

        public void OnTimerStop(CCSPlayerController? player)
        {
            if (!player.PawnIsAlive || player == null || playerTimers[player.Slot].IsTimerRunning == false || !player.IsValid) return;

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

            if (useMySQL == false) _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, true);

            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {beepSound}");
        }

        [ConsoleCommand("css_addstartzone", "Adds a startzone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddStartZoneCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].IsAddingStartZone == true)
            {
                playerTimers[player.Slot].IsAddingStartZone = false;
                playerTimers[player.Slot].IsAddingEndZone = false;
                playerTimers[player.Slot].StartZoneC1 = "";
                playerTimers[player.Slot].StartZoneC2 = "";
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Grey}Startzone cancelled...");
            }
            else
            {
                playerTimers[player.Slot].StartZoneC1 = "";
                playerTimers[player.Slot].StartZoneC2 = "";
                playerTimers[player.Slot].IsAddingStartZone = true;
                playerTimers[player.Slot].IsAddingEndZone = false;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Please stand on one of the opposite start zone corners and type {ChatColors.Green}!c1 & !c2");
                player.PrintToCenter($" {ChatColors.Grey}Please stand on one of the opposite start zone corners and type !c1 & !c2");
                player.PrintToChat($" {ChatColors.Grey}Type !addendzone again to cancel...");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone,");
                player.PrintToChat($" {ChatColors.Grey}!addrespawnpos, !savezones");
            }
        }

        [ConsoleCommand("css_addendzone", "Adds a endzone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddEndZoneCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].IsAddingEndZone == true)
            {
                playerTimers[player.Slot].IsAddingStartZone = false;
                playerTimers[player.Slot].IsAddingEndZone = false;
                playerTimers[player.Slot].EndZoneC1 = "";
                playerTimers[player.Slot].EndZoneC2 = "";
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Grey}Endzone cancelled...");
            }
            else
            {
                playerTimers[player.Slot].EndZoneC1 = "";
                playerTimers[player.Slot].EndZoneC2 = "";
                playerTimers[player.Slot].IsAddingStartZone = false;
                playerTimers[player.Slot].IsAddingEndZone = true;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Please stand on one of the opposite end zone corners and type {ChatColors.Green}!c1 & !c2");
                player.PrintToCenter($" Please stand on one of the opposite end zone corners and type !c1 & !c2");
                player.PrintToChat($" {ChatColors.Grey}Type !addendzone again to cancel...");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone,");
                player.PrintToChat($" {ChatColors.Grey}!addrespawnpos, !savezones");
            }
        }

        [ConsoleCommand("css_addrespawnpos", "Adds a RespawnPos to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddRespawnPosCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            // Get the player's current position
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);

            // Convert position
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";
            playerTimers[player.Slot].RespawnPos = positionString;
            player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} RespawnPos added!");
        }

        [ConsoleCommand("css_savezones", "Saves defined zones")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SaveZonesCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].EndZoneC1 == null || playerTimers[player.Slot].EndZoneC2 == null || playerTimers[player.Slot].StartZoneC1 == null || playerTimers[player.Slot].StartZoneC2 == null || playerTimers[player.Slot].RespawnPos == null)
            {
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Red} Please make sure you have done all 3 zoning steps (!addstartzone, !addendzone, !addrespawnpos)");
                return;
            }

            // Create a new MapInfo object with the necessary data
            MapInfo newMapInfo = new MapInfo
            {
                MapStartC1 = playerTimers[player.Slot].StartZoneC1,
                MapStartC2 = playerTimers[player.Slot].StartZoneC2,
                MapEndC1 = playerTimers[player.Slot].EndZoneC1,
                MapEndC2 = playerTimers[player.Slot].EndZoneC2,
                RespawnPos = playerTimers[player.Slot].RespawnPos
            };

            // Load existing map data from the JSON file
            string mapdataFileName = $"SharpTimer/MapData/{currentMapName}.json"; // Use the map name in the filename
            string mapdataPath = Path.Join(Server.GameDirectory + "/csgo/cfg", mapdataFileName);

            // Save the updated data back to the JSON file without including the map name inside the JSON
            string updatedJson = JsonSerializer.Serialize(newMapInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(mapdataPath, updatedJson);

            // For example, you might want to print a message to the player
            player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default}Zones saved successfully! {ChatColors.Grey}Reloading data...");
            Server.ExecuteCommand("mp_restartgame 1");
        }

        [ConsoleCommand("css_c1", "Adds a zone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddZoneCorner1Command(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            // Get the player's current position
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);

            // Convert position
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";

            //Start Zone
            if (playerTimers[player.Slot].IsAddingStartZone == true)
            {
                playerTimers[player.Slot].StartZoneC1 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Start Zone Corner 1 added!");
            }

            //End Zone
            if (playerTimers[player.Slot].IsAddingEndZone == true)
            {
                playerTimers[player.Slot].EndZoneC1 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} End Zone Corner 1 added!");
            }
        }

        [ConsoleCommand("css_c2", "Adds a zone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddZoneCorner2Command(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            // Get the player's current position
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);

            // Convert position
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";

            //Start Zone
            if (playerTimers[player.Slot].IsAddingStartZone == true)
            {
                playerTimers[player.Slot].StartZoneC2 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Start Zone Corner 2 added!");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone, !addrespawnpos, !savezones");
                playerTimers[player.Slot].IsAddingStartZone = false;
            }

            //End Zone
            if (playerTimers[player.Slot].IsAddingEndZone == true)
            {
                playerTimers[player.Slot].EndZoneC2 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default}End Zone Corner 2 added!");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone, !addrespawnpos, !savezones");
                playerTimers[player.Slot].IsAddingEndZone = false;
            }
        }

        /* [ConsoleCommand("css_noclip", "toggles noclip for admin")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public static void AdminNoclipCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (player.MoveType == (MoveType_t)8)
            {
                player.MoveType = (MoveType_t)2;
            }
            else
            {
                player.MoveType = (MoveType_t)8;
            }
        } */

        [ConsoleCommand("css_azerty", "Switches layout to AZERTY")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AzertySwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (playerTimers[player.Slot].Azerty == true)
            {
                playerTimers[player.Slot].Azerty = false;
                //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "Azerty", false);
            }
            else
            {
                playerTimers[player.Slot].Azerty = true;
                //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "Azerty", true);
            }
        }

        [ConsoleCommand("css_hud", "Draws/Hides The timer HUD")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void HUDSwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (playerTimers[player.Slot].HideTimerHud == true)
            {
                playerTimers[player.Slot].HideTimerHud = false;
                player.PrintToChat($"Hide Timer HUD set to: {ChatColors.Green}{playerTimers[player.Slot].HideTimerHud}");
                //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "HideTimerHud", false);
                return;
            }
            else
            {
                playerTimers[player.Slot].HideTimerHud = true;
                player.PrintToChat($"Hide Timer HUD set to: {ChatColors.Green}{playerTimers[player.Slot].HideTimerHud}");
                //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "HideTimerHud", true);
                return;
            }
        }

        [ConsoleCommand("css_sounds", "Toggles Sounds")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SoundsSwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (playerTimers[player.Slot].SoundsEnabled == true)
            {
                playerTimers[player.Slot].SoundsEnabled = false;
                player.PrintToChat($"Sounds set to: {ChatColors.Green}{playerTimers[player.Slot].SoundsEnabled}");
                //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "SoundsEnabled", false);
                return;
            }
            else
            {
                playerTimers[player.Slot].SoundsEnabled = true;
                player.PrintToChat($"Sounds set to: {ChatColors.Green}{playerTimers[player.Slot].SoundsEnabled}");
                //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "SoundsEnabled", true);
                return;
            }
        }

        [ConsoleCommand("css_top", "Prints top players of this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void PrintTopRecords(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || topEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
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

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot);
        }

        public async Task RankCommandHandler(CCSPlayerController? player, string steamId, int playerSlot, bool toHUD = false)
        {
            string ranking = await GetPlayerPlacementWithTotal(player, steamId, playerSlot);

            int pbTicks;
            if (useMySQL == false)
            {
                pbTicks = GetPreviousPlayerRecord(player);
            }
            else
            {
                pbTicks = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName);
            }

            playerTimers[playerSlot].TimerRank = ranking;
            if(pbTicks != 0)
            {
                playerTimers[playerSlot].PB = FormatTime(pbTicks);
            }
            else
            {
                playerTimers[playerSlot].PB = "n/a";
            }
            
            if(toHUD == false)
            {
                Server.NextFrame(() => player.PrintToChat(msgPrefix + $" You are currently {ChatColors.Green}{ranking}"));
                if(pbTicks != 0) Server.NextFrame(() => player.PrintToChat(msgPrefix + $" Your current PB: {ChatColors.Green}{FormatTime(pbTicks)}"));
            }
        }

        [ConsoleCommand("css_sr", "Tells you the Server record on this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SRCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || rankEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
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

            Server.NextFrame(() => player.PrintToChat($"{msgPrefix} Current Server Record on {ChatColors.Green}{currentMapName}{ChatColors.White}: "));

            foreach (var kvp in sortedRecords.Take(1))
            {
                string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                Server.NextFrame(() => player.PrintToChat(msgPrefix + $" {ChatColors.Green}{playerName} {ChatColors.White}- {ChatColors.Green}{FormatTime(timerTicks)}"));
            }
        }

        [ConsoleCommand("css_r", "Teleports you to start")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RespawnPlayer(CCSPlayerController? player, CommandInfo command)
        {
            if (!player.PawnIsAlive || player == null || respawnEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            if (useTriggers == true)
            {
                if (FindStartTriggerPos() == null)
                {
                    player.PrintToChat(msgPrefix + $" {ChatColors.LightRed} No RespawnPos found for current map!");
                    return;
                }
                player.PlayerPawn.Value.Teleport(FindStartTriggerPos(), player.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0), new Vector(0, 0, 0));
            }
            else
            {
                if (currentRespawnPos == null)
                {
                    player.PrintToChat(msgPrefix + $" {ChatColors.LightRed} No RespawnPos found for current map!");
                    return;
                }
                player.PlayerPawn.Value.Teleport(currentRespawnPos, player.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0), new Vector(0, 0, 0));
            }
            playerTimers[player.Slot].IsTimerRunning = false;
            playerTimers[player.Slot].TimerTicks = 0;
            playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();
            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {respawnSound}");
        }

        [ConsoleCommand("css_cp", "Sets a checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SetPlayerCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!player.PawnIsAlive || player == null || cpEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            if (!player.PlayerPawn.Value.OnGroundLastTick && removeCpRestrictEnabled == false)
            {
                player.PrintToChat(msgPrefix + $"{ChatColors.LightRed}Cant set checkpoint while in air");
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {cpSoundAir}");
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
            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {cpSound}");
        }

        [ConsoleCommand("css_tp", "Tp to the most recent checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpPlayerCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!player.PawnIsAlive || player == null || cpEnabled == false) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
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
            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {tpSound}");
            player.PrintToChat(msgPrefix + "Teleported to most recent checkpoint!");
        }

        [ConsoleCommand("css_prevcp", "Tp to the previous checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpPreviousCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!player.PawnIsAlive || player == null || !cpEnabled) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
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
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {tpSound}");
                player.PrintToChat(msgPrefix + "Teleported to the previous checkpoint!");
            }
        }

        [ConsoleCommand("css_nextcp", "Tp to the next checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpNextCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!player.PawnIsAlive || player == null || !cpEnabled) return;

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
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
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {tpSound}");
                player.PrintToChat(msgPrefix + "Teleported to the next checkpoint!");
            }
        }
    }
}
