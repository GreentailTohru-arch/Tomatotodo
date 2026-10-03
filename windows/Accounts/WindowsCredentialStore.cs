using System.Text.Json;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace Tomatotodo_Windows.Accounts;

/// <summary>DPAPI-protected, current Windows user only. No plaintext passwords are persisted.</summary>
public sealed class WindowsCredentialStore(string root) : ICredentialStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(root, "Security.dat");
    private sealed class Vault
    {
        public Dictionary<Guid, CredentialRecord> Credentials { get; set; } = [];
        public RememberedSession? Session { get; set; }
        public RememberedLogin? Login { get; set; }
    }

    private async Task<Vault> ReadAsync()
    {
        if (!File.Exists(_path)) return new();
        var bytes = await File.ReadAllBytesAsync(_path);
        var clear = await new DataProtectionProvider().UnprotectAsync(CryptographicBuffer.CreateFromByteArray(bytes));
        CryptographicBuffer.CopyToByteArray(clear, out var json);
        return JsonSerializer.Deserialize<Vault>(json) ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u51ED\u636E\u635F\u574F\u3002"));
    }

    private async Task WriteAsync(Vault vault)
    {
        var protectedBuffer = await new DataProtectionProvider("LOCAL=user").ProtectAsync(
            CryptographicBuffer.CreateFromByteArray(JsonSerializer.SerializeToUtf8Bytes(vault)));
        CryptographicBuffer.CopyToByteArray(protectedBuffer, out var encrypted);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await File.WriteAllBytesAsync(temp, encrypted);
        File.Move(temp, _path, true);
    }

    private async Task<T> ReadValue<T>(Func<Vault, T> get)
    { await _gate.WaitAsync(); try { return get(await ReadAsync()); } finally { _gate.Release(); } }
    private async Task Update(Action<Vault> update)
    { await _gate.WaitAsync(); try { var vault = await ReadAsync(); update(vault); await WriteAsync(vault); } finally { _gate.Release(); } }
    public Task<CredentialRecord?> GetAsync(Guid id) => ReadValue(v => v.Credentials.GetValueOrDefault(id));
    public Task SetAsync(Guid id, CredentialRecord credential) => Update(v => v.Credentials[id] = credential);
    public Task RemoveAsync(Guid id) => Update(v => v.Credentials.Remove(id));
    public Task<RememberedSession?> ReadSessionAsync() => ReadValue(v => v.Session);
    public Task SaveSessionAsync(RememberedSession? session) => Update(v => v.Session = session);
    public Task<RememberedLogin?> ReadLoginAsync() => ReadValue(v => v.Login);
    public Task SaveLoginAsync(RememberedLogin? login) => Update(v => v.Login = login);
}
