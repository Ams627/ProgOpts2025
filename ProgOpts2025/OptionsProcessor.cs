using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgOpts2025;

public class OptionsProcessor
{
    private readonly List<IllegalOption> _illegalOptions = [];
    private readonly IDictionary<string, OptionSpec> _longOptions;
    private readonly Dictionary<string, int> _stringToAllowedOptionsIndex;

    private readonly List<NonOption> _nonOptions = [];

    /// <summary>
    /// mapping between the index into the allowed options array and any options found on the command line:
    /// </summary>
    private readonly Dictionary<int, List<ParsedOption>> _parsedOptions = [];
    private readonly IDictionary<char, OptionSpec> _shortOptions;
    private readonly Dictionary<char, int> _charToAllowedOptionsIndex;

    /// <summary>
    /// supported options specified to the constructor
    /// </summary>
    private readonly OptionSpec[] _supportedOptions;
    private HashSet<string> _allowedGroups;
    private RewindableQueue<string> _rewindableQueue;
    public OptionsProcessor(OptionSpec[] options)
    {
        _supportedOptions = options;

        var shortDups = options.GroupBy(x => x.ShortOption).Where(x => x.Count() > 1);
        var longDups = options.GroupBy(x => x.LongOption).Where(x => x.Count() > 1);

        if (shortDups.Any() || longDups.Any())
        {
            var smessages = shortDups.Select(x => $"option ={x} specified more than once.")
                .Concat(longDups.Select(x => $"option --{x} specified more than once"));
            throw new ArgumentException(string.Join("\r\n", smessages), "options");
        }

        // short option (char) to the index of the option in the options array:
        _charToAllowedOptionsIndex = options.Select((x, i) => new { x.ShortOption, Index = i }).ToDictionary(x => x.ShortOption, x => x.Index);

        // long option (string) to the index of the option in the options array:
        _stringToAllowedOptionsIndex = options.Select((x, i) => new { x.LongOption, Index = i }).ToDictionary(x => x.LongOption, x => x.Index);

        _shortOptions = options.ToDictionary(x => x.ShortOption);
        _longOptions = options.ToDictionary(x => x.LongOption);
    }

    public object[] AllowedGroups => [.. _allowedGroups];
    public IllegalOption[] IllegalOptions => [.. _illegalOptions];
    public NonOption[] NonOptions => [.. _nonOptions];

