// uses the first available transport for server and client.
// example: to use Apathy if on Windows/Mac/Linux and fall back to Telepathy
//          otherwise.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [HelpURL("https://mirror-networking.com/docs/Transports/Fallback.html")]
    public class FallbackTransport : Transport
    {
        public Transport[] transports;

        // the first transport that is available on this platform
        Transport available;

        public void Awake()
        {
            if (transports == null || transports.Length == 0)
            {
                throw new Exception("FallbackTransport requiere al menos 1 transporte subyacente");
            }
            Debug.Log("Antes del initCliente en el metodo awake del fallback transport");
            InitClient();
            Debug.Log("Antes del iniServer en el metodo awake del fallback transport");
            InitServer();
            available = GetAvailableTransport();
            Debug.Log("Transporte alternativo disponible: " + available.GetType());
        }

        // The client just uses the first transport available
        Transport GetAvailableTransport()
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    return transport;
                }
            }
            throw new Exception("Ningún transporte adecuado para esta plataforma");
        }

        public override bool Available()
        {
            Debug.Log("Available del FallbackTransport");

            return available.Available();
        }

        // clients always pick the first transport
        void InitClient()
        {
            // wire all the base transports to our events
            foreach (Transport transport in transports)
            {
                Debug.Log("initClient en fallback transport");
                Debug.Log("Antes del OnclientConeected en fallback transport");
                transport.OnClientConnected.AddListener(OnClientConnected.Invoke);
                Debug.Log("Antes del OnclientDataReceived en fallback transport");
                transport.OnClientDataReceived.AddListener(OnClientDataReceived.Invoke);
                Debug.Log("Antes del OnclientError en fallback transport");
                transport.OnClientError.AddListener(OnClientError.Invoke);
                Debug.Log("Antes del OnclientDisconect en fallback transport");
                transport.OnClientDisconnected.AddListener(OnClientDisconnected.Invoke);
            }
        }

        public override void ClientConnect(string address)
        {
            available.ClientConnect(address);
        }

        public override void ClientConnect(Uri uri)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    try
                    {
                        transport.ClientConnect(uri);
                        available = transport;
                    }
                    catch (ArgumentException)
                    {
                        // transport does not support the schema, just move on to the next one
                    }
                }
            }
            throw new Exception("Ningún transporte adecuado para esta plataforma");
        }

        public override bool ClientConnected()
        {
            return available.ClientConnected();
        }

        public override void ClientDisconnect()
        {
            available.ClientDisconnect();
        }

        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            return available.ClientSend(channelId, segment);
        }

        void InitServer()
        {
            // wire all the base transports to our events
            foreach (Transport transport in transports)
            {
                Debug.Log("initServer en fallback transport");

                transport.OnServerConnected.AddListener(OnServerConnected.Invoke);
                transport.OnServerDataReceived.AddListener(OnServerDataReceived.Invoke);
                transport.OnServerError.AddListener(OnServerError.Invoke);
                transport.OnServerDisconnected.AddListener(OnServerDisconnected.Invoke);
            }
        }

        // right now this just returns the first available uri,
        // should we return the list of all available uri?
        public override Uri ServerUri() => available.ServerUri();

        public override bool ServerActive()
        {
            return available.ServerActive();
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return available.ServerGetClientAddress(connectionId);
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return available.ServerDisconnect(connectionId);
        }

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            return available.ServerSend(connectionIds, channelId, segment);
        }

        public override void ServerStart()
        {
            available.ServerStart();
        }

        public override void ServerStop()
        {
            available.ServerStop();
        }

        public override void Shutdown()
        {
            available.Shutdown();
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            // finding the max packet size in a fallback environment has to be
            // done very carefully:
            // * servers and clients might run different transports depending on
            //   which platform they are on.
            // * there should only ever be ONE true max packet size for everyone,
            //   otherwise a spawn message might be sent to all tcp sockets, but
            //   be too big for some udp sockets. that would be a debugging
            //   nightmare and allow for possible exploits and players on
            //   different platforms seeing a different game state.
            // => the safest solution is to use the smallest max size for all
            //    transports. that will never fail.
            int mininumAllowedSize = int.MaxValue;
            foreach (Transport transport in transports)
            {
                int size = transport.GetMaxPacketSize(channelId);
                mininumAllowedSize = Mathf.Min(size, mininumAllowedSize);
            }
            return mininumAllowedSize;
        }

        public override string ToString()
        {
            return available.ToString();
        }

    }
}
