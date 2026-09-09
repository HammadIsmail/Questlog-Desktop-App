using System.Threading.Tasks;
using Questlog.Common;

namespace Questlog.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;
}
