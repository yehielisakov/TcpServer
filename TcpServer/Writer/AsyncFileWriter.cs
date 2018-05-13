using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

// Async file writer: separate thread writes to file in chunks (to make less IO), while
// user inserts data into blocking collection. If one has enough data in a chunk it is written immediately,
// else one waits a bit, then writes whatever there is to write to file.

namespace TcpServer.Writer
{
    public class AsyncFileWriter : IAsyncWriter
    {
        private StreamWriter m_streamWriter;
        private string m_filePath;
        private readonly Thread m_writeThread;
        private readonly BlockingCollection<string> m_messages = new BlockingCollection<string>();
        private volatile bool m_stopWriteThread;
        private volatile bool m_isDisposing;
        private int m_messageBufferSize;


        public AsyncFileWriter(string filePath, int? messageBufferSize = null)
        {
            m_filePath = filePath;
            m_messageBufferSize = messageBufferSize ?? 100;
            m_writeThread = new Thread(WriteToFile);
            m_writeThread.Start();
        }

        public void Write(string str)
        {
            if (!m_isDisposing)
                m_messages.Add(str);
        }

        public void WriteLine(string str)
        {
            if (!m_isDisposing)
                m_messages.Add(str + Environment.NewLine);
        }

        private void InitializeStreamWriter()
        {
            if (File.Exists(m_filePath))
            {
                File.WriteAllText(m_filePath, String.Empty);
            }
            m_streamWriter = File.AppendText(m_filePath);
        }

        private void WriteToFile()
        {
            if (m_streamWriter == null)
                InitializeStreamWriter();
            int msgCounter = 0;
            StringBuilder chunk = new StringBuilder();
            Stopwatch timeout = new Stopwatch();
            while (!m_stopWriteThread)
            {
                foreach (var msg in m_messages.GetConsumingEnumerable())
                {
                    chunk.Append(msg);
                    msgCounter++;
                    if (msgCounter == m_messageBufferSize)
                    {
                        m_streamWriter.Write(chunk.ToString());
                        m_streamWriter.Flush();
                        msgCounter = 0;
                        chunk.Clear();
                        continue;
                    }
                    if (m_messages.Count == 0)
                    {
                        timeout.Start();
                        while (m_messages.Count < m_messageBufferSize - msgCounter)
                        {
                            if (timeout.ElapsedMilliseconds >= 5000)
                            {
                                timeout.Reset();
                                m_streamWriter.Write(chunk.ToString());
                                m_streamWriter.Flush();
                                msgCounter = 0;
                                chunk.Clear();
                                break;
                            }

                        }
                    }
                }
            }

        }

        public void Dispose()
        {
            m_isDisposing = true;
            m_messages.CompleteAdding();
            m_stopWriteThread = true;
            m_writeThread.Join();
            m_messages.Dispose();
            m_streamWriter.Flush();
            m_streamWriter.Dispose();
            m_streamWriter.Close();
        }

        
    }
}
