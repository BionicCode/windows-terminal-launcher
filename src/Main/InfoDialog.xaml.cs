namespace Main;

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

/// <summary>
/// Interaction logic for InfoDialog.xaml
/// </summary>
public partial class InfoDialog : Window
{
    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header ), 
        typeof(string), 
        typeof(InfoDialog), 
        new PropertyMetadata("Test"));
    public string Body { get => (string)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body),
        typeof(string),
        typeof(InfoDialog),
        new PropertyMetadata(string.Empty));

    public InfoDialog()
    {
        InitializeComponent();

        CommandBinding okCommandBinding = new CommandBinding(ApplicationCommands.Close, OkCommandExecuted, CanExecuteOkCommand);
        _ = CommandBindings.Add(okCommandBinding);
    }

    private void CanExecuteOkCommand(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void OkCommandExecuted(object sender, ExecutedRoutedEventArgs e) => Close();
}