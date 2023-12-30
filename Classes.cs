using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace SharpTimer
{
    public class MapInfo
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapStartTrigger { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapStartC1 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapStartC2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapEndTrigger { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapEndC1 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapEndC2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RespawnPos { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapTier { get; set; }
    }

    public class PlayerTimerInfo
    {
        public bool IsTimerRunning { get; set; }
        public bool IsTimerBlocked { get; set; }
        public int TimerTicks { get; set; }
        public bool IsBonusTimerRunning { get; set; }
        public int BonusTimerTicks { get; set; }
        public int BonusStage { get; set; }
        public string? TimerRank { get; set; }
        public string? PB { get; set; }
        public bool IsRankPbCached { get; set; }
        public string? PreSpeed { get; set; }
        public int? TicksInAir { get; set; }
        public int CheckpointIndex { get; set; }
        public bool Azerty { get; set; }
        public bool HideTimerHud { get; set; }
        public bool HideKeys { get; set; }
        public bool SoundsEnabled { get; set; }
        public int TimesConnected { get; set; }
        public int TicksSinceLastCmd { get; set; }
        public Dictionary<int, int>? StageRecords { get; set; }
        public Dictionary<string, PlayerRecord>? SortedCachedRecords { get; set; }
        public CCSPlayer_MovementServices? MovementService { get; set; }

        //admin stuff
        public bool IsNoclipEnabled { get; set; }
        public bool IsAddingStartZone { get; set; }
        public string? StartZoneC1 { get; set; }
        public string? StartZoneC2 { get; set; }
        public bool IsAddingEndZone { get; set; }
        public string? EndZoneC1 { get; set; }
        public string? EndZoneC2 { get; set; }
        public string? RespawnPos { get; set; }
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

    public class TriggerPushData
    {
        public float PushSpeed { get; set; }
        public QAngle PushEntitySpace { get; set; }
        public Vector PushDirEntitySpace { get; set; }
        public TriggerPushData(float pushSpeed, QAngle pushEntitySpace, Vector pushDirEntitySpace)
        {
            PushSpeed = pushSpeed;
            PushEntitySpace = pushEntitySpace;
            PushDirEntitySpace = pushDirEntitySpace;
        }
    }

    public class WIN_LINUX<T>
    {
        [JsonPropertyName("Windows")]
        public T Windows { get; private set; }

        [JsonPropertyName("Linux")]
        public T Linux { get; private set; }

        public WIN_LINUX(T windows, T linux)
        {
            this.Windows = windows;
            this.Linux = linux;
        }

        public T Get()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return this.Windows;
            }
            else
            {
                return this.Linux;
            }
        }
    }
}