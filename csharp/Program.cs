using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("HIRELAYER_API_KEY")
    ?? throw new InvalidOperationException("HIRELAYER_API_KEY is not set");

using var http = new HttpClient
{
    BaseAddress = new Uri("https://hirelayer.co/"),
    Timeout = TimeSpan.FromSeconds(150), // the default 100 s can cut a slow parse short
};
http.DefaultRequestHeaders.Add("X-API-Key", apiKey);

using var form = new MultipartFormDataContent();

var file = new ByteArrayContent(await File.ReadAllBytesAsync("resume.pdf"));
file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
// Quoted name and filename, and no filename*: see the next section.
file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
{
    Name = "\"file\"",
    FileName = "\"resume.pdf\"",
};
form.Add(file);
form.Add(FormField("application_id", "candidate-123"));
form.Add(FormField("do_not_store_data", "true"));

using var response = await http.PostAsync("api/v3/parser", form);
var json = await response.Content.ReadFromJsonAsync<JsonElement>();

if (!response.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"HTTP {(int)response.StatusCode}: {json.GetProperty("error")}");
    return 1;
}

Console.WriteLine(json.GetProperty("info_candidate").GetProperty("full_name"));
Console.WriteLine($"{json.GetProperty("work_experiences").GetArrayLength()} positions");
return 0;

static StringContent FormField(string name, string value) => new(value)
{
    Headers = { ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{name}\"" } },
};
