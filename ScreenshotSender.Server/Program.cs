using System;
using System.Net;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ScreenshotSender.Server
{
    public static class Program
    {
        public const int throughputLength = 50_000;
        public const int ReSendMilliseconds = 3000;
        public const int MaxWarningCount = 7;

        public static TcpListener ServerListener { get; set; }
        public static List<ServerClient> ConnectedClients { get; set; }

        public static IPAddress LocalAddress { get; }
        public static int ConnectionPort { get; }
        public static int SendScreenshotPort { get; }


        static Program()
        {
            LocalAddress = IPAddress.Parse(ConfigurationManager.AppSettings["IpAddress"]);
            ConnectionPort = int.Parse(ConfigurationManager.AppSettings["ConnectionPortNumber"]);
            SendScreenshotPort = int.Parse(ConfigurationManager.AppSettings["SendScreenshotPortNumber"]);

            ConnectedClients = new List<ServerClient>();
            ServerListener = new TcpListener(new IPEndPoint(LocalAddress, ConnectionPort));

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                NetworkStream stream = null;
                byte[] bytes = null;

                foreach (var item in ConnectedClients)
                {
                    try
                    {
                        stream = item.ClientSocket.GetStream();
                        bytes = Encoding.Default.GetBytes("ServerDisConnect");

                        stream.Write(bytes, 0, bytes.Length);
                    }
                    catch { }
                }

                ConnectedClients.ForEach(item => item.ClientSocket.Dispose());
            };
        }


        private static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            try
            {
                ServerListener.Start();
            }
            catch
            {
                Console.WriteLine("Failed to start server!"); return;
            }

            AcceptTcpClientsAsync();

            while (true)
            {
                SendScreenshotToClientsAsync();
                Thread.Sleep(ReSendMilliseconds);
            }
        }

        private static byte[] GetWindowScreenshot()
        {
            var image = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

            using (Graphics g = Graphics.FromImage(image))
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
            }

            return (byte[])new ImageConverter().ConvertTo(image, typeof(byte[]));
        }

        private static List<ServerClient> CheckConnectedClients()
        {
            var disConnectClients = new List<ServerClient>();

            NetworkStream stream = null;
            string msg = string.Empty; 
            byte[] bytes = null;
            int length = default;

            foreach (var item in ConnectedClients)
            {
                stream = item.ClientSocket.GetStream();

                if (!stream.DataAvailable)
                {
                    if (item.Counter >= MaxWarningCount) disConnectClients.Add(item);
                    else item.Counter++;
                    continue;
                }

                bytes = new byte[100];
                length = stream.Read(bytes, 0, bytes.Length);
                msg = Encoding.Default.GetString(bytes, 0, length);

                if (msg == "HasConnect") item.Counter = default;
                else if (msg.Contains("DisConnect")) disConnectClients.Add(item);
            }

            var time = DateTime.Now;

            ConnectedClients.RemoveAll(item => disConnectClients.Contains(item));
            disConnectClients.ForEach(item =>
            {
                Console.WriteLine("{0} --- {1}: Connection aborted!", time, item.ClientSocket.Client.RemoteEndPoint);
                item.ClientSocket.Dispose();
            });

            Console.WriteLine();
            return new List<ServerClient>(ConnectedClients);
        }


        private static void AcceptTcpClients()
        {
            while (true)
            {
                var client = ServerListener.AcceptTcpClient();
                ConnectedClients.Add(new ServerClient(client));

                Console.WriteLine("{0} --- {1}: Connected!", DateTime.Now, client.Client.RemoteEndPoint);
            }
        }

        private static async Task AcceptTcpClientsAsync() => await Task.Factory.StartNew(() => AcceptTcpClients());

        private static void SendScreenshotToClients()
        {
            if (ConnectedClients.Count == 0)
            {
                Console.WriteLine("No connected clients."); return;
            }

            var task = Task<List<ServerClient>>.Factory.StartNew(() => CheckConnectedClients());

            var imgBytes = GetWindowScreenshot().ToList();

            var clients = task.Result;

            if (clients.Count == 0)
            {
                Console.WriteLine("No connected clients."); return;
            }

            using (var client = new UdpClient())
            {
                IPAddress address = null;
                byte[] bytes = null;

                while (true)
                {
                    bytes = imgBytes.Take(throughputLength).ToArray();

                    foreach (var item in clients)
                    {
                        address = IPAddress.Parse(item.ClientSocket.Client.RemoteEndPoint.ToString().Split(':')[0]);

                        client.Send(bytes, bytes.Length, new IPEndPoint(address, SendScreenshotPort));
                    }

                    if (bytes.Length != throughputLength)
                    {
                        string report = string.Empty;
                        var time = DateTime.Now;

                        foreach (var item in clients)
                        {
                            if (item.Counter != default) report = $"Failed to receive! {item.Counter} warning out of {MaxWarningCount}.";
                            else report = "Successfully received!";

                            Console.WriteLine("{0} --- {1}: {2}", time, item.ClientSocket.Client.RemoteEndPoint, report);
                        }

                        break;
                    }

                    imgBytes = imgBytes.Skip(throughputLength).TakeWhile(item => true).ToList();
                }

                Console.WriteLine();
            };
        }

        private static async Task SendScreenshotToClientsAsync() => await Task.Factory.StartNew(() => SendScreenshotToClients());
    }
}
