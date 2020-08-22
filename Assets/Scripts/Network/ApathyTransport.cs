// uses Apathy for both client & server
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Mirror
{
    public class ApathyTransport : Transport
    {

        public const string Scheme = "tcp4";

        public ushort port = 7777;

        [Tooltip("El algoritmo de Nagle se puede deshabilitar habilitando NoDelay")]
        public bool NoDelay = true;

        [Header("Server")]
        [Tooltip("La tasa de tics del cliente es a menudo más alta que la tasa de tics del servidor, especialmente si el servidor está bajo una carga pesada o limitado a 20Hz o similar. El servidor necesita procesar 'algunos' mensajes por tick por conexión. Procesar solo uno por tick puede causar una acumulación de pedidos cada vez mayor y, por lo tanto, una latencia cada vez mayor en el cliente. Establezca esto en una cantidad razonable, pero no demasiado grande para que el servidor nunca bloquee la lectura de demasiados mensajes por tic (lo que sería mucho peor que uno de los clientes que tiene una latencia alta).")]
        public int serverMaxReceivesPerTickPerConnection = 100;

        [Header("Client")]
        [Tooltip("La tasa de tics del cliente es a menudo más alta que la tasa de tics del servidor, especialmente si el servidor está bajo una carga pesada o limitado a 20Hz o similar. El servidor necesita procesar 'algunos' mensajes por tick por conexión. Procesar solo uno por tick puede causar una acumulación cada vez mayor, por lo tanto, una latencia cada vez mayor en el cliente. Este valor puede ser mayor que el valor del servidor, porque el servidor nunca atacará al cliente, y el cliente generalmente recibe muchos más mensajes de los que envía al servidor .")]
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

            Debug.Log("ApathyTransport iniciado!");
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
                throw new ArgumentException($" url invalida {uri}, use {Scheme}://host:port instead", nameof(uri));

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
            Debug.Log("ApathyTransport apagar()");
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