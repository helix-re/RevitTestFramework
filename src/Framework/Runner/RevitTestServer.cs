using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace RTF.Framework
{
    public class RevitTestServer
    {
        private static RevitTestServer instance;
        private static readonly Object mutex = new Object();
        private static MessageBuffer buffer;
        private static int receiveTimeout;

        private Socket handlerSocket;
        private Socket serverSocket;

        private TcpListener tcpListener;
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();
        /// <summary>
        /// The port number for the server to be connected to
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// A singleton instance
        /// </summary>
        public static RevitTestServer Instance
        {
            get
            {
                lock (mutex)
                {
                    return instance ?? (instance = new RevitTestServer());
                }
            }
        }

        /// <summary>
        /// Start the server at localhost
        /// </summary>
        /// <param name="timeout"></param>
        public void Start(int timeout)
        {
            IPAddress ipAddress = IPAddress.Parse(CommonData.LocalIPAddress);
            //serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.Bind(new IPEndPoint(ipAddress, 0));

            tcpListener = new TcpListener(new IPEndPoint(ipAddress, 0));

            IPEndPoint endPoint = tcpListener.LocalEndpoint as IPEndPoint;
            Port = endPoint.Port;

            tcpListener.Start(1);
            //serverSocket.Listen(1);
            receiveTimeout = timeout;
        }

        /// <summary>
        /// End the server
        /// </summary>
        public void End()
        {
            if (handlerSocket != null)
            {
                handlerSocket.Close();
                handlerSocket = null;
            }
            if (serverSocket != null)
            {
                tcpListener.Stop();
                tcpListener = null;
                //serverSocket.Close();
                //serverSocket = null;
            }
        }

        /// <summary>
        /// Close the working socket and also reset the message ID
        /// </summary>
        public void ResetWorkingSocket()
        {
            cancellationToken = new CancellationTokenSource();
            if (handlerSocket != null)
            {
                handlerSocket.Close();
                handlerSocket = null;
            }
            MessageResult.PrevMessageID = -1;
        }

        public void Reset()
        {
            cancellationToken.Cancel();
            //ResetWorkingSocket();

            //int port = (serverSocket.LocalEndPoint as IPEndPoint).Port;
            //serverSocket.Shutdown(SocketShutdown.Both);
            //serverSocket.Close();

            //IPAddress ipAddress = IPAddress.Parse(CommonData.LocalIPAddress);
            //serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //serverSocket.Bind(new IPEndPoint(ipAddress, port));

            //serverSocket.Listen(1);
        }

        /// <summary>
        /// This will try to accept a connection from localhost and receive a packet.
        /// It will then buffer the packet and try to create a message.
        /// </summary>
        /// <returns></returns>
        public MessageResult GetNextMessageResult()
        {
            MessageResult result = new MessageResult();
            if (handlerSocket == null)
            {
                handlerSocket = AcceptLocalConnection();

                if (handlerSocket == null)
                {
                    result.Status = MessageStatus.OtherError;
                    return result;
                }
                handlerSocket.ReceiveTimeout = receiveTimeout;
            }

            if (buffer != null)
            {
                var msg = buffer.GetMessage();
                if (msg != null)
                {
                    result.Message = msg;
                    return result;
                }
            }

            var bytes = new byte[1024];
            int size = 0;
            try
            {
                size = handlerSocket.Receive(bytes, 1024, SocketFlags.None);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    result.Status = MessageStatus.TimedOut;
                    return result;
                }
                else
                {
                    result.Status = MessageStatus.OtherError;
                    return result;
                }
            }
            if (size > 0)
            {
                if (buffer == null)
                {
                    buffer = new MessageBuffer(bytes.Take(size).ToArray());
                }
                else
                {
                    buffer.Add(bytes.Take(size).ToArray());
                }
                result.Message = buffer.GetMessage();
            }

            return result;
        }

        /// <summary>
        /// To accept a connection from localhost only
        /// </summary>
        /// <returns></returns>
        private Socket AcceptLocalConnection()
        {
            Socket handlerSocket = null;
            while (true)
            {
                Task<Socket> acceptTask = tcpListener.AcceptSocketAsync();

                try
                {
                    acceptTask.Wait(cancellationToken.Token);

                    if (!acceptTask.IsCanceled) {
                        handlerSocket = acceptTask.Result;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var endPoint = handlerSocket.RemoteEndPoint as IPEndPoint;
                if (endPoint != null && 
                    string.CompareOrdinal(endPoint.Address.ToString(), CommonData.LocalIPAddress) == 0)
                {
                    break;
                }
                handlerSocket.Close();
            }

            return handlerSocket;
        }
    }
}
