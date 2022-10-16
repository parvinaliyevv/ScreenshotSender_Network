namespace ScreenshotSender.Client.ViewModels;

public class MainViewModel: DependencyObject
{
    public const int throughputLength = 50_000;

    public TcpClient ConnectionClient { get; set; }

    public bool IsConnected { get; set; }
    public bool TryConnection { get; set; }

    public int ConnectionPort { get; init; }
    public int SendScreenshotPort { get; init; }

    public byte[] Image
    {
        get { return (byte[])GetValue(ImageProperty); }
        set { SetValue(ImageProperty, value); }
    }
    public static readonly DependencyProperty ImageProperty =
        DependencyProperty.Register("Image", typeof(byte[]), typeof(MainViewModel));

    public string IpAddressValue
    {
        get { return (string)GetValue(IpAddressValueProperty); }
        set { SetValue(IpAddressValueProperty, value); }
    }
    public static readonly DependencyProperty IpAddressValueProperty =
        DependencyProperty.Register("IpAddressValue", typeof(string), typeof(MainViewModel));

    public bool IpAddressIsEnabled
    {
        get { return (bool)GetValue(IpAddressIsEnabledProperty); }
        set { SetValue(IpAddressIsEnabledProperty, value); }
    }
    public static readonly DependencyProperty IpAddressIsEnabledProperty =
        DependencyProperty.Register("IpAddressIsEnabled", typeof(bool), typeof(MainViewModel));

    public RelayCommand ConnectToServerCommand { get; set; }
    public RelayCommand DisConnectServerCommand { get; set; }


    public MainViewModel()
    {
        ConnectionClient = new TcpClient();

        IsConnected = false;
        IpAddressValue = string.Empty;
        IpAddressIsEnabled = true;

        ConnectionPort = int.Parse(ConfigurationManager.AppSettings["ConnectionPortNumber"]);
        SendScreenshotPort = int.Parse(ConfigurationManager.AppSettings["SendScreenshotPortNumber"]);

        ConnectToServerCommand = new(_ => ConnectToServerAsync(), _ => !string.IsNullOrWhiteSpace(IpAddressValue) && !IsConnected && !TryConnection);
        DisConnectServerCommand = new(_ => DisConnectServerAsync(), _ => IsConnected);

        var timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (sender, e) => CommandManager.InvalidateRequerySuggested();
        timer.Start();
    }


    public void ConnectToServer()
    {
        TryConnection = true;
        IPAddress address;

        try
        {
            var ipAddress = Application.Current.Dispatcher.Invoke(() => IpAddressValue);
            address = IPAddress.Parse(ipAddress);
        }
        catch
        {
            MessageBox.Show("Invalid address!", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return;
        }

        var stopWatch = new Stopwatch();

        stopWatch.Start();
        while (!IsConnected)
        {
            try
            {
                ConnectionClient.Connect(new IPEndPoint(address, ConnectionPort));
                IsConnected = true;
            }
            catch { }

            if (stopWatch.ElapsedMilliseconds > 5000)
            {
                TryConnection = false;
                MessageBox.Show("Failed to connect to server!", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
        }
        stopWatch.Stop();

        var stream = ConnectionClient.GetStream();
        var bytes = Encoding.Default.GetBytes("HasConnect");

        try
        {
            stream.Write(bytes, 0, bytes.Length);
        }
        catch { }

        TcpReceiveFromServerAsync();
        UdpReceiveFromServerAsync();

        Application.Current.Dispatcher.Invoke(() => IpAddressIsEnabled = false);
        MessageBox.Show("Successful connection to the server!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    public async Task ConnectToServerAsync() => await Task.Factory.StartNew(() => ConnectToServer());

    public void DisConnectServer()
    {
        if (!IsConnected) return;

        var stream = ConnectionClient.GetStream();
        var bytes = Encoding.Default.GetBytes("DisConnect");

        try
        {
            stream.Write(bytes, 0, bytes.Length);
            ConnectionClient.Close();
            ConnectionClient.Dispose();
        }
        catch { }

        ConnectionClient = new TcpClient();
        IsConnected = false;
        TryConnection = false;

        Application.Current.Dispatcher.Invoke(() =>
        {
            Image = null;
            IpAddressIsEnabled = true;
        });
    }
    public async Task DisConnectServerAsync() => await Task.Factory.StartNew(() => DisConnectServer());

    private void TcpReceiveFromServer()
    {
        var stream = ConnectionClient.GetStream();
        string msg = string.Empty; 
        byte[] bytes = null;
        int length = default;

        while (true)
        {
            if (stream.DataAvailable)
            {
                bytes = new byte[100];

                length = stream.Read(bytes, 0, bytes.Length);
                msg = Encoding.Default.GetString(bytes, 0, length);
                
                if (msg.Contains("ServerDisConnect"))
                {
                    MessageBox.Show("Server is down!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    DisConnectServerAsync();
                    break;
                }
            }
        }
    }
    private async Task TcpReceiveFromServerAsync() => await Task.Factory.StartNew(() => TcpReceiveFromServer());

    private void UdpReceiveFromServer()
    {
        if (!IsConnected) return;

        using var client = new UdpClient(SendScreenshotPort);
        var endPoint = new IPEndPoint(IPAddress.Any, 0);

        byte[]? bytes = null;
        var list = new List<byte>();
        NetworkStream stream = null;

        while (true)
        {
            bytes = client.Receive(ref endPoint);
            list.AddRange(bytes);

            if (bytes.Length != throughputLength)
            {
                Application.Current.Dispatcher.Invoke(() =>  Image = list.ToArray());

                try
                {
                    stream = ConnectionClient.GetStream();
                    bytes = Encoding.Default.GetBytes("HasConnect");

                    stream.Write(bytes, 0, bytes.Length);
                }
                catch { }

                list.Clear();
            }

            if (!IsConnected) break;
        }
    }
    private async Task UdpReceiveFromServerAsync() => await Task.Factory.StartNew(() => UdpReceiveFromServer());
}
