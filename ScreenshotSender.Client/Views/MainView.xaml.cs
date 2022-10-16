namespace ScreenshotSender.Client.Views;

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }


    private async void AppClose_ButtonClicked(object sender, RoutedEventArgs e)
    {
        await (DataContext as MainViewModel)?.DisConnectServerAsync();
        Close();
    }

    private void DragWindow_MouseDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void MinimizeWindow_ButtonClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
}
