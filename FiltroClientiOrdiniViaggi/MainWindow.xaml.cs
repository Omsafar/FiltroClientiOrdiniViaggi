using System.Windows;
using System.Windows.Controls;
using FiltroClientiOrdiniViaggi.ViewModels;

namespace FiltroClientiOrdiniViaggi;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void ListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ListBox lb && lb.SelectedItem is string selected)
        {
            vm.QueryText = selected;
            if (vm.AnalyzeCommand.CanExecute(null))
            {
                vm.AnalyzeCommand.Execute(null);
            }
        }
    }
}
