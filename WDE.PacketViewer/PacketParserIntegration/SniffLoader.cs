using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WDE.Common.Services.MessageBox;
using WDE.Module.Attributes;
using WoWPacketParser.Proto;

namespace WDE.PacketViewer.PacketParserIntegration
{
    [AutoRegister]
    [SingleInstance]
    public class SniffLoader : ISniffLoader
    {
        private readonly IPacketParserService packetParserService;
        private readonly IMessageBoxService messageBoxService;

        public SniffLoader(IPacketParserService packetParserService,
            IMessageBoxService messageBoxService)
        {
            this.packetParserService = packetParserService;
            this.messageBoxService = messageBoxService;
        }
        
        public async Task<Packets> LoadSniff(string path, IProgress<float> progress)
        {
            Packets packets = null!;
            var extension = Path.GetExtension(path);

            if (extension != ".pkt" && extension != ".dat")
            {
                extension = await messageBoxService.ShowDialog(new MessageBoxFactory<string>()
                    .SetTitle("Unknown file type")
                    .SetMainInstruction("File doesn't have .pkt nor .dat extension")
                    .SetContent("Therefore, cannot determine whether to open the file as parsed sniff or unparsed sniff")
                    .WithButton("Open as parsed sniff", ".dat")
                    .WithButton("Open as raw sniff", ".pkt")
                    .Build());
            }
            
            if (extension == ".pkt")
            {
                progress.Report(-1);
                var parsedPath = Path.ChangeExtension(path, null) + "_parsed.dat";
                bool runParser = true;
                
                // if sniff below 10MB - do not bother loading parsed version, reparse will be very quick
                if (File.Exists(parsedPath) && new FileInfo(path).Length >= 10_000_000)
                {
                    runParser = await messageBoxService.ShowDialog(new MessageBoxFactory<bool>()
                        .SetTitle("Found parsed sniff")
                        .SetMainInstruction("Found parsed sniff")
                        .SetContent(
                            "It looks like you have already parsed the sniff, WoW Database Editor can use existing parser sniff for quicker load.\n\nHowever, if it was parsed long time ago in an old WDE version, it may not load correctly. In case of any bugs, try to reparse the sniff.")
                        .WithButton("Re-parse the sniff again", true)
                        .WithButton("Use parsed sniff", false)
                        .Build());
                }
                
                if (runParser)
                    await packetParserService.RunParser(path, DumpFormatType.UniversalProtoWithText, progress);
                path = parsedPath;
            }

            progress.Report(-1);
            await Task.Run(async () =>
            {
                await using var input = File.OpenRead(path);
                packets = Packets.Parser.ParseFrom(input);
            }).ConfigureAwait(true);
            progress.Report(1);

            return packets;
        }
    }
}