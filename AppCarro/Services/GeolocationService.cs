using System.Net.Http;
using System.Net.Http.Json; // Necesitarás el paquete System.Net.Http.Json
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors; // Para la clase Location
using System.Diagnostics;
using System.Text.Json; 

namespace AppCarro.Services
{
    // Clase para deserializar la respuesta JSON de ip-api.com
    public class IpApiResponse
    {
        public string Status { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Query { get; set; } // La IP que se consultó
        public string Country { get; set; }
        public string City { get; set; }
        public string Isp { get; set; }
        public string Message { get; set; } // En caso de error en la API
    }

    public class GeolocationService
    {
        private readonly HttpClient _httpClient;

        public GeolocationService()
        {
            _httpClient = new HttpClient();
            // Establecer un User-Agent es buena práctica
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AppCarroMauiClient/1.0");
        }

        /// <summary>
        /// Obtiene la ubicación geográfica (latitud y longitud) a partir de una dirección IP pública.
        /// </summary>
        /// <param name="ipAddress">La dirección IP pública a consultar.</param>
        /// <returns>Un objeto Location si tiene éxito, null en caso contrario.</returns>
        public async Task<Location> GetLocationFromIpAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                Debug.WriteLine("[GeolocationService] Error: La dirección IP no puede estar vacía.");
                return null;
            }

            // Validar si es una IP válida (opcional, pero recomendado)
            // if (!System.Net.IPAddress.TryParse(ipAddress, out _))
            // {
            //     Debug.WriteLine($"[GeolocationService] Error: La dirección IP '{ipAddress}' no es válida.");
            //     return null;
            // }

            // La API de ip-api.com. Documentación: https://ip-api.com/docs/api:json
            string apiUrl = $"http://ip-api.com/json/{ipAddress}?fields=status,message,lat,lon,query,country,city,isp";

            try
            {
                Debug.WriteLine($"[GeolocationService] Consultando API: {apiUrl}");
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    IpApiResponse apiResponse = await response.Content.ReadFromJsonAsync<IpApiResponse>();

                    if (apiResponse != null && apiResponse.Status == "success")
                    {
                        Debug.WriteLine($"[GeolocationService] IP: {apiResponse.Query}, Lat: {apiResponse.Lat}, Lon: {apiResponse.Lon}, Ciudad: {apiResponse.City}, País: {apiResponse.Country}, ISP: {apiResponse.Isp}");
                        return new Location(apiResponse.Lat, apiResponse.Lon);
                    }
                    else
                    {
                        Debug.WriteLine($"[GeolocationService] Error de la API ip-api.com: {apiResponse?.Message ?? "Respuesta desconocida."} para IP: {ipAddress}");
                        return null;
                    }
                }
                else
                {
                    Debug.WriteLine($"[GeolocationService] Error al llamar a la API ip-api.com. Código: {response.StatusCode}. IP: {ipAddress}");
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[GeolocationService] Contenido del error: {errorContent}");
                    return null;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"[GeolocationService] Excepción de HttpRequest al obtener ubicación para IP {ipAddress}: {httpEx.Message}");
                return null;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"[GeolocationService] Excepción de JSON al procesar respuesta para IP {ipAddress}: {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeolocationService] Excepción general al obtener ubicación para IP {ipAddress}: {ex.Message}");
                return null;
            }
        }
    }
}

