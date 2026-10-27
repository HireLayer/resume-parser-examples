// HireLayer API v4 from C# with HttpClient (.NET 8).
//
//   dotnet run --project csharp -- parse [resume.pdf]
//   dotnet run --project csharp -- score [job.txt] [resume1.pdf resume2.pdf ...]
//
// Resume Parser: 1 credit per resume; 0 when the same file is already stored.
// Resume Scorer: 1 credit per resume evaluated, +1 per resume file parsed in the call; 0 for a pair already scored.
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

const int MaxAttempts = 4;

var apiKey = Environment.GetEnvironmentVariable("HIRELAYER_API_KEY")
    ?? throw new InvalidOperationException("HIRELAYER_API_KEY is not set");

using var http = new HttpClient
{
    BaseAddress = new Uri("https://hirelayer.co/api/v4/"),
    Timeout = TimeSpan.FromSeconds(150), // the default 100 s can cut a slow parse short
};
http.DefaultRequestHeaders.Add("X-API-Key", apiKey);

var command = args.Length > 0 ? args[0] : "parse";
try
{
    return command switch
    {
        "parse" => await ParseAsync(args.Length > 1 ? args[1] : "resume.pdf"),
        "score" => await ScoreAsync(args.Length > 1 ? args[1] : "job.txt", args.Length > 2 ? args[2..] : new[] { "resume.pdf" }),
        _ => Usage(),
    };
}
catch (HireLayerException error)
{
    // The v4 error format: {"error": {"code", "message", "param", "doc_url"}}. Branch on the code.
    Console.Error.WriteLine(error.Code switch
    {
        "insufficient_credits" => "Out of credits: see https://hirelayer.co/pricing",
        "resume_unreadable" => "The resume file could not be read (not billed)",
        "job_unreadable" => "The job file could not be read (not billed)",
        _ => $"{error.Message} ({error.DocUrl})",
    });
    return 1;
}

async Task<int> ParseAsync(string path)
{
    var bytes = await File.ReadAllBytesAsync(path); // read once, so a retry sends the same bytes
    var (_, json) = await PostAsync("resumes/parse", () =>
    {
        var form = new MultipartFormDataContent();
        form.Add(FilePart("file", path, bytes));
        return form;
    });

    var resume = json.GetProperty("resume"); // the parsed resume
    var candidate = resume.GetProperty("info_candidate");
    var skills = JsonRead.Items(resume, "skills").Select(skill => JsonRead.Str(skill, "skill_title"));

    Console.WriteLine($"application_id: {JsonRead.Str(json, "application_id") ?? "(not stored)"}");
    Console.WriteLine($"job title: {JsonRead.Str(candidate, "job_title") ?? "(none found)"}");
    Console.WriteLine($"positions: {JsonRead.Items(resume, "work_experiences").Count()}");
    Console.WriteLine($"skills: {string.Join(", ", skills)}");
    Console.WriteLine($"photo found: {JsonRead.Str(resume.GetProperty("info_resume"), "face_url") is not null}");
    foreach (var warning in JsonRead.Items(json, "warnings"))
    {
        Console.WriteLine($"warning: {warning.GetString()}");
    }
    Console.WriteLine($"credits used: {json.GetProperty("credits_used").GetInt32()}");
    return 0;
}

