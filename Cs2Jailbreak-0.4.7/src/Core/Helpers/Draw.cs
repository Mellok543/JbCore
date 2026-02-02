using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;

namespace JbCore.Helpers;

public static class EntityHelpers
{
    public static readonly Vector VecZero = new(0.0f, 0.0f, 0.0f);
    public static readonly QAngle AngleZero = new(0.0f, 0.0f, 0.0f);

    public static void RemoveEntity(int index, string name)
    {
        var ent = Utilities.GetEntityFromIndex<CBaseEntity>(index);
        if (ent != null && ent.DesignerName == name)
        {
            ent.Remove();
        }
    }

    public static void MoveLaser(this CEnvBeam? laser, Vector start, Vector end)
    {
        if (laser == null)
        {
            return;
        }

        laser.Teleport(start, AngleZero, VecZero);
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;
        Utilities.SetStateChanged(laser, "CBeam", "m_vecEndPos");
    }

    public static void MoveLaserByIndex(int laserIndex, Vector start, Vector end)
    {
        var laser = Utilities.GetEntityFromIndex<CEnvBeam>(laserIndex);
        if (laser != null && laser.DesignerName == "env_beam")
        {
            laser.MoveLaser(start, end);
        }
    }

    public static int DrawLaser(Vector start, Vector end, float width, Color colour)
    {
        var laser = Utilities.CreateEntityByName<CEnvBeam>("env_beam");
        if (laser == null)
        {
            return -1;
        }

        laser.Render = colour;
        laser.Width = width;
        laser.MoveLaser(start, end);
        laser.DispatchSpawn();

        return (int)laser.Index;
    }

    public static CCSPlayerController? PlayerFromEntity(this CBaseEntity? entity)
    {
        if (entity != null && entity.DesignerName == "player")
        {
            var pawn = new CCSPlayerPawn(entity.Handle);
            if (pawn.OriginalController != null && pawn.OriginalController.IsValid)
            {
                return pawn.OriginalController.Value;
            }
        }

        return null;
    }

    public static CCSPlayerController? PlayerFromHandle(this CHandle<CBaseEntity> handle)
    {
        return handle.IsValid ? handle.Value.PlayerFromEntity() : null;
    }
}

public sealed class Line
{
    public Color Colour { get; set; } = Color.FromArgb(255, 153, 255, 255);
    private int _laserIndex = -1;

    public void Move(Vector start, Vector end)
    {
        if (_laserIndex == -1)
        {
            _laserIndex = EntityHelpers.DrawLaser(start, end, 2.0f, Colour);
        }
        else
        {
            EntityHelpers.MoveLaserByIndex(_laserIndex, start, end);
        }
    }

    public void Destroy()
    {
        if (_laserIndex != -1)
        {
            EntityHelpers.RemoveEntity(_laserIndex, "env_beam");
            _laserIndex = -1;
        }
    }
}

public sealed class Circle
{
    private readonly Line[] _lines = new Line[50];
    public Color Colour { get; set; } = Color.FromArgb(255, 153, 255, 255);

    public Circle()
    {
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = new Line();
        }
    }

    public void Draw(float life, float radius, Vector center)
    {
        var step = (float)(2.0f * Math.PI) / _lines.Length;
        var angleOld = 0.0f;
        var angleCur = step;

        for (var i = 0; i < _lines.Length; i++)
        {
            var start = AngleOnCircle(angleOld, radius, center);
            var end = AngleOnCircle(angleCur, radius, center);

            _lines[i].Colour = Colour;
            _lines[i].Move(start, end);

            angleOld = angleCur;
            angleCur += step;
        }
    }

    public void Destroy()
    {
        foreach (var line in _lines)
        {
            line.Destroy();
        }
    }

    private static Vector AngleOnCircle(float angle, float radius, Vector center)
    {
        return new Vector(
            (float)(center.X + (radius * Math.Cos(angle))),
            (float)(center.Y + (radius * Math.Sin(angle))),
            center.Z + 6.0f
        );
    }
}
