namespace ProgOpts2025;

public record OptionSpec
{
    public char ShortOption { get; }
    public string LongOption { get; }
    public int MaxOccurs { get; }
    public int NumberOfParams { get; }
    public string Group { get; }

    internal OptionSpec(char shortOption, string longOption, int maxOccurs, int numberOfParams, string group)
    {
        ShortOption = shortOption;
        LongOption = longOption;
        MaxOccurs = maxOccurs;
        NumberOfParams = numberOfParams;
        Group = group;
    }
}
