using System.Windows;
using Questlog.ViewModels;

namespace Questlog;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}