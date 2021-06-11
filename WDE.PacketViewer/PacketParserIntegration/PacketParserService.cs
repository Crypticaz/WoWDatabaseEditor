using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WDE.Module.Attributes;
using WoWPacketParser.Proto;

namespace WDE.PacketViewer.PacketParserIntegration
{
    [AutoRegister]
    [SingleInstance]
    public class PacketParserService : IPacketParserService
    {
        private readonly IPacketParserLocator locator;

        public PacketParserService(IPacketParserLocator locator)
        {
            this.locator = locator;
        }
        
        public Task RunParser(string input, DumpFormatType dumpType, IProgress<float> progress)
        {
            var path = locator.GetPacketParserPath();
            if (path == null)
                throw new ParserNotFoundException();

            var args = $"--DumpFormat {(int) dumpType} \"{input}\"";

            var executable = path;
            if (Path.GetExtension(executable) == ".dll")
            {
                executable = "dotnet";
                args = path + " " + args;
            }
            
            var processStartInfo = new ProcessStartInfo(executable, args);
            processStartInfo.WorkingDirectory = Path.GetDirectoryName(path)!;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.CreateNoWindow = true;

            Process process = new Process();
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (sender, data) =>
            {
                if (float.TryParse(data.Data, out var p))
                    progress.Report(p);
            };
            
            if (!process.Start())
                throw new CouldNotRunParserException();
            
            process.BeginOutputReadLine();
            return process.WaitForExitAsync();
        }
    }

    public class ParserNotFoundException : Exception
    {
        public ParserNotFoundException() : base("Couldn't find parser!")
        {
        }
    }

    public class CouldNotRunParserException : Exception
    {
        
    }
}