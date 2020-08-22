using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace Apathy
{
    public class Server : Common
    {
        // listener socket handle (-1 if unassigned, see network.bindings.c)
        long listener = -1;

        // class with all the client's data. let's call it Token for consistency
        // with the async socket methods.
        class ClientToken
        {
            // client socket
            public long socket;

            // connect processed yet?
            public bool connectProcessed;

            // header content size
            public int contentSize;

            // receive buffer of size MaxMessageSize
            public byte[] messageBuffer;

            public ClientToken(long socket, int MaxMessageSize)
            {
                this.socket = socket;

                // create receive buffer
                messageBuffer = new byte[MaxMessageSize];
            }
        }
        // clients with <connectionId, ClientToken>
        Dictionary<int, ClientToken> clients = new Dictionary<int, ClientToken>();
        int nextConnectionId;

        // check if server is active
        public bool Active => listener != -1;

        // cache RemoveIds for GetNextMessages to avoid allocations
        List<int> removeIds = new List<int>();

        // Connected/Data/Disconnected events.
        // Called on the same thread at all times.
        //
        // Initially we used a GetNextMessages approach, but it didn't scale:
        // - Server may send a lot of messages, and the client needs to receive
        //   more than one message per tick to not lag behind.
        // - With GetNextMessages, we need 'MaxMessagesPerTick' receive buffers,
        //   each of 'MaxMessageSize' bytes.
        // - DOTSNET can easily reach 10k or 100k messages per tick, which means
        //   that we would need 64KB * 100k = Gigabytes of buffers per
        //   connection. This takes a lot of time to create, and would not scale
        //   unless we buy a lot of RAM.
        // - Storing messages in a queue first and then calling Events adds zero
        //   value over calling events directly. It's the same end result.
        //
        // With events:
        // + We only need one 'MaxMessageSize' buffer (64KB instead of few GB).
        // + The code is easier & way less computations (no extra queue step).
        // + We initialize the events so that we can always call them without
        //   null checks and we get log messages if they weren't set up.
        //
        // NOTE: if necessary, the caller can use the OnData event to convert
        //       to a NetworkMessage struct and then cache + batch process them.
        //       (This is what DOTSNET does. It's way better than doing any kind
        //        of buffering here, because we would need to allocate byte[]
        //        instead of stack allocated NetworkMessage structs!)
        //
        public Action<int> OnConnected =
            (connectionId) => { Debug.LogWarning("Apathy.Server.OnServerConnected: " + connectionId); };

        public Action<int, ArraySegment<byte>> OnData =
            (connectionId, segment) => { Debug.LogWarning("Apathy.Server.OnServerData: " + connectionId + " => " + BitConverter.ToString(segment.Array, segment.Offset, segment.Count)); };

        public Action<int> OnDisconnected =
            (connectionId) => { Debug.LogWarning("Apathy.Server.OnServerDisconnected: " + connectionId); };

        // ideally call this once per frame in main loop.
        // IMPORTANT: NOT THREAD SAFE! otherwise header/content reading will be
        //            corrupted.
        public void Update()
        {
            // only if server is active
            if (!Active) return;

            // can we accept someone?
            if (AcceptNext(out long accepted))
            {
                // configure the client socket
                ConfigureSocket(accepted);

                // add client with next connection id ('++' because it should
                // start at '1' for Mirror. '0' is reserved for local player)
                clients[++nextConnectionId] = new ClientToken(accepted, MaxMessageSize);
            }

            // check all clients for incoming data
            removeIds.Clear();
            foreach (KeyValuePair<int, ClientToken> kvp in clients)
            {
                // first of all: did the client just connect?
                if (!kvp.Value.connectProcessed)
                {
                    // call connected event
                    kvp.Value.connectProcessed = true;
                    OnConnected(kvp.Key);
                }
                // closed the handle at some point (e.g. in Send)?
                // or detected a disconnect?
                else if (kvp.Value.socket == -1 || WasDisconnected(kvp.Value.socket))
                {
                    // remove it when done
                    removeIds.Add(kvp.Key);
                }
                // still connected? then read a few messages
                else
                {
                    // IMPORTANT: read a few (not just one) messages per tick(!)
                    // -> client usually has higher tick rate than server
                    // -> if client sends 2 messages per tick but server only
                    //    reads 1 message per tick, then it will be an ever
                    //    growing receive buffer and cause extremely high
                    //    latency for that client (seconds/minutes) and never
                    //    recover from it.
                    // -> this happened in uMMORPG V2 with the new character
                    //    controller movement. Cmds were called each FixedUpdate
                    //    and the server didn't read them quickly enough,
                    //    causing ever growing latency on client for no obvious
                    //    reason whatsoever.
                    // => that's why we always need to read A FEW at once
                    //    * reading ALL available messages via while(available)
                    //      would make deadlocks possible if an attacker sends
                    //      lots of small messages very often, causing the
                    //      server to never exit the while loop (= deadlock)
                    //    * allowing N messages PER SECOND makes sense. but it's
                    //      a bit odd to implement.
                    //    * reading N messages PER FRAME is the most simple
                    //      solution. if the client still lags then so be it,
                    //      the server doesn't care. it does its best to process
                    //      N per tick and that's that.
                    //
                    // NOTE: we don't care if we reached 'max' in this frame and
                    //       there are still some pending. we could kick the
                    //       client, but let's just let it have some growing
                    //       latency and hope for it to recover later. after all
                    //       the server doesn't really care too much about ONE
                    //       client's latency!
                    int i;
                    for (i = 0; i < MaxReceivesPerTickPerConnection; ++i)
                    {
                        // header not read yet, but can read it now?
                        if (kvp.Value.contentSize == 0)
                        {
                            if (ReadIfAvailable(kvp.Value.socket, 4, headerBuffer))
                            {
                                kvp.Value.contentSize = Utils.BytesToIntBigEndian(headerBuffer);
                            }
                            // can't read content size yet. try again next frame.
                            else break;
                        }
                        // otherwise header was read last time. just read content.
                        // (don't break)

                        // try to read content
                        if (kvp.Value.contentSize > 0)
                        {
                            // protect against allocation attacks. an attacker might
                            // send multiple fake '2GB header' packets in a row,
                            // causing the server to allocate multiple 2GB byte
                            // arrays and run out of memory.
                            if (kvp.Value.contentSize <= MaxMessageSize)
                            {
                                if (ReadIfAvailable(kvp.Value.socket, kvp.Value.contentSize, kvp.Value.messageBuffer))
                                {
                                    // create ArraySegment from read content
                                    ArraySegment<byte> segment = new ArraySegment<byte>(kvp.Value.messageBuffer, 0, kvp.Value.contentSize);

                                    // reset contentSize for next time
                                    kvp.Value.contentSize = 0;

                                    // call OnData event
                                    OnData(kvp.Key, segment);
                                }
                                // can't fully read it yet. try again next frame.
                                else break;
                            }
                            else
                            {
                                // remove it when done
                                Debug.LogWarning("[Server]: possible allocation attack with a header of: " + kvp.Value.contentSize + " bytes > MaxMessageSize=" + MaxMessageSize + " from connectionId: " + kvp.Key + " IP: " + GetClientAddress(kvp.Key));
                                removeIds.Add(kvp.Key);

                                // no need to receive any more messages
                                break;
                            }
                        }
                        // no content yet. try again next frame.
                        else break;
                    }

                    // for debugging
                    //if (i > 1) Debug.Log("[Server]: read multiple (" + i + ") messages for connection: " + kvp.Key + " this tick.");
                }
            }

            // close all connections in removeIds
            foreach (int connectionId in removeIds)
            {
                // close socket if not closed yet.
                // if WE disconnected it via Disconnect(connectionId), then it
                // was already closed. closing it again would cause EBADF error.
                long socket = clients[connectionId].socket;
                if (socket != -1)
                {
                    int error = 0;
                    if (NativeBindings.network_close(socket, ref error) != 0)
                    {
                        Debug.LogError("network_close client failed for connId=" + connectionId + " error=" + (NativeError)error);
                    }
                }

                // remove from clients, then call disconnected event
                // -> always AFTER closing the connection, because that's when
                //    it's truly disconnected.
                // -> call Disconnected event in this GetNextMessages call
                //    don't just close and add the message later, otherwise the
                //    server might assume a player is still online for one more
                //    tick.
                clients.Remove(connectionId);
                OnDisconnected(connectionId);
            }
        }

        // start listening on a port
        public void Start(ushort port)
        {
            Initialize(port);
            Listen();
        }

        void Initialize(ushort port)
        {
            // initialize native layer
            int initialize = NativeBindings.network_initialize();
            if (initialize == 0)
            {
                Debug.Log("network_initialized");

                // create the socket and listen on IPv6 and IPv4 via DualMode
                int error = 0;
                byte[] bytes = IPAddress.IPv6Any.GetAddressBytes();
                NetworkEndPoint address = NetworkEndPoint.CreateIPv6(bytes, port);
                if (NativeBindings.network_create_socket(ref listener, ref address, ref error) == 0)
                {
                    // try to enable dual mode to support both IPv6 and IPv4
                    if (NativeBindings.network_set_dualmode(listener, 1, ref error) != 0)
                    {
                        Debug.LogError("network_set_dualmode failed: " + (NativeError)error);
                    }

                    // bind the socket
                    if (NativeBindings.network_bind(listener, ref address, ref error) == 0)
                    {
                        // configure the socket
                        ConfigureSocket(listener);
                    }
                    else Debug.LogError("network_bind failed: " + (NativeError)error);
                }
                else Debug.LogError("network_create_socket failed: " + (NativeError)error);
            }
            else Debug.LogError("network_initialize failed: " + initialize);
        }

        void Listen()
        {
            int error = 0;
            if (NativeBindings.network_listen(listener, ref error) != 0)
            {
                Debug.LogError("network_listen failed: " + (NativeError)error);
            }
        }

        // try to accept the next connection
        bool AcceptNext(out long socket)
        {
            socket = -1;
            int error = 0;
            NetworkEndPoint clientAddress = new NetworkEndPoint();
            if (NativeBindings.network_accept(listener, ref socket, ref clientAddress, ref error) == 0)
            {
                //Debug.Log("network_accept: " + socket + " address=" + clientAddress);
                return true;
            }
            // log error if unusual
            // (ignore EWOULDBLOCK, which is expected for nonblocking sockets)
            // (http://www.workers.com.br/manuais/53/html/tcp53/mu/mu-7.htm)
            else if ((NativeError)error != NativeError.EWOULDBLOCK)
            {
                Debug.LogError("network_accept failed: " + (NativeError)error);
            }

            return false;
        }

        public bool Send(int connectionId, ArraySegment<byte> segment)
        {
            // respect max message size to avoid allocation attacks.
            if (segment.Count <= MaxMessageSize)
            {
                // find the connection
                if (clients.TryGetValue(connectionId, out ClientToken token))
                {
                    // check if the socket is still valid.
                    // it can be '-1' between a failed send and the next
                    // GetNextMessages call which will remove it from clients.
                    if (token.socket != -1)
                    {
                        // try to send.
                        // if the buffer is full then it will close the
                        // connection (= "TCP for Games")
                        if (!SendIfNotFull(token.socket, segment))
                        {
                            // if send failed, then apathy closes the socket in
                            // native C already. we close it here again just in
                            // case, but we DO NOT log an error message if it
                            // failed, because it is expected to fail with EBADF
                            int error = 0;
                            NativeBindings.network_close(token.socket, ref error);
                            token.socket = -1;

                            // note: client will be removed in GetNextMessages
                            //       automatically. no need to do it here.
                            return false;
                        }
                        return true;
                    }
                    return false;
                }
                // sending to an invalid connectionId is expected sometimes.
                // for example, if a client disconnects, the server might still
                // try to send for one frame before it calls GetNextMessages
                // again and realizes that a disconnect happened.
                // so let's not spam the console with log messages.
                //Debug.Log("[Server] Send: invalid connectionId: " + connectionId);
                return false;
            }
            Debug.LogError("[Server] Send: message too big: " + segment.Count + ". Limit: " + MaxMessageSize);
            return false;
        }

        // Send byte[] for ease of use. can be allocation free too if the same
        // byte[] is used by the caller.
        public bool Send(int connectionId, byte[] data) => Send(connectionId, new ArraySegment<byte>(data));

        // client's ip is sometimes needed by the server, e.g. for bans
        public unsafe string GetClientAddress(int connectionId)
        {
            // find the connection
            if (clients.TryGetValue(connectionId, out ClientToken token) &&
                token.socket != -1)
            {
                int error = 0;
                // NetworkEndPoint NEEDS to be created with length, otherwise
                // data array is empty and get_peer_address won't write into it
                NetworkEndPoint address = new NetworkEndPoint {length = sizeof(NetworkEndPoint)};
                if (NativeBindings.network_get_peer_address(token.socket, ref address, ref error) == 0)
                {
                    return address.Ip;
                }
                else Debug.LogError("network_get_socket_address failed: " + (NativeError)error);
            }
            return "";
        }

        // disconnect (kick) a client
        public bool Disconnect(int connectionId)
        {
            // find the connection
            if (clients.TryGetValue(connectionId, out ClientToken token))
            {
                // close if not closed yet
                // (a failed Send might have already closed it, but it might
                //  still be in clients until next GetNextMessages call)
                if (token.socket != -1)
                {
                    // just close it. GetNextMessage will take care of the event.
                    int error = 0;
                    if (NativeBindings.network_close(token.socket, ref error) != 0)
                        Debug.LogError("network_close client failed: " + (NativeError)error);
                    token.socket = -1;
                }
                return true;
            }
            return false;
        }

        public void Stop()
        {
            // close all client sockets
            foreach (KeyValuePair<int, ClientToken> kvp in clients)
            {
                // close socket if not closed yet
                // (might have been closed in last send/recv already)
                if (kvp.Value.socket != -1)
                {
                    int error = 0;
                    if (NativeBindings.network_close(kvp.Value.socket, ref error) != 0)
                        Debug.LogError("network_close client failed: " + (NativeError)error);
                    kvp.Value.socket = -1;
                }
            }
            clients.Clear();

            // close listener socket
            if (listener != -1)
            {
                int error = 0;
                if (NativeBindings.network_close(listener, ref error) != 0)
                    Debug.LogError("network_close listener failed: " + (NativeError)error);
                listener = -1;
            }

            // terminate network
            int result = NativeBindings.network_terminate();
            if (result != 0)
                Debug.LogError("network_terminate failed:" + result);
        }
    }
}