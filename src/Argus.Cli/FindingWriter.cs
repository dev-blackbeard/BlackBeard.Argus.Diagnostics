using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Argus.Contracts;

namespace Argus.Cli;

/// <summary>The shape findings are written in.</summary>
internal enum OutputFormat
{
    Jsonl = 0,
    Csv = 1,
}

/// <summary>
/// Writes findings out in a form somebody who does not have this repository can read.
/// </summary>
/// <remarks>
/// Every row carries the flag name, its one-line definition, the measured value and the
/// expected one (architecture rule 7). That redundancy is the point: the output of this tool
/// is meant to be pasted into a conversation with the team producing the stream, and it has
/// to survive being read by somebody who cannot look up what the flag means.
/// </remarks>
internal sealed class FindingWriter : IDisposable
{
    private static readonly string[] CsvHeaders =
    {
        "entityId", "timestampUtc", "outcome", "flag", "category", "detectorId",
        "measured", "expected", "measuredValue", "unit", "reason", "definition",
    };

    private readonly TextWriter _writer;
    private readonly OutputFormat _format;
    private readonly bool _ownsWriter;

    private bool _headerWritten;

    internal FindingWriter(TextWriter writer, OutputFormat format, bool ownsWriter)
    {
        _writer = writer;
        _format = format;
        _ownsWriter = ownsWriter;
    }

    internal long Written { get; private set; }

    internal void Write(EntityHealthReport report, HealthFinding finding)
    {
        if (_format == OutputFormat.Csv)
        {
            WriteCsv(report, finding);
        }
        else
        {
            WriteJsonl(report, finding);
        }

        Written++;
    }

    public void Dispose()
    {
        _writer.Flush();
        if (_ownsWriter)
        {
            _writer.Dispose();
        }
    }

    private void WriteJsonl(EntityHealthReport report, HealthFinding finding)
    {
        var record = new Dictionary<string, object?>
        {
            ["entityId"] = report.EntityId,
            ["timestampUtc"] = report.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            ["outcome"] = finding.Outcome.ToString(),
            ["flag"] = finding.FlagName,
            ["category"] = finding.Category.ToString(),
            ["detectorId"] = finding.DetectorId,
            ["measured"] = finding.Measured,
            ["expected"] = finding.Expected,
            ["measuredValue"] = finding.MeasuredValue,
            ["unit"] = finding.Unit,
            ["reason"] = finding.Reason,
            ["definition"] = finding.Definition,
        };

        _writer.WriteLine(JsonSerializer.Serialize(record));
    }

    private void WriteCsv(EntityHealthReport report, HealthFinding finding)
    {
        if (!_headerWritten)
        {
            _writer.WriteLine(string.Join(",", CsvHeaders));
            _headerWritten = true;
        }

        var fields = new[]
        {
            report.EntityId,
            report.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            finding.Outcome.ToString(),
            finding.FlagName,
            finding.Category.ToString(),
            finding.DetectorId,
            finding.Measured,
            finding.Expected,
            finding.MeasuredValue.HasValue ? finding.MeasuredValue.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty,
            finding.Unit ?? string.Empty,
            finding.Reason ?? string.Empty,
            finding.Definition,
        };

        var line = new StringBuilder();
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                line.Append(',');
            }

            line.Append(Quote(fields[i]));
        }

        _writer.WriteLine(line.ToString());
    }

    private static string Quote(string? value)
    {
        string actual = value ?? string.Empty;
        return "\"" + actual.Replace("\"", "\"\"") + "\"";
    }
}
