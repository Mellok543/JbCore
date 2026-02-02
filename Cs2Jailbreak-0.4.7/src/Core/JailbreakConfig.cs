using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace JbCore;

public class JailbreakConfig : BasePluginConfig
{
    [JsonPropertyName("auto_warden_delay_seconds")]
    public int AutoWardenDelaySeconds { get; set; } = 30;

    [JsonPropertyName("warden_hp")]
    public int WardenHp { get; set; } = 150;

    [JsonPropertyName("warden_armor")]
    public int WardenArmor { get; set; } = 120;

    [JsonPropertyName("deputy_hp")]
    public int DeputyHp { get; set; } = 120;

    [JsonPropertyName("deputy_armor")]
    public int DeputyArmor { get; set; } = 110;

    [JsonPropertyName("ct_hp")]
    public int CtHp { get; set; } = 100;

    [JsonPropertyName("ct_armor")]
    public int CtArmor { get; set; } = 100;

    [JsonPropertyName("heal_to_hp")]
    public int HealToHp { get; set; } = 100;

    [JsonPropertyName("game_day_cooldown_rounds")]
    public int GameDayCooldownRounds { get; set; } = 10;

    [JsonPropertyName("friendly_fire_delay_seconds")]
    public int FriendlyFireDelaySeconds { get; set; } = 30;

    [JsonPropertyName("rooster_head_height")]
    public float RoosterHeadHeight { get; set; } = 40.0f;

    [JsonPropertyName("marker_tick_seconds")]
    public float MarkerTickSeconds { get; set; } = 0.1f;

    [JsonPropertyName("ct_weapons")]
    public string[] CtWeapons { get; set; } = new[] { "ak47", "deagle", "knife" };

    [JsonPropertyName("ct_grenades")]
    public string[] CtGrenades { get; set; } = new[] { "hegrenade", "flashbang", "smokegrenade", "molotov" };

    [JsonPropertyName("warden_give_weapons")]
    public string[] WardenGiveWeapons { get; set; } = new[] { "ak47", "m4a1_silencer", "awp", "ssg08", "nova", "deagle" };

    [JsonPropertyName("hunger_games_weapons")]
    public string[] HungerGamesWeapons { get; set; } = new[] { "ak47", "m4a1_silencer", "awp", "ssg08", "p90", "nova", "deagle" };

    [JsonPropertyName("lr_one_shot_weapons")]
    public string[] LrOneShotWeapons { get; set; } = new[] { "deagle", "ak47", "ssg08", "awp" };

    [JsonPropertyName("lr_min_t_alive")]
    public int LrMinTAlive { get; set; } = 2;
}
