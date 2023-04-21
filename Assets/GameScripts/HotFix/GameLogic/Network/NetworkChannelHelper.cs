﻿using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using TEngine;

namespace GameLogic
{
    public class NetworkChannelHelper : INetworkChannelHelper, IMemory
    {
        private readonly Dictionary<int, Type> _serverToClientPacketTypes = new Dictionary<int, Type>();
        private readonly MemoryStream _cachedStream = new MemoryStream(1024 * 8);
        private INetworkChannel _networkChannel = null;

        /// <summary>
        /// 获取消息包头长度。
        /// <remarks>4。</remarks>
        /// </summary>
        public int PacketHeaderLength => sizeof(int);

        /// <summary>
        /// 初始化网络频道辅助器。
        /// </summary>
        /// <param name="networkChannel">网络频道。</param>
        public void Initialize(INetworkChannel networkChannel)
        {
            _networkChannel = networkChannel;

            // 反射注册包和包处理函数。
            Type packetBaseType = typeof(ProtoPacket);
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                if (!types[i].IsClass || types[i].IsAbstract)
                {
                    continue;
                }

                if (types[i].BaseType == packetBaseType)
                {
                    PacketBase packetBase = (PacketBase)Activator.CreateInstance(types[i]);
                    Type packetType = GetServerToClientPacketType(packetBase.Id);
                    if (packetType != null)
                    {
                        Log.Warning("Already exist packet type '{0}', check '{1}' or '{2}'?.", packetBase.Id.ToString(), packetType.Name, packetBase.GetType().Name);
                        continue;
                    }

                    _serverToClientPacketTypes.Add(packetBase.Id, types[i]);
                }
            }

            GameEvent.AddEventListener<INetworkChannel, object>(NetworkEvent.NetworkConnectedEvent, OnNetworkConnected);
            GameEvent.AddEventListener<INetworkChannel>(NetworkEvent.NetworkClosedEvent, OnNetworkClosed);
            GameEvent.AddEventListener<INetworkChannel, int>(NetworkEvent.NetworkMissHeartBeatEvent, OnNetworkMissHeartBeat);
            GameEvent.AddEventListener<INetworkChannel, NetworkErrorCode, SocketError, string>(NetworkEvent.NetworkErrorEvent, OnNetworkError);
            GameEvent.AddEventListener<INetworkChannel, object>(NetworkEvent.NetworkCustomErrorEvent, OnNetworkCustomError);
        }

        /// <summary>
        /// 关闭并清理网络频道辅助器。
        /// </summary>
        public void Shutdown()
        {
            GameEvent.RemoveEventListener<INetworkChannel, object>(NetworkEvent.NetworkConnectedEvent, OnNetworkConnected);
            GameEvent.RemoveEventListener<INetworkChannel>(NetworkEvent.NetworkClosedEvent, OnNetworkClosed);
            GameEvent.RemoveEventListener<INetworkChannel, int>(NetworkEvent.NetworkMissHeartBeatEvent, OnNetworkMissHeartBeat);
            GameEvent.RemoveEventListener<INetworkChannel, NetworkErrorCode, SocketError, string>(NetworkEvent.NetworkErrorEvent, OnNetworkError);
            GameEvent.RemoveEventListener<INetworkChannel, object>(NetworkEvent.NetworkCustomErrorEvent, OnNetworkCustomError);

            _networkChannel = null;
        }

        /// <summary>
        /// 准备进行连接。
        /// </summary>
        public void PrepareForConnecting()
        {
            _networkChannel.Socket.ReceiveBufferSize = 1024 * 64;
            _networkChannel.Socket.SendBufferSize = 1024 * 64;
        }

        /// <summary>
        /// 发送心跳消息包。
        /// </summary>
        /// <returns>是否发送心跳消息包成功。</returns>
        public bool SendHeartBeat()
        {
            _networkChannel.Send(MemoryPool.Acquire<HeartBeat>());
            return true;
        }

