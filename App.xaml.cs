using System.Text;

public partial class App : Application
{
    public App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        InitializeComponent();
    }
}