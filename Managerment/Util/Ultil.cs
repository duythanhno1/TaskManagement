namespace Managerment.Util
{
    public class Ultil
    {
        // BCrypt — secure password hashing
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public static bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        // Legacy — keep for backward compatibility
        public static string GenerateMD5(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                var sb = new System.Text.StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }       
    }
}
