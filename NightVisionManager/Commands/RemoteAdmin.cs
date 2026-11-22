using System;
using System.Globalization;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace InitStudio.NightVision;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class RemoteAdmin : ICommand
{
    private const string Usage = "Usage: nightvision grant <id> [profile] | remove <id> | reset <id> | "
        + "on <id> [seconds] | off <id> | status <id> | profile <id> <profile> | reload";

    public string Command => "nightvision";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Manages NightVisionManager on this server.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (NightVisionPlugin.Instance is not { } plugin)
        {
            response = "NightVisionManager is unavailable.";
            return false;
        }

        string[] args = arguments.ToArray();
        string action = args.Length == 0 ? string.Empty : args[0].ToLowerInvariant();
        VisionController controller = plugin.Controller;
        AdminPermissions permissions = controller.Settings.Permissions;
        string? permission = action switch
        {
            "grant" => permissions.Grant,
            "remove" => permissions.Remove,
            "reset" => permissions.Grant,
            "on" or "off" => permissions.Toggle,
            "status" => permissions.Status,
            "profile" => permissions.Profile,
            "reload" => permissions.Reload,
            _ => null,
        };
        if (permission is null)
        {
            response = Usage;
            return false;
        }

        if (sender is not CommandSender || !sender.CheckPermission(permission)
            || (action == "reset" && !sender.CheckPermission(permissions.Remove)))
        {
            response = "You do not have permission for this NightVisionManager action.";
            return false;
        }

        if (action == "reload")
        {
            if (args.Length == 1)
                return plugin.ReloadSettings(out response);
            response = Usage;
            return false;
        }

        bool validArguments = action switch
        {
            "grant" or "on" => args.Length is 2 or 3,
            "profile" => args.Length == 3,
            _ => args.Length == 2,
        };
        if (!validArguments)
        {
            response = Usage;
            return false;
        }

        if (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            || !Player.TryGet(id, out Player target) || !target.IsVerified || target.IsHost || target.IsNPC)
        {
            response = $"Unknown connected player ID '{args[1]}'.";
            return false;
        }

        switch (action)
        {
            case "grant":
                return controller.SetAssignment(target, true, args.Length == 3 ? args[2] : null, out response);
            case "remove":
                return controller.SetAssignment(target, false, null, out response);
            case "reset":
                return controller.SetAssignment(target, null, null, out response);
            case "on":
                if (args.Length == 2)
                    return controller.SetActive(target, true, null, out response);
                if (double.TryParse(args[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds)
                    && seconds > 0 && !double.IsInfinity(seconds))
                    return controller.SetActive(target, true, seconds, out response);
                response = "Enter a positive duration in seconds.";
                return false;
            case "off":
                return controller.SetActive(target, false, null, out response);
            case "profile":
                return controller.SetProfile(target, args[2], out response);
            default:
                response = controller.Status(target);
                return true;
        }
    }
}
