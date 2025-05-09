using AppCarro.Services; // Para MqttService y GeolocationService
using Microsoft.Maui.Controls.Maps; // Para Map, Pin, Location, MapSpan
using Microsoft.Maui.Devices.Sensors; // Para Location (aunque usamos el de Maps)
using System.Text; // Para Encoding
using MQTTnet.Client; // Para MqttApplicationMessageReceivedEventArgs
using System.Diagnostics;
using Microsoft.Maui.Maps; 

namespace AppCarro.Views
{
    public partial class Ubicacion : ContentPage
    {
        private readonly MqttService _mqttService;
        private readonly GeolocationService _geolocationService;
        private const string IpLocationTopic = "carroIoT/ubicacion/ipPublica";
        private Pin _vehiclePin; // Para mantener una referencia al pin del veh�culo en el mapa

        public Ubicacion(MqttService mqttService, GeolocationService geolocationService)
        {
            InitializeComponent();
            _mqttService = mqttService;
            _geolocationService = geolocationService;

            // Establecer una ubicaci�n inicial para el mapa (opcional, pero bueno para que no aparezca vac�o)
            Location initialLocation = new Location(20.593684, -100.389888); // Ejemplo: Quer�taro, M�xico
            MapSpan initialSpan = new MapSpan(initialLocation, 0.5, 0.5); // Latitud/Longitud grados de zoom
            VehicleLocationMap.MoveToRegion(initialSpan);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _mqttService.MessageReceived += MqttService_MessageReceived;

            if (_mqttService.IsConnected)
            {
                await _mqttService.SubscribeAsync(IpLocationTopic);
                Debug.WriteLine($"[UbicacionPage] Suscrito a {IpLocationTopic}");
            }
            else
            {
                Debug.WriteLine("[UbicacionPage] MqttService no conectado al aparecer. No se pudo suscribir.");
                await DisplayAlert("MQTT Desconectado", "No se pudo suscribir al t�pico de ubicaci�n. Con�ctate primero desde la pantalla de conducci�n.", "OK");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            _mqttService.MessageReceived -= MqttService_MessageReceived;

            // Opcional: Desuscribirse si solo esta p�gina necesita este t�pico
            if (_mqttService.IsConnected)
            {
                await _mqttService.UnsubscribeAsync(IpLocationTopic);
                Debug.WriteLine($"[UbicacionPage] Desuscrito de {IpLocationTopic}");
            }
        }

        private async Task MqttService_MessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic == IpLocationTopic)
            {
                var ipAddress = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                Debug.WriteLine($"[UbicacionPage] IP P�blica recibida: {ipAddress}");

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    IpAddressLabel.Text = $"IP Recibida: {ipAddress}";
                    CoordinatesLabel.Text = "Coordenadas: Obteniendo...";
                    MapLoadingIndicator.IsRunning = true;

                    Location vehicleLocation = await _geolocationService.GetLocationFromIpAsync(ipAddress);
                    MapLoadingIndicator.IsRunning = false;

                    if (vehicleLocation != null)
                    {
                        CoordinatesLabel.Text = $"Coordenadas: {vehicleLocation.Latitude:F6}, {vehicleLocation.Longitude:F6}";
                        UpdateMapLocation(vehicleLocation, ipAddress);
                    }
                    else
                    {
                        CoordinatesLabel.Text = "Coordenadas: No se pudo obtener";
                        Debug.WriteLine($"[UbicacionPage] No se pudo obtener la ubicaci�n para la IP: {ipAddress}");
                        // Podr�as mostrar una alerta al usuario aqu� si lo deseas
                        // await DisplayAlert("Error de Ubicaci�n", $"No se pudo obtener la ubicaci�n para la IP: {ipAddress}", "OK");
                    }
                });
            }
        }

        private void UpdateMapLocation(Location location, string ipAddress)
        {
            // Si ya existe un pin, lo removemos para a�adir el nuevo (o podr�amos actualizar su posici�n)
            if (_vehiclePin != null)
            {
                VehicleLocationMap.Pins.Remove(_vehiclePin);
            }

            _vehiclePin = new Pin
            {
                Label = "Ubicaci�n del Veh�culo (IP)",
                Address = $"IP: {ipAddress}\nCoords: {location.Latitude:F5}, {location.Longitude:F5}",
                Type = PinType.Place, // O PinType.Generic, PinType.SavedPin
                Location = location
            };

            VehicleLocationMap.Pins.Add(_vehiclePin);

            // Centrar el mapa en la nueva ubicaci�n del pin con un nivel de zoom adecuado
            // El segundo y tercer argumento de MapSpan son los grados de latitud/longitud para el zoom
            // Valores m�s peque�os = m�s zoom.
            MapSpan mapSpan = new MapSpan(location, 0.01, 0.01); // Zoom cercano
            VehicleLocationMap.MoveToRegion(mapSpan);
        }
    }
}
