using ProgOpts2025;

namespace ProgOptsTest;

class Program
{
    private static void Main(string[] args)
    {
        try
        {
            var option1 = new OptionBuilder().WithShortOption('f').WithLongOption("file").WithNumberOfParams(1).WithGroup("Hello").Build();
            var option2 = new OptionBuilder().WithShortOption('n').WithLongOption("max-count").WithNumberOfParams(1).WithGroup("Count").Build();
            var r = new OptionsProcessor([option1, option2]);
            if (!r.ParseCommandLine(args, allowedGroups: ["Hello"]))
            {
                Console.WriteLine("Illegal options found");
                foreach (var opt in r.IllegalOptions)
                {
                    Console.WriteLine($"{opt.Name} {opt.Index} {opt.ErrorCode}");
                }
            }
            else
            {
                foreach (var normalParm in r.NonOptions)
                {
                    Console.WriteLine($"{normalParm}");
                }
                if (r.GetParam("file", out string filename))
                {
                    Console.WriteLine(filename);
                }
            }
        }
        catch (Exception ex)
        {
            var fullname = System.Reflection.Assembly.GetEntryAssembly().Location;
            var progname = Path.GetFileNameWithoutExtension(fullname);
            Console.Error.WriteLine($"{progname} Error: {ex.Message}");
        }
    }
}
