using System.Windows;
using System.Windows.Media;

namespace IndustrialDAQ.UI.Views;

public partial class FramelessDialogWindow : Window, IDialogWindow
{
    public IDialogResult Result { get; set; } = new DialogResult();

    public FramelessDialogWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        
        // Let Prism manage the content fully
    }
}
