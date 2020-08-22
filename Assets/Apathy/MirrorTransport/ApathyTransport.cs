// uses Apathy for both client & server
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Mirror
{
    public class ApathyTransport : Transport
    {
        // scheme used by this transport
        // "tcp4" means tcp with 4 bytes header, network byte order
        public const string Scheme = "tcp4";

        public ushort port = 7777;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        [Header("Server")]
        [Tooltip("Client tick rate is often higher than server tick rate, especially if server is under heavy load or limited to 20Hz or similar. Server needs to process 'a few' messages per tick per connection. Processing only one per tick can cause an ever growing backlog, hence ever growing latency on the client. Set this to a reasonable amount, but not too big so that the server never deadlocks reading too many messages per tick (which would be way worse than one of the clients having high latency.")]
        public int serverMaxReceivesPerTickPerConnection = 100;

        [Header("Client")]
        [Tooltip("Client tick rate is often higher than server tick rate, especially if server is under heavy load or limited to 20Hz or similar. Server needs to process 'a few' messages per tick per connection. Processing only one per tick can cause an ever growing backlog, hence ever growing latency on the client. This value can be higher than the server's value, because the server is never going to attack the client, and the client usually receives way more messages than it sends to the server.")]
        public int clientMaxReceivesPerTick = 1000;

        protected Apathy.Client client = new Apathy.Client();
        protected Apathy.Server server = new Apathy.Server();

        void Awake()
        {
            // configure
            client.NoDelay = NoDelay;
            client.MaxReceivesPerTickPerConnection = clientMaxReceivesPerTick;

            server.NoDelay = NoDelay;
            server.MaxReceivesPerTickPerConnection = serverMaxReceivesPerTickPerConnection;

            // set up events
            client.OnConnected = () => OnClientConnected.Invoke();
            client.OnData = (message) => OnClientDataReceived.Invoke(message, Channels.DefaultReliable);
            client.OnDisconnected = () => OnClientDisconnected.Invoke();

            server.OnConnected = (connectionId) => OnServerConnected.Invoke(connectionId);
            server.OnData = (connectionId, message) => OnServerDataReceived.Invoke(connectionId, message, Channels.DefaultReliable);
            server.OnDisconnected = (connectionId) => OnServerDisconnected.Invoke(connectionId);

            Debug.Log("ApathyTransport initialized!");
        }

        public override bool Available()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                   Application.platform == RuntimePlatform.OSXPlayer ||
                   Application.platform == RuntimePlatform.WindowsEditor ||
                   Application.platform == RuntimePlatform.WindowsPlayer ||
                   Application.platform == RuntimePlatform.LinuxEditor ||
                   Application.platform == RuntimePlatform.LinuxPlayer;
        }

        // client
        public override bool ClientConnected() => client.Connected;
        public override void ClientConnect(string address) => client.Connect(address, port);
        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != Scheme)
                throw new ArgumentException($"Invalid url {uri}, use {Scheme}://host:port instead", nameof(uri));

            ushort serverPort = uri.IsDefaultPort ? port : (ushort)uri.Port;
            client.Connect(uri.Host, serverPort);
        }
        public override bool ClientSend(int channelId, ArraySegment<byte> segment) => client.Send(segment);
        public override void ClientDisconnect() => client.Disconnect();

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (enabled) client.Update();
            if (enabled) server.Update();
        }

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = port;
            return builder.Uri;
        }
        public override bool ServerActive() => server.Active;
        public override void ServerStart() => server.Start(port);
        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            // send to all
            bool result = true;
            foreach (int connectionId in connectionIds)
                result &= server.Send(connectionId, segment);
            return result;
        }
        public override bool ServerDisconnect(int connectionId) => server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);
        public override void ServerStop() => server.Stop();

        // common
        public override void Shutdown()
        {
            Debug.Log("ApathyTransport Shutdown()");
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId)
        {
            return Apathy.Common.MaxMessageSize;
        }

        public override string ToString()
        {
            if (server.Active)
            {
                return "Apathy Server port: " + port;
            }
            else if (client.Connecting || client.Connected)
            {
                return "Apathy Client port: " + port;
            }
            return "Apathy (inactive/disconnected)";
        }
    }
}