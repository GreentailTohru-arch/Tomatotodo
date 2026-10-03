using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows.Accounts;

public enum AccountKind { Local, Cloud }

public sealed record UserProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; init; } = "";
    public AccountKind Kind { get; init; }
    public string Nickname { get; init; } = "";
    public string Biography { get; init; } = "";
    public string? ActivationCode { get; init; }
    public string? CloudIdentity { get; init; }
    public bool IsCloudPlaceholder { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string DisplayLabel => $"{Nickname} · {Email} · {(Kind == AccountKind.Local ? "本地" : "云端")}";
}

public sealed record RegisterAccountRequest(string Email, string Password, string Nickname, string ActivationCode);
public sealed record UserDataSnapshot(UserProfile Profile, AppState Settings, CourseScheduleData Timetable, byte[] Avatar);
public sealed record CredentialRecord(string Salt, string Hash, int Iterations, string? SessionHash = null);
public sealed record RememberedSession(Guid UserId, AccountKind Kind, string Token, DateTimeOffset ExpiresAt);
public sealed record RememberedLogin(string Email, AccountKind Kind, string? Password = null);
public sealed record AvatarSelection(byte[] Bytes, string FileName);

public static class AccountRules
{
    public static string NormalizeEmail(string email)
    {
        email = email.Trim();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email ||
            !parsed.Host.Contains('.') || email.Any(char.IsWhiteSpace))
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u8F93\u5165\u6709\u6548\u7684\u90AE\u7BB1\u5730\u5740\uFF0C\u4F8B\u5982 name@outlook.com\u3002"));
        return email.ToLowerInvariant();
    }

    public static int PasswordScore(string value) =>
        (value.Length >= 8 ? 1 : 0) + (value.Any(char.IsUpper) ? 1 : 0) +
        (value.Any(char.IsLower) ? 1 : 0) + (value.Any(char.IsDigit) ? 1 : 0) +
        (value.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) ? 1 : 0);

    public static void ValidatePassword(string password)
    {
        if (password.Length > 128 || PasswordScore(password) != 5)
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u5BC6\u7801\u9700\u4E3A 8\u2013128 \u4F4D\uFF0C\u5305\u542B\u5927\u5199\u5B57\u6BCD\u3001\u5C0F\u5199\u5B57\u6BCD\u3001\u6570\u5B57\u548C\u7279\u6B8A\u5B57\u7B26\u3002"));
    }

    public static string ValidateNickname(string nickname)
    {
        nickname = nickname.Trim();
        if (nickname.Length is < 1 or > 24 || nickname.Any(char.IsControl))
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u6635\u79F0\u9700\u4E3A 1\u201324 \u4E2A\u5B57\u7B26\u3002"));
        return nickname;
    }

    public static string FormatActivationCode(string text)
    {
        var clean = new string(text.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').Take(25).ToArray());
        return string.Join('-', Enumerable.Range(0, (clean.Length + 4) / 5)
            .Select(i => clean.Substring(i * 5, Math.Min(5, clean.Length - i * 5))));
    }

    public static void ValidateActivationCode(string code)
    {
        if (!Regex.IsMatch(code, "^[A-Z0-9]{5}(-[A-Z0-9]{5}){4}$", RegexOptions.CultureInvariant))
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u6FC0\u6D3B\u7801\u9700\u5305\u542B 25 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57\uFF0C\u683C\u5F0F\u4E3A XXXXX-XXXXX-XXXXX-XXXXX-XXXXX\u3002"));
    }
}

public static class PasswordSecurity
{
    public const int Iterations = 600_000;
    public static CredentialRecord Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return new(Convert.ToBase64String(salt), Convert.ToBase64String(
            Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32)), Iterations);
    }

    public static bool Verify(string password, CredentialRecord record)
    {
        if (record.Iterations is < 100_000 or > 2_000_000 || password.Length > 128) return false;
        try
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(record.Salt), record.Iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(record.Hash));
        }
        catch (FormatException) { return false; }
    }

    public static string TokenHash(string token) => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
