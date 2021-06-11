using System;
using WoWPacketParser.Proto;

namespace WDE.PacketViewer.Processing
{
    public abstract class PacketProcessor<T>
    {
        public T? Process(PacketHolder packet)
        {
            switch (packet.KindCase)
            {
                case PacketHolder.KindOneofCase.Chat:
                    return Process(packet.BaseData, packet.Chat);
                case PacketHolder.KindOneofCase.QueryCreatureResponse:
                    return Process(packet.BaseData, packet.QueryCreatureResponse);
                case PacketHolder.KindOneofCase.None:
                    return default;
                case PacketHolder.KindOneofCase.Emote:
                    return Process(packet.BaseData, packet.Emote);
                case PacketHolder.KindOneofCase.PlaySound:
                    return Process(packet.BaseData, packet.PlaySound);
                case PacketHolder.KindOneofCase.PlayMusic:
                    return Process(packet.BaseData, packet.PlayMusic);
                case PacketHolder.KindOneofCase.PlayObjectSound:
                    return Process(packet.BaseData, packet.PlayObjectSound);
                case PacketHolder.KindOneofCase.GossipHello:
                    return Process(packet.BaseData, packet.GossipHello);
                case PacketHolder.KindOneofCase.GossipMessage:
                    return Process(packet.BaseData, packet.GossipMessage);
                case PacketHolder.KindOneofCase.GossipSelect:
                    return Process(packet.BaseData, packet.GossipSelect);
                case PacketHolder.KindOneofCase.GossipClose:
                    return Process(packet.BaseData, packet.GossipClose);
                case PacketHolder.KindOneofCase.PacketSpellStart:
                    return Process(packet.BaseData, packet.PacketSpellStart);
                case PacketHolder.KindOneofCase.PacketSpellGo:
                    return Process(packet.BaseData, packet.PacketSpellGo);
                case PacketHolder.KindOneofCase.PacketAuraUpdate:
                    return Process(packet.BaseData, packet.PacketAuraUpdate);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual T? Process(PacketBase packetBaseData, PacketAuraUpdate packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketSpellGo packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketSpellStart packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketGossipClose packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketGossipSelect packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketGossipMessage packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketGossipHello packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketPlayObjectSound packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketPlayMusic packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketPlaySound packet) => default;
        protected virtual T? Process(PacketBase packetBaseData, PacketEmote packet) => default;
        protected virtual T? Process(PacketBase basePacket, PacketChat packet) => default;
        protected virtual T? Process(PacketBase basePacket, PacketQueryCreatureResponse packet) => default;
    }
}