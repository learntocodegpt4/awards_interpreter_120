using System.Net.Http.Headers;
using AngleSharp.Html.Parser;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AwardInterpretationRulesEngine;

public sealed class AwardSourceClient
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http = new();

    public AwardSourceClient(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<AwardSourceSnapshot> FetchAsync(string awardCode, CancellationToken cancellationToken)
    {
        var onlineUrl = _settings.OnlineAwards.AwardHtmlUrlTemplate.Replace("{awardCode}", awardCode, StringComparison.OrdinalIgnoreCase);
        var retrievedAt = _settings.OnlineAwards.FixedRetrievedAtUtc ?? DateTimeOffset.UtcNow;
        var snapshot = new AwardSourceSnapshot
        {
            AwardCode = awardCode,
            OnlineUrl = onlineUrl,
            RetrievedAtUtc = retrievedAt
        };

        snapshot.Html = await ReadSourceTextAsync(onlineUrl, cancellationToken);
        snapshot.ContentSha256 = ComputeSha256(snapshot.Html);
        snapshot.SourceRecordId = $"{awardCode.ToUpperInvariant()}-{snapshot.ContentSha256[..12]}";

        if (_settings.FairWorkApi.Enabled && !string.IsNullOrWhiteSpace(_settings.FairWorkApi.SubscriptionKey))
        {
            snapshot.ApiAwardJson = await TryGetApiJsonAsync(_settings.FairWorkApi.AwardByCodePath, awardCode, cancellationToken);
            snapshot.ApiRatesJson = await TryGetApiJsonAsync(_settings.FairWorkApi.RatesByAwardPath, awardCode, cancellationToken);
        }

        return snapshot;
    }

    private async Task<string> ReadSourceTextAsync(string source, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile)
            return await File.ReadAllTextAsync(uri.LocalPath, cancellationToken);

        if (File.Exists(source))
            return await File.ReadAllTextAsync(source, cancellationToken);

        return await _http.GetStringAsync(source, cancellationToken);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<string?> TryGetApiJsonAsync(string pathTemplate, string awardCode, CancellationToken cancellationToken)
    {
        try
        {
            var baseUri = new Uri(_settings.FairWorkApi.BaseUrl.TrimEnd('/') + "/");
            var relative = pathTemplate.TrimStart('/').Replace("{awardCode}", Uri.EscapeDataString(awardCode), StringComparison.OrdinalIgnoreCase);
            var uri = new Uri(baseUri, relative);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation(_settings.FairWorkApi.SubscriptionHeaderName, _settings.FairWorkApi.SubscriptionKey);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class OnlineAwardHtmlParser
{
    public async Task<ParsedAwardDocument> ParseAsync(AwardSourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(snapshot.Html, cancellationToken);

        foreach (var node in document.QuerySelectorAll("script,style,nav,header,footer"))
            node.Remove();

        var title = Normalise(document.QuerySelector("h1")?.TextContent ?? document.Title ?? snapshot.AwardCode);
        var text = NormaliseMultiline(document.Body?.TextContent ?? "");

        var parsed = new ParsedAwardDocument
        {
            AwardCode = snapshot.AwardCode,
            AwardTitle = title,
            ConsolidationSummary = ExtractConsolidationSummary(text),
            PlainText = text
        };

        foreach (var clauseNumber in new[] { "1", "2", "3", "4", "10", "13", "14", "15", "18", "21", "22", "23", "25", "27" })
        {
            var clause = ExtractClause(text, clauseNumber);
            if (clause is not null) parsed.Clauses.Add(clause);
        }

        if (parsed.Clauses.Count == 0)
            parsed.ParseWarnings.Add("No clauses were extracted. Check public award page markup and parser rules.");

        return parsed;
    }

    private static string ExtractConsolidationSummary(string text)
    {
        var match = Regex.Match(text, @"This Fair Work Commission consolidated modern award incorporates all amendments up to and including\s+(?<date>.+?)(?:Clause|Part 1|1\.)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? Normalise(match.Value) : "";
    }

    private static AwardClause? ExtractClause(string text, string clauseNumber)
    {
        var pattern = $@"(?ms)(^|\n)\s*{Regex.Escape(clauseNumber)}\.\s*(?<body>.*?)(?=\n\s*(?:\d+[A-Z]?\.|Schedule\s+[A-Z])\s+|$)";
        var match = Regex.Match(text, pattern);
        if (!match.Success) return null;

        var body = NormaliseMultiline(match.Groups["body"].Value);
        var firstLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return new AwardClause
        {
            ClauseNumber = clauseNumber,
            Heading = firstLine.Length > 160 ? firstLine[..160] : firstLine,
            Text = body
        };
    }

    private static string Normalise(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static string NormaliseMultiline(string value)
    {
        value = value.Replace('\u00A0', ' ');
        value = Regex.Replace(value, @"[ \t]+", " ");
        value = Regex.Replace(value, @"\n\s*\n+", "\n");
        value = Regex.Replace(value, @"(?<!\n)(\d+[A-Z]?\.)\s+", "\n$1 ");
        return value.Trim();
    }
}
