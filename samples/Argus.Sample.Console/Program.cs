using System;
using System.Collections.Generic;
using System.Globalization;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Pipeline;
using Argus.State;
using Argus.Testing;
using Argus.Testing.Injectors;

namespace Argus.Sample;

/// <summary>
/// Generates a clean synthetic stream, damages it, and prints what Argus makes of it.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "sample",
            EntityCount = 6,
            TickCount = 12,
        };

        // Deployment gates have no defaults on purpose: a detector with no gate configured
        // reports NotEvaluable rather than inventing a number. These are the sample's numbers,
        // and they describe nothing but this sample.
        var options = new MonitorOptions();
        options.Thresholds.MaxTeleportDistanceMeters = 2000.0;
        options.Thresholds.MaxSpeedMetersPerSecond = 500.0;
        options.Thresholds.GroupOutlierRadiusMeters = 10000.0;

        Console.WriteLine("== clean stream ==");
        Report(new SyntheticStreamSource(scenario), options);

        Console.WriteLine();
        Console.WriteLine("== same stream, read from the wrong byte offset ==");
        var damaged = new InjectedStreamSource(
            new SyntheticStreamSource(scenario),
            scenario.Seed,
            new List<ISampleInjector> { new ByteShiftInjector(byteShift: 8, everyNthTick: 4) }.AsReadOnly());

        Report(damaged, options);

        return 0;
    }

    private static void Report(IEntityStreamSource source, MonitorOptions options)
    {
        var monitor = new EntityHealthMonitor(options);
        var totals = new Dictionary<HealthFlags, int>();
        int samples = 0;

        foreach (StreamTick tick in source.Read())
        {
            // Once per tick, not once per entity.
            GroupTickContext group = monitor.CreateTickContext(tick.Samples, tick.TimeUtc);

            for (int i = 0; i < tick.Samples.Count; i++)
            {
                samples++;
                EntityHealthReport report = monitor.Observe(tick.Samples[i], group);

                foreach (HealthFlags flag in HealthFlagInfo.Split(report.Flags))
                {
                    int count;
                    totals.TryGetValue(flag, out count);
                    totals[flag] = count + 1;
                }

                if (report.Flags != HealthFlags.None && samples <= 200)
                {
                    foreach (HealthFinding finding in report.FlaggedFindings())
                    {
                        Console.WriteLine("  " + report.EntityId + " :: " + finding.ToString());
                    }
                }
            }
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0} samples observed", samples));
        if (totals.Count == 0)
        {
            Console.WriteLine("  no flags raised");
            return;
        }

        foreach (KeyValuePair<HealthFlags, int> total in totals)
        {
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "  {0}: {1} samples",
                total.Key,
                total.Value));
        }
    }
}
