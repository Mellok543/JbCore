using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using CSTimer = CounterStrikeSharp.API.Modules.Timers;
using JbCore.Helpers;

namespace JbCore;

public enum GameDayType
{
    HungerGames,
    RoosterFight,
    SniperArena,
    KnifeArena
}

public enum LastRequestType
{
    OneShot,
    GunToss,
    Golf,
    KnifeDuel,
    ShotgunDuel
}

[MinimumApiVersion(244)]
public sealed class JailbreakPlugin : BasePlugin, IPluginConfig<JailbreakConfig>
{
    public override string ModuleName => "Jailbreak Core";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "OpenAI";

    public JailbreakConfig Config { get; set; } = new();

    private CCSPlayerController? _warden;
    private CCSPlayerController? _deputy;
    private bool _wardenLocked;
    private bool _lrActive;
    private bool _lrAvailable;

    private LastRequestType? _lrType;
    private CCSPlayerController? _lrT;
    private CCSPlayerController? _lrCt;
    private CCSPlayerController? _lrTurnOwner;
    private string? _lrOneShotWeapon;
    private Vector? _lrPointA;
    private Vector? _lrPointB;
    private readonly Dictionary<ulong, float> _lrDistances = new();

    private GameDayType? _activeGameDay;
    private int _roundNumber;
    private int _lastGameDayRound = -100;

    private CSTimer.Timer? _autoWardenTimer;
    private CSTimer.Timer? _markerTimer;
    private CSTimer.Timer? _roosterCrouchTimer;

    private readonly Line _wardenLaser = new();
    private readonly Circle _wardenMarker = new();

    private readonly HashSet<ulong> _lrParticipants = new();

    private readonly ConVar? _friendlyFireCvar = ConVar.Find("mp_teammates_are_enemies");
    private readonly ConVar? _ignoreWinConditionsCvar = ConVar.Find("mp_ignore_round_win_conditions");

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventItemRemove>(OnItemRemove);
        RegisterEventHandler<EventItemPickup>(OnItemPickup);

        AddCommandListener("say", OnPlayerChat);
        AddCommandListener("say_team", OnPlayerChat);

        if (!PlayerExtensions.IsWindows())
        {
            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
        }

        _markerTimer = AddTimer(Config.MarkerTickSeconds, WardenMarkerTick, CSTimer.TimerFlags.REPEAT | CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
    }

    public override void Unload(bool hotReload)
    {
        if (!PlayerExtensions.IsWindows())
        {
            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        }
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundNumber++;
        ResetRoundState();
        ApplyLoadoutToAll();
        ScheduleAutoWarden();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        EndGameDay();
        EndLastRequest(resetLoadout: false);
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!player.IsLegal())
        {
            return HookResult.Continue;
        }

        AddTimer(0.2f, () => ApplyLoadout(player), CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (!victim.IsLegal())
        {
            return HookResult.Continue;
        }

        if (_lrActive && IsLrParticipant(victim))
        {
            FinishLastRequest(victim);
        }

        if (_warden != null && victim.Slot == _warden.Slot)
        {
            PromoteDeputy();
        }
        else if (_deputy != null && victim.Slot == _deputy.Slot)
        {
            _deputy = null;
        }

        CheckLastRequestAvailability();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!player.IsLegal())
        {
            return HookResult.Continue;
        }

        if (_warden != null && player.Slot == _warden.Slot)
        {
            _warden = null;
            PromoteDeputy();
        }

        if (_deputy != null && player.Slot == _deputy.Slot)
        {
            _deputy = null;
        }

        if (_lrActive && IsLrParticipant(player))
        {
            FinishLastRequest(player);
        }

