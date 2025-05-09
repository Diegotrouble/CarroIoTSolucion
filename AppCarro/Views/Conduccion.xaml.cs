using AppCarro.Services; // Aseg�rate que este namespace sea el correcto para tu MqttService
using System.ComponentModel;
using System.Text;
using MQTTnet.Client; // Para MqttApplicationMessageReceivedEventArgs

namespace AppCarro.Views
{
    public partial class Conduccion : ContentPage
    {
        private readonly MqttService _mqttService;
        private const string TopicComandoBase = "carroIoT/conduccion"; // T�pico para enviar comandos
        // Puedes definir t�picos espec�ficos para suscribirte si es necesario
        private const string TopicSuscripcionGeneral = "carroIoT/#";

        public Conduccion(MqttService mqttService) // Inyecci�n de dependencias
        {
            InitializeComponent();
            _mqttService = mqttService;
            // No establecemos el BindingContext directamente al servicio aqu�,
            // manejaremos las actualizaciones de UI a trav�s de eventos.
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Suscribirse a los cambios de propiedades del MqttService
            _mqttService.PropertyChanged += MqttService_PropertyChanged;
            // Suscribirse al evento de mensajes recibidos del servicio (opcional si solo usas el log general)
            // _mqttService.MessageReceived += MqttService_MessageReceived_SpecificForThisPage;

            // Actualizar la UI con el estado actual del servicio
            UpdateUIFromMqttServiceState();

            // Si el servicio ya est� conectado al aparecer la p�gina,
            // asegurarse de que las suscripciones necesarias est�n activas.
            if (_mqttService.IsConnected)
            {
                // Usamos Task.Run para no bloquear el hilo de UI si la suscripci�n tarda.
                Task.Run(async () => await _mqttService.SubscribeAsync(TopicSuscripcionGeneral));
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Darse de baja de los eventos para evitar fugas de memoria
            _mqttService.PropertyChanged -= MqttService_PropertyChanged;
            // _mqttService.MessageReceived -= MqttService_MessageReceived_SpecificForThisPage;

            // Opcional: Desuscribirse de t�picos si solo son relevantes para esta p�gina
            // y no quieres que sigan activos en segundo plano al salir de esta vista.
            // if (_mqttService.IsConnected)
            // {
            //     Task.Run(async () => await _mqttService.UnsubscribeAsync(TopicSuscripcionGeneral));
            // }
        }

        private void MqttService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Actualizar la UI cuando cambien las propiedades del MqttService
            // Asegurarse de que se ejecuta en el hilo de UI
            MainThread.BeginInvokeOnMainThread(UpdateUIFromMqttServiceState);
        }

        // Ejemplo de c�mo procesar�as mensajes espec�ficos para esta p�gina
        // si te suscribes al evento _mqttService.MessageReceived
        /*
        private async Task MqttService_MessageReceived_SpecificForThisPage(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            if (topic == "carroIoT/estadoBateria")
            {
                // Actualiza un label de bater�a en esta p�gina
                // MainThread.BeginInvokeOnMainThread(() => BatteryStatusLabel.Text = $"Bater�a: {payload}%");
            }
            // El log general ya se actualiza a trav�s de ReceivedMessagesLog en MqttService
        }
        */

        private void UpdateUIFromMqttServiceState()
        {
            ConnectionStatusLabel.Text = _mqttService.ConnectionStatus;
            ConnectionStatusLabel.TextColor = _mqttService.StatusColor;

            ConnectionActivityIndicator.IsRunning = _mqttService.ConnectionStatus == "Conectando...";

            ConnectButton.IsEnabled = !_mqttService.IsConnected && _mqttService.ConnectionStatus != "Conectando...";
            DisconnectButton.IsEnabled = _mqttService.IsConnected;

            // Cambiar el color del bot�n de desconexi�n para indicar estado
            DisconnectButton.BackgroundColor = _mqttService.IsConnected ? Colors.DarkRed : Colors.DarkGray;
            ConnectButton.BackgroundColor = !_mqttService.IsConnected ? (Color)Application.Current.Resources["Primary"] : Colors.DarkGray;


            // Habilitar/deshabilitar botones de control basados en la conexi�n MQTT
            bool controlsEnabled = _mqttService.IsConnected;
            BtnUp.IsEnabled = controlsEnabled;
            BtnLeft.IsEnabled = controlsEnabled;
            BtnRight.IsEnabled = controlsEnabled;
            BtnDown.IsEnabled = controlsEnabled;

            // Actualizar el editor de mensajes recibidos
            // Hacemos scroll al final si el contenido cambia y el editor tiene el foco o es visible.
            // Esta es una forma simple, podr�as necesitar algo m�s robusto si el rendimiento es un problema con muchos mensajes.
            if (ReceivedMessagesEditor.Text != _mqttService.ReceivedMessagesLog)
            {
                ReceivedMessagesEditor.Text = _mqttService.ReceivedMessagesLog;
                // Scroll al final del Editor
                // MainThread.BeginInvokeOnMainThread(async () => {
                //     await Task.Delay(100); // Peque�o delay para asegurar que el texto se renderice
                //     await ReceivedMessagesEditor.ScrollToAsync(0, ReceivedMessagesEditor.Text.Length, true);
                // });
                // La funcionalidad de ScrollToAsync para Editor puede ser limitada o no existir directamente.
                // Una alternativa es usar un CollectionView si cada mensaje es un item.
                // Por ahora, simplemente actualizamos el texto.
            }
        }

        private async void ConnectButton_Clicked(object sender, EventArgs e)
        {
            // Mostrar indicador de actividad
            ConnectionActivityIndicator.IsRunning = true;
            ConnectionStatusLabel.Text = "Conectando...";
            ConnectionStatusLabel.TextColor = Colors.Orange;
            ConnectButton.IsEnabled = false; // Deshabilitar mientras se conecta

            await _mqttService.ConnectAsync();

            // UpdateUIFromMqttServiceState se llamar� a trav�s de PropertyChanged,
            // pero podemos forzar una actualizaci�n si es necesario o si la conexi�n falla r�pidamente.
            if (!_mqttService.IsConnected)
            {
                ConnectionActivityIndicator.IsRunning = false;
                ConnectButton.IsEnabled = true; // Rehabilitar si falla
            }
            else
            {
                // Suscribirse a los t�picos necesarios una vez conectado
                await _mqttService.SubscribeAsync(TopicSuscripcionGeneral);
            }
            UpdateUIFromMqttServiceState(); // Asegurar que la UI refleje el estado final
        }

        private async void DisconnectButton_Clicked(object sender, EventArgs e)
        {
            await _mqttService.DisconnectAsync();
            // UpdateUIFromMqttServiceState se llamar� a trav�s de PropertyChanged
        }

        // M�todos de los botones de control (actualizados para usar MqttService)
        private async void BtnAdelante_Clicked(object sender, EventArgs e)
        {
            await _mqttService.PublishAsync(TopicComandoBase, "1");
        }

        private async void BtnIzquierda_Clicked(object sender, EventArgs e)
        {
            await _mqttService.PublishAsync(TopicComandoBase, "2");
        }

        private async void BtnDerecha_Clicked(object sender, EventArgs e)
        {
            await _mqttService.PublishAsync(TopicComandoBase, "3");
        }

        private async void BtnAtras_Clicked(object sender, EventArgs e)
        {
            await _mqttService.PublishAsync(TopicComandoBase, "0");
        }
    }
}