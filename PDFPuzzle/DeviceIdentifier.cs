using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace PDFPuzzle
{
    /// <summary>
    /// 端末識別子の生成（チーム版 v0 §3.1）。
    /// MachineGuid（マシン単位）+ Windows アカウント SID（ユーザー単位）を連結し
    /// SHA-256 ハッシュ化、先頭16文字を短縮ID として返す。
    /// 同一マシン・同一ユーザーで決定的（複数回呼んで同値）。
    /// レジストリ読取・SID 取得は失敗しても例外を投げず "unknown" を代入する。
    /// </summary>
    public static class DeviceIdentifier
    {
        /// <summary>
        /// 現在の端末の短縮ID（小文字 hex 16文字）を返す。
        /// </summary>
        public static string GetCurrent()
        {
            var machineGuid = ReadMachineGuid();
            var sid = ReadCurrentUserSid();
            var raw = $"{machineGuid}|{sid}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }

        private static string ReadMachineGuid()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static string ReadCurrentUserSid()
        {
            try
            {
                return WindowsIdentity.GetCurrent().User?.Value ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
