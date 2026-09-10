using System.Windows.Controls;
using System.Windows.Input;
using Questlog.ViewModels;

namespace Questlog.Views;

public partial class CoachView : UserControl
{
    public CoachView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is CoachViewModel vm)
            {
                vm.Messages.CollectionChanged += (_, _) =>
                {
                    Dispatcher.InvokeAsync(() => ChatScroll.ScrollToEnd());
                };
            }
        };
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is CoachViewModel vm)
        {
            if (vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}
