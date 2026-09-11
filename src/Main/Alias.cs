namespace Main;

internal record Alias(string Name, string ResolvedName, bool IsDefault)
{
    public override string ToString() => $"{Name} -> {ResolvedName}";
}
