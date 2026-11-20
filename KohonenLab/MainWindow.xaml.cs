using System.Windows;
using Microsoft.Win32;
using KohonenLab.ViewModels;

namespace KohonenLab;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Текстовые файлы данных (*.txt)|*.txt|Все файлы (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
            vm.DataPath = dialog.FileName;
    }
}
