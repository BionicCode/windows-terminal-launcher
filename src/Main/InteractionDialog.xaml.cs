namespace Main;

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using YamlDotNet.Core.Tokens;

/// <summary>
/// Interaction logic for InteractionDialog.xaml
/// </summary>
public partial class InteractionDialog : Window
{
    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(string),
        typeof(InteractionDialog),
        new PropertyMetadata(string.Empty));
    public string Body { get => (string)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body),
        typeof(string),
        typeof(InteractionDialog),
        new PropertyMetadata(string.Empty));

    public InteractionDialog()
    {
        InitializeComponent();

        CommandBinding okCommandBinding = new CommandBinding(ApplicationCommands.Close, OkCommandExecuted, CanExecuteOkCommand);
        _ = CommandBindings.Add(okCommandBinding);
    }

    private void CanExecuteOkCommand(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void OkCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
