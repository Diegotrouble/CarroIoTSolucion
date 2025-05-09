using AppCarro.Services; // Asegúrate que este namespace sea el correcto para tu MqttService
using System.ComponentModel;
using System.Text;
using MQTTnet.Client; // Para MqttApplicationMessageReceivedEventArgs

namespace AppCarro.Views
{
    public partial class Conduccion : ContentPage
    {
        private readonly MqttService _mqttService;
        private const string TopicComandoBase = "carroIoT/conduccion"; // Tópico para enviar comandos
        // Puedes definir tópicos específicos para suscribirte si es necesario
        private const string TopicSuscripcionGeneral = "carroIoT/#";

        public Conduccion(MqttService mqttService) // Inyección de dependencias
        {
            InitializeComponent();
            _mqttService = mqttService;
            // No establecemos el BindingContext directamente al servicio aquí,
            // manejaremos las actualizaciones de UI a través de eventos.
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

            // Si el servicio ya está conectado al aparecer la página,
            // asegurarse de que las suscripciones necesarias estén activas.
            if (_mqttService.IsConnected)
            {
                // Usamos Task.Run para no bloquear el hilo de UI si la suscripción tarda.
                Task.Run(async () => await _mqttService.SubscribeAsync(TopicSuscripcionGeneral));
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Darse de baja de los eventos para evitar fugas de memoria
            _mqttService.PropertyChanged -= MqttService_PropertyChanged;
            // _mqttService.MessageReceived -= MqttService_MessageReceived_SpecificForThisPage;

            // Opcional: Desuscribirse de tópicos si solo son relevantes para esta página
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

        // Ejemplo de cómo procesarías mensajes específicos para esta página
        // si te suscribes al evento _mqttService.MessageReceived
        /*
        private async Task MqttService_MessageReceived_SpecificForThisPage(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            if (topic == "carroIoT/estadoBateria")
            {
                // Actualiza un label de batería en esta página
                // MainThread.BeginInvokeOnMainThread(() => BatteryStatusLabel.Text = $"Batería: {payload}%");
            }
            // El log general ya se actualiza a través de ReceivedMessagesLog en MqttService
        }
        */

        private void UpdateUIFromMqttServiceState()
        {
            ConnectionStatusLabel.Text = _mqttService.ConnectionStatus;
            ConnectionStatusLabel.TextColor = _mqttService.StatusColor;

            ConnectionActivityIndicator.IsRunning = _mqttService.ConnectionStatus == "Conectando...";

            ConnectButton.IsEnabled = !_mqttService.IsConnected && _mqttService.ConnectionStatus != "Conectando...";
            DisconnectButton.IsEnabled = _mqttService.IsConnected;

            // Cambiar el color del botón de desconexión para indicar estado
            DisconnectButton.BackgroundColor = _mqttService.IsConnected ? Colors.DarkRed : Colors.DarkGray;
            ConnectButton.BackgroundColor = !_mqttService.IsConnected ? (Color)Application.Current.Resources["Primary"] : Colors.DarkGray;


            // Habilitar/deshabilitar botones de control basados en la conexión MQTT
            bool controlsEnabled = _mqttService.IsConnected;
            BtnUp.IsEnabled = controlsEnabled;
            BtnLeft.IsEnabled = controlsEnabled;
            BtnRight.IsEnabled = controlsEnabled;
            BtnDown.IsEnabled = controlsEnabled;

            // Actualizar el editor de mensajes recibidos
            // Hacemos scroll al final si el contenido cambia y el editor tiene el foco o es visible.
            // Esta es una forma simple, podrías necesitar algo más robusto si el rendimiento es un problema con muchos mensajes.
            if (ReceivedMessagesEditor.Text != _mqttService.ReceivedMessagesLog)
            {
                ReceivedMessagesEditor.Text = _mqttService.ReceivedMessagesLog;
                // Scroll al final del Editor
                // MainThread.BeginInvokeOnMainThread(async () => {
                //     await Task.Delay(100); // Pequeño delay para asegurar que el texto se renderice
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

            // UpdateUIFromMqttServiceState se llamará a través de PropertyChanged,
            // pero podemos forzar una actualización si es necesario o si la conexión falla rápidamente.
            if (!_mqttService.IsConnected)
            {
                ConnectionActivityIndicator.IsRunning = false;
                ConnectButton.IsEnabled = true; // Rehabilitar si falla
            }
            else
            {
                // Suscribirse a los tópicos necesarios una vez conectado
                await _mqttService.SubscribeAsync(TopicSuscripcionGeneral);
            }
            UpdateUIFromMqttServiceState(); // Asegurar que la UI refleje el estado final
        }

        private async void DisconnectButton_Clicked(object sender, EventArgs e)
        {
            await _mqttService.DisconnectAsync();
            // UpdateUIFromMqttServiceState se llamará a través de PropertyChanged
        }

        // Métodos de los botones de control (actualizados para usar MqttService)
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