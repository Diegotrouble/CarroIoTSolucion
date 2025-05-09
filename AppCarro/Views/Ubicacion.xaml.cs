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
        private Pin _vehiclePin; // Para mantener una referencia al pin del vehículo en el mapa

        public Ubicacion(MqttService mqttService, GeolocationService geolocationService)
        {
            InitializeComponent();
            _mqttService = mqttService;
            _geolocationService = geolocationService;

            // Establecer una ubicación inicial para el mapa (opcional, pero bueno para que no aparezca vacío)
            Location initialLocation = new Location(20.593684, -100.389888); // Ejemplo: Querétaro, México
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
                await DisplayAlert("MQTT Desconectado", "No se pudo suscribir al tópico de ubicación. Conéctate primero desde la pantalla de conducción.", "OK");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            _mqttService.MessageReceived -= MqttService_MessageReceived;

            // Opcional: Desuscribirse si solo esta página necesita este tópico
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
                Debug.WriteLine($"[UbicacionPage] IP Pública recibida: {ipAddress}");

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
                        Debug.WriteLine($"[UbicacionPage] No se pudo obtener la ubicación para la IP: {ipAddress}");
                        // Podrías mostrar una alerta al usuario aquí si lo deseas
                        // await DisplayAlert("Error de Ubicación", $"No se pudo obtener la ubicación para la IP: {ipAddress}", "OK");
                    }
                });
            }
        }

        private void UpdateMapLocation(Location location, string ipAddress)
        {
            // Si ya existe un pin, lo removemos para añadir el nuevo (o podríamos actualizar su posición)
            if (_vehiclePin != null)
            {
                VehicleLocationMap.Pins.Remove(_vehiclePin);
            }

            _vehiclePin = new Pin
            {
                Label = "Ubicación del Vehículo (IP)",
                Address = $"IP: {ipAddress}\nCoords: {location.Latitude:F5}, {location.Longitude:F5}",
                Type = PinType.Place, // O PinType.Generic, PinType.SavedPin
                Location = location
            };

            VehicleLocationMap.Pins.Add(_vehiclePin);

            // Centrar el mapa en la nueva ubicación del pin con un nivel de zoom adecuado
            // El segundo y tercer argumento de MapSpan son los grados de latitud/longitud para el zoom
            // Valores más pequeños = más zoom.
            MapSpan mapSpan = new MapSpan(location, 0.01, 0.01); // Zoom cercano
            VehicleLocationMap.MoveToRegion(mapSpan);
        }
    }
}