        /// <summary>
        /// 序列化消息包。
        /// </summary>
        /// <typeparam name="T">消息包类型。</typeparam>
        /// <param name="packet">要序列化的消息包。</param>
        /// <param name="destination">要序列化的目标流。</param>
        /// <returns>是否序列化成功。</returns>
        public bool Serialize<T>(T packet, Stream destination) where T : Packet
        {
            PacketBase packetImpl = packet as PacketBase;
            if (packetImpl == null)
            {
                Log.Warning("Packet is invalid.");
                return false;
            }

            _cachedStream.SetLength(_cachedStream.Capacity); // 此行防止 Array.Copy 的数据无法写入
            _cachedStream.Position = 0L;

            PacketHeader packetHeader = MemoryPool.Acquire<PacketHeader>();
            Serializer.Serialize(_cachedStream, packetHeader);
            MemoryPool.Release(packetHeader);

            Serializer.SerializeWithLengthPrefix(_cachedStream, packet, PrefixStyle.Fixed32);
            MemoryPool.Release((IMemory)packet);

            _cachedStream.WriteTo(destination);
            return true;
        }

        /// <summary>
        /// 反序列化消息包头。
        /// </summary>
        /// <param name="source">要反序列化的来源流。</param>
        /// <param name="customErrorData">用户自定义错误数据。</param>
        /// <returns>反序列化后的消息包头。</returns>
        public IPacketHeader DeserializePacketHeader(Stream source, out object customErrorData)
        {
            // 注意：此函数并不在主线程调用！
            customErrorData = null;
            return (IPacketHeader)RuntimeTypeModel.Default.Deserialize(source, MemoryPool.Acquire<PacketHeader>(), typeof(PacketHeader));
        }

        /// <summary>
        /// 反序列化消息包。
        /// </summary>
        /// <param name="packetHeader">消息包头。</param>
        /// <param name="source">要反序列化的来源流。</param>
        /// <param name="customErrorData">用户自定义错误数据。</param>
        /// <returns>反序列化后的消息包。</returns>
        public Packet DeserializePacket(IPacketHeader packetHeader, Stream source, out object customErrorData)
        {
            // 注意：此函数并不在主线程调用！
            customErrorData = null;

            PacketHeader scPacketHeader = packetHeader as PacketHeader;
            if (scPacketHeader == null)
            {
                Log.Warning("Packet header is invalid.");
                return null;
            }

            Packet packet = null;
            if (scPacketHeader.IsValid)
            {
                Type packetType = GetServerToClientPacketType(scPacketHeader.Id);
                if (packetType != null)
                {
                    packet = (Packet)RuntimeTypeModel.Default.DeserializeWithLengthPrefix(source, MemoryPool.Acquire(packetType), packetType, PrefixStyle.Fixed32, 0);
                }
                else
                {
                    Log.Warning("Can not deserialize packet for packet id '{0}'.", scPacketHeader.Id.ToString());
                }
            }
            else
            {
                Log.Warning("Packet header is invalid.");
            }

            MemoryPool.Release(scPacketHeader);
            return packet;
        }

        private Type GetServerToClientPacketType(int id)
        {
            if (_serverToClientPacketTypes.TryGetValue(id, out var type))
            {
                return type;
            }

            return null;
        }

        private void OnNetworkConnected(INetworkChannel channel, object userdata)
        {
            if (channel != _networkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' connected, local address '{1}', remote address '{2}'.",
                channel.Name, channel.Socket.LocalEndPoint.ToString(),
                channel.Socket.RemoteEndPoint.ToString());
        }

        private void OnNetworkClosed(INetworkChannel channel)
        {
            if (channel != _networkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' closed.", channel.Name);
        }

        private void OnNetworkMissHeartBeat(INetworkChannel channel, int missCount)
        {
            if (channel != _networkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' miss heart beat '{1}' times.", channel.Name, missCount.ToString());

            if (missCount < 2)
            {
                return;
            }

            channel.Close();
        }

        private void OnNetworkError(INetworkChannel channel, NetworkErrorCode networkErrorCode, SocketError socketError, string errorMessage)
        {
            if (channel != _networkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' error, error code is '{1}', error message is '{2}'.", channel.Name, networkErrorCode.ToString(), errorMessage);

            channel.Close();
        }

        private void OnNetworkCustomError(INetworkChannel channel, object userData)
        {
            if (channel != _networkChannel)
            {
                return;
            }
        }

        public void Clear()
        {
        }
    }
}