using System.Text;
using DollIrisGenerator.Cli;

// Japanese messages and design names must come out right in the Windows console and in pipes
Console.OutputEncoding = new UTF8Encoding(false);
return CliApp.Run(args, Console.Out, Console.Error);
