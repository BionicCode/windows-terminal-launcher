namespace Main;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

internal sealed class ShowHelpAction : CommandAction
{
    public ShowHelpAction() : base(CommandLineOptionId.Help)
    {        
    }

    protected override CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userConfiguration, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ShowHelp(validCommandOptionsTable, userConfiguration);
        return CommandExitMode.Auto;
    }

    private static void ShowHelp(IReadOnlyDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validOptionsTable, UserConfiguration userConfiguration)
    {
        ArgumentNullException.ThrowIfNull(validOptionsTable);

        StringBuilder helpMessageBuilder = new StringBuilder()
            .AppendLine("This application is a command line utility for launching a Windows Terminal tab or")
            .AppendLine("instance from the Windows Explorer's address bar with a specified Windows Terminal")
            .AppendLine("profile. The terminal's working directory is set to the path of the currently")
            .AppendLine("navigated Windows Explorer folder.")
            .AppendLine()
            .AppendLine("For more information, please visit the GitHub repository:")
            .AppendLine("https://github.com/BionicCode/windows-terminal-launcher")
            .AppendLine();

        CreateUsageMessage(helpMessageBuilder);
        helpMessageBuilder = CreateAliasesMessage(userConfiguration, helpMessageBuilder);
        helpMessageBuilder = CreateOptionsMessage(validOptionsTable, helpMessageBuilder);

        CommandHandlerHelpers.ShowInfoDialog(helpMessageBuilder.ToString());
    }

    public static async Task ShowAliasesAsync(string userConfigurationFilePath)
    {
        UserConfiguration configuration = await ConfigurationReader.ReadConfigurationAsync(userConfigurationFilePath);
        var helpMessageBuilder = new StringBuilder();
        helpMessageBuilder = CreateAliasesMessage(configuration, helpMessageBuilder);

        CommandHandlerHelpers.ShowInfoDialog(helpMessageBuilder.ToString());
    }

    private static StringBuilder CreateOptionsMessage(IReadOnlyDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validOptionsTable, StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine()
            .AppendLine("Options:");

        IEnumerable<CommandLineOptionDescriptor> availableOptions = validOptionsTable.Values.Distinct();
        string optionsSeparator = ", ";
        int optionsSeparatorLength = optionsSeparator.Length;
        int maxOptionLength = availableOptions.Max(descriptor => descriptor.Name.Length + (descriptor.HasAlternativeName ? descriptor.AlternativeName.Length + optionsSeparatorLength : 0)) + Padding;
        int maxKindLength = availableOptions.Max(descriptor => descriptor.Kind.ToDisplayString().Length) + Padding;
        int maxDescriptionLength = availableOptions
            .SelectMany(descriptor => descriptor.DescriptionLines)
            .Max(descriptioLine => descriptioLine.Length);

        string nameColumnName = "Option";
        string kindColumnName = "Option Type";
        string descriptionColumnName = "DescriptionLines";
        int cell2Padding = maxOptionLength - nameColumnName.Length;
        int cell3Padding = maxKindLength - kindColumnName.Length;
        _ = messageBuilder.Append(' ', LineIndentation)
            .Append(nameColumnName)
            .Append(' ', cell2Padding)
            .Append(kindColumnName)
            .Append(' ', cell3Padding)
            .AppendLine(descriptionColumnName)
            .Append(' ', LineIndentation)
            .Append('-', maxOptionLength + maxKindLength + maxDescriptionLength)
            .AppendLine();

        foreach (CommandLineOptionDescriptor descriptor in availableOptions.OrderBy(o => o.Name))
        {
            cell2Padding = maxOptionLength - (descriptor.Name.Length + (descriptor.HasAlternativeName ? descriptor.AlternativeName.Length + optionsSeparatorLength : 0));
            cell3Padding = maxKindLength - descriptor.Kind.ToDisplayString().Length;
            _ = messageBuilder.Append(' ', LineIndentation);
            if (descriptor.HasAlternativeName)
            {
                _ = messageBuilder.AppendJoin(optionsSeparator, descriptor.Name, descriptor.AlternativeName);
            }
            else
            {
                _ = messageBuilder.Append(descriptor.Name);
            }

            _ = messageBuilder.Append(' ', cell2Padding)
                .Append(descriptor.Kind.ToDisplayString())
                .Append(' ', cell3Padding);

            for (int lineIndex = 0; lineIndex < descriptor.DescriptionLines.Length; lineIndex++)
            {
                string descriptionLine = descriptor.DescriptionLines[lineIndex];
                _ = messageBuilder.AppendLine(descriptionLine);

                if (lineIndex + 1 < descriptor.DescriptionLines.Length)
                {
                    _ = messageBuilder.Append(' ', LineIndentation + maxOptionLength + maxKindLength);
                }
            }
        }

        return messageBuilder;
    }

    private static StringBuilder CreateAliasesMessage(UserConfiguration configuration, StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine("Aliases:")
            .Append(' ', LineIndentation)
            .AppendLine("Note: If no alias is provided, the default alias as specified in the")
            .Append(' ', LineIndentation)
            .AppendLine("user configuration YAML file will be used.")
            .Append(' ', LineIndentation)
            .AppendLine("If no such default alias was specified, the default profile of")
            .Append(' ', LineIndentation)
            .AppendLine("the Windows Terminal will be used.")
            .Append(' ', LineIndentation)
            .AppendLine("Furthermore, if the options of type 'Mode' e.g., '--config' or '--variable'")
            .Append(' ', LineIndentation)
            .AppendLine("are specified, the alias argument will be ignored.")
            .AppendLine();

        int maxAliasLength = configuration.TerminalProfiles.Max(profile => profile.Alias.Length);
        foreach (TerminalProfile terminalProfile in configuration.TerminalProfiles.OrderBy(profile => profile.Alias))
        {
            int padding = maxAliasLength - terminalProfile.Alias.Length + Padding;
            _ = messageBuilder.Append(' ', LineIndentation)
                .Append(terminalProfile.Alias)
                .Append(' ', padding)
                .Append(terminalProfile.Name);

            if (terminalProfile.IsDefault)
            {
                _ = messageBuilder.Append(" (default)");
            }

            _ = messageBuilder.AppendLine();
        }

        return messageBuilder.AppendLine()
            .Append(' ', LineIndentation)
            .AppendLine(@">> Note: Edit the user configuration file to manage aliases.")
            .Append(' ', LineIndentation)
            .AppendLine(@"   Use 'lit -c -p' to show the current location.");
    }

    private static void CreateUsageMessage(StringBuilder messageBuilder) => _ = messageBuilder
        .AppendLine("Usage:")
        .Append(' ', LineIndentation)
        .AppendLine("Launch Windows Terminal at the current working directory (explorer older):")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("lit [<alias>]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("[(-a | --admin)]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("[(-w | --working-directory) <working-directory-path>]")
        .AppendLine()
        .Append(' ', LineIndentation)
        .AppendLine("Show the current location of the user configuration file:")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("lit (-c | --config) (-p | --print)")
        .AppendLine()
        .Append(' ', LineIndentation)
        .AppendLine("Set the new location of the user configuration file:")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("lit (-c | --config)")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("[(-s | --source) <source-path>]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("[(-d | --destination) <destination-path>]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine(">> Note: If '-s' is not provided the current location")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         of the config will be used.")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         If '-d' is not provided the current explorer location")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         (lineIndex.e. working directory) will be used.")
        .AppendLine()
        .Append(' ', LineIndentation)
        .AppendLine("Set a specified environment variable:")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("lit (--var | --variable)")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("[(--val | --value) <value>]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("((-j | --join) | (-r | --replace) [--del | --delimiter]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("(-m | --machine) | (-u | --user)")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("[--fp | --fold-path]")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine(">> Note: If '--val' is not provided the current working directory")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         will be used as the new value of the envirnoment variable.")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         If the variable doesn't exist it will create it.")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         If '--del' is not provided then the default path limiter")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("         ';' will be used.")
        .AppendLine()
        .Append(' ', LineIndentation)
        .AppendLine("Show the value of a specified environment variable:")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("lit (--var | --variable)")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("(-m | --machine) | (-u | --user)")
        .Append(' ', LineIndentation)
        .Append(' ', LineIndentation)
        .AppendLine("(--p | --print)")
        .AppendLine();
}

internal sealed class ShowVersionAction : CommandAction
{
    public ShowVersionAction() : base(CommandLineOptionId.Help)
    {
    }

    protected override CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userConfiguration, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ShowVersion(validCommandOptionsTable, userConfiguration);
        return CommandExitMode.Auto;
    }

    private static void ShowHelp(IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptionsTable, UserConfiguration userConfiguration)
    {
        ArgumentNullException.ThrowIfNull(validOptionsTable);

        StringBuilder helpMessageBuilder = new StringBuilder()
            .AppendLine("This application is a command line utility for launching a Windows Terminal tab or")
            .AppendLine("instance from the Windows Explorer's address bar with a specified Windows Terminal")
            .AppendLine("profile. The terminal's working directory is set to the path of the currently")
            .AppendLine("navigated Windows Explorer folder.")
            .AppendLine()
            .AppendLine("For more information, please visit the GitHub repository:")
            .AppendLine("https://github.com/BionicCode/windows-terminal-launcher")
            .AppendLine();

        CreateUsageMessage(helpMessageBuilder);
        helpMessageBuilder = CreateAliasesMessage(userConfiguration, helpMessageBuilder);
        helpMessageBuilder = CreateOptionsMessage(validOptionsTable, helpMessageBuilder);

        CommandHandlerHelpers.ShowInfoDialog(helpMessageBuilder.ToString());
    }
}