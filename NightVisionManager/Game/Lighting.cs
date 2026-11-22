using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.Scp079Events;
using MapGeneration;
using PlayerRoles.PlayableScps.Scp079;

namespace InitStudio.NightVision;

internal sealed class Lighting
{
    // The vanilla darkness check and SCP-079 use this vertical controller range.
    private const float ControllerHeightRange = 50f;
    private readonly HashSet<RoomLightController> blackoutControllers = new();

    public LightSample Read(Player player)
    {
        Room? room = player.CurrentRoom;
        if (room is null || room.Identifier == null)
            return default;

        var position = player.Position;
        bool hasLocalLights = false;
        bool blackout = false;
        foreach (RoomLightController controller in room.Identifier.LightControllers)
        {
            if (!AtHeight(controller, position.y))
                continue;

            hasLocalLights = true;
            blackout |= blackoutControllers.Contains(controller) && !controller.NetworkLightsEnabled;
        }

        return new LightSample(room.Zone, hasLocalLights,
            hasLocalLights && RoomLightController.IsInDarkenedRoom(room.Identifier, position), blackout);
    }

    public void OnRoomBlackout(Scp079BlackedOutRoomEventArgs ev)
    {
        if (ev.Room is null || ev.Player.RoleBase is not Scp079Role role || role.CurrentCamera is not { } camera)
            return;

        // Match the first eligible controller selected by the vanilla room ability.
        RoomLightController? controller = RoomLightController.Instances.FirstOrDefault(candidate =>
            candidate != null && candidate.Room == ev.Room.Base && AtHeight(candidate, camera.Position.y));
        Track(controller);
    }

    public void OnZoneBlackout(Scp079BlackedOutZoneEventArgs ev)
    {
        foreach (RoomLightController controller in RoomLightController.Instances)
        {
            if (controller != null && controller.Room.Zone == ev.Zone)
                Track(controller);
        }
    }

    public void Prune() => blackoutControllers.RemoveWhere(controller =>
        controller == null || controller.NetworkLightsEnabled);

    public void Clear() => blackoutControllers.Clear();

    private void Track(RoomLightController? controller)
    {
        if (controller != null && !controller.NetworkLightsEnabled)
            blackoutControllers.Add(controller);
    }

    private static bool AtHeight(RoomLightController? controller, float height) =>
        controller != null && Math.Abs(controller.transform.position.y - height) <= ControllerHeightRange;
}

internal readonly struct LightSample
{
    public LightSample(ZoneType zone, bool hasLocalLights, bool dark, bool blackout)
    {
        Zone = zone;
        HasLocalLights = hasLocalLights;
        Dark = dark;
        Blackout = blackout;
    }

    public ZoneType Zone { get; }
    public bool HasLocalLights { get; }
    public bool Dark { get; }
    public bool Blackout { get; }
}