        CheckLastRequestAvailability();
        return HookResult.Continue;
    }

    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!_lrActive || _lrType != LastRequestType.OneShot)
        {
            return HookResult.Continue;
        }

        var shooter = @event.Userid;
        if (!IsLrParticipant(shooter) || _lrTurnOwner == null)
        {
            return HookResult.Continue;
        }

        if (shooter.Slot != _lrTurnOwner.Slot)
        {
            SetWeaponAmmo(shooter, _lrOneShotWeapon ?? "deagle", 0, 0);
            return HookResult.Continue;
        }

        var other = shooter.Slot == _lrT?.Slot ? _lrCt : _lrT;
        if (other != null)
        {
            SetWeaponAmmo(shooter, _lrOneShotWeapon ?? "deagle", 0, 0);
            SetWeaponAmmo(other, _lrOneShotWeapon ?? "deagle", 1, 0);
            _lrTurnOwner = other;
        }

        return HookResult.Continue;
    }

    private HookResult OnItemRemove(EventItemRemove @event, GameEventInfo info)
    {
        if (!_lrActive || _lrType == null)
        {
            return HookResult.Continue;
        }

        var player = @event.Userid;
        if (!IsLrParticipant(player))
        {
            return HookResult.Continue;
        }

        if (_lrType == LastRequestType.GunToss && @event.Item == "deagle")
        {
            RecordTossDistance(player, _lrPointA);
        }

        if (_lrType == LastRequestType.Golf && @event.Item == "deagle")
        {
            RecordTossDistance(player, _lrPointB);
        }

        return HookResult.Continue;
    }

    private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        if (!_lrActive || !IsLrParticipant(@event.Userid))
        {
            return HookResult.Continue;
        }

        var player = @event.Userid;
        if (player == null || _lrType == null)
        {
            return HookResult.Continue;
        }

        var allowed = GetAllowedWeaponsForLr(_lrType.Value);
        if (!allowed.Contains(@event.Item, StringComparer.OrdinalIgnoreCase))
        {
            AddTimer(0.1f, () => ApplyLrLoadout(player), CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }

    private HookResult OnTakeDamage(DynamicHook hook)
    {
        var victimEntity = hook.GetParam<CEntityInstance>(0);
        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

        var victim = new CBaseEntity(victimEntity.Handle).PlayerFromEntity();
        var attacker = damageInfo.Attacker.PlayerFromHandle();

        if (_lrActive && victim.IsLegal() && attacker.IsLegal())
        {
            if (!IsLrParticipant(victim) || !IsLrParticipant(attacker))
            {
                damageInfo.Damage = 0;
                hook.SetParam(1, damageInfo);
                return HookResult.Continue;
            }
        }

        if (_activeGameDay == GameDayType.RoosterFight && victim.IsLegal() && attacker.IsLegal())
        {
            if (!IsRoosterDamageAllowed(attacker, victim))
            {
                damageInfo.Damage = 0;
                hook.SetParam(1, damageInfo);
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo command)
    {
        if (!player.IsLegal())
        {
            return HookResult.Continue;
        }

        var text = ExtractChatText(command);
        if (string.IsNullOrWhiteSpace(text))
        {
            return HookResult.Continue;
        }

        switch (text.ToLowerInvariant())
        {
            case "!w":
                TryBecomeWarden(player);
                return HookResult.Handled;
            case "!wm":
                if (IsWarden(player))
                {
                    ShowWardenMenu(player);
                }
                else
                {
                    player.PrintToChat("[JB] Только командир может открыть меню.");
                }
                return HookResult.Handled;
            case "!lr":
                TryOpenLastRequest(player);
                return HookResult.Handled;
            case "!lrpoint":
                HandleLrPoint(player);
                return HookResult.Handled;
            default:
                return HookResult.Continue;
        }
    }

    private void TryBecomeWarden(CCSPlayerController player)
    {
        if (_wardenLocked)
        {
            player.PrintToChat("[JB] Командир заблокирован до следующего раунда.");
            return;
        }

        if (!player.IsLegalAlive() || !player.IsCt())
        {
            player.PrintToChat("[JB] Командиром может быть только живой КТ.");
            return;
        }

        if (_warden != null)
        {
            player.PrintToChat("[JB] Командир уже выбран.");
            return;
        }

        SetWarden(player);
    }

    private void SetWarden(CCSPlayerController player)
    {
        _warden = player;
        _autoWardenTimer?.Kill();
        _autoWardenTimer = null;

        player.PrintToChat("[JB] Вы стали командиром. Используйте !wm для меню.");
        Server.PrintToChatAll($"[JB] Командир: {player.PlayerName}");

        ApplyRoleStats(player);
    }

    private void PromoteDeputy()
    {
        if (_deputy != null && _deputy.IsLegalAlive() && _deputy.IsCt())
        {
            SetWarden(_deputy);
            _deputy = null;
            return;
        }

        _warden = null;
    }

    private void AssignDeputy(CCSPlayerController player)
    {
        _deputy = player;
        player.PrintToChat("[JB] Вы назначены замом.");
        Server.PrintToChatAll($"[JB] Зам командира: {player.PlayerName}");
        ApplyRoleStats(player);
    }

    private void ScheduleAutoWarden()
    {
        _autoWardenTimer?.Kill();
        _autoWardenTimer = AddTimer(Config.AutoWardenDelaySeconds, () =>
        {
            if (_warden != null || _wardenLocked)
            {
                return;
            }

            var candidates = GetAliveCt();
            if (candidates.Count == 0)
            {
                return;
            }

            var random = new Random();
            var chosen = candidates[random.Next(candidates.Count)];
            SetWarden(chosen);
        }, CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void ShowWardenMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Командир: меню");
        menu.AddMenuOption("Назначить зама", (_, _) => OpenDeputyMenu(player));
        menu.AddMenuOption("Вылечить игрока", (_, _) => OpenHealMenu(player));
        menu.AddMenuOption("Возродить игрока", (_, _) => OpenRespawnMenu(player));
        menu.AddMenuOption("Убить игрока", (_, _) => OpenSlayMenu(player));
        menu.AddMenuOption("Выдать оружие КТ", (_, _) => OpenGiveWeaponMenu(player));
        menu.AddMenuOption("Игровые дни", (_, _) => OpenGameDayMenu(player));
        menu.AddMenuOption(_friendlyFireCvar?.GetPrimitiveValue<bool>() == true ? "Огонь по своим: ВКЛ" : "Огонь по своим: ВЫКЛ",
            (_, _) => ToggleFriendlyFire(player));
        MenuManager.OpenChatMenu(player, menu);
    }

    private void OpenDeputyMenu(CCSPlayerController player)
    {
        OpenPlayerMenu(player, "Выберите зама", target => target.IsLegalAlive() && target.IsCt() && !IsWarden(target), AssignDeputy);
    }

    private void OpenHealMenu(CCSPlayerController player)
    {
        OpenPlayerMenu(player, "Кого лечить", target => target.IsLegalAlive(), target => target.SetHealthSafe(Config.HealToHp));
    }

    private void OpenRespawnMenu(CCSPlayerController player)
    {
        OpenPlayerMenu(player, "Кого возродить", target => target.IsConnected() && !target.IsLegalAlive(), target =>
        {
            target.Respawn();
            AddTimer(0.2f, () => ApplyLoadout(target), CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
        });
    }

    private void OpenSlayMenu(CCSPlayerController player)
    {
        OpenPlayerMenu(player, "Кого убить", target => target.IsLegalAlive(), target => target.Slay());
    }

    private void OpenGiveWeaponMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Оружие для КТ");
        foreach (var weapon in Config.WardenGiveWeapons)
        {
            menu.AddMenuOption(weapon, (_, _) =>
            {
                OpenPlayerMenu(player, "Кому выдать", target => target.IsLegalAlive() && target.IsCt(), target => target.GiveWeapon(weapon));
            });
        }
        MenuManager.OpenChatMenu(player, menu);
    }

    private void OpenGameDayMenu(CCSPlayerController player)
    {
        if (!CanStartGameDay())
        {
            var remaining = Math.Max(0, Config.GameDayCooldownRounds - (_roundNumber - _lastGameDayRound));
            player.PrintToChat($"[JB] Игровые дни доступны через {remaining} раунд(ов).");
            return;
        }

        var menu = new ChatMenu("Игровые дни");
        menu.AddMenuOption("Голодные игры", (_, _) => StartGameDay(GameDayType.HungerGames));
        menu.AddMenuOption("Петушиные бои", (_, _) => StartGameDay(GameDayType.RoosterFight));
        menu.AddMenuOption("Снайперская арена", (_, _) => StartGameDay(GameDayType.SniperArena));
        menu.AddMenuOption("Ножевой хаос", (_, _) => StartGameDay(GameDayType.KnifeArena));
        MenuManager.OpenChatMenu(player, menu);
    }

    private void ToggleFriendlyFire(CCSPlayerController player)
    {
        var enabled = !(_friendlyFireCvar?.GetPrimitiveValue<bool>() ?? false);
        SetFriendlyFire(enabled);
        player.PrintToChat(enabled ? "[JB] Огонь по своим включен." : "[JB] Огонь по своим выключен.");
    }

    private void OpenPlayerMenu(CCSPlayerController player, string title, Func<CCSPlayerController, bool> filter, Action<CCSPlayerController> onSelect)
    {
        var menu = new ChatMenu(title);
        foreach (var target in Utilities.GetPlayers().Where(filter))
        {
            var captured = target;
            menu.AddMenuOption(target.PlayerName, (_, _) => onSelect(captured));
        }

        if (menu.MenuOptions.Count == 0)
        {
            player.PrintToChat("[JB] Нет доступных игроков.");
            return;
        }

        MenuManager.OpenChatMenu(player, menu);
    }

    private void ApplyLoadoutToAll()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            ApplyLoadout(player);
        }
    }

    private void ApplyLoadout(CCSPlayerController player)
    {
        if (!player.IsLegalAlive())
        {
            return;
        }

        if (_lrActive && IsLrParticipant(player))
        {
            return;
        }

        if (_activeGameDay != null)
        {
            ApplyGameDayLoadout(player);
            return;
        }

        if (player.IsCt())
        {
            player.StripWeaponsSafe();
            foreach (var weapon in Config.CtWeapons)
            {
                player.GiveWeapon(weapon);
            }

            foreach (var grenade in Config.CtGrenades)
            {
                player.GiveWeapon(grenade);
            }

            ApplyRoleStats(player);
        }
        else if (player.IsT())
        {
            player.StripWeaponsSafe();
            player.GiveWeapon("knife");
        }
    }

    private void ApplyRoleStats(CCSPlayerController player)
    {
        if (!player.IsLegalAlive() || !player.IsCt())
        {
            return;
        }

        if (IsWarden(player))
        {
            player.SetHealthSafe(Config.WardenHp);
            player.SetArmorSafe(Config.WardenArmor);
            return;
        }

        if (_deputy != null && player.Slot == _deputy.Slot)
        {
            player.SetHealthSafe(Config.DeputyHp);
            player.SetArmorSafe(Config.DeputyArmor);
            return;
        }

        player.SetHealthSafe(Config.CtHp);
        player.SetArmorSafe(Config.CtArmor);
    }

    private void ApplyGameDayLoadout(CCSPlayerController player)
    {
        if (!player.IsLegalAlive())
        {
            return;
        }

        if (_activeGameDay == GameDayType.RoosterFight)
        {
            player.StripWeaponsSafe();
            return;
        }

        if (_activeGameDay == GameDayType.KnifeArena)
        {
            player.StripWeaponsSafe();
            player.GiveWeapon("knife");
            return;
        }

        if (_activeGameDay == GameDayType.SniperArena)
        {
            player.StripWeaponsSafe();
            player.GiveWeapon("awp");
            player.GiveWeapon("deagle");
            return;
        }
    }

    private void StartGameDay(GameDayType type)
    {
        if (_activeGameDay != null)
        {
            return;
        }

        _activeGameDay = type;
        _lastGameDayRound = _roundNumber;
        SetIgnoreRoundWinConditions(true);

        Server.PrintToChatAll($"[JB] Запущен игровой день: {type}");

        if (type == GameDayType.HungerGames)
        {
            OpenHungerGamesMenu();
            AddTimer(Config.FriendlyFireDelaySeconds, () => SetFriendlyFire(true), CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
            return;
        }

        if (type == GameDayType.RoosterFight)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsLegalAlive())
                {
                    player.StripWeaponsSafe();
                }
            }

            _roosterCrouchTimer?.Kill();
            _roosterCrouchTimer = AddTimer(0.2f, ForceRoosterCrouch, CSTimer.TimerFlags.REPEAT | CSTimer.TimerFlags.STOP_ON_MAPCHANGE);
            return;
        }

        if (type == GameDayType.SniperArena)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsLegalAlive())
                {
                    player.StripWeaponsSafe();
                    player.GiveWeapon("awp");
                    player.GiveWeapon("deagle");
                }
            }

            return;
        }

        if (type == GameDayType.KnifeArena)
        {
            SetFriendlyFire(true);
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsLegalAlive())
                {
                    player.StripWeaponsSafe();
                    player.GiveWeapon("knife");
                }
            }
        }
    }

    private void EndGameDay()
    {
        if (_activeGameDay == null)
        {
            return;
        }

        _activeGameDay = null;
        _roosterCrouchTimer?.Kill();
        _roosterCrouchTimer = null;
        SetFriendlyFire(false);
        SetIgnoreRoundWinConditions(false);
    }

    private bool CanStartGameDay()
    {
        return (_roundNumber - _lastGameDayRound) >= Config.GameDayCooldownRounds;
    }

    private void OpenHungerGamesMenu()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsLegalAlive())
            {
                continue;
            }

            var menu = new ChatMenu("Голодные игры: оружие");
            foreach (var weapon in Config.HungerGamesWeapons)
            {
                menu.AddMenuOption(weapon, (_, _) =>
                {
                    player.StripWeaponsSafe();
                    player.GiveWeapon(weapon);
                    player.GiveWeapon("knife");
                });
            }
            MenuManager.OpenChatMenu(player, menu);
        }
    }

    private void ForceRoosterCrouch()
    {
        if (_activeGameDay != GameDayType.RoosterFight)
        {
            return;
        }

        foreach (var player in Utilities.GetPlayers())
        {
            var pawn = player.Pawn();
            if (pawn == null)
            {
                continue;
            }

            pawn.DuckAmount = 1.0f;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flDuckAmount");
        }
    }

    private bool IsRoosterDamageAllowed(CCSPlayerController attacker, CCSPlayerController victim)
    {
        var attackerPawn = attacker.Pawn();
        var victimPawn = victim.Pawn();
        if (attackerPawn?.AbsOrigin == null || victimPawn?.AbsOrigin == null)
        {
            return false;
        }

        var heightDiff = attackerPawn.AbsOrigin.Z - victimPawn.AbsOrigin.Z;
        return heightDiff >= Config.RoosterHeadHeight;
    }

    private void TryOpenLastRequest(CCSPlayerController player)
    {
        if (!_lrAvailable || _lrActive)
        {
            player.PrintToChat("[JB] Последнее желание сейчас недоступно.");
            return;
        }

        if (!player.IsLegalAlive() || !player.IsT())
        {
            player.PrintToChat("[JB] Последнее желание доступно только террористу.");
            return;
        }

        OpenLastRequestMenu(player);
    }

    private void OpenLastRequestMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Last Request");
        menu.AddMenuOption("Один выстрел", (_, _) => StartLastRequest(player, LastRequestType.OneShot));
        menu.AddMenuOption("Метание", (_, _) => StartLastRequest(player, LastRequestType.GunToss));
        menu.AddMenuOption("Гольф", (_, _) => StartLastRequest(player, LastRequestType.Golf));
        menu.AddMenuOption("Ножевой дуэль", (_, _) => StartLastRequest(player, LastRequestType.KnifeDuel));
        menu.AddMenuOption("Дуэль с дробовиком", (_, _) => StartLastRequest(player, LastRequestType.ShotgunDuel));
        MenuManager.OpenChatMenu(player, menu);
    }

    private void StartLastRequest(CCSPlayerController tPlayer, LastRequestType type)
    {
        if (_lrActive)
        {
            return;
        }

        var ctCandidates = GetAliveCt();
        if (ctCandidates.Count == 0)
        {
            tPlayer.PrintToChat("[JB] Нет доступных КТ для игры.");
            return;
        }

        var random = new Random();
        var ctPlayer = ctCandidates[random.Next(ctCandidates.Count)];

        _lrActive = true;
        _lrAvailable = false;
        _lrType = type;
        _lrT = tPlayer;
        _lrCt = ctPlayer;
        _lrParticipants.Clear();
        _lrParticipants.Add(tPlayer.SteamID);
        _lrParticipants.Add(ctPlayer.SteamID);
        _lrDistances.Clear();
        _lrPointA = null;
        _lrPointB = null;
        _lrOneShotWeapon = null;
        _lrTurnOwner = null;

        _wardenLocked = true;
        _warden = null;
        _deputy = null;

        Server.PrintToChatAll($"[JB] Last Request: {tPlayer.PlayerName} vs {ctPlayer.PlayerName}");

        if (type == LastRequestType.OneShot)
        {
            OpenOneShotWeaponMenu(tPlayer);
            return;
        }

        if (type == LastRequestType.GunToss)
        {
            _lrPointA = tPlayer.Pawn()?.AbsOrigin;
            ApplyLrLoadout(tPlayer);
            ApplyLrLoadout(ctPlayer);
            tPlayer.PrintToChat("[JB] Точка метания установлена по вашей позиции.");
            return;
        }

        if (type == LastRequestType.Golf)
        {
            tPlayer.PrintToChat("[JB] Используйте !lrpoint дважды для установки точек.");
            return;
        }

        ApplyLrLoadout(tPlayer);
        ApplyLrLoadout(ctPlayer);
    }

    private void OpenOneShotWeaponMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Один выстрел: оружие");
        foreach (var weapon in Config.LrOneShotWeapons)
        {
            menu.AddMenuOption(weapon, (_, _) =>
            {
                _lrOneShotWeapon = weapon;
                StartOneShotGame();
            });
        }
        MenuManager.OpenChatMenu(player, menu);
    }

    private void StartOneShotGame()
    {
        if (_lrT == null || _lrCt == null)
        {
            return;
        }

        ApplyLrLoadout(_lrT);
        ApplyLrLoadout(_lrCt);

        var random = new Random();
        _lrTurnOwner = random.Next(2) == 0 ? _lrT : _lrCt;

        var shooter = _lrTurnOwner;
        var other = shooter == _lrT ? _lrCt : _lrT;

        SetWeaponAmmo(shooter, _lrOneShotWeapon ?? "deagle", 1, 0);
        SetWeaponAmmo(other, _lrOneShotWeapon ?? "deagle", 0, 0);

        shooter.PrintToChat("[JB] Ваш ход стрелять.");
        other.PrintToChat("[JB] Ожидайте ход соперника.");
    }

    private void HandleLrPoint(CCSPlayerController player)
    {
        if (!_lrActive || _lrType != LastRequestType.Golf || _lrT == null)
        {
            return;
        }

        if (player.Slot != _lrT.Slot)
        {
            return;
        }

        if (_lrPointA == null)
        {
            _lrPointA = player.Pawn()?.AbsOrigin;
            player.PrintToChat("[JB] Точка A установлена.");
            return;
        }

        if (_lrPointB == null)
        {
            _lrPointB = player.Pawn()?.AbsOrigin;
            player.PrintToChat("[JB] Точка B установлена. Начинаем гольф.");
            ApplyLrLoadout(_lrT);
            ApplyLrLoadout(_lrCt!);
        }
    }

    private void ApplyLrLoadout(CCSPlayerController player)
    {
        if (!player.IsLegalAlive() || _lrType == null)
        {
            return;
        }

        player.StripWeaponsSafe();

        foreach (var weapon in GetAllowedWeaponsForLr(_lrType.Value))
        {
            player.GiveWeapon(weapon);
        }

        if (_lrType == LastRequestType.GunToss || _lrType == LastRequestType.Golf)
        {
            SetWeaponAmmo(player, "deagle", 0, 0);
        }
    }

    private HashSet<string> GetAllowedWeaponsForLr(LastRequestType type)
    {
        return type switch
        {
            LastRequestType.OneShot => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _lrOneShotWeapon ?? "deagle", "knife" },
            LastRequestType.GunToss => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deagle", "knife" },
            LastRequestType.Golf => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deagle", "knife" },
            LastRequestType.KnifeDuel => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "knife" },
            LastRequestType.ShotgunDuel => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "nova", "knife" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "knife" }
        };
    }

    private void RecordTossDistance(CCSPlayerController player, Vector? target)
    {
        if (target == null)
        {
            return;
        }

        if (_lrDistances.ContainsKey(player.SteamID))
        {
            return;
        }

        var origin = player.Pawn()?.AbsOrigin;
        if (origin == null)
        {
            return;
        }

        var distance = VectorDistance(origin, target.Value);
        _lrDistances[player.SteamID] = distance;

        if (_lrT != null && _lrCt != null && _lrDistances.Count >= 2)
        {
            var tDist = _lrDistances.GetValueOrDefault(_lrT.SteamID, float.MaxValue);
            var ctDist = _lrDistances.GetValueOrDefault(_lrCt.SteamID, float.MaxValue);

            var winner = tDist >= ctDist ? _lrT : _lrCt;
            var loser = winner == _lrT ? _lrCt : _lrT;
            loser?.Slay();
        }
    }

    private void FinishLastRequest(CCSPlayerController victim)
    {
        if (_lrT == null || _lrCt == null)
        {
            EndLastRequest(resetLoadout: true);
            return;
        }

        var winner = victim.Slot == _lrT.Slot ? _lrCt : _lrT;
        if (winner != null)
        {
            winner.PrintToChat("[JB] Вы победили в Last Request.");
        }

        EndLastRequest(resetLoadout: true);
    }

    private void EndLastRequest(bool resetLoadout)
    {
        _lrActive = false;
        _lrType = null;
        _lrParticipants.Clear();
        _lrT = null;
        _lrCt = null;
        _lrTurnOwner = null;
        _lrOneShotWeapon = null;
        _lrPointA = null;
        _lrPointB = null;
        _lrDistances.Clear();

        if (resetLoadout)
        {
            ApplyLoadoutToAll();
        }
    }

    private void CheckLastRequestAvailability()
    {
        if (_lrActive)
        {
            return;
        }

        var aliveT = GetAliveT().Count;
        _lrAvailable = aliveT == Config.LrMinTAlive;

        if (_lrAvailable)
        {
            foreach (var t in GetAliveT())
            {
                t.PrintToChat("[JB] Доступно !lr для Last Request.");
            }
        }
    }

    private void ResetRoundState()
    {
        _warden = null;
        _deputy = null;
        _wardenLocked = false;
        _lrActive = false;
        _lrAvailable = false;
        _lrType = null;
        _lrParticipants.Clear();
        _lrDistances.Clear();
        _lrT = null;
        _lrCt = null;
        _lrTurnOwner = null;
        _lrOneShotWeapon = null;
        _lrPointA = null;
        _lrPointB = null;

        EndGameDay();
        SetFriendlyFire(false);
        SetIgnoreRoundWinConditions(false);
    }

    private void SetFriendlyFire(bool enabled)
    {
        _friendlyFireCvar?.SetValue(enabled);
    }

    private void SetIgnoreRoundWinConditions(bool enabled)
    {
        _ignoreWinConditionsCvar?.SetValue(enabled);
    }

    private void WardenMarkerTick()
    {
        if (_warden == null || !_warden.IsLegalAlive())
        {
            _wardenLaser.Destroy();
            _wardenMarker.Destroy();
            return;
        }

        var useKey = (_warden.Buttons & PlayerButtons.Use) == PlayerButtons.Use;
        var pawn = _warden.Pawn();
        if (!useKey || pawn?.AbsOrigin == null)
        {
            _wardenLaser.Destroy();
            return;
        }

        var eyeVector = _warden.EyeVector();
        if (eyeVector == null)
        {
            return;
        }

        var camera = pawn.CameraServices;
        var eye = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + (camera?.OldPlayerViewOffsetZ ?? 64.0f));
        var end = new Vector(
            eye.X + eyeVector.Value.X * 3000,
            eye.Y + eyeVector.Value.Y * 3000,
            eye.Z + eyeVector.Value.Z * 3000
        );

        _wardenLaser.Colour = Color.Cyan;
        _wardenLaser.Move(eye, end);
        _wardenMarker.Colour = Color.Cyan;
        _wardenMarker.Draw(0.2f, 50.0f, end);
    }

    private bool IsWarden(CCSPlayerController player)
    {
        return _warden != null && player.Slot == _warden.Slot;
    }

    private bool IsLrParticipant(CCSPlayerController? player)
    {
        return player != null && _lrParticipants.Contains(player.SteamID);
    }

    private static float VectorDistance(Vector a, Vector b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static string ExtractChatText(CommandInfo command)
    {
        var text = command.ArgString.Trim();
        if (text.StartsWith('"') && text.EndsWith('"') && text.Length > 1)
        {
            text = text[1..^1];
        }

        return text.Trim();
    }

    private static List<CCSPlayerController> GetAliveCt()
    {
        return Utilities.GetPlayers().Where(player => player.IsLegalAlive() && player.IsCt()).ToList();
    }

    private static List<CCSPlayerController> GetAliveT()
    {
        return Utilities.GetPlayers().Where(player => player.IsLegalAlive() && player.IsT()).ToList();
    }

    private static void SetWeaponAmmo(CCSPlayerController player, string weaponName, int clip, int reserve)
    {
        var pawn = player.Pawn();
        var weapons = pawn?.WeaponServices?.MyWeapons;
        if (weapons == null)
        {
            return;
        }

        var normalized = weaponName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)
            ? weaponName
            : $"weapon_{weaponName}";

        foreach (var weaponHandle in weapons)
        {
            var weapon = weaponHandle.Value;
            if (weapon == null)
            {
                continue;
            }

            if (weapon.DesignerName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                weapon.Clip1 = clip;
                if (weapon.ReserveAmmo.Length > 0)
                {
                    weapon.ReserveAmmo[0] = reserve;
                }
                Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_iClip1");
                Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
                return;
            }
        }
    }
}
