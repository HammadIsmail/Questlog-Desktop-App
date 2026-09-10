using System.Windows;
using System.Windows.Controls;
using Questlog.ViewModels;

namespace Questlog.Views;

public partial class AuthView : UserControl
{
    public AuthView()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel vm && sender is PasswordBox pb)
        {
            vm.Password = pb.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel vm && sender is PasswordBox pb)
        {
            vm.ConfirmPassword = pb.Password;
        }
    }
}
