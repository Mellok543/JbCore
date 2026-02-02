using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace JbCore.Helpers;

public static class PlayerExtensions
{
    public const int TeamT = 2;
    public const int TeamCt = 3;

    public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsLegal(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid && player.PlayerPawn.Value?.IsValid == true;
    }

    public static bool IsConnected(this CCSPlayerController? player)
    {
        return player.IsLegal() && player.Connected == PlayerConnectedState.PlayerConnected;
    }

    public static bool IsT(this CCSPlayerController? player)
    {
        return player.IsLegal() && player.TeamNum == TeamT;
    }

    public static bool IsCt(this CCSPlayerController? player)
    {
        return player.IsLegal() && player.TeamNum == TeamCt;
    }

    public static bool IsLegalAlive(this CCSPlayerController? player)
    {
        return player.IsConnected() && player.PawnIsAlive && player.PlayerPawn.Value?.LifeState == (byte)LifeState_t.LIFE_ALIVE;
    }

    public static CCSPlayerPawn? Pawn(this CCSPlayerController? player)
    {
        if (!player.IsLegalAlive())
        {
            return null;
        }

        return player.PlayerPawn.Value;
    }

    public static void SetHealthSafe(this CCSPlayerController? player, int hp)
    {
        var pawn = player.Pawn();
        if (pawn != null)
        {
            pawn.Health = hp;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
    }

    public static void SetArmorSafe(this CCSPlayerController? player, int armor)
    {
        var pawn = player.Pawn();
        if (pawn != null)
        {
            pawn.ArmorValue = armor;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
        }
    }

    public static void StripWeaponsSafe(this CCSPlayerController? player, bool removeKnife = false)
    {
        if (!player.IsLegalAlive())
        {
            return;
        }

        player.RemoveWeapons();

        if (!removeKnife)
        {
            player.GiveNamedItem("weapon_knife");
        }
    }

    public static void GiveWeapon(this CCSPlayerController? player, string weaponName)
    {
        if (!player.IsLegalAlive())
        {
            return;
        }

        var normalized = weaponName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)
            ? weaponName
            : $"weapon_{weaponName}";

        player.GiveNamedItem(normalized);
    }

    public static void Slay(this CCSPlayerController? player)
    {
        if (player.IsLegalAlive())
        {
            player.PlayerPawn.Value?.CommitSuicide(true, true);
        }
    }

    public static Vector? EyeVector(this CCSPlayerController? player)
    {
        var pawn = player.Pawn();
        if (pawn == null)
        {
            return null;
        }

        var eyeAngle = pawn.EyeAngles;
        double pitch = (Math.PI / 180) * eyeAngle.X;
        double yaw = (Math.PI / 180) * eyeAngle.Y;

        return new Vector(
            (float)(Math.Cos(yaw) * Math.Cos(pitch)),
            (float)(Math.Sin(yaw) * Math.Cos(pitch)),
            (float)(-Math.Sin(pitch))
        );
    }
}
