using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;

namespace ConsoleApp1
{
    internal class Program
    {
        static async Task Main()
        {
            var client = new MqttFactory().CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("eb4f02d5.ala.us-east-1.emqxsl.com", 8883)
                .WithClientId("cliente-test")
                .WithCredentials("userPrueba", "userPrueba")
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    AllowUntrustedCertificates = true, // cambiar a false si usás certificados válidos
                    IgnoreCertificateChainErrors = true, // opcional
                    IgnoreCertificateRevocationErrors = true // opcional
                })
                .Build();

            client.ApplicationMessageReceivedAsync += e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                Console.WriteLine($"Mensaje recibido: {payload}");
                return Task.CompletedTask;
            };

            client.ConnectedAsync += _ =>
            {
                Console.WriteLine("Conectado al broker.");
                return Task.CompletedTask;
            };

            client.DisconnectedAsync += _ =>
            {
                Console.WriteLine("Desconectado del broker.");
                return Task.CompletedTask;
            };

            await client.ConnectAsync(options);

            // Suscripción
            await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("carroIoT/#")
                .Build());

            Console.WriteLine("Suscrito. Esperando mensajes...");

            // Enviar un mensaje al broker
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("carroIoT/comando")
                .WithPayload("Mover hacia adelante")  // El contenido del mensaje
                .WithQualityOfServiceLevel(0)  // Calidad de servicio (QoS)
                .Build();

            // Publicar el mensaje
            await client.PublishAsync(message);
            Console.WriteLine("Mensaje enviado.");

            Console.ReadLine();
        }
    }
}
