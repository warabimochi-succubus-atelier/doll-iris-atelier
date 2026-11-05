using System.IO;
using System.Text;

namespace DollIrisGenerator.Services
{
    public static class AtomicFileWriter
    {
        /// <summary>
        /// Writes through a temporary file and then replaces the target, so a crash or power loss
        /// during the write can't leave a truncated file behind.
        /// </summary>
        /// <exception cref="Exception">Any write failure; the caller reports it to the user.</exception>
        public static void WriteAllText(string path, string text)
        {
            string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
