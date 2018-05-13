using System;
using System.Threading;
using TcpServer.Server;

namespace TcpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new AsyncTcpServer();
            Thread serverThread = new Thread(server.StartListening);
            serverThread.Start();
            ConsoleKeyInfo cki = Console.ReadKey(true);
            if (cki.Key == ConsoleKey.Q)
            {
                server.StopListening();
                serverThread.Join();
            }
        }
    }
}
