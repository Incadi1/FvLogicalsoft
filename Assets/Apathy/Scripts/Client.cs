using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Apathy
{
    public class Client : Common
    {
        long socket = -1;
        Thread connectThread;

        // connecting state (volatile for thread safety)
        // ! USE LOCK !
        volatile bool _Connecting;
        public bool Connecting => _Connecting;

        // connected state (volatile for thread safety)
        // ! USE LOCK !
        volatile bool _Connected;
        public bool Connected => _Connected;

        // receive buffer of size MaxMessageSize
        protected byte[] messageBuffer;

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
        public Action OnConnected =
            () => { Debug.LogWarning("Apathy.Client.OnConnected"); };

        public Action<ArraySegment<byte>> OnData =
            (segment) => { Debug.LogWarning("Apathy.Client.OnData: " + BitConverter.ToString(segment.Array, segment.Offset, segment.Count)); };

        public Action OnDisconnected =
            () => { Debug.LogWarning("Apathy.Client.OnDisconnected"); };

        // Update should be called once per tick to process events.
        // IMPORTANT: NOT THREAD SAFE! otherwise header/content reading will be
        //            corrupted.
        bool triedToConnect;
        int contentSize;
        public void Update()
        {
            // tried to connect, and not connecting anymore?
            // in other words, is a result message expected now?
            if (triedToConnect && !Connecting)
            {
                // connect succeeded
                if (Connected)
                {
                    // call connected event
                    OnConnected();
                }
                // connect failed
                else
                {
                    // clean up properly, then call disconnected event
                    CloseSocketAndCleanUp();
                    OnDisconnected();
                }

                // the connection attempt was definitely finished and handled
                triedToConnect = false;
            }

            // connected?
            if (Connected)
            {
                // connected and detected a disconnect?
                if (socket == -1 || WasDisconnected(socket))
                {
                    // clean up properly, then call disconnected event
                    CloseSocketAndCleanUp();
                    OnDisconnected();
                }
                // still connected? then read a few messages
                else
                {
                    // IMPORTANT: read a few (not just one) messages per tick(!)
                    // server usually sends a lot of messages, and if we were to
                    // only process one per tick then we would lag behind and
                    // see ever growing latencies/buffers.
                    int i;
                    for (i = 0; i < MaxReceivesPerTickPerConnection; ++i)
                    {
                        // header not read yet? then read if available
                        if (contentSize == 0)
                        {
                            if (ReadIfAvailable(socket, 4, headerBuffer))
                            {
                                contentSize = Utils.BytesToIntBigEndian(headerBuffer);
                            }
                            // can't read content size yet. try again next frame.
                            else break;
                        }
                        // otherwise header was read last time. just read content.
                        // (don't break)

                        // try to read content
                        if (contentSize > 0)
                        {
                            // protect against allocation attacks. an attacker might send
                            // multiple fake '2GB header' packets in a row, causing the server
                            // to allocate multiple 2GB byte arrays and run out of memory.
                            if (contentSize <= MaxMessageSize)
                            {
                                // read it
                                if (ReadIfAvailable(socket, contentSize, messageBuffer))
                                {
                                    // create ArraySegment from read content
                                    ArraySegment<byte> segment = new ArraySegment<byte>(messageBuffer, 0, contentSize);

                                    // reset contentSize for next time
                                    contentSize = 0;

                                    // call OnData event
                                    OnData(segment);
                                }
                                // can't fully read it yet. try again next frame.
                                else break;
                            }
                            else
                            {
                                Debug.LogWarning("[Client] possible allocation attack with a header of: " + contentSize + " bytes > MaxMessageSize=" + MaxMessageSize);

                                // clean up properly, then call disconnected event
                                // -> DO NOT wait for next Update to
                                //    detect a closed client. otherwise Mirror
                                //    would think we are still connected until
                                //    next frame!
                                CloseSocketAndCleanUp();
                                OnDisconnected();

                                // no need to receive any more messages
                                break;
                            }
                        }
                        // no content yet. try again next frame.
                        else break;
                    }

                    // for debugging
                    //if (i > 1) Debug.Log("[Client]: read multiple (" + i + ") messages this tick.");
                }
            }
        }

        // the connect thread function
        void ConnectThreadFunction(NetworkEndPoint address)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // connect (blocking)
                int error = 0;
                if (NativeBindings.network_connect(socket, ref address, ref error) == 0)
                {
                    // configure the socket
                    ConfigureSocket(socket);

                    // connect successful
                    // AFTER configuring the socket. so that we don't call recv
                    // while still blocking!
                    Debug.Log("[Client] connected!");
                    lock (this) { _Connected = true; }
                }
                // log errors if failed.
                // (ECONNABORTED is expected if we call Disconnect while connecting)
                else if ((NativeError)error != NativeError.ECONNABORTED)
                {
                    Debug.LogError("network_connect failed: " + (NativeError)error);
                }
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Debug.LogError("[Client] Connect Exception: " + exception);
            }
            finally
            {
                // we definitely aren't connecting anymore. either it worked or
                // it failed.
                lock (this) { _Connecting = false; }
            }
        }

        public void Connect(string hostname, ushort port)
        {
            // not if already started
            if (Connecting || Connected) return;

            // create receive buffer
            messageBuffer = new byte[MaxMessageSize];

            // reset state
            contentSize = 0;

            // let GetNextMessages know that we tried to connect so it can
            // return either a Connected or a Disconnected message, but never no
            // message at all.
            triedToConnect = true;

            // resolve host name (if hostname. otherwise it returns the IP)
            // and connect to the first available address (IPv4 or IPv6)
            // => GetHostAddresses is BLOCKING (for a very short time). we could
            //    move it to the ConnectThread, but it's hardly worth the extra
            //    code since we would have to create the socket in ConnectThread
            //    too, which would require us to use locks around socket every-
            //    where. it's better to live with a <1s block (if any).
            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(hostname);
                if (addresses.Length > 0)
                {
                    // try to parse the IP
                    string ip = addresses[0].ToString();
                    if (NetworkEndPoint.TryParse(ip, port, out NetworkEndPoint address))
                    {
                        // create the socket
                        int error = 0;
                        if (NativeBindings.network_create_socket(ref socket, ref address, ref error) == 0)
                        {
                            // note: no need to enable DualMode because we
                            // connect to IPv4 or IPv6 depending on 'hostname'
                            // and then we don't reuse the socket.

                            // we are connecting
                            _Connecting = true; // thread isn't running, no lock needed!
                            Debug.Log("[Client] connecting to: " + ip);

                            // connect is blocking. let's call it in the thread and
                            // return immediately.
                            connectThread = new Thread(() => { ConnectThreadFunction(address); });
                            connectThread.IsBackground = true;
                            connectThread.Start();
                        }
                        else Debug.LogError("network_create_socket failed: " + (NativeError)error);
                    }
                    else Debug.LogError("[Client] Connect: failed to parse ip: " + ip + " port: " + port);
                }
                // it's not an error. just an invalid host so log a warning.
                else Debug.LogWarning("[Client] Connect: failed to resolve host: " + hostname + " (no address found)");
            }
            catch (SocketException exception)
            {
                // it's not an error. just an invalid host so log a warning.
                Debug.LogWarning("[Client] Connect: failed to resolve host: " + hostname + " reason: " + exception);
            }
        }

        // close the socket
        void CloseSocket()
        {
            // close client if already created
            // (it's still -1 for a short moment after calling Connect)
            if (socket != -1)
            {
                int error = 0;
                if (NativeBindings.network_close(socket, ref error) != 0)
                    Debug.LogError("network_close client failed: " + (NativeError)error);
                socket = -1;
            }
        }

        // clean up the state
        void CleanUp()
        {
            // not connected anymore
            lock (this)
            {
                _Connected = false;
                _Connecting = false;
            }

            // reset GetNextMessage helpers
            contentSize = 0;
        }

        // close and cleanup together. can be used if we don't need to Join on
        // the connect thread inbetween.
        void CloseSocketAndCleanUp()
        {
            CloseSocket();
            CleanUp();
        }

        public void Disconnect()
        {
            // only if connecting or connected
            if (Connecting || Connected)
            {
                // close the socket. this will make the connectThread fail.
                CloseSocket();

                // let the connect thread finish gracefully. connect will abort
                // either way because we closed the socket.
                connectThread?.Join();

                // clean up the state only AFTER the connect thread was FULLY
                // finished. if we close the socket and clean up BEFORE calling
                // connectThread.Join, then there is a rare case where cleanup
                // would set connected=false, but the connectThread would still
                // set it connected=true shortly after (= race condition).
                // -> we clean up EVERYTHING because we don't know in which
                //    state the connect thread might have finished. this is the
                //    only way to guarantee that we can call Connect again
                //    immediately after returning.
                CleanUp();
            }
        }

        // Send ArraySegment for allocation free calls. byte[] can be allocation
        // free too, but Mirror would require ArraySegment sending for that.
        public bool Send(ArraySegment<byte> segment)
        {
            // only if connected
            if (Connected)
            {
                // check if the socket is still valid.
                // it can be '-1' between a failed send and the next
                // GetNextMessages call which will remove it from clients.
                if (socket != -1)
                {
                    // respect max message size to avoid allocation attacks.
                    if (segment.Count <= MaxMessageSize)
                    {
                        // try to send.
                        // if the buffer is full then it will close the
                        // connection (= "TCP for Games")
                        if (!SendIfNotFull(socket, segment))
                        {
                            // if send failed, then apathy closes the socket in
                            // native C already. we close it here again just in
                            // case, but we DO NOT log an error message if it
                            // failed, because it is expected to fail with EBADF
                            int error = 0;
                            NativeBindings.network_close(socket, ref error);
                            socket = -1;
                            return false;
                        }
                        return true;
                    }
                    Debug.LogError("[Client] Send: message too big: " + segment.Count + ". Limit: " + MaxMessageSize);
                    return false;
                }
                return false;
            }
            Debug.LogWarning("[Client] Send: not connected!");
            return false;
        }

        // Send byte[] for ease of use. can be allocation free too if the same
        // byte[] is used by the caller.
        public bool Send(byte[] data) => Send(new ArraySegment<byte>(data));
    }
}