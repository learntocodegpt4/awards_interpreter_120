using AwardInterpretationRulesEngine;

var options = CliOptions.Parse(args);

if (options.ShowHelp)
{
    Console.WriteLine(CliOptions.HelpText);
    return;
}

if (options.RunAcceptance)
{
    await Ma000120AcceptanceSuite.RunAsync(CancellationToken.None);
    return;
}

var settings = AppSettings.Load(options.ConfigPath);
var pipeline = new AwardPipeline(settings);

var payRun = PayRunInput.Load(options.InputPath);
var result = await pipeline.RunAsync(options.AwardCode, payRun, CancellationToken.None);

Directory.CreateDirectory(options.OutputDirectory);

var interpretationPath = Path.Combine(options.OutputDirectory, $"{options.AwardCode}.interpretation.json");
var libraryPath = Path.Combine(options.OutputDirectory, $"{options.AwardCode}.governed-library.json");
var calculationPath = Path.Combine(options.OutputDirectory, $"{options.AwardCode}.calculation.json");

await File.WriteAllTextAsync(interpretationPath, JsonUtil.ToJson(result.Interpretation));
await File.WriteAllTextAsync(libraryPath, JsonUtil.ToJson(result.Library));
await File.WriteAllTextAsync(calculationPath, JsonUtil.ToJson(result.Calculation));

Console.WriteLine("Award interpretation and calculation completed.");
Console.WriteLine($"Interpretation: {interpretationPath}");
Console.WriteLine($"Governed library: {libraryPath}");
Console.WriteLine($"Calculation: {calculationPath}");
Console.WriteLine($"Payroll gross: {result.Calculation.PayrollGross:C}");
Console.WriteLine($"Award reference gross: {result.Calculation.AwardReferenceGross:C}");
Console.WriteLine($"Warnings: {result.Calculation.Warnings.Count}");
