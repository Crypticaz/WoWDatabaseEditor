using WoWPacketParser.Proto;

namespace WDE.PacketViewer.Processing.Processors
{
    public class EntryExtractorProcessor : PacketProcessor<uint>
    {
        protected override uint Process(PacketBase packetBaseData, PacketGossipSelect packet)
        {
            return packet.GossipUnit.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketGossipMessage packet)
        {
            return packet.GossipSource.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketGossipHello packet)
        {
            return packet.GossipSource.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketPlayObjectSound packet)
        {
            return packet.Source?.Entry ?? 0;
        }

        protected override uint Process(PacketBase packetBaseData, PacketPlaySound packet)
        {
            return packet.Source?.Entry ?? 0;
        }

        protected override uint Process(PacketBase packetBaseData, PacketEmote packet)
        {
            return packet.Sender.Entry;
        }

        protected override uint Process(PacketBase basePacket, PacketChat packet)
        {
            return packet.Sender.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketSpellGo packet)
        {
            return packet.Data.Caster.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketSpellStart packet)
        {
            return packet.Data.Caster.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketGossipClose packet)
        {
            return packet.GossipSource.Entry;
        }

        protected override uint Process(PacketBase packetBaseData, PacketAuraUpdate packet)
        {
            return packet.Unit.Entry;
        }
    }
}