using System;

namespace TcpServer.Writer
{
    // Async data writer interface
    public interface IAsyncWriter : IDisposable
    {
        void Write(string str);
        void WriteLine(string str);
    }
}
