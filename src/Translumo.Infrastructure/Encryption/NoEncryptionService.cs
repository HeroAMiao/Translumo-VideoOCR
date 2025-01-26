using System.IO;

namespace Translumo.Infrastructure.Encryption
{
    public class NoEncryptionService : IEncryptionService
    {
        public byte[] Encrypt(Stream toEncrypt, string password)
        {
            using var s = new MemoryStream();
            toEncrypt.CopyTo(s);
            return s.ToArray();
        }

        public string Decrypt(Stream encryptedData, string password)
        {
            StreamReader sr = new StreamReader(encryptedData);
            return sr.ReadToEnd();
        }
    }
}