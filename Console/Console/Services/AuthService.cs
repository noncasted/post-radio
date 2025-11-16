using Microsoft.JSInterop;

namespace Console.Services;

public interface IAuthService
{
    Task<bool> IsAuthenticated();
    Task<bool> ValidateToken(string token);
    Task Logout();
    string? GetToken();
}

public class AuthService : IAuthService
{
    public AuthService(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;

        // Получаем токен из переменной окружения, либо из конфигурации
        _validToken = Environment.GetEnvironmentVariable("AuthToken") ??
                      configuration["AuthToken"] ?? "default-secret-token";
    }

    private readonly IJSRuntime _jsRuntime;
    private readonly string _validToken;
    private string? _currentToken;
    private bool _isInitialized;

    public async Task<bool> IsAuthenticated()
    {
        if (_isInitialized == false)
            await InitializeAsync();

        return !string.IsNullOrEmpty(_currentToken);
    }

    public async Task<bool> ValidateToken(string token)
    {
        if (token == _validToken)
        {
            _currentToken = token;
            await SaveTokenAsync(token);
            return true;
        }

        return false;
    }

    public async Task Logout()
    {
        _currentToken = null;
        await RemoveTokenAsync();
    }

    public string? GetToken()
    {
        return _currentToken;
    }

    private async Task InitializeAsync()
    {
        try
        {
            _currentToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");

            // Проверяем, что токен валидный
            if (!string.IsNullOrEmpty(_currentToken) && _currentToken != _validToken)
            {
                _currentToken = null;
                await RemoveTokenAsync();
            }
        }
        catch
        {
            _currentToken = null;
        }
        finally
        {
            _isInitialized = true;
        }
    }

    private async Task SaveTokenAsync(string token)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        }
        catch
        {
            // Ignore errors
        }
    }

    private async Task RemoveTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
        }
        catch
        {
            // Ignore errors
        }
    }
}