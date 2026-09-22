namespace Main;

internal record TerminalProfile(string Alias, string Name, bool IsDefault)
{
    public static readonly TerminalProfile Default = new TerminalProfile(string.Empty, string.Empty, false);
    public bool HasName => !string.IsNullOrWhiteSpace(Name);
    public override string ToString() => $"{Alias} -> {Name}";
}