async Task<int> ScoreAsync(string jobPath, string[] resumePaths)
{
    var jobText = await File.ReadAllTextAsync(jobPath);
    var files = new List<byte[]>();
    foreach (var path in resumePaths)
    {
        files.Add(await File.ReadAllBytesAsync(path)); // read once, so a retry sends the same bytes
    }

    // The call as JSON in the "request" part; each resume file in the part it names.
    var requestJson = JsonSerializer.Serialize(new
    {
        job = new { text = jobText },
        resumes = resumePaths
            .Select((path, i) => new { file = $"cv{i + 1}", reference = Path.GetFileName(path) })
            .ToArray(),
    });

    var (status, json) = await PostAsync("resumes/score", () =>
    {
        var form = new MultipartFormDataContent();
        var request = new StringContent(requestJson, Encoding.UTF8, "application/json");
        request.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"request\"" };
        form.Add(request);
        for (var i = 0; i < files.Count; i++)
        {
            form.Add(FilePart($"cv{i + 1}", resumePaths[i], files[i]));
        }
        return form;
    });

    if (status == HttpStatusCode.Accepted)
    {
        // A large call: the scores are computed in the background.
        Console.WriteLine($"Accepted, results at: {JsonRead.Str(json, "results_url")}");
        return 0;
    }

    foreach (var score in JsonRead.Items(json, "scores")) // best first
    {
        var rank = score.GetProperty("rank").GetInt32();
        var reference = JsonRead.Str(score, "reference");
        var evaluationStatus = JsonRead.Str(score, "evaluation_status");
        if (evaluationStatus != "completed")
        {
            var code = score.TryGetProperty("error", out var error) ? JsonRead.Str(error, "code") : null;
            Console.WriteLine($"{rank}. {reference}: {evaluationStatus} ({code})");
            continue;
        }
        var verdict = JsonRead.Str(score, "verdict") ?? "(no verdict)";
        Console.WriteLine($"{rank}. {reference}: {score.GetProperty("score").GetDouble():0.00} {verdict}");
        Console.WriteLine($"   {JsonRead.Str(score, "summary")}");
    }
    Console.WriteLine($"job_id: {JsonRead.Str(json.GetProperty("job"), "job_id") ?? "(not stored)"}");
    Console.WriteLine($"credits used: {json.GetProperty("credits_used").GetInt32()}");
    return 0;
}

// POST the content built by makeContent (again on each attempt).
// Retries 429 after Retry-After, and 502/503 with backoff; any other error throws HireLayerException.
async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string path, Func<HttpContent> makeContent)
{
    for (var attempt = 1; ; attempt++)
    {
        using var content = makeContent();
        using var response = await http.PostAsync(path, content);
        var status = (int)response.StatusCode;
        if ((status == 429 || status == 502 || status == 503) && attempt < MaxAttempts)
        {
            await Task.Delay(RetryDelay(response, attempt));
            continue;
        }
        var body = await ReadJsonAsync(response);
        if (status >= 400)
        {
            throw HireLayerException.From(status, body);
        }
        return (response.StatusCode, body);
    }
}

static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
{
    var retryAfter = response.Headers.RetryAfter?.Delta;
    if ((int)response.StatusCode == 429 && retryAfter is not null)
    {
        return retryAfter.Value;
    }
    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2, 4, 8 s
    return retryAfter > backoff ? retryAfter.Value : backoff;
}

static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
{
    var text = await response.Content.ReadAsStringAsync();
    try
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
    catch (JsonException)
    {
        return default; // not JSON, such as a proxy error page
    }
}

// Quoted name and filename, and no filename*: see the C# guide linked in the README.
static ByteArrayContent FilePart(string name, string path, byte[] bytes)
{
    var file = new ByteArrayContent(bytes);
    file.Headers.ContentType = new MediaTypeHeaderValue(
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream");
    file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
    {
        Name = $"\"{name}\"",
        FileName = $"\"{Path.GetFileName(path)}\"",
    };
    return file;
}

static int Usage()
{
    Console.Error.WriteLine("Usage: dotnet run -- parse [resume.pdf]");
    Console.Error.WriteLine("       dotnet run -- score [job.txt] [resume1.pdf resume2.pdf ...]");
    return 2;
}

/// <summary>An answer in the v4 error format: {"error": {"code", "message", "param", "doc_url"}}.</summary>
sealed class HireLayerException(int status, string code, string message, string? docUrl)
    : Exception($"HTTP {status} {code}: {message}")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string? DocUrl { get; } = docUrl;

    public static HireLayerException From(int statusCode, JsonElement body)
    {
        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object)
        {
            return new(statusCode, JsonRead.Str(error, "code") ?? "unknown", JsonRead.Str(error, "message") ?? "", JsonRead.Str(error, "doc_url"));
        }
        return new(statusCode, "unexpected_response", $"HTTP {statusCode}", null);
    }
}

static class JsonRead
{
    public static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : Enumerable.Empty<JsonElement>();
}
