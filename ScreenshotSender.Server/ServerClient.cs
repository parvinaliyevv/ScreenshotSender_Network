using System.Net.Sockets;

namespace ScreenshotSender.Server
{
    public class ServerClient
    {
        public TcpClient ClientSocket { get; set; }

        public byte Counter { get; set; }


        public ServerClient(TcpClient clientSocket)
        {
            ClientSocket = clientSocket;
            Counter = default;
        }

    }
}
