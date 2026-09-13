namespace Main;

internal record Alias(string Name, string ResolvedName, bool IsDefault)
{
    public static readonly Alias Default = new Alias(string.Empty, string.Empty, false);

    public override string ToString() => $"{Name} -> {ResolvedName}";
}
