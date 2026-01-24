using System;
using System.Security.Cryptography;
using System.Text;

namespace PresetLauncher.Services
{
    public static class PasswordService
    {
        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            var inputHash = HashPassword(password);
            return inputHash == hash;
        }
    }
}
