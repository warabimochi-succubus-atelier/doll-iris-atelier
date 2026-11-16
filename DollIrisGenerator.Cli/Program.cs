using System.Text;

namespace DollIrisGenerator.Cli
{
    internal static class Program
    {
        // The PNG / PDF output is drawn with WPF (RenderTargetBitmap for the Japanese labels), which needs an STA thread
        [STAThread]
        private static int Main(string[] args)
        {
            // Japanese messages and design names must come out right in the Windows console and in pipes, and design
            // files piped in (input "-") are UTF-8 like the files the app writes, not the console's code page
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            return CliApp.Run(args, Console.Out, Console.Error);
        }
    }
}
