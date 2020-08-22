using System;
using UnityEngine;

namespace Apathy
{
    public abstract class Common
    {
        // we use large buffers instead of threads and queues (= TCP for games)
        // * on mac, we can use around 7MB
        // * on linux, the limit is 416KB (=425984 bytes)
        //
        // we use the minimum that we can expect to work on windows/mac/linux
        // and don't allow anyone to modify it to prevent problems.
        public const int MaxBufferSize = 416 * 1024;

        // actual max usable buffer size is a bit smaller on Linux.
        // sending 416KB buffer and comparing received values fails after 316KB.
        //
        // when using a 416KB receive buffer, we can send 363KB on Linux.
        // if we send anything bigger, the data is cut off / zeroed out.
        // (see send_receive_nonblocking_max test)
        public const int MaxUsableBufferSize = 316 * 1024;

        // NoDelay disables nagle algorithm. lowers CPU% and latency but
        // increases bandwidth
        public bool NoDelay = false;

        // cache buffers to avoid allocations
        // -> header: 4 bytes for 1 integer
        protected byte[] headerBuffer = new byte[4];
        // -> payload: MaxMessageSize + header size
        protected byte[] payloadBuffer;

        // max allowed message size is MaxUsable - 4 for header
        public const int MaxMessageSize = MaxUsableBufferSize - 4;

        // Client tick rate is often higher than server tick rate, especially if
        // server is under heavy load or limited to 20Hz or similar. Server
        // needs to process 'a few' messages per tick per connection. Processing
        // only one per tick can cause an ever growing backlog, hence ever
        // growing latency on the client. Set this to a reasonable amount, but
        // not too big so that the server never deadlocks reading too many
        // messages per tick (which would be way worse than one of the clients
        // having high latency.
        // -> see GetNextMessages for a more in-depth explanation!
        //
        // note: changes at runtime don't have any effect!
        public int MaxReceivesPerTickPerConnection = 1000;

        // disconnect detection
        protected static bool WasDisconnected(long socket)
        {
            // check if any recent errors
            if (NativeBindings.network_get_error(socket) != 0)
                return true;

            // try our disconnected method
            if (NativeBindings.network_disconnected(socket) == 1)
                return true;

            // doesn't look like a disconnect
            return false;
        }

        protected void ConfigureSocket(long socket)
        {
            int error = 0;

            // set non blocking
            if (NativeBindings.network_set_nonblocking(socket, ref error) != 0)
                Debug.LogError("network_set_nonblocking failed: " + (NativeError)error);

            // set send buffer
            // (linux might not return an error but still set a smaller size
            //  internally, so we need to double check if it worked)
            if (NativeBindings.network_set_send_buffer_size(socket, MaxBufferSize, ref error) != 0)
                Debug.LogError("network_set_send_buffer_size failed: " + (NativeError)error);
            int actualSendBufferSize = 0;
            if (NativeBindings.network_get_send_buffer_size(socket, ref actualSendBufferSize, ref error) != 0)
                Debug.LogError("network_get_send_buffer_size failed: " + (NativeError)error);
#if UNITY_EDITOR_LINUX
            // linux doubles the buffer size internally
            // see also: https://linux.die.net/man/7/socket (SO_RCVBUF)
            // -> it doesn't exactly double all values, e.g. odd numbers
            // -> the only thing we can assume that it's greater than size.
            if (actualSendBufferSize < MaxBufferSize)
#else
            if (actualSendBufferSize != MaxBufferSize)
#endif
                Debug.LogError("Failed to set send buffer size. OS has chosen " + actualSendBufferSize + " instead of " + MaxBufferSize + " internally.");

            // set receive buffer
            // (linux might not return an error but still set a smaller size
            //  internally, so we need to double check if it worked)
            if (NativeBindings.network_set_receive_buffer_size(socket, MaxBufferSize, ref error) != 0)
                Debug.LogError("network_set_receive_buffer_size failed: " + (NativeError)error);
            int actualReceiveBufferSize = 0;
            if (NativeBindings.network_get_receive_buffer_size(socket, ref actualReceiveBufferSize, ref error) != 0)
                Debug.LogError("network_get_receive_buffer_size failed: " + (NativeError)error);
#if UNITY_EDITOR_LINUX
            // linux doubles the buffer size internally
            // see also: https://linux.die.net/man/7/socket (SO_RCVBUF)
            // -> it doesn't exactly double all values, e.g. odd numbers
            // -> the only thing we can assume that it's greater than size.
            if (actualReceiveBufferSize < MaxBufferSize)
#else
            if (actualReceiveBufferSize != MaxBufferSize)
#endif
                Debug.LogError("Failed to set receive buffer size. OS has chosen " + actualReceiveBufferSize + " instead of " + MaxBufferSize + " internally.");

            // set no delay
            if (NativeBindings.network_set_nodelay(socket, NoDelay ? 1 : 0, ref error) != 0)
                Debug.LogError("network_set_nodelay failed: " + (NativeError)error);

            // enable TCP_KEEPALIVE to detect closed connections / wires
            if (NativeBindings.network_set_keepalive(socket, 1, ref error) != 0)
                Debug.LogError("network_set_keepalive failed: " + (NativeError)error);
        }

