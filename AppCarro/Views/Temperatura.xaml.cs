using AppCarro.Services;
using MQTTnet.Client;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AppCarro.Views;

public partial class Temperatura : ContentPage
{
    private readonly MqttService _mqttService;
    private const string TopicGeneral = "carroIoT/#";
    //este diccionario estará almacenando los ultimos valores de los sensores
    private Dictionary<string, string> lastSensorValues = new Dictionary<string, string>();


    
    public Temperatura(MqttService mqttService)
    {
        InitializeComponent();
        _mqttService = mqttService;
        // Actualizar etiquetas de rango si los cambias arriba

    }

    //método cuando entras a la pantalla 
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _mqttService.MessageReceived += _mqttService_MessageReceived;

        await _mqttService.SubscribeAsync(TopicGeneral);


    }
    //método cuando sales de la pantalla
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_mqttService.IsConnected)
        {
            await _mqttService.UnsubscribeAsync(TopicGeneral);

        }

    }

    private async Task _mqttService_MessageReceived(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);

        double value;

        //booleano que prueba si logra convertirlo a numero
        bool parsedSuccessfully = double.TryParse(payload, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

        if (topic == "carroIoT/temperatura" || topic == "carroIoT/rssi" || topic == "carroIoT/corriente")
        {
            lastSensorValues[topic] = payload;
            Debug.WriteLine($"[MQTT Receiver] Guardado: Tópico='{topic}', Payload='{payload}'");
        }
        //Si no llega como numero dará error
        if (!parsedSuccessfully)
        {
            Debug.WriteLine($"[DataPage] No se pudo parsear el payload '{payload}' del tópico '{topic}' a double.");
            return; // Salir si el payload no es un número válido
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {


        });
    }




    private void ObtenerSensor(object sender, EventArgs e)
    {
        ObtenerSensorAsincrono();
    }

    private async void ObtenerSensorAsincrono()
    {
        // 1. Obtener el texto del Entry.

        string sensorNombreIngresado = SensorABuscar.Text;

        if (string.IsNullOrWhiteSpace(sensorNombreIngresado))
        {
            await DisplayAlert("Entrada Vacía", "Por favor, escribe el nombre del sensor (ej: temperatura, rssi, intensidad).", "OK");
            return;
        }

        // Normalizar el nombre del sensor (minúsculas y sin espacios extra)
        string sensorNormalizado = sensorNombreIngresado.Trim().ToLower();
        string topicoDelSensorBuscado = "";

        // 2. Determinar el tópico completo basado en el nombre del sensor ingresado.
        //    El tópico base es "carroIoT/" seguido por el nombre del sensor.
        if (sensorNormalizado.Equals("temperatura"))
        {
            topicoDelSensorBuscado = "carroIoT/temperatura";
        }
        else if (sensorNormalizado.Equals("rssi"))
        {
            topicoDelSensorBuscado = "carroIoT/rssi";
        }
        else if (sensorNormalizado.Equals("corriente"))
        {
            topicoDelSensorBuscado = "carroIoT/corriente";
        }
        else
        {
            await DisplayAlert("Sensor Desconocido", $"El sensor '{sensorNombreIngresado}' no es reconocido. Los valores válidos son 'temperatura', 'rssi', 'intensidad'.", "OK");
            return;
        }

        // 3. Intentar obtener el último valor guardado para ese tópico.
        if (lastSensorValues.TryGetValue(topicoDelSensorBuscado, out string ultimoValor))
        {
            // Si se encontró, mostrarlo.
            await DisplayAlert($"Último valor para {sensorNormalizado}", $"El valor es: {ultimoValor}", "OK");
        }
        else
        {
            // Si no se encontró (porque aún no ha llegado ningún mensaje para ese tópico específico).
            await DisplayAlert("Valor no Encontrado", $"Aún no se ha recibido ningún valor para el sensor '{sensorNormalizado}' (tópico: '{topicoDelSensorBuscado}').", "OK");
        }
    }
}