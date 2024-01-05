using System.Text;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        private Dictionary<int, PlayerTimerInfo> playerTimers = new Dictionary<int, PlayerTimerInfo>();
        private Dictionary<int, List<PlayerCheckpoint>> playerCheckpoints = new Dictionary<int, List<PlayerCheckpoint>>();
        private Dictionary<int, CCSPlayerController> connectedPlayers = new Dictionary<int, CCSPlayerController>();
        Dictionary<nint, TriggerPushData> triggerPushData = new Dictionary<nint, TriggerPushData>();

        public override string ModuleName => "SharpTimer";
        public override string ModuleVersion => $"0.1.6b - {new DateTime(Builtin.CompileTime, DateTimeKind.Utc)}";
        public override string ModuleAuthor => "DEAFPS https://github.com/DEAFPS/";
        public override string ModuleDescription => "A simple CSS Timer Plugin";
        public string msgPrefix = $"[SharpTimer] ";
        public string primaryHUDcolor = "green";
        public string secondaryHUDcolor = "orange";
        public string tertiaryHUDcolor = "white";
        public string currentMapStartTrigger = "trigger_startzone";
        public string currentMapEndTrigger = "trigger_endzone";
        public Vector currentMapStartC1 = new Vector(0, 0, 0);
        public Vector currentMapStartC2 = new Vector(0, 0, 0);
        public Vector currentMapEndC1 = new Vector(0, 0, 0);
        public Vector currentMapEndC2 = new Vector(0, 0, 0);
        public Vector? currentRespawnPos = null;
        public QAngle? currentRespawnAng = null;
        public bool currentMapOverrideDisableTelehop = false;
        private Dictionary<int, Vector?> bonusRespawnPoses = new Dictionary<int, Vector?>();
        private Dictionary<int, QAngle?> bonusRespawnAngs = new Dictionary<int, QAngle?>();
        private Dictionary<nint, int> stageTriggers = new Dictionary<nint, int>();
        private Dictionary<nint, int> cpTriggers = new Dictionary<nint, int>();
        private Dictionary<int, Vector?> stageTriggerPoses = new Dictionary<int, Vector?>();
        private Dictionary<int, QAngle?> stageTriggerAngs = new Dictionary<int, QAngle?>();
        private int stageTriggerCount;
        private int cpTriggerCount;
        private bool useStageTriggers = false;
        private bool useCheckpointTriggers = false;
        public string? currentMapType = null;
        public int? currentMapTier = null;

        public bool enableDebug = true;
        public bool useMySQL = false;

        public bool useTriggers = true;
        public bool respawnEnabled = true;
        public bool topEnabled = true;
        public bool rankEnabled = true;
        public bool pbComEnabled = true;
        public bool alternativeSpeedometer = false;
        public bool removeLegsEnabled = true;
        public bool removeCollisionEnabled = true;
        public bool disableDamage = true;
        public bool cpEnabled = false;
        public bool removeCpRestrictEnabled = false;
        public bool connectMsgEnabled = true;
        public bool cmdJoinMsgEnabled = true;
        public bool autosetHostname = true;
        public bool srEnabled = true;
        public int srTimer = 120;
        public int rankHUDTimer = 170;
        public bool resetTriggerTeleportSpeedEnabled = false;
        public bool maxStartingSpeedEnabled = true;
        public int maxStartingSpeed = 320;
        public bool isADTimerRunning = false;
        public bool isRankHUDTimerRunning = false;
        public bool removeCrouchFatigueEnabled = true;
        public bool goToEnabled = false;
        public bool fovChangerEnabled = true;
        public bool triggerPushFixEnabled = false;
        public int cmdCooldown = 64;
        public float fakeTriggerHeight = 50;
        public int altVeloMaxSpeed = 3000;

        public string beepSound = "sounds/ui/csgo_ui_button_rollover_large.vsnd";
        public string respawnSound = "sounds/ui/menu_accept.vsnd";
        public string cpSound = "sounds/ui/counter_beep.vsnd";
        public string cpSoundAir = "sounds/ui/weapon_cant_buy.vsnd";
        public string tpSound = "sounds/ui/buttonclick.vsnd";
        public string? gameDir;
        public string? mySQLpath;
        public string? playerRecordsPath;
        public string? currentMapName;
        public string? defaultServerHostname = ConVar.Find("hostname").StringValue;

        public string? remoteBhopDataSource = "https://raw.githubusercontent.com/DEAFPS/SharpTimer/main/remote_data/bhop_.json";
        public string? remoteKZDataSource = "https://raw.githubusercontent.com/DEAFPS/SharpTimer/main/remote_data/kz_.json";
        public string? remoteSurfDataSource = "https://raw.githubusercontent.com/DEAFPS/SharpTimer/main/remote_data/surf_.json";
        public string? testerPersonalGifsSource = "https://raw.githubusercontent.com/DEAFPS/SharpTimer/main/remote_data/tester_bling.json";

        private readonly WIN_LINUX<int> OnCollisionRulesChangedOffset = new WIN_LINUX<int>(174, 173);
    }
}