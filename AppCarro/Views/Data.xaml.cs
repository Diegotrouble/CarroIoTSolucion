using AppCarro.Services; // Para MqttService
using MQTTnet.Client; // Para MqttApplicationMessageReceivedEventArgs
using System.Text;
using System.Globalization; // Para CultureInfo.InvariantCulture
using System.Diagnostics; // Para Debug.WriteLine

namespace AppCarro.Views
{
    public partial class Data : ContentPage
    {
        private readonly MqttService _mqttService;

        // T�picos MQTT para los sensores
        private const string TopicTemperature = "carroIoT/temperatura";
        private const string TopicRssi = "carroIoT/rssi";
        private const string TopicCurrent = "carroIoT/corriente";

        // Rangos para las barras de progreso (ajustar seg�n sea necesario)
        private const double TempMin = 0.0;
        private const double TempMax = 100.0;
        private const double RssiMin = -90.0; // Se�al m�s d�bil que consideramos
        private const double RssiMax = -30.0; // Se�al m�s fuerte
        private const double CurrentMin = 0.0;
        private const double CurrentMax = 150.0; // Ejemplo, ajustar

        public Data(MqttService mqttService)
        {
            InitializeComponent();
            _mqttService = mqttService;

            // Actualizar etiquetas de rango si los cambias arriba
            TemperatureRangeLabel.Text = $"Rango: {TempMin}�C - {TempMax}�C";
            RssiRangeLabel.Text = $"Rango: {RssiMin}dBm (malo) a {RssiMax}dBm (excelente)";
            CurrentRangeLabel.Text = $"Rango: {CurrentMin}A - {CurrentMax}A";
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _mqttService.MessageReceived += MqttService_MessageReceived;

            if (_mqttService.IsConnected)
            {
                await _mqttService.SubscribeAsync(TopicTemperature);
                await _mqttService.SubscribeAsync(TopicRssi);
                await _mqttService.SubscribeAsync(TopicCurrent);
                Debug.WriteLine($"[DataPage] Suscrito a t�picos de sensores.");
            }
            else
            {
                Debug.WriteLine("[DataPage] MqttService no conectado. No se pudo suscribir a t�picos de sensores.");
                await DisplayAlert("MQTT Desconectado", "No se pudo suscribir a los t�picos de sensores. Con�ctate primero desde la pantalla de conducci�n.", "OK");
            }
            UpdateLastUpdateTime();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            _mqttService.MessageReceived -= MqttService_MessageReceived;

            // Opcional: Desuscribirse si solo esta p�gina necesita estos t�picos
            if (_mqttService.IsConnected)
            {
                await _mqttService.UnsubscribeAsync(TopicTemperature);
                await _mqttService.UnsubscribeAsync(TopicRssi);
                await _mqttService.UnsubscribeAsync(TopicCurrent);
                Debug.WriteLine($"[DataPage] Desuscrito de t�picos de sensores.");
            }
        }

        private async Task MqttService_MessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            double value;

            // Usar CultureInfo.InvariantCulture para parsear n�meros decimales independientemente de la configuraci�n regional
            bool parsedSuccessfully = double.TryParse(payload, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

            if (!parsedSuccessfully)
            {
                Debug.WriteLine($"[DataPage] No se pudo parsear el payload '{payload}' del t�pico '{topic}' a double.");
                return; // Salir si el payload no es un n�mero v�lido
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (topic == TopicTemperature)
                {
                    TemperatureValueLabel.Text = $"{value:F1} �C"; // F1 = 1 decimal
                    TemperatureProgressBar.Progress = NormalizeValue(value, TempMin, TempMax);
                    Debug.WriteLine($"[DataPage] Temperatura actualizada: {value}�C");
                }
                else if (topic == TopicRssi)
                {
                    RssiValueLabel.Text = $"{value:F0} dBm"; // F0 = 0 decimales
                    // Para RSSI, un valor m�s alto (menos negativo) es mejor.
                    // La normalizaci�n debe tener esto en cuenta.
                    RssiProgressBar.Progress = NormalizeValue(value, RssiMin, RssiMax);
                    Debug.WriteLine($"[DataPage] RSSI actualizado: {value}dBm");
                }
                else if (topic == TopicCurrent)
                {
                    CurrentValueLabel.Text = $"{value:F2} A"; // F2 = 2 decimales
                    CurrentProgressBar.Progress = NormalizeValue(value, CurrentMin, CurrentMax);
                    Debug.WriteLine($"[DataPage] Corriente actualizada: {value}A");
                }
                UpdateLastUpdateTime();
            });
        }

        /// <summary>
        /// Normaliza un valor a un rango de 0.0 a 1.0 para el ProgressBar.
        /// </summary>
        private double NormalizeValue(double value, double min, double max)
        {
            if (max <= min) return 0; // Evitar divisi�n por cero o rangos inv�lidos
            var normalized = (value - min) / (max - min);
            return Math.Clamp(normalized, 0.0, 1.0); // Asegurar que est� entre 0 y 1
        }

        private void UpdateLastUpdateTime()
        {
            LastUpdateLabel.Text = $"�ltima actualizaci�n: {DateTime.Now:HH:mm:ss}";
        }
    }
}
