using Solvix.Client.Core.Interfaces;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.Core.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ISecureStorageService _secureStorageService;
        private readonly IConnectivityService _connectivityService;
        private readonly IToastService _toastService;
        private readonly ILogger<ApiService> _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public ApiService(
            ISecureStorageService secureStorageService,
            IConnectivityService connectivityService,
            IToastService toastService,
            ILogger<ApiService> logger)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Constants.ApiUrl),
                Timeout = TimeSpan.FromSeconds(30) // Setting a reasonable timeout
            };

            // In DEBUG, allow self-signed certificates for localhost
#if DEBUG
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(Constants.ApiUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
#endif

            _secureStorageService = secureStorageService;
            _connectivityService = connectivityService;
            _toastService = toastService;
            _logger = logger;

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public async Task<T?> GetAsync<T>(string endpoint, bool requiresAuth = true)
        {
            return await GetAsync<T>(endpoint, null, requiresAuth);
        }

        public async Task<T?> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams, bool requiresAuth = true)
        {
            try
            {
                if (!_connectivityService.IsConnected)
                {
                    _logger.LogWarning("Cannot make API request. No internet connection");

                    // Only show toast in UI contexts, not during app startup
                    if (MainThread.IsMainThread)
                    {
                        await _toastService.ShowToastAsync("No internet connection", ToastType.Error);
                    }

#if DEBUG
                    // In debug mode, for certain endpoints, return mock data
                    if (typeof(T).Name.Contains("ChatModel") || endpoint.Contains("chat"))
                    {
                        _logger.LogInformation("Using mock data for {Type} in DEBUG mode", typeof(T).Name);
                        return default;
                    }
#endif

                    return default;
                }

                var requestUri = endpoint;
                if (queryParams != null && queryParams.Count > 0)
                {
                    var queryString = string.Join("&", queryParams.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
                    requestUri = $"{endpoint}?{queryString}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                _logger.LogInformation("Making GET request to {Endpoint}", requestUri);

                if (requiresAuth)
                {
                    var token = await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogWarning("Authentication required but no token available");

                        // Only show toast in UI contexts, not during app startup
                        if (MainThread.IsMainThread)
                        {
                            await _toastService.ShowToastAsync("Authentication required", ToastType.Error);
                        }
                        return default;
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.SendAsync(request);
                _logger.LogInformation("Received response: {StatusCode} for GET {Endpoint}",
                    response.StatusCode, requestUri);

                return await HandleResponse<T>(response);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Request to {Endpoint} timed out", endpoint);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Request timed out. Please try again.", ToastType.Error);
                }
                return default;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error in GET request to {Endpoint}: {Message}", endpoint, ex.Message);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Server connection failed. Please check your connection.", ToastType.Error);
                }
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GET request to {Endpoint}", endpoint);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Network error occurred. Please try again.", ToastType.Error);
                }
                return default;
            }
        }

        public async Task<T?> PostAsync<T>(string endpoint, object data, bool requiresAuth = true)
        {
            try
            {
                if (!_connectivityService.IsConnected)
                {
                    _logger.LogWarning("Cannot make API request. No internet connection");

                    // Only show toast in UI contexts, not during app startup
                    if (MainThread.IsMainThread)
                    {
                        await _toastService.ShowToastAsync("No internet connection", ToastType.Error);
                    }
                    return default;
                }

                var jsonContent = JsonSerializer.Serialize(data, _serializerOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

                _logger.LogInformation("Making POST request to {Endpoint}", endpoint);

                if (requiresAuth)
                {
                    var token = await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogWarning("Authentication required but no token available");

                        // Only show toast in UI contexts, not during app startup
                        if (MainThread.IsMainThread)
                        {
                            await _toastService.ShowToastAsync("Authentication required", ToastType.Error);
                        }
                        return default;
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.SendAsync(request);
                _logger.LogInformation("Received response: {StatusCode} for POST {Endpoint}",
                    response.StatusCode, endpoint);

                return await HandleResponse<T>(response);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Request to {Endpoint} timed out", endpoint);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Request timed out. Please try again.", ToastType.Error);
                }
                return default;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error in POST request to {Endpoint}: {Message}", endpoint, ex.Message);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Server connection failed. Please check your connection.", ToastType.Error);
                }
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in POST request to {Endpoint}", endpoint);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Network error occurred. Please try again.", ToastType.Error);
                }
                return default;
            }
        }

        public async Task<T?> PutAsync<T>(string endpoint, object data, bool requiresAuth = true)
        {
            // Implementation follows similar pattern to PostAsync, omitted for brevity
            try
            {
                if (!_connectivityService.IsConnected)
                {
                    _logger.LogWarning("Cannot make API request. No internet connection");

                    // Only show toast in UI contexts, not during app startup
                    if (MainThread.IsMainThread)
                    {
                        await _toastService.ShowToastAsync("No internet connection", ToastType.Error);
                    }
                    return default;
                }

                var jsonContent = JsonSerializer.Serialize(data, _serializerOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = content };

                _logger.LogInformation("Making PUT request to {Endpoint}", endpoint);

                if (requiresAuth)
                {
                    var token = await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogWarning("Authentication required but no token available");

                        // Only show toast in UI contexts, not during app startup
                        if (MainThread.IsMainThread)
                        {
                            await _toastService.ShowToastAsync("Authentication required", ToastType.Error);
                        }
                        return default;
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.SendAsync(request);
                _logger.LogInformation("Received response: {StatusCode} for PUT {Endpoint}",
                    response.StatusCode, endpoint);

                return await HandleResponse<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PUT request to {Endpoint}", endpoint);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Network error occurred. Please try again.", ToastType.Error);
                }
                return default;
            }
        }

        public async Task<T?> DeleteAsync<T>(string endpoint, bool requiresAuth = true)
        {
            // Implementation follows similar pattern to GetAsync, omitted for brevity
            try
            {
                if (!_connectivityService.IsConnected)
                {
                    _logger.LogWarning("Cannot make API request. No internet connection");

                    // Only show toast in UI contexts, not during app startup
                    if (MainThread.IsMainThread)
                    {
                        await _toastService.ShowToastAsync("No internet connection", ToastType.Error);
                    }
                    return default;
                }

                var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                _logger.LogInformation("Making DELETE request to {Endpoint}", endpoint);

                if (requiresAuth)
                {
                    var token = await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogWarning("Authentication required but no token available");

                        // Only show toast in UI contexts, not during app startup
                        if (MainThread.IsMainThread)
                        {
                            await _toastService.ShowToastAsync("Authentication required", ToastType.Error);
                        }
                        return default;
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.SendAsync(request);
                _logger.LogInformation("Received response: {StatusCode} for DELETE {Endpoint}",
                    response.StatusCode, endpoint);

                return await HandleResponse<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DELETE request to {Endpoint}", endpoint);

                // Only show toast in UI contexts, not during app startup
                if (MainThread.IsMainThread)
                {
                    await _toastService.ShowToastAsync("Network error occurred. Please try again.", ToastType.Error);
                }
                return default;
            }
        }

        private async Task<T?> HandleResponse<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Response content: {Content}", content);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    // Intenta deserializar como ApiResponse<T>
                    var apiResponse = JsonSerializer.Deserialize<Models.ApiResponse<T>>(content, _serializerOptions);

                    if (apiResponse != null)
                    {
                        if (!string.IsNullOrEmpty(apiResponse.Message) && MainThread.IsMainThread)
                        {
                            var toastType = apiResponse.Success ? ToastType.Success : ToastType.Error;
                            await _toastService.ShowToastAsync(apiResponse.Message, toastType);
                        }

                        if (apiResponse.Success && apiResponse.Data != null)
                        {
                            return apiResponse.Data;
                        }
                        else if (!apiResponse.Success)
                        {
                            _logger.LogWarning("API returned success=false with message: {Message}",
                                apiResponse.Message);
                            return default;
                        }
                    }

                    // Si ApiResponse deserialización falla o data es null, intenta deserialización directa
                    var result = JsonSerializer.Deserialize<T>(content, _serializerOptions);
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization error for content: {Content}", content);

                    // Deserialización directa si estructura ApiResponse no está presente
                    try
                    {
                        return JsonSerializer.Deserialize<T>(content, _serializerOptions);
                    }
                    catch
                    {
                        _logger.LogError("Failed to deserialize response as {Type}", typeof(T).Name);

                        // Solo muestra toast en contextos de UI, no durante inicio de app
                        if (MainThread.IsMainThread)
                        {
                            await _toastService.ShowToastAsync("Error processing data from server", ToastType.Error);
                        }
                        return default;
                    }
                }
            }
            else
            {
                // Manejar respuestas de error
                try
                {
                    // Intentar deserializar mensaje de error
                    var errorResponse = JsonSerializer.Deserialize<Models.ApiResponse<object>>(content, _serializerOptions);
                    if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                    {
                        _logger.LogWarning("API error: {StatusCode} - {Message}", response.StatusCode, errorResponse.Message);

                        // Solo muestra toast en contextos de UI, no durante inicio de app
                        if (MainThread.IsMainThread)
                        {
                            await _toastService.ShowToastAsync(errorResponse.Message, ToastType.Error);
                        }
                        return default;
                    }
                }
                catch (JsonException)
                {
                    // Si error no puede ser deserializado, muestra error genérico
                    _logger.LogWarning("API error: {StatusCode}", response.StatusCode);
                    var errorMessage = $"Server error: {response.StatusCode}";

                    // Mostrar mensajes específicos para códigos de estado comunes
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        errorMessage = "Authentication failed. Please log in again.";
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        errorMessage = "The requested resource was not found.";
                    }
                    else if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        errorMessage = "Invalid request. Please check your input.";
                    }

                    // Solo muestra toast en contextos de UI, no durante inicio de app
                    if (MainThread.IsMainThread)
                    {
                        await _toastService.ShowToastAsync(errorMessage, ToastType.Error);
                    }
                }

                return default;
            }
        }
    }
}