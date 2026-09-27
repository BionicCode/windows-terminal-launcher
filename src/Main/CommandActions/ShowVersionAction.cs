namespace Main;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

internal sealed class ShowVersionAction : CommandAction
{
    public ShowVersionAction() : base(CommandLineOptionId.Help)
    {
    }

    protected override CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userConfiguration, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ShowVersion();
        return CommandExitMode.Auto;
    }

    private static void ShowVersion()
    {
        StringBuilder helpMessageBuilder = new StringBuilder()
            .AppendLine("This application is a command line utility for launching a Windows Terminal tab or")
            .AppendLine("instance from the Windows Explorer's address bar with a specified Windows Terminal")
            .AppendLine("profile. The terminal's working directory is set to the path of the currently")
            .AppendLine("navigated Windows Explorer folder.")
            .AppendLine()
            .AppendLine("For more information, please visit the GitHub repository:")
            .AppendLine("https://github.com/BionicCode/windows-terminal-launcher")
            .AppendLine();

        

        CommandHelpers.ShowInfoDialog(helpMessageBuilder.ToString());
    }
}