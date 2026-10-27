using System.IO;
using System.Text;
using System.Text.Json;

namespace PanBridge;

public static class SelfTest
{
    public static void Run()
    {
        string fake = new('a', 128);
        var cookie = new SavedCookie("USER_TOKEN", fake, "pan.sjtu.edu.cn", "/", true, true, false, DateTime.UtcNow.AddHours(1), 0);
        if (SessionRules.SelectToken([cookie])?.Value != fake) throw new Exception("Current cookie name not recognized.");
        if (SessionRules.SelectToken([cookie with { Name = "UserToken" }]) is null) throw new Exception("Legacy cookie not recognized.");
        if (SessionRules.SelectToken([cookie with { Expires = DateTime.UtcNow.AddDays(-1) }]) is not null) throw new Exception("Expired cookie accepted.");
        if (SessionRules.SelectToken([cookie with { Domain = "example.com" }]) is not null) throw new Exception("Untrusted cookie accepted.");
        string path = Path.Combine(AppContext.BaseDirectory, "self-test-session.dpapi");
        try
        {
            SessionVault.Save(new AuthSession(fake, DateTimeOffset.UtcNow, [cookie]), path);
            if (Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(fake)) throw new Exception("Session was stored as plaintext.");
            if (SessionVault.Load(path)?.UserToken != fake) throw new Exception("DPAPI roundtrip failed.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:multistatus xmlns:D=\"DAV:\"><D:response><D:propstat><D:prop><D:resourcetype><D:collection/></D:resourcetype></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response><D:response><D:href>/example</D:href></D:response></D:multistatus>";
        if (TboxBackend.ParseListing(Encoding.UTF8.GetBytes(xml)) != 1) throw new Exception("Directory response parsing failed.");
        bool rejected = false;
        try { TboxBackend.ParseListing(Encoding.UTF8.GetBytes("<html/>")); } catch (CloudAccessException) { rejected = true; }
        if (!rejected) throw new Exception("Invalid listing accepted.");
        if (JsonSerializer.Deserialize<DriveSettings>("{\"DriveLetter\":\"J:\",\"AutoMount\":false}")?.Access != DriveAccess.Full) throw new Exception("Upgrade default access is incorrect.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.json"), JsonSerializer.Serialize(new { Success = true, Checks = new[] { "cookie_names", "cookie_expiry", "cookie_domain", "dpapi_roundtrip", "no_plaintext_token", "webdav_xml", "invalid_xml_rejected" } }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
