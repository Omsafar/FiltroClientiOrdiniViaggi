using System.Windows;
using System.Windows.Controls;
using ElencoAnalyzer.ViewModels;

namespace ElencoAnalyzer.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void ListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Doppio click su un suggerimento => imposta QueryText e lancia Analizza
                if (sender is ListBox lb && lb.SelectedItem is string s)
                {
                    vm.QueryText = s;
                    if (vm.AnalyzeCommand.CanExecute(null))
                        vm.AnalyzeCommand.Execute(null);
                }
            }
        }
    }
}