    /// <summary>
    /// Get the number of occurrences of a particular option specified on the command line sent to ParseOptions
    /// </summary>
    /// <typeparam name="T">should be string (double-letter option) or char (single letter option)</typeparam>
    /// <param name="optionName">a letter or a string for which to return the count</param>
    /// <returns>the number of occurrences of the option</returns>
    public int GetOptionCount<T>(T optionName)
    {
        int optionIndex = -1;

        if (optionName is char c)
        {
            _charToAllowedOptionsIndex.TryGetValue(c, out optionIndex);
        }
        else if (optionName is string s)
        {
            _stringToAllowedOptionsIndex.TryGetValue(s, out optionIndex);
        }

        if (optionIndex == -1)
        {
            return 0;
        }

        var isPresent = _parsedOptions.TryGetValue(optionIndex, out var optionList);
        if (!isPresent)
        {
            return 0;
        }
        return optionList.Count();
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T">string or char to specify either short option name or long option name - will normally be inferred by the compiler</typeparam>
    /// <param name="optionName">either a single letter or a long option name without the leading double-dash</param>
    /// <param name="offset">where an option can occur more than once on the command line, this specifies the offset. For example, -i may occur more than one to specify several input files</param>
    /// <returns>The option parameter(s) or null if the option was not present.</returns>
    public bool GetParam<T>(T optionName, out string result, int offset = 0)
    {
        int optionIndex = -1;
        bool found = false;

        // is it a long (double-dash) option (a string) or a short option (a single char)?
        // which ever one it is, we'll get the index of the option in the allowed option array.
        // Obviously, if there IS an index the option is valid
        if (optionName is string strOptionName)
        {
            found = _stringToAllowedOptionsIndex.TryGetValue(strOptionName, out optionIndex);
        }
        else if (optionName is char optionChar)
        {
            found = _charToAllowedOptionsIndex.TryGetValue(optionChar, out optionIndex);
        }

        if (!found)
        {
            result = default;
            return false;
        }

        if (!_parsedOptions.TryGetValue(optionIndex, out var parsedOption))
        {
            result = default;
            return false;
        }

        // there can be several of the same options - e.g. progname -i file1.c -i file2.c
        if (parsedOption[offset].Params is string str)
        {
            result = str;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// GetParam - get the parameter list for an option. The option is either a single letter (single-dash option)
    ///            or a string (double-dash or long option).
    /// </summary>
    /// <typeparam name="T">Either char or string</typeparam>
    /// <param name="optionName">Either a single character or a string</param>
    /// <param name="result">the value of the option parameter - e.g. if the option is --file=f1.c, then result is f1.c</param>
    /// <param name="offset">Where more than one option by the same letter or string is allowed, this is the zero-based offset of the option</param>
    /// <returns>true if the parameter was found</returns>
    public bool GetParam<T>(T optionName, out List<string> result, int offset = 0)
    {
        int optionIndex = -1;
        bool found = false;

        // is it a long (double-dash) option (a string) or a short option (a single char)?
        // which ever one it is, we'll get the index of the option in the allowed option array.
        // Obviously, if there IS an index the option is valid
        if (optionName is string strOptionName)
        {
            found = _stringToAllowedOptionsIndex.TryGetValue(strOptionName, out optionIndex);
        }
        else if (optionName is char optionChar)
        {
            found = _charToAllowedOptionsIndex.TryGetValue(optionChar, out optionIndex);
        }

        if (!found)
        {
            result = default;
            return false;
        }

        if (!_parsedOptions.TryGetValue(optionIndex, out var parsedOption))
        {
            result = default;
            return false;
        }

        // there can be several of the same options - e.g. progname -i file1.c -i file2.c
        if (parsedOption[offset].Params is List<string> list)
        {
            result = list;
            return true;
        }

        result = default;
        return false;
    }


    /// <summary>
    /// Determine if a specified single character option is present on the command line. If the option
    /// is present on the command line and has been specified as a valid option, then return true; else
    /// return false.
    /// </summary>
    /// <param name="c">The single character option</param>
    /// <returns>true if the option is present, otherwise false</returns>
    public bool IsOptionPresent(char c) => _charToAllowedOptionsIndex.TryGetValue(c, out var optionIndex) && _parsedOptions.TryGetValue(optionIndex, out _);

    /// <summary>
    /// Determine if a specified long (string) option is present on the command line. If the option
    /// is present on the command line and has been specified as a valid option, then return true; else
    /// return false.
    /// </summary>
    /// <param name="s">The long double-dashed option</param>
    /// <returns>true if the option is present, otherwise false</returns>
    public bool IsOptionPresent(string s) => _stringToAllowedOptionsIndex.TryGetValue(s, out var optionIndex) && _parsedOptions.TryGetValue(optionIndex, out _);

    /// <summary>
    /// Parses a command line passed as an array and fills in internal structures. Options can then be checked via various get accessors.
    /// </summary>
    /// <param name="args">The full command line</param>
    /// <param name="offset">offset within the args array at which to start processing</param>
    /// <param name="allowedGroups">option groups to allow</param>
    public bool ParseCommandLine(string[] args, int offset = 0, string[] allowedGroups = null)
    {
        _rewindableQueue = new RewindableQueue<string>(args, offset);
        _allowedGroups = allowedGroups == null ? [] : [.. allowedGroups];

        while (!_rewindableQueue.Empty)
        {
            var (arg, index) = _rewindableQueue.PopFront();

            // check for a double-dash (long) option:
            if (arg[0] == '-' && arg[1] == '-')
            {
                if (arg.Length == 2)
                {
                    // here we have found an end-of=options marker (just two dashes) with nothing else:
                    break;
                }

                bool continueArgProcessing = ProcessDoubleDashOption(arg.Substring(2), index);
                if (!continueArgProcessing)
                {
                    break;
                }
            }
            else if (arg[0] == '-')
            {
                // this means stdin and we don't know what to do yet here
                if (arg.Length == 1)
                {
                    _nonOptions.Add(new(arg, index));
                }
                else
                {
                    bool continueArgProcessing = ProcessSingleDashOptions(arg.Substring(1), index);
                    if (!continueArgProcessing)
                    {
                        break;
                    }
                }
            }
            else
            {
                _nonOptions.Add(new(arg, index));
            }
        }
        return !_illegalOptions.Any();
    }


    /// <summary>
    /// returns the parameter list for a long option
    /// </summary>
    /// <param name="option">The option</param>
    /// <param name="list">the output parameter</param>
    /// <param name="offset">the index of the parameter</param>
    /// <returns>bool</returns>
    public bool TryGetList(string option, out string[] list, int offset = 0)
    {
        bool result = _stringToAllowedOptionsIndex.TryGetValue(option, out var optionIndex);
        if (!result)
        {
            list = default;
            return false;
        }
        result = _parsedOptions.TryGetValue(optionIndex, out var optionParam);
        if (!result)
        {
            list = default;
            return false;
        }
        if (optionParam[offset].Params is List<string> strList)
        {
            list = strList.ToArray();
            return true;
        }
        list = default;
        return false;
    }

    public bool TryGetValue(string option, out string value, int offset = 0)
    {
        bool result = _stringToAllowedOptionsIndex.TryGetValue(option, out var optionIndex);
        if (!result)
        {
            value = default;
            return false;
        }
        result = _parsedOptions.TryGetValue(optionIndex, out var optionParam);
        if (!result)
        {
            value = default;
            return false;
        }
        if (optionParam[offset].Params is string str)
        {
            value = str;
            return true;
        }
        value = default;
        return false;
    }


    private ParsedOption ConsumeOptionParameters(ParsedOption option)
    {
        object paramList;
        var numberOfParams = _supportedOptions[option.OptionIndex].NumberOfParams;
        if (numberOfParams == 1)
        {
            paramList = _rewindableQueue.PopFront().item;
        }
        else
        {
            var list = new List<string>();
            paramList = list;
            for (int p = 0; p < numberOfParams; p++)
            {
                list.Add(_rewindableQueue.PopFront().item);
            }
        }
        return option with { Params = paramList };
    }

    private bool ProcessDoubleDashOption(string arg, int index)
    {
        // check for a double-dash option with equals in it - the value after equals is the option parameter.
        // subsequent equals after the first ones are allowed - they become part of the option parameter:
        var equalsPosition = arg.IndexOf('=');
        if (equalsPosition == 0)
        {
            _illegalOptions.Add(new("--=", index, ErrorCodes.EqualFirstChar));
        }
        else if (equalsPosition > 0)
        {
            var option = arg.Substring(0, equalsPosition);
            var found = _longOptions.TryGetValue(option, out var optionSpec);
            var optionGroupAllowed = found && (optionSpec.Group == null || _allowedGroups.Contains(optionSpec.Group));

            if (!found || !optionGroupAllowed)
            {
                _illegalOptions.Add(new(option, index, ErrorCodes.OptionNotSpecified));
                return true;
            }

            // only a single parameter is allowed after the equals:
            if (optionSpec.NumberOfParams != 1)
            {
                _illegalOptions.Add(new(option, index, ErrorCodes.EqualOptionNotSingleParam));
                return true;
            }

            var parameter = arg.Substring(equalsPosition + 1);
            if (!parameter.Any())
            {
                _illegalOptions.Add(new(option, index, ErrorCodes.EqualOptionEmptyParameter));
            }

            var parsedOption = new ParsedOption(index, false, _stringToAllowedOptionsIndex[option], parameter);
            DictUtils.AddEntryToList(_parsedOptions, parsedOption.OptionIndex, parsedOption);
        }
        else
        {
            // no equals in arg
            var found = _longOptions.TryGetValue(arg, out var optionSpec);
            var optionGroupAllowed = found && (optionSpec.Group == null || _allowedGroups.Contains(optionSpec.Group));

            if (!found || !optionGroupAllowed)
            {
                _illegalOptions.Add(new(arg, index, ErrorCodes.OptionNotSpecified));
                return true;
            }

            if (optionSpec.NumberOfParams > _rewindableQueue.Remaining)
            {
                _illegalOptions.Add(new(arg, index, ErrorCodes.OptionNotEnoughParams));
                // can't do anything else here as we don't have enough args to process:
                return false;
            }

            var optionIndex = _stringToAllowedOptionsIndex[arg];
            var parsedOption = new ParsedOption(index, false, optionIndex, default);
            if (optionSpec.NumberOfParams > 0)
            {
                parsedOption = ConsumeOptionParameters(parsedOption);
            }
            DictUtils.AddEntryToList(_parsedOptions, parsedOption.OptionIndex, parsedOption);
        }
        return true;
    }

    private bool ProcessSingleDashOptions(string arg, int index)
    {
        for (int i = 0; i < arg.Length; i++)
        {
            var isLast = (i == arg.Length - 1);
            var c = arg[i];

            var found = _shortOptions.TryGetValue(c, out var optionSpec);
            var optionGroupAllowed = found && (optionSpec.Group == null || _allowedGroups.Contains(optionSpec.Group));

            if (!found || !optionGroupAllowed)
            {
                // store illegal option, but continue processing:
                _illegalOptions.Add(new($"{c}", index, ErrorCodes.OptionNotSpecified));
                continue;
            }

            if (isLast)
            {
                if (optionSpec.NumberOfParams > _rewindableQueue.Remaining)
                {
                    // not enough args left as option parameters:
                    _illegalOptions.Add(new($"{c}", index, ErrorCodes.OptionNotEnoughParams));

                    // can't parse anything else here so return false:
                    return false;
                }
                else
                {
                    var optionIndex = _charToAllowedOptionsIndex[c];
                    var parsedOpt = new ParsedOption(index, false, optionIndex, default);
                    parsedOpt = ConsumeOptionParameters(parsedOpt);
                    DictUtils.AddEntryToList(_parsedOptions, optionIndex, parsedOpt);
                }
            }
            else if (optionSpec.NumberOfParams == 0)
            {
                // it's a "boolean" option at this point (it's just a flag and has no parameters) - there may be more options
                // in this arg as in grep -iPo pattern *.txt

                var optionIndex = _charToAllowedOptionsIndex[c];
                var parsedOpt = new ParsedOption(index, true, optionIndex, default);
                DictUtils.AddEntryToList(_parsedOptions, optionIndex, parsedOpt);
            }
            else if (optionSpec.NumberOfParams == 1)
            {
                // the single parameter is the rest of the arg - store it and stop scanning this arg:
                var optionIndex = _charToAllowedOptionsIndex[c];
                var parsedOpt = new ParsedOption(index, true, optionIndex, arg.Substring(i + 1));
                DictUtils.AddEntryToList(_parsedOptions, optionIndex, parsedOpt);
                break;
            }
            else
            {
                // you can't have an option with more than one parameter right next to the single char option:
                _illegalOptions.Add(new($"{c}", index, ErrorCodes.AdjoiningOptionNotSingleParam));

                // can't parse anything else here so return:
                return false;
            }
        }

        return true;
    }
}