using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TcpServer.DataObjects;
using TcpServer.Writer;

namespace TcpServer.Server
{
    public class AsyncTcpServer
    {
        private ManualResetEvent m_done = new ManualResetEvent(false);
        private int m_serverPort;
        private IPAddress m_serverIpAddress;
        private IAsyncWriter m_asynchWriter;
        private static int msg = 0;
        private volatile bool m_stopServer;
        public AsyncTcpServer(IPAddress serverIpAddress, int serverPort, IAsyncWriter asynchWriter)
        {
            m_serverIpAddress = serverIpAddress;
            m_serverPort = serverPort;
            m_asynchWriter = asynchWriter;
        }

        public AsyncTcpServer()
        {
            m_serverIpAddress = IPAddress.Loopback;
            m_serverPort = 7777;
            m_asynchWriter = new AsyncFileWriter("traffic.txt");
        }

        public void StartListening()
        {
            // Data buffer for incoming data.  
            IPEndPoint localEndPoint = new IPEndPoint(m_serverIpAddress, m_serverPort);

            // Create a TCP/IP socket.  
            using (Socket listener = new Socket(m_serverIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(100000);
                    Console.WriteLine(string.Format("Server started on {0}:{1} and listening ...", m_serverIpAddress, m_serverPort));
                    Console.WriteLine("Press Q to stop the server at any time ...");
                    while (!m_stopServer)
                    {
                        // Set the event to nonsignaled state.  
                        m_done.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        listener.BeginAccept(AcceptCallback, listener);

                        // Wait until a connection is made before continuing.  
                        m_done.WaitOne();
                    }
                    Console.WriteLine("\nServer stopped.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public void StopListening()
        {
            m_stopServer = true;
            m_done.Set();
            m_asynchWriter.Dispose();
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                // Signal the main thread to continue.  
                m_done.Set();

                // Get the socket that handles the client request.  
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.  
                StateObject state = new StateObject { workSocket = handler };
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
            }
            catch (Exception ex)
            {
                // If we stop the server socket won't be accessible since it is disposed!
                if ((ex is ObjectDisposedException) && (m_stopServer))
                    return;
                throw;
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.   
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                content = state.sb.ToString();
                msg++;
                Trace.WriteLine(string.Format("{0} --- {1}", msg, content));
                m_asynchWriter.WriteLine(content);
            }
        }
    }
}
