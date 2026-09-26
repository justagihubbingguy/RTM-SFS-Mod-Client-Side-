using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace NtsanServer
{
    // by KAMO
    internal sealed class Lobby
    {
        public string Code;
        public readonly List<NetPeer> Peers = new List<NetPeer>();
    }

    internal static class Program
    {
        private const string ConnectionKey = "SFS_Multiplayer";
        private const string DefaultCode = "00000";
        private const byte PacketLobbyInfo = 3;

        private static readonly Dictionary<string, Lobby> Lobbies = new Dictionary<string, Lobby>();
        private static readonly Dictionary<int, Lobby> LobbyByPeer = new Dictionary<int, Lobby>();
        private static readonly Dictionary<int, string> PendingCode = new Dictionary<int, string>();

        private static void Main(string[] args)
        {
            int port = ParsePort(args, Environment.GetEnvironmentVariable("NTSAN_PORT"), 9050);

            var listener = new EventBasedNetListener();
            var server = new NetManager(listener)
            {
                AutoRecycle = true,
                UpdateTime = 15,
                UnconnectedMessagesEnabled = false,
            };

            listener.ConnectionRequestEvent += request =>
            {
                string key;
                try { key = request.Data.GetString(); }
                catch { request.Reject(); return; }

                if (key == ConnectionKey)
                {
                    Accept(request, GetOrCreateLobby(DefaultCode));
                    return;
                }

                if (key == null || !key.StartsWith(ConnectionKey + ":"))
                {
                    request.Reject();
                    return;
                }

                string arg = key.Substring(ConnectionKey.Length + 1);

                if (arg == "create")
                {
                    Accept(request, CreateLobby());
                    return;
                }

                if (arg.Length == 5 && IsDigits(arg) && Lobbies.TryGetValue(arg, out var lobby))
                {
                    Accept(request, lobby);
                    return;
                }

                request.Reject();
            };

            listener.PeerConnectedEvent += peer =>
            {
                if (!PendingCode.TryGetValue(peer.Id, out string code)) return;
                PendingCode.Remove(peer.Id);

                var writer = new NetDataWriter();
                writer.Put(PacketLobbyInfo);
                writer.Put(code);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);

                Console.WriteLine($"[+] Peer {peer.Id} ({peer.Address}) joined lobby {code} — {CountPeers(code)} in lobby, {server.ConnectedPeersCount} online");
            };

            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                PendingCode.Remove(peer.Id);
                if (LobbyByPeer.TryGetValue(peer.Id, out var lobby))
                {
                    lobby.Peers.Remove(peer);
                    LobbyByPeer.Remove(peer.Id);
                    if (lobby.Peers.Count == 0)
                    {
                        Lobbies.Remove(lobby.Code);
                        Console.WriteLine($"[-] Lobby {lobby.Code} closed (empty)");
                    }
                }
                Console.WriteLine($"[-] Peer {peer.Id} disconnected ({info.Reason}) — {server.ConnectedPeersCount} online");
            };

            listener.NetworkErrorEvent += (endPoint, error) =>
                Console.WriteLine($"[!] Network error from {endPoint}: {error}");

            listener.NetworkReceiveEvent += (fromPeer, reader, channel, deliveryMethod) =>
            {
                byte[] data = reader.GetRemainingBytes();
                if (data.Length == 0) return;
                if (!LobbyByPeer.TryGetValue(fromPeer.Id, out var lobby)) return;

                foreach (var peer in lobby.Peers)
                {
                    if (peer.Id == fromPeer.Id) continue;
                    peer.Send(data, deliveryMethod);
                }

                if (data[0] == 1)
                    Console.WriteLine($"[relay] BlueprintSpawn from peer {fromPeer.Id} → {lobby.Peers.Count - 1} peer(s) in lobby {lobby.Code}");
            };

            server.Start(port);
            Console.WriteLine($"Server listening on UDP {port}, key \"{ConnectionKey}\".");
            Console.WriteLine($"Lobbies: \"{ConnectionKey}\" = shared default room, \"{ConnectionKey}:create\" = new lobby, \"{ConnectionKey}:<code>\" = join.");

            bool running = true;
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; running = false; };
            while (running)
            {
                server.PollEvents();
                Thread.Sleep(10);
            }

            Console.WriteLine("Shutting down…");
            server.Stop();
        }

        private static void Accept(ConnectionRequest request, Lobby lobby)
        {
            var peer = request.Accept();
            if (peer == null) return;
            lobby.Peers.Add(peer);
            LobbyByPeer[peer.Id] = lobby;
            PendingCode[peer.Id] = lobby.Code;
        }

        private static Lobby CreateLobby()
        {
            string code;
            do
            {
                code = RandomNumberGenerator.GetInt32(0, 100000).ToString("D5");
            } while (code == DefaultCode || Lobbies.ContainsKey(code));

            var lobby = new Lobby { Code = code };
            Lobbies[code] = lobby;
            Console.WriteLine($"[+] Lobby {code} created");
            return lobby;
        }

        private static Lobby GetOrCreateLobby(string code)
        {
            if (!Lobbies.TryGetValue(code, out var lobby))
            {
                lobby = new Lobby { Code = code };
                Lobbies[code] = lobby;
            }
            return lobby;
        }

        private static int CountPeers(string code) =>
            Lobbies.TryGetValue(code, out var lobby) ? lobby.Peers.Count : 0;

        private static bool IsDigits(string s)
        {
            foreach (char c in s)
                if (c < '0' || c > '9') return false;
            return true;
        }

        private static int ParsePort(string[] args, string envPort, int fallback)
        {
            if (args != null && args.Length > 0 && int.TryParse(args[0], out int p)) return p;
            if (int.TryParse(envPort, out int e)) return e;
            return fallback;
        }
    }
}
