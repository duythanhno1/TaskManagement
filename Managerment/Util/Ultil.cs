namespace Managerment.Util
{
    public class Ultil
    {
        public static string GenerateMD5(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                // Convert to hexadecimal string
                var sb = new System.Text.StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2")); // "x2" => lowercase hex
                }

                return sb.ToString();
            }
        }       
    }
}
