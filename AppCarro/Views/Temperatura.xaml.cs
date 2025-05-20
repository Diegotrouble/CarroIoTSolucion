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
    //este diccionario estar� almacenando los ultimos valores de los sensores
    private Dictionary<string, string> lastSensorValues = new Dictionary<string, string>();


    
    public Temperatura(MqttService mqttService)
    {
        InitializeComponent();
        _mqttService = mqttService;
        // Actualizar etiquetas de rango si los cambias arriba

    }

    //m�todo cuando entras a la pantalla 
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _mqttService.MessageReceived += _mqttService_MessageReceived;

        await _mqttService.SubscribeAsync(TopicGeneral);


    }
    //m�todo cuando sales de la pantalla
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
            Debug.WriteLine($"[MQTT Receiver] Guardado: T�pico='{topic}', Payload='{payload}'");
        }
        //Si no llega como numero dar� error
        if (!parsedSuccessfully)
        {
            Debug.WriteLine($"[DataPage] No se pudo parsear el payload '{payload}' del t�pico '{topic}' a double.");
            return; // Salir si el payload no es un n�mero v�lido
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
            await DisplayAlert("Entrada Vac�a", "Por favor, escribe el nombre del sensor (ej: temperatura, rssi, intensidad).", "OK");
            return;
        }

        // Normalizar el nombre del sensor (min�sculas y sin espacios extra)
        string sensorNormalizado = sensorNombreIngresado.Trim().ToLower();
        string topicoDelSensorBuscado = "";

        // 2. Determinar el t�pico completo basado en el nombre del sensor ingresado.
        //    El t�pico base es "carroIoT/" seguido por el nombre del sensor.
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
            await DisplayAlert("Sensor Desconocido", $"El sensor '{sensorNombreIngresado}' no es reconocido. Los valores v�lidos son 'temperatura', 'rssi', 'intensidad'.", "OK");
            return;
        }

        // 3. Intentar obtener el �ltimo valor guardado para ese t�pico.
        if (lastSensorValues.TryGetValue(topicoDelSensorBuscado, out string ultimoValor))
        {
            // Si se encontr�, mostrarlo.
            await DisplayAlert($"�ltimo valor para {sensorNormalizado}", $"El valor es: {ultimoValor}", "OK");
        }
        else
        {
            // Si no se encontr� (porque a�n no ha llegado ning�n mensaje para ese t�pico espec�fico).
            await DisplayAlert("Valor no Encontrado", $"A�n no se ha recibido ning�n valor para el sensor '{sensorNormalizado}' (t�pico: '{topicoDelSensorBuscado}').", "OK");
        }
    }
}