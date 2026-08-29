using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Pipeline;
using Argus.State;

namespace Argus.Cli;

/// <summary>
/// Replays a capture through the monitor and writes the findings out.
/// </summary>
/// <remarks>
/// A capture is JSON Lines: one <c>CaptureRecord</c> per line. Records are grouped into ticks
/// by arrival timestamp, so the group checks see contemporaries — which means a capture whose
/// timestamps are all distinct produces no group findings, and the tool says so rather than
/// silently reporting every entity as fine.
/// </remarks>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        if (!string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Unknown command '" + args[0] + "'.");
            PrintUsage();
            return 2;
        }

        try
        {
            return Replay(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintUsage();
            return 2;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 3;
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine("The capture could not be parsed: " + exception.Message);
            return 4;
        }
    }

    private static int Replay(string[] args)
    {
        string? inputPath = null;
        string? outputPath = null;
        string? thresholdsPath = null;
        OutputFormat format = OutputFormat.Jsonl;
        bool includeHealthy = false;
        bool includeUnimplemented = false;

        for (int i = 1; i < args.Length; i++)
        {
            string argument = args[i];
            switch (argument)
            {
                case "--out":
                case "-o":
                    outputPath = Next(args, ref i, "--out");
                    break;
                case "--format":
                case "-f":
                    format = ParseFormat(Next(args, ref i, "--format"));
                    break;
                case "--thresholds":
                    thresholdsPath = Next(args, ref i, "--thresholds");
                    break;
                case "--include-healthy":
                    includeHealthy = true;
                    break;
                case "--include-unimplemented":
                    includeUnimplemented = true;
                    break;
                default:
                    if (argument.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Unknown option '" + argument + "'.");
                    }

                    inputPath = argument;
                    break;
            }
        }

        if (inputPath == null)
        {
            Console.Error.WriteLine("A capture file is required.");
            PrintUsage();
            return 2;
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Capture file not found: " + inputPath, inputPath);
        }

        var options = new MonitorOptions
        {
            IncludeHealthyFindings = includeHealthy,
            IncludeUnimplementedDetectors = includeUnimplemented,
        };

        if (thresholdsPath != null)
        {
            if (!File.Exists(thresholdsPath))
            {
                throw new FileNotFoundException("Thresholds file not found: " + thresholdsPath, thresholdsPath);
            }

            DetectorThresholds? thresholds = JsonSerializer.Deserialize<DetectorThresholds>(
                File.ReadAllText(thresholdsPath),
                JsonOptions);

            if (thresholds != null)
            {
                options.Thresholds = thresholds;
            }
        }

        var monitor = new EntityHealthMonitor(options);

        TextWriter writer = outputPath == null ? Console.Out : new StreamWriter(outputPath, false);
        long flaggedSamples = 0;
        long totalSamples = 0;

        using (var findingWriter = new FindingWriter(writer, format, outputPath != null))
        {
            foreach (List<EntitySample> tick in ReadTicks(inputPath))
            {
                totalSamples += tick.Count;

                // One context per tick, built once. This is the same discipline the library
                // asks of every caller, and the reason the replay is linear in sample count.
                GroupTickContext group = monitor.CreateTickContext(tick, tick[0].ArrivalTimeUtc);

                for (int i = 0; i < tick.Count; i++)
                {
                    EntityHealthReport report = monitor.Observe(tick[i], group);
                    if (report.Flags != HealthFlags.None)
                    {
                        flaggedSamples++;
                    }

                    for (int f = 0; f < report.Findings.Count; f++)
                    {
                        findingWriter.Write(report, report.Findings[f]);
                    }
                }
            }

            Console.Error.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} samples replayed, {1} flagged, {2} findings written.",
                totalSamples,
                flaggedSamples,
                findingWriter.Written));
        }

        return flaggedSamples > 0 ? 1 : 0;
    }

    /// <summary>
    /// Groups consecutive records that share an arrival timestamp into ticks.
    /// </summary>
    /// <remarks>
    /// Consecutive, not sorted: a capture is a record of arrivals, and reordering it to make
    /// the grouping tidier would erase exactly the fault the temporal detectors exist to find.
    /// </remarks>
    private static IEnumerable<List<EntitySample>> ReadTicks(string path)
    {
        var current = new List<EntitySample>();
        DateTime currentTime = default(DateTime);

        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            CaptureRecord? record = JsonSerializer.Deserialize<CaptureRecord>(line, JsonOptions);
            EntitySample? sample = record?.ToSample();
            if (sample == null)
            {
                continue;
            }

            if (current.Count > 0 && sample.ArrivalTimeUtc != currentTime)
            {
                yield return current;
                current = new List<EntitySample>();
            }

            currentTime = sample.ArrivalTimeUtc;
            current.Add(sample);
        }

        if (current.Count > 0)
        {
            yield return current;
        }
    }

    private static string Next(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length)
        {
            throw new ArgumentException(option + " requires a value.");
        }

        return args[index];
    }

    private static OutputFormat ParseFormat(string value)
    {
        if (string.Equals(value, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Csv;
        }

        if (string.Equals(value, "jsonl", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Jsonl;
        }

        throw new ArgumentException("Unknown format '" + value + "'. Use jsonl or csv.");
    }

    private static bool IsHelp(string argument)
    {
        return string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("argus replay <capture.jsonl> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Replays a capture through the Argus detectors and writes findings.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -o, --out <file>            Write findings here instead of stdout.");
        Console.Error.WriteLine("  -f, --format <jsonl|csv>    Output shape. Default jsonl.");
        Console.Error.WriteLine("      --thresholds <file>     A JSON DetectorThresholds to use instead of the defaults.");
        Console.Error.WriteLine("      --include-healthy       Emit findings for checks that passed, not just ones that failed.");
        Console.Error.WriteLine("      --include-unimplemented Emit NotEvaluable findings for catalogue entries with no detector yet.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("The capture is JSON Lines: one record per line, with EntityId and ArrivalTimeUtc");
        Console.Error.WriteLine("required and every measurement field optional. Records sharing an arrival timestamp");
        Console.Error.WriteLine("form one tick, which is what the group checks compare within.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Exit codes: 0 nothing flagged, 1 findings raised, 2 usage, 3 missing file, 4 bad capture.");
    }
}
