using CommunityToolkit.Mvvm.ComponentModel; // Para ObservableObject
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics; // Para Colors
using System;
using System.ComponentModel; // Para INotifyPropertyChanged si no usas CommunityToolkit

namespace AppCarro.Services // Asegúrate que este namespace coincida con tu proyecto
{
    // Si no usas CommunityToolkit.Mvvm, puedes implementar INotifyPropertyChanged manualmente
    // public class MqttService : INotifyPropertyChanged
    public class MqttService : ObservableObject // Hereda de ObservableObject para notificaciones de cambio
    {
        private IMqttClient _mqttClient;
        private MqttClientOptions _mqttOptions;

        // --- Propiedades Observables para la UI ---
        private string _connectionStatus = "Desconectado";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value); // Notifica a la UI del cambio
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(StatusColor)); // Notificar que StatusColor también cambió
                }
            }
        }

        private string _receivedMessagesLog = string.Empty;
        public string ReceivedMessagesLog
        {
            get => _receivedMessagesLog;
            private set => SetProperty(ref _receivedMessagesLog, value);
        }

        public Color StatusColor => IsConnected ? Colors.Green : (_connectionStatus == "Conectando..." ? Colors.Orange : Colors.Red);

        // Evento para notificar la recepción de un mensaje (útil si diferentes páginas quieren reaccionar de forma distinta)
        public event Func<MqttApplicationMessageReceivedEventArgs, Task> MessageReceived;


        public MqttService()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // Configuración del cliente MQTT
            _mqttOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("eb4f02d5.ala.us-east-1.emqxsl.com", 8883) // TU SERVIDOR MQTT
                .WithClientId("AppCarroCliente-" + Guid.NewGuid().ToString("N").Substring(0, 12)) // ClientId único
                .WithCredentials("userPrueba", "userPrueba") // TUS CREDENCIALES
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    AllowUntrustedCertificates = true, // IMPORTANTE: Cambiar a false en producción
                    IgnoreCertificateChainErrors = true,
                    IgnoreCertificateRevocationErrors = true
                })
                .WithCleanSession()
                .Build();

            _mqttClient.ConnectedAsync += OnMqttClientConnectedAsync;
            _mqttClient.DisconnectedAsync += OnMqttClientDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += OnMqttClientMessageReceivedAsync;
        }

        private Task OnMqttClientConnectedAsync(MqttClientConnectedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnected = true;
                ConnectionStatus = "Conectado";
                LogMessage("Cliente MQTT conectado al broker.");
            });
            return Task.CompletedTask;
        }

        private Task OnMqttClientDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnected = false;
                ConnectionStatus = $"Desconectado: {e.ReasonString}";
                LogMessage($"Cliente MQTT desconectado. Razón: {e.ReasonString}. Intentos de reconexión: {e.ClientWasConnected}");
                // Aquí podrías implementar lógica de reconexión si lo deseas
            });
            return Task.CompletedTask;
        }

        private async Task OnMqttClientMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var qos = e.ApplicationMessage.QualityOfServiceLevel;
            var retain = e.ApplicationMessage.Retain;

            string logEntry = $"[{DateTime.Now:HH:mm:ss}] T: {topic} | Q: {qos} | R: {retain} | P: {payload}";
            LogMessage(logEntry);

            // Invocar el evento para que otras partes de la app puedan procesar el mensaje
            if (MessageReceived != null)
            {
                // No queremos que un suscriptor bloquee a otros, así que no esperamos la tarea directamente aquí
                // o podríamos envolverlo en un try-catch si es crítico.
                // Considera invocar delegados de forma segura.
                // await MessageReceived.Invoke(e); // Si solo hay un suscriptor o quieres esperar

                // Para múltiples suscriptores, invoca cada uno
                foreach (Func<MqttApplicationMessageReceivedEventArgs, Task> handler in MessageReceived.GetInvocationList())
                {
                    try
                    {
                        await handler(e);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error al procesar mensaje recibido por un suscriptor: {ex.Message}");
                        // Considera un logging más robusto aquí
                    }
                }
            }
        }

        public async Task ConnectAsync()
        {
            if (IsConnected) return;

            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ConnectionStatus = "Conectando...";
                });
                await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsConnected = false; // Asegurar que el estado es correcto
                    ConnectionStatus = "Error de conexión";
                    LogMessage($"Error al conectar con el broker MQTT: {ex.Message}");
                });
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected) return;

            try
            {
                await _mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                LogMessage($"Error al desconectar del broker MQTT: {ex.Message}");
                // Forzar el estado de desconexión en la UI si la desconexión falla pero queremos reflejarlo
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsConnected = false;
                    ConnectionStatus = "Error al desconectar";
                });
            }
        }

        public async Task PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            if (!IsConnected)
            {
                LogMessage("Error: No se puede publicar, cliente no conectado.");
                // Podrías lanzar una excepción o mostrar una alerta al usuario desde la UI que llama.
                // await MainThread.InvokeOnMainThreadAsync(() => 
                //    Application.Current.MainPage.DisplayAlert("Error MQTT", "No conectado al broker.", "OK"));
                return;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            try
            {
                var result = await _mqttClient.PublishAsync(message, CancellationToken.None);
                if (result.IsSuccess)
                {
                    LogMessage($"Mensaje publicado a '{topic}': {payload}");
                }
                else
                {
                    LogMessage($"Fallo al publicar en '{topic}'. Razón: {result.ReasonCode}. Desc: {result.ReasonString}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Excepción al publicar mensaje en '{topic}': {ex.Message}");
            }
        }

        public async Task SubscribeAsync(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            if (!IsConnected)
            {
                LogMessage("Error: No se puede suscribir, cliente no conectado.");
                return;
            }

            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(qos)
                .Build();

            try
            {
                var subscribeResult = await _mqttClient.SubscribeAsync(topicFilter, CancellationToken.None);

                // En MQTTnet v4+, subscribeResult.Items contiene información detallada por cada filtro.
                // Por simplicidad, aquí solo logueamos el intento.
                // Puedes iterar sobre subscribeResult.Items para verificar cada suscripción.
                // Ejemplo: if (subscribeResult.Items.All(i => i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 || i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 || i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2))
                LogMessage($"Intento de suscripción a: {topic} con QoS {qos}");

                // Para verificar explícitamente (ejemplo para MQTTnet v4+)
                foreach (var item in subscribeResult.Items)
                {
                    if (item.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 ||
                        item.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 ||
                        item.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2)
                    {
                        LogMessage($"Suscripción exitosa para el filtro '{item.TopicFilter.Topic}' con QoS otorgado: {item.ResultCode}");
                    }
                    else
                    {
                        LogMessage($"Suscripción fallida para el filtro '{item.TopicFilter.Topic}'. Código: {item.ResultCode}");
                    }
                }

            }
            catch (Exception ex)
            {
                LogMessage($"Error al suscribir al tópico '{topic}': {ex.Message}");
            }
        }

        public async Task UnsubscribeAsync(string topic)
        {
            if (!IsConnected)
            {
                LogMessage("Error: No se puede desuscribir, cliente no conectado.");
                return;
            }

            try
            {
                await _mqttClient.UnsubscribeAsync(topic, CancellationToken.None);
                LogMessage($"Desuscrito del tópico: {topic}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error al desuscribir del tópico '{topic}': {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            // Actualizar el log en el hilo principal para que la UI pueda bindearse de forma segura
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Añadir al principio para ver los mensajes más recientes arriba, o al final.
                // ReceivedMessagesLog = message + "\n" + ReceivedMessagesLog; 
                ReceivedMessagesLog += message + "\n"; // Añade al final

                // Opcional: Limitar el tamaño del log para no consumir demasiada memoria
                const int maxLogLines = 200;
                var lines = ReceivedMessagesLog.Split('\n');
                if (lines.Length > maxLogLines)
                {
                    ReceivedMessagesLog = string.Join("\n", lines.TakeLast(maxLogLines));
                }
            });
        }

        // Método para limpiar recursos si es necesario (aunque MQTTnet maneja mucho internamente)
        public async ValueTask DisposeAsync()
        {
            if (_mqttClient != null)
            {
                if (_mqttClient.IsConnected)
                {
                    await DisconnectAsync();
                }
                _mqttClient.Dispose();
            }
        }

        // Si no usas CommunityToolkit.Mvvm, necesitarías esto:
        // public event PropertyChangedEventHandler PropertyChanged;
        // protected virtual void OnPropertyChanged(string propertyName)
        // {
        //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }
        // protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        // {
        //     if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        //     field = value;
        //     OnPropertyChanged(propertyName);
        //     return true;
        // }
    }
}