        // read exactly 'size' bytes if (and only if) available
        protected static unsafe bool ReadIfAvailable(long socket, int size, byte[] buffer)
        {
            // check how much is available
            int error = 0;
            int available = NativeBindings.network_available(socket, ref error);
            if (available >= size)
            {
                if (size <= buffer.Length)
                {
                    // need to pin memory before passing to C
                    // (https://stackoverflow.com/questions/46527470/pass-byte-array-from-unity-c-sharp-to-c-plugin)
                    fixed (void* buf = buffer)
                    {
                        int bytesRead = NativeBindings.network_recv(socket, buf, size, ref error);
                        if (bytesRead > 0)
                        {
                            //Debug.LogWarning("network_recv: avail=" + available + " read=" + bytesRead);
                            return true;
                        }
                        else Debug.LogError("network_recv failed: " + bytesRead + " error=" + (NativeError)error);
                    }
                }
                else Debug.LogError("ReadIfAvailable: buffer(" + buffer.Length + ") too small for " + size + " bytes");
            }
            else if (available == -1)
            {
                Debug.LogError("network_available failed for socket:" + socket + " error: " + (NativeError)error);
            }
            return false;
        }

        // send bytes if send buffer not full (in which case we should
        // disconnect because the receiving end pretty much timed out)
        // (internal for tests where we need to send without the max size check)
        internal unsafe bool SendIfNotFull(long socket, ArraySegment<byte> data)
        {
            // construct payload if not constructed yet or MaxSize changed
            // (we do allow changing MaxMessageSize at runtime)
            int payloadSize = MaxMessageSize + headerBuffer.Length;
            if (payloadBuffer == null || payloadBuffer.Length != payloadSize)
            {
                payloadBuffer = new byte[payloadSize];
            }

            // construct header (size)
            Utils.IntToBytesBigEndianNonAlloc(data.Count, headerBuffer);

            // calculate packet size (header + data)
            int packetSize = headerBuffer.Length + data.Count;

            // copy into payload buffer
            // NOTE: we write the full payload at once instead of writing first
            //       header and then data, because this way NODELAY mode is more
            //       efficient by sending the whole message as one packet.
            Array.Copy(headerBuffer, 0, payloadBuffer, 0, headerBuffer.Length);
            Array.Copy(data.Array, data.Offset, payloadBuffer, headerBuffer.Length, data.Count);

            fixed (void* buffer = payloadBuffer)
            {
                //Debug.Log("network_send: " + socketHandle + " payload=" + BitConverter.ToString(payloadBuffer, 0, packetSize));
                int error = 0;
                int sent = NativeBindings.network_send(socket, buffer, packetSize, ref error);
                if (sent < 0)
                {
                    // if the buffer is full then we will get an
                    // EAGAIN/EWOULDBLOCK error depending on the platform.
                    // this is expected behaviour for our "TCP for Games"
                    // approach where instead of threads, we write directly in
                    // to the buffer and if it's too full, we consider the
                    // connection broken/too slow and simply disconnect it.
                    // -> we only log as warning so it's obvious that it
                    //    happened, but no one assumes it's a strange error that
                    //    needs to be reported.
                    // -> NativeError.EWOULDBLOCK is available on all platforms.
                    if ((NativeError)error == NativeError.EWOULDBLOCK)
                    {
                        Debug.LogWarning("network_send buffer full, connection was closed for load balancing: socket=" + socket + " error=" + (NativeError)error + ". This is fine because the connection either stopped processing messages, or it's too slow and thousands of messages behind, or the server is under heavy load and sending way more messages than the network can handle. Note that you can intentionally allow higher load by increasing MaxMessagesPerTick in client & server.");
                    }
                    // other error codes should still be logged as errors.
                    else
                    {
                        Debug.LogError("network_send failed: socket=" + socket + " error=" + (NativeError)error);
                    }
                    return false;
                }
                return true;
            }
        }
    }
}
