using System.Net;
using System.Text;
using Tomatotodo_Windows.Services;

static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
var json = """{"developers":[{"name":"Ren","url":"https://example.com/ren"},{"name":"Two"}],"thanks":[{"name":"Thanks"}],"sponsors":[{"name":"Sponsor","url":"https://example.com"}],"sponsor_url":"https://example.com/support"}""";
var data = AboutService.Decode(json);
Check(data.Developers.Count == 2 && data.Thanks.Count == 1 && data.Sponsors.Count == 1, "shared names and order");
Check(data.SponsorUrl == "https://example.com/support" && data.Sponsors[0].Url == "", "sponsorship link and inert sponsor portraits");
foreach (var invalid in new[] {"javascript:alert(1)","http://example.com","https://user:password@example.com","https://example.com/a b"}) Check(AboutService.SafeLink(invalid) is null, "reject unsafe link");
Check(AboutService.Decode("{\"developers\":[]}").Developers.Count == 0, "empty content");
try { AboutService.Decode("{\"developers\":[{\"name\":\" \"}]}"); throw new Exception("accepted blank name"); } catch (System.Text.Json.JsonException) { Console.WriteLine("PASS invalid name"); }
var directory = Path.Combine(Path.GetTempPath(), "tomatotodo-about-test-" + Guid.NewGuid());
try {
    using var client = new HttpClient(new Handler(json));
    var service = new AboutService(client, Path.Combine(directory,"about.json"));
    Check(await service.CachedAsync() is null,"no cache");
    await service.RefreshAsync(CancellationToken.None);
    Check((await service.CachedAsync())!.SponsorUrl == data.SponsorUrl,"offline cache round trip");
} finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
Console.WriteLine("All about tests passed.");
sealed class Handler(string json) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
        if (request.RequestUri!.AbsolutePath != "/api/about" || request.Headers.Authorization is not null) throw new Exception("wrong public endpoint");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json,Encoding.UTF8,"application/json") });
    }
}
