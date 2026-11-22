using System;
using CommandSystem;
using Exiled.API.Features;

namespace InitStudio.NightVision;

[CommandHandler(typeof(ClientCommandHandler))]
public sealed class ClientToggle : ICommand
{
    public string Command => "nvg";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Toggles your assigned night vision.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count != 0)
        {
            response = "Usage: .nvg";
            return false;
        }

        if (NightVisionPlugin.Instance is not { } plugin || !plugin.Controller.Settings.Activation.CommandEnabled
            || !Player.TryGet(sender, out Player player))
        {
            response = "The night vision command is unavailable.";
            return false;
        }

        return plugin.Controller.Toggle(player, out response);
    }
}
