using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("Tomatotodo Setup 1.5")]
[assembly: AssemblyProduct("Tomatotodo")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]

internal static class WinUISetup
{
    private const string PackageName = "Tomatotodo.Windows_1.5.0.0_x64.msix";
    private const string RuntimeName = "Microsoft.WindowsAppRuntime.2.msix";
    private const string CertificateName = "Tomatotodo.Windows_1.5.0.0_x64.cer";
    private const string CertificateThumbprint = "4173C99E34DD286AA0BC9A0EC7DD9A1139C5BDD5";
    private const string PackageHash = "E3750D75F6817CA78B333BE69427BB7F27702FD4888BA6C6AEE0AA6711B58491";
    private const string RuntimeHash = "C1F5C71CF3A87CFB824C6257F4C1EE8787833B84FCC7DBD18C129786574F11B9";
    private const string CertificateHash = "9788A853381F8C8EC15ACB85E263437321808041D88DBF21CD0A12295AB3B97B";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        if (args.Length == 2 && args[0] == "--trust-cert") return TrustCertificate(args[1]);
        if (MessageBox.Show("安装 Tomatotodo 1.5（WinUI 3，x64）？\n\n升级安装会保留应用账户与本地数据。",
            "Tomatotodo 安装程序", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;

        string work = Path.Combine(Path.GetTempPath(), "Tomatotodo-Setup-1.5-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            string zipPath = Path.Combine(work, "packages.zip");
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("TomatotodoPackages.zip"))
            {
                if (source == null) throw new InvalidDataException("安装包缺少应用资源。");
                using (FileStream target = File.Create(zipPath)) source.CopyTo(target);
            }
            ZipFile.ExtractToDirectory(zipPath, work);
            string app = Path.Combine(work, PackageName);
            string runtime = Path.Combine(work, RuntimeName);
            string certificatePath = Path.Combine(work, CertificateName);
            VerifySha256(app, PackageHash);
            VerifySha256(runtime, RuntimeHash);
            VerifySha256(certificatePath, CertificateHash);
            var certificate = new X509Certificate2(certificatePath);
            if (!string.Equals(certificate.Thumbprint, CertificateThumbprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("签名证书与预期不符。");

            using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                bool trusted = store.Certificates.Find(X509FindType.FindByThumbprint, CertificateThumbprint, false).Count > 0;
                if (!trusted)
                {
                    string warning = "此应用使用自签名证书。Windows 安装前需要将以下证书加入本地计算机的“受信任人”证书存储区，系统将要求管理员确认：\n\n" +
                        "颁发给：" + certificate.Subject + "\n" +
                        "SHA-1 指纹：" + CertificateThumbprint + "\n\n" +
                        "这会让此设备上的用户信任同一证书签发的应用。请仅在确认安装包来自可信来源时继续。\n\n是否信任此证书并继续安装？";
                    if (MessageBox.Show(warning, "确认信任签名证书", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return 0;
                    var elevate = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                        "--trust-cert \"" + certificatePath + "\"")
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    using (Process helper = Process.Start(elevate))
                    {
                        helper.WaitForExit();
                        if (helper.ExitCode != 0) throw new InvalidOperationException("未能信任签名证书，安装已取消。");
                    }
                }
            }

            string script = Path.Combine(work, "install.ps1");
            File.WriteAllText(script,
                "$ErrorActionPreference = 'Stop'\r\n" +
                "try { Add-AppxPackage -Path $args[0] -DependencyPath $args[1] -ForceApplicationShutdown; exit 0 } " +
                "catch { Write-Error $_.Exception.Message; exit 1 }\r\n", new UTF8Encoding(false));
            var start = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script +
                "\" \"" + app + "\" \"" + runtime + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using (Process process = Process.Start(start))
            {
                string error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(
                    "Windows 无法安装应用。请确认签名证书已受信任。\n\n" + error.Trim());
            }
            MessageBox.Show("Tomatotodo 1.5 安装完成。可从开始菜单启动。", "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch { }
        }
    }

    private static int TrustCertificate(string path)
    {
        try
        {
            VerifySha256(path, CertificateHash);
            var certificate = new X509Certificate2(path);
            if (!string.Equals(certificate.Thumbprint, CertificateThumbprint, StringComparison.OrdinalIgnoreCase)) return 1;
            using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
            }
            return 0;
        }
        catch { return 1; }
    }

    private static void VerifySha256(string path, string expected)
    {
        if (!File.Exists(path)) throw new InvalidDataException("安装包缺少文件：" + Path.GetFileName(path));
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
        {
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包文件校验失败：" + Path.GetFileName(path));
        }
    }
}
