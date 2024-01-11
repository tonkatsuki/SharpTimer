<div align="center">
  <img src="https://github.com/DEAFPS/SharpTimer/assets/43534349/8097684d-8b0b-4c39-b093-0acc76ee8fd6" alt="">
</div>

<div align="center">
<a href='https://ko-fi.com/L3L7T5ZSB' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi1.png?v=3' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>
</div>

# SharpTimer
SharpTimer is a simple Surf/KZ/Bhop/MG/Deathrun/etc. CS2 Timer plugin using CounterStrikeSharp<br>


## Features
<details> 
  <summary>Timer, speedometer and key input with color customization</summary>
   <img src="https://i.imgur.com/TxAwgbC.png">
</details>

<details> 
  <summary>Players PB</summary>
  <img src="https://i.imgur.com/9HGOhRR.png">
</details>

<details> 
  <summary>Surf Stages and Checkpoints</summary>
  <img src="https://i.imgur.com/xL2y6vs.png">
</details>

<details> 
    <summary>Bonus stages</summary>
  <img src="https://i.imgur.com/NURlZBK.png">
</details>

<details> 
  <summary>Rank Icons</summary>
  <img src="https://i.imgur.com/7vSKeCv.png">
</details>

<details> 
  <summary>KZ Checkpoint system (disabled by default)</summary>
   <img src="https://i.imgur.com/USX5i8C.png"><br>
   <img src="https://i.imgur.com/kWiHOlz.png"><br>
   <img src="https://i.imgur.com/lXwXNN7.png"><br>
   <img src="https://i.imgur.com/nyn76Q4.png">
</details>

## Dependencies

[**MetaMod**](https://cs2.poggu.me/metamod/installation/)

[**CounterStrikeSharp** *(at least v141)*](https://github.com/roflmuffin/CounterStrikeSharp/releases)

[**MovementUnlocker** *(optional but recommended)*](https://github.com/Source2ZE/MovementUnlocker)

[**Web panel** *(optional but recommended)*](https://github.com/Letaryat/sharptimer-web-panel)

:exclamation: **CS2Fixes** does clash with **CSS** there fore the plugin might not work correctly with it

## Install
* Download the [latest release](https://github.com/DEAFPS/SharpTimer/releases),

* Unzip into your servers `game/csgo/` directory,

* :exclamation: See `game/csgo/cfg/SharpTimer/config.cfg` for basic plugin configuration,

* :exclamation: It is recommended to have a custom server cfg with your desired settings (for example [SURF](https://github.com/DEAFPS/cs-cfg/blob/main/surf.cfg) or [KZ](https://github.com/DEAFPS/cs-cfg/blob/main/kz.cfg)),

## MySQL
* Head over to `game/csgo/cfg/SharpTimer/config.cfg` and enable `sharptimer_mysql_enabled`,

* Configure MySQL connection with database credentials in `mysqlConfig.json`, which is located in the same directory,

## Commands

| Command  | What it does |
| ------------- | ------------- |
| `!r`  | Teleports the player back to Spawn |
| `!rb`  | Teleports the player to a Bonus Spawn |
| `!top`  | Prints the top 10 times on the current map |
| `!topbonus`  | Prints the top 10 bonus times on the current map |
| `!rank` | Tells you your rank on the current map |
| `!sr` | Tells you the current server record of the map |
| `!hud` | Hides the Timer HUD |
| `!keys` | Hides the HUD Keys |
| `!azerty` | Changes Key Layout to Azerty on the HUD |
| `!cp` | Sets a checkpoint |
| `!tp` | Teleports the player to the latest checkpoint |
| `!prevcp` | Teleports the player to the previous checkpoint |
| `!nextcp` | Teleports the player to the previous checkpoint |
| `!timer` | Stops timer and blocks it from starting |´
| `!goto` | Teleports the player to another player by name |
| `!stage` | Teleports the player to a stage by index |
| `!sthelp` | Prints commdands for player |
| `!stver` | Prints the current plugin version |


## Admin Commands
These commands require the `@css/root` admin flag

| Command  | What it does |
| ------------- | ------------- |
| `!noclip`  | Enables noclip for admin |

## Map Data

### Default Zone Triggers
* This plugin will look for trigger_multiple entities by default depending what map is being played. By default the plugin tries to hook the following target names:

| Start targetname  | End targetname |
| ------------- | ------------- |
| timer_startzone  | timer_endzone  |
| zone_start | zone_end |
| map_start | map_end  |
| s1_start |   |
| stage1_start |   |

### Adding Map Specific Variables
Map configuration can be customized with a simple `.json` file in `game/csgo/cfg/SharpTimer/MapData`

To add map specific Variables simply create a file with the maps name. For example: `kz_hub.json`

and add the variables you with to use

| Variable  | What it does |
| ------------- | ------------- |
| `MapStartTrigger`  | Sets the start zone trigger targetname for the map* |
| `MapEndTrigger`  | Sets the end zone trigger targetname for the map* |
| `RespawnPos` | Sets a custom respawn destination for the `!r` command |
| `OverrideStageRequirement` | Forces the map to check if the player has been through all stages before finishing |
| `OverrideDisableTelehop` | If `sharptimer_disable_telehop` is true you can override it to false for this specific map |

### Manual Zoning Variables
| Variable  | What it does |
| ------------- | ------------- |
| `MapStartC1`  | Sets the 1st start zone corner coordinates for the map** |
| `MapStartC2` | Sets the 2nd start zone corner coordinates for the map** |
| `MapEndC1` | Sets the 1st end zone corner coordinates for the map** |
| `MapEndC2` | Sets the 2nd end zone corner coordinates for the map** |

*⚠️ Only use these if the map does not support the trigger targetnames supported by default in SharpTimer!

**⚠️ Only use these as "the last resort" if the map does not have ANY start/end triggers! Alternatively as a server admin with a `@css/root` flag you can use `!addrespawnpos`, `!addstartzone`, `!addendzone` & `!savezones` to manually add "fake" zone triggers! [Example Video](https://streamable.com/9ez6gq)

<details>
<summary>Here is a Example of what the `map.json` can look like:</summary>

### surf_example.json
```
{
  "MapStartTrigger": "zone_start",
  "MapEndTrigger": "zone_end",
  "RespawnPos": "-2 0 64.03125",
  "OverrideDisableTelehop": "true",
  "OverrideStageRequirement": "true"
}
```

</details>

## Author: [@DEAFPS_](https://twitter.com/deafps_)
