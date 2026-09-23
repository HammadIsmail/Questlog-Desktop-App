using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Services;

namespace Questlog.ViewModels;

public class AuthViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    private bool _isSignUpMode;
    public bool IsSignUpMode
    {
        get => _isSignUpMode;
        set
        {
            if (SetProperty(ref _isSignUpMode, value))
            {
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(TogglePromptText));
                OnPropertyChanged(nameof(ToggleActionText));
                ErrorMessage = string.Empty;
            }
        }
    }

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _confirmPassword = string.Empty;
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string SubmitButtonText => IsSignUpMode ? "Create Adventurer Account" : "Enter the Realm (Sign In)";
    public string TogglePromptText => IsSignUpMode ? "Already have an account?" : "New to Questlog?";
    public string ToggleActionText => IsSignUpMode ? "Sign In" : "Create Account";

    public ICommand SubmitCommand { get; }
    public ICommand ToggleModeCommand { get; }
    public ICommand QuickStartCommand { get; }

    public event Action? Authenticated;

    public AuthViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;

        SubmitCommand = new AsyncRelayCommand(SubmitAsync);
        ToggleModeCommand = new RelayCommand(() => IsSignUpMode = !IsSignUpMode);
        QuickStartCommand = new AsyncRelayCommand(QuickStartAsync);
    }

    public async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Please enter a valid email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters.";
            return;
        }

        if (IsSignUpMode)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Please enter your adventurer name.";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }
        }

        IsLoading = true;
        try
        {
            if (IsSignUpMode)
            {
                var reg = await _apiClient.RegisterAsync(Email.Trim(), Name.Trim(), Password);
                if (reg != null)
                {
                    Authenticated?.Invoke();
                }
                else
                {
                    ErrorMessage = "Registration failed. Email may already be registered or backend unreachable.";
                }
            }
            else
            {
                var login = await _apiClient.LoginAsync(Email.Trim(), Password);
                if (login != null)
                {
                    Authenticated?.Invoke();
                }
                else
                {
                    ErrorMessage = "Invalid email or password, or server is offline.";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Authentication error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task QuickStartAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            bool ok = await _apiClient.QuickLoginGuestAsync();
            if (ok)
            {
                Authenticated?.Invoke();
            }
            else
            {
                ErrorMessage = "Could not connect to backend server. Please verify your internet connection.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
