using System.Security.Cryptography;
using System.Text;
using AwardInterpretationRulesEngine;
using Xunit;

namespace AwardInterpretationRulesEngine.Tests;

public sealed class Ma000120PocParityTests
{
    [Fact]
    public async Task Production_pipeline_outputs_match_poc_cli_fixtures()
    {
        var projectRoot = ProjectRoot();
        var originalCurrentDirectory = Environment.CurrentDirectory;

        PipelineResult result;
        try
        {
            Environment.CurrentDirectory = projectRoot;
            var settings = AppSettings.Load(Path.Combine("fixtures", "MA000120.parity-appsettings.json"));
            var payRun = PayRunInput.Load(Path.Combine("samples", "sample-payrun-ma000120.json"));
            result = await new AwardPipeline(settings).RunAsync("MA000120", payRun, CancellationToken.None);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }

        var artifacts = new Dictionary<string, string>
        {
            ["MA000120.interpretation.json"] = JsonUtil.ToJson(result.Interpretation),
            ["MA000120.governed-library.json"] = JsonUtil.ToJson(result.Library),
            ["MA000120.calculation.json"] = JsonUtil.ToJson(result.Calculation)
        };

        var mismatches = new List<string>();
        foreach (var (fileName, actualJson) in artifacts)
        {
            var fixturePath = Path.Combine(projectRoot, "fixtures", "poc-cli", fileName);
            var expectedJson = await File.ReadAllTextAsync(fixturePath);
            if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
                mismatches.Add(DescribeMismatch(fileName, expectedJson, actualJson));
        }

        Assert.True(mismatches.Count == 0, "POC parity fixture mismatches:" + Environment.NewLine + string.Join(Environment.NewLine, mismatches));
    }

    private static string DescribeMismatch(string fileName, string expectedJson, string actualJson)
    {
        var expectedLines = expectedJson.ReplaceLineEndings("\n").Split('\n');
        var actualLines = actualJson.ReplaceLineEndings("\n").Split('\n');
        var max = Math.Max(expectedLines.Length, actualLines.Length);

        for (var i = 0; i < max; i++)
        {
            var expected = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actual = i < actualLines.Length ? actualLines[i] : "<missing>";
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return $"{fileName}: first difference at line {i + 1}; " +
                       $"expected sha256 {Sha256(expectedJson)}, actual sha256 {Sha256(actualJson)}; " +
                       $"expected `{expected}`, actual `{actual}`";
            }
        }

        return $"{fileName}: content differs; expected sha256 {Sha256(expectedJson)}, actual sha256 {Sha256(actualJson)}";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "award_interpretation_rules_engine", "AwardInterpretationRulesEngine.csproj");
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;

            if (File.Exists(Path.Combine(current.FullName, "AwardInterpretationRulesEngine.csproj")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate AwardInterpretationRulesEngine.csproj.");
    }
}
