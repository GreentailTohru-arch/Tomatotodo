using Tomatotodo_Windows.Accounts;
using Tomatotodo_Windows.Data;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

if (args.Length == 2 && args[0] == "--lease-probe")
{
    using var probe = new DeviceAccountLease(args[1]);
    try { probe.Acquire(); Environment.ExitCode = 0; }
    catch (InvalidOperationException) { Environment.ExitCode = 23; }
    return;
}

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
var cropPixels = new byte[4 * 2 * 4];
for (var p = 0; p < 8; p++) { cropPixels[p * 4] = (byte)(p * 20); cropPixels[p * 4 + 3] = 255; }
var cropTest = new AvatarCrop(cropPixels, 4, 2);
var centered = cropTest.Export(2);
Check(centered[0] == 20 && centered[4] == 40 && centered[8] == 100 && centered[12] == 120, "crop centered export uses expected source pixels");
cropTest.Move(100, -100);
Check(cropTest.Left == 0 && cropTest.Top == 0, "crop clamps horizontal and vertical pan");
cropTest.SetZoom(2);
cropTest.Move(-100, 100);
Check(cropTest.Left == 3 && cropTest.Top == 0 && cropTest.Side == 1, "zoomed crop reaches source edge without blank pixels");
cropTest.Center();
var beforeRotate = cropTest.Export(2);
cropTest.Rotate();
var rotatedCrop = cropTest.Export(2);
Check(cropTest.Width == 2 && cropTest.Height == 4 && rotatedCrop[0] == beforeRotate[8] && rotatedCrop[4] == beforeRotate[0], "clockwise rotation matches exported crop");
for (var turn = 0; turn < 3; turn++) cropTest.Rotate();
Check(cropTest.Pixels.SequenceEqual(cropPixels) && cropTest.Export(2).SequenceEqual(beforeRotate), "four rotations restore exact original pixels");
foreach (var dimensions in new[] { (7, 29), (29, 7), (13, 13) })
{
    var cropBounds = new AvatarCrop(new byte[dimensions.Item1 * dimensions.Item2 * 4], dimensions.Item1, dimensions.Item2);
    for (var turn = 0; turn < 4; turn++)
    {
        cropBounds.SetZoom(4); cropBounds.Move(10000, -10000); cropBounds.Rotate(); cropBounds.SetZoom(1);
        Check(cropBounds.Left >= 0 && cropBounds.Top >= 0 && cropBounds.Left + cropBounds.Side <= cropBounds.Width && cropBounds.Top + cropBounds.Side <= cropBounds.Height,
            "rotated portrait/landscape/square crop stays inside source when zooming out");
    }
}
async Task Reject(Func<Task> action, string name)
{ try { await action(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { checks++; return; } throw new Exception(name); }
var root = Path.Combine(Path.GetTempPath(), "Tomatotodo-AccountTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var images = new WindowsImageProcessingService();
    var storage = new UserDataStorageService(root);
    var vault = new WindowsCredentialStore(root);
    var local = new LocalAccountService(storage, vault, images);
    var cloud = new MockCloudAccountService(new LocalAccountService(storage, vault, images, AccountKind.Cloud));
    var password = "OnlyTest!2026";
    var profile = await local.RegisterAsync(new("  TEST@example.com ", password, "测试账户", ""));
    Check(profile.Email == "test@example.com", "normalized email");
    Check((await local.LoginAsync("test@example.com", password)).Id == profile.Id, "local login");
    await Reject(() => local.LoginAsync("test@example.com", "wrong"), "wrong password accepted");
    await Reject(() => local.RegisterAsync(new("test@example.com", password, "重复", "")), "duplicate accepted");
    await Reject(() => local.RegisterAsync(new("bad", password, "坏邮箱", "")), "invalid email accepted");
    await Reject(() => local.RegisterAsync(new("weak@example.com", "abcdefgh", "弱密码", "")), "weak password accepted");
    await Reject(() => cloud.RegisterAsync(new("test@example.com", password, "云端", "INVALID")), "invalid activation accepted");
    var activation = AccountRules.FormatActivationCode("abcde fghij klmno pqrst uvwxy");
    Check(activation == "ABCDE-FGHIJ-KLMNO-PQRST-UVWXY", "activation formatting");
    var remote = await cloud.RegisterAsync(new("test@example.com", password, "云端测试", activation));
    Check(remote.Id != profile.Id && remote.IsCloudPlaceholder && remote.CloudIdentity!.StartsWith("mock-"), "cloud identity");
    Check((await cloud.LoginAsync("test@example.com", password)).Id == remote.Id, "cloud login");
    await Reject(() => cloud.RegisterAsync(new("other@example.com", password, "重复激活码", activation)), "activation reuse accepted");
    Check(storage.ListProfiles().Count == 2, "reused code must not create partial account");

    var files = Directory.GetFiles(Path.GetDirectoryName(storage.GetAvatarPath(profile.Id))!).Select(Path.GetFileName).Order().ToArray();
    Check(files.SequenceEqual(new[] { "AppSettings.json", "avatar.png", "Timetable.json", "UserProfile.json" }.Order()), "four-file contract");
    var settingsJson = File.ReadAllText(Path.Combine(Path.GetDirectoryName(storage.GetAvatarPath(profile.Id))!, "AppSettings.json"));
    Check(!settingsJson.Contains("CourseSchedule\""), "timetable duplicated in settings");
    Check(!File.ReadAllText(Path.Combine(Path.GetDirectoryName(storage.GetAvatarPath(profile.Id))!, "UserProfile.json")).Contains(password), "password leaked to profile");
    Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "Security.dat"))).Contains("Credentials"), "vault not protected");
    var credential = await vault.GetAsync(profile.Id);
    Check(credential is not null && PasswordSecurity.Verify(password, credential), "DPAPI credential round trip");
    var secondHash = PasswordSecurity.Hash(password);
    Check(secondHash.Hash != credential!.Hash && secondHash.Salt != credential.Salt, "salt uniqueness");

    var edited = storage.LoadAppState(profile.Id);
    edited.FocusMinutes = 47; edited.Theme = "light"; edited.FocusLogs.Add(new() { Seconds = 123 });
    storage.SaveAppState(profile.Id, edited);
    Check(storage.LoadAppState(profile.Id).FocusMinutes == 47 && storage.LoadAppState(remote.Id).FocusMinutes == 25, "account isolation");
    await local.UpdateProfileAsync(profile.Id, "新昵称", "两行\n简介");
    await Reject(() => local.UpdateProfileAsync(profile.Id, new string('名', 25), ""), "nickname limit");
    await Reject(() => local.UpdateProfileAsync(profile.Id, "昵称", new string('字', 301)), "bio limit");

    var workspace = new TestWorkspace();
    var leaseName = @"Local\Tomatotodo.Test." + Guid.NewGuid().ToString("N");
    using var session = new AccountSession(vault, storage, workspace, new DeviceAccountLease(leaseName));
    await session.SignInAsync(storage.Read(profile.Id).Profile, true, true, password);
    Check((await new WindowsCredentialStore(root).ReadLoginAsync())?.Password == password, "remembered password survives encrypted vault reload");
    Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "Security.dat"))).Contains(password), "remembered password is not plaintext on disk");
    var savedSession = await vault.ReadSessionAsync();
    Check(savedSession is not null && savedSession.Token != password && workspace.Active == profile.Id, "secure auto token");
    using var contender = new AccountSession(vault, storage, new TestWorkspace(), new DeviceAccountLease(leaseName));
    await Reject(() => contender.SignInAsync(remote, false, false), "parallel account login allowed");
    await Reject(() => session.SignInAsync(remote, false, false), "account switched without logout");
    var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add("--lease-probe"); start.ArgumentList.Add(leaseName);
    using (var child = System.Diagnostics.Process.Start(start)!)
    { await child.WaitForExitAsync(); Check(child.ExitCode == 23, "cross-process device lock"); }
    session.Dispose(); // Simulate application exit without revoking remembered login.
    using var restored = new AccountSession(vault, storage, workspace, new DeviceAccountLease(leaseName));
    await restored.RestoreAsync(); Check(restored.Current?.Id == profile.Id, "auto login");
    await restored.SignOutAsync();
    using var signedOut = new AccountSession(vault, storage, workspace, new DeviceAccountLease(leaseName));
    await signedOut.RestoreAsync(); Check(signedOut.Current is null && workspace.Active is null, "logout revokes automatic login");
    Check((await vault.GetAsync(profile.Id))?.SessionHash is null, "server-side token invalidation");
    Check((await vault.ReadLoginAsync())?.Email == profile.Email, "remember email survives logout");
    await session.SignInAsync(storage.Read(profile.Id).Profile, false, false);
    Check(await vault.ReadLoginAsync() is null && await vault.ReadSessionAsync() is null, "unchecked persistence disabled");
    await session.SignInAsync(storage.Read(profile.Id).Profile, true, true);
    await vault.SaveSessionAsync((await vault.ReadSessionAsync())! with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) });
    using var expired = new AccountSession(vault, storage, workspace, new DeviceAccountLease(leaseName)); await expired.RestoreAsync();
    Check(expired.Current is null, "expired automatic login rejected");

    var interactions = new TestInteractions();
    var migration = new MockAccountMigrationService(storage);
    using var vm = new AccountViewModel(kind => kind == AccountKind.Local ? local : cloud, storage, images, migration, vault, interactions, workspace, session);
    Check(vm.IsViewingProfile && !vm.IsEditingProfile, "profile defaults to read-only");
    vm.EditProfileCommand.Execute(null); vm.Nickname = "未保存修改";
    Check(vm.IsEditingProfile, "edit profile command");
    vm.CancelEditCommand.Execute(null);
    Check(vm.IsViewingProfile && vm.Nickname == "新昵称", "cancel restores profile");
    vm.IsRegistration = true; vm.Password = password; vm.ConfirmPassword = "different";
    Check(vm.PasswordMismatch, "inline mismatch error");
    vm.ConfirmPassword = password; Check(!vm.PasswordMismatch, "matching passwords clear error");
    vm.IsRegistration = false;
    vm.SelectedTarget = vm.MigrationTargets.Single(); vm.TargetPassword = password;
    await vm.MigrateCommand.ExecuteAsync(null);
    Check(interactions.ConfirmCount == 1 && storage.LoadAppState(remote.Id).FocusMinutes == 25, "cancel leaves target untouched");
    interactions.Accept = true; vm.TargetPassword = "wrong";
    await vm.MigrateCommand.ExecuteAsync(null);
    Check(interactions.ConfirmCount == 1 && storage.LoadAppState(remote.Id).FocusMinutes == 25, "target authentication gates overwrite");
    vm.TargetPassword = password; await vm.MigrateCommand.ExecuteAsync(null);
    var migrated = storage.Read(remote.Id);
    Check(migrated.Settings.FocusMinutes == 47 && migrated.Settings.FocusLogs.Single().Seconds == 123, "migration copies settings and archive");
    Check(migrated.Profile.Nickname == "新昵称" && migrated.Profile.Id == remote.Id && migrated.Profile.Kind == AccountKind.Cloud && migrated.Profile.ActivationCode == activation, "migration preserves target identity");
    Check((await cloud.LoginAsync(remote.Email, password)).Id == remote.Id, "migration preserves credentials");
    var targetSettings = storage.LoadAppState(remote.Id); targetSettings.FocusMinutes = 38; storage.SaveAppState(remote.Id, targetSettings);
    await migration.MigrateAsync(remote.Id, profile.Id);
    Check(storage.LoadAppState(profile.Id).FocusMinutes == 38, "cloud to local migration");
    await Reject(() => migration.MigrateAsync(profile.Id, profile.Id), "self overwrite rejected");

    var defaultAvatar = await images.CreateDefaultAvatarAsync();
    using (var source = new MemoryStream(defaultAvatar))
    using (var stream = source.AsRandomAccessStream())
    { var decoder = await BitmapDecoder.CreateAsync(stream); Check(decoder.PixelWidth == 256 && decoder.PixelHeight == 256, "default avatar size"); }
    // Center stripe remains after landscape center crop; side stripes must disappear.
    var pixels = new byte[512 * 256 * 4];
    for (var y = 0; y < 256; y++) for (var x = 0; x < 512; x++)
    { var i = (y * 512 + x) * 4; pixels[i + 1] = (byte)(x >= 128 && x < 384 ? 255 : 0); pixels[i + 2] = (byte)(x < 128 || x >= 384 ? 255 : 0); pixels[i + 3] = 255; }
    using (var encoded = new InMemoryRandomAccessStream())
    {
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, encoded);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, 512, 256, 96, 96, pixels); await encoder.FlushAsync(); encoded.Seek(0);
        using var reader = new DataReader(encoded); await reader.LoadAsync((uint)encoded.Size);
        var bytes = new byte[(int)encoded.Size]; reader.ReadBytes(bytes);
        var cropped = await images.ProcessAvatarAsync(new(bytes, "test.png"));
        using var output = new MemoryStream(cropped); using var stream = output.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var result = (await decoder.GetPixelDataAsync()).DetachPixelData();
        Check(decoder.PixelWidth == 256 && decoder.PixelHeight == 256 && result[1] == 255 && result[2] == 0, "center crop pixels");
        storage.SaveAvatar(profile.Id, cropped);
        Check(storage.Read(profile.Id).Avatar.SequenceEqual(cropped), "avatar persistence");
    }
    await Reject(() => images.ProcessAvatarAsync(new(defaultAvatar, "bad.gif")), "avatar format allowlist");
    // Verify the real cloud adapter's snake_case API parsing and PascalCase document payload.
    var serverId = Guid.NewGuid();
    string? pushed = null;
    using var http = new HttpClient(new StubHandler((request, body) =>
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/api/auth/login")
            return System.Net.HttpStatusCode.OK.Json(System.Text.Json.JsonSerializer.Serialize(new
            { access_token = "test-token", expires_in = 3600, user = new { id = serverId, email = "cloud@example.com" } }));
        if (path == "/api/sync/pull")
            return System.Net.HttpStatusCode.OK.Json(System.Text.Json.JsonSerializer.Serialize(new
            { user_profile = new { Id = serverId, Email = "cloud@example.com", Kind = "Cloud", Nickname = "云端", Biography = "" },
              app_settings = new { FocusMinutes = 31 }, timetable = new { }, version = 1 }));
        if (path == "/api/sync/push")
        {
            pushed = body;
            return System.Net.HttpStatusCode.OK.Json(System.Text.Json.JsonSerializer.Serialize(new
            { user_profile = new { Id = serverId, Email = "cloud@example.com", Kind = "Cloud", Nickname = "云端", Biography = "" },
              app_settings = new { FocusMinutes = 31 }, timetable = new { }, version = 2 }));
        }
        throw new Exception("Unexpected API path: " + path);
    })) { BaseAddress = new Uri("https://api.example.test/") };
    var realCloud = new CloudAccountService(storage, vault, images, root, http);
    var cloudProfile = await realCloud.LoginAsync("cloud@example.com", password);
    Check(cloudProfile.Id == serverId && storage.LoadAppState(serverId).FocusMinutes == 31, "cloud login and pull");
    await realCloud.PushAsync(serverId);
    Check(pushed is not null && pushed.Contains("\"base_version\":1") && pushed.Contains("\"FocusMinutes\":31") &&
          !pushed.Contains("LocalMusicFolderToken") && !pushed.Contains("CourseSchedule\""), "cloud JSONB payload");
    var unsynced = storage.LoadAppState(serverId); unsynced.FocusMinutes = 44;
    storage.SaveAppState(serverId, unsynced);
    await realCloud.LoginAsync("cloud@example.com", password);
    Check(storage.LoadAppState(serverId).FocusMinutes == 44, "cloud login preserves unuploaded local changes");
    Console.WriteLine($"Accounts: {checks} checks passed (DPAPI, authentication, sessions, isolation, migration, MVVM cancellation, 256px image processing).");
}
finally { Directory.Delete(root, true); }

sealed class TestWorkspace : IAccountWorkspace
{
    public Guid? Active { get; private set; }
    public void SwitchAccount(Guid? id) => Active = id;
    public void FlushCurrentAccount() { }
}
sealed class TestInteractions : IAccountInteractionService
{
    public bool Accept { get; set; }
    public int ConfirmCount { get; private set; }
    public Task<AvatarSelection?> PickAvatarAsync() => Task.FromResult<AvatarSelection?>(null);
    public Task<bool> ConfirmMigrationAsync(UserProfile source, UserProfile target)
    { ConfirmCount++; return Task.FromResult(Accept); }
}
sealed class StubHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return handler(request, body);
    }
}
static class HttpTestResponses
{
    public static HttpResponseMessage Json(this System.Net.HttpStatusCode status, string body) => new(status)
    { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
