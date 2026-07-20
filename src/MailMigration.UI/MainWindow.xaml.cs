using System.Windows;

namespace MailMigration.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); this.viewModel = viewModel; DataContext = viewModel;
        Closing += (_, _) => { if (viewModel.CancelCommand.CanExecute(null)) viewModel.CancelCommand.Execute(null); };
    }
    private void Test_Click(object sender, RoutedEventArgs e)
    {
        viewModel.SourcePassword = SourcePassword.Password; viewModel.TargetPassword = TargetPassword.Password;
        if (viewModel.TestCommand.CanExecute(null)) viewModel.TestCommand.Execute(null);
    }
}
