<div align="center">
  <img src="https://github.com/DEAFPS/SharpTimer/assets/43534349/c353662a-eb64-43e7-9294-40cfed3d58af" alt="">
</div>

<div align="center">
<a href='https://ko-fi.com/L3L7T5ZSB' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi1.png?v=3' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>
</div>
<div align="center">
<a href='https://discord.gg/SmQXeyMcny' target='_blank'><img src='https://discordapp.com/api/guilds/1196646791450472488/widget.png?style=banner2' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>
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
  <summary>KZ Checkpoint system (disabled by default, check config)</summary>
   <img src="https://i.imgur.com/USX5i8C.png"><br>
   <img src="https://i.imgur.com/kWiHOlz.png"><br>
   <img src="https://i.imgur.com/lXwXNN7.png"><br>
   <img src="https://i.imgur.com/nyn76Q4.png">
</details>

## Dependencies

[**MetaMod**](https://cs2.poggu.me/metamod/installation/)

[**CounterStrikeSharp** *(v141-144)*](https://github.com/roflmuffin/CounterStrikeSharp/releases)

[**MovementUnlocker** *(optional but recommended)*](https://github.com/Source2ZE/MovementUnlocker)

[**Web panel** *(optional but recommended)*](https://github.com/Letaryat/sharptimer-web-panel)


## Install
* Download the [latest release](https://github.com/DEAFPS/SharpTimer/releases),

* Unzip into your servers `game/csgo/` directory,

* :exclamation: See `game/csgo/cfg/SharpTimer/config.cfg` for basic plugin configuration,

* :exclamation: It is recommended to have a custom server cfg with your desired settings (for example [SURF](https://github.com/DEAFPS/cs-cfg/blob/main/surf.cfg) or [KZ](https://github.com/DEAFPS/cs-cfg/blob/main/kz.cfg)),

# [SharpTimer Wiki/Docs](https://github.com/DEAFPS/SharpTimer/wiki)

# TODO List
- [x] HUD
  - [x] Speedometer
  - [x] Pre
  - [x] Timer
  - [x] Info
    - [x] PB
    - [x] Map Rank Icon
    - [x] Map Rank (ie 1/100)
    - [x] Map Tier
    - [x] Map Type
  - [ ] Spectator HUD
- [x] Zones
  - [x] Hook common triggers by default
  - [x] Manual Zones
  - [x] Hook Bonus Zones Triggers (KZ & Surf) 
- [x] Player PBs
  - [x] Save to Json
  - [x] Save to MySQL
- [x] Ranks
  - [x] Map !top
  - [x] Map !topbonus
  - [ ] Global server ranks
    - [ ] !topserver
    - [ ] Global Point system _(base 230400 (1h) each timerTick = 1 point. example: player finishes at 1000 ticks. math: 230400 - 1000 = 229400 points x 0.1 = 22940 x 1.maptier)_
- [x] Surf Stages/Checkpoint support
  - [x] Stage/Checkpoint PBs with u/s
    - [x] Json Stage/Checkpoint PBs saving
    - [ ] MySql Stage/Checkpoint PBs saving
- [x] MySQL
	- [x] Basic Player Records
  - [ ] Player Server Stats
  - [ ] Player Map Stats
- [ ] Replays
- [ ] Jumpstats
- [x] Silly Stuff
  - [x] Color customization
  - [x] Special Tester Gifs
  - [ ] Custom Player Gifs
  - [ ] Velocity Bar


## Author: [@DEAFPS_](https://twitter.com/deafps_)

