using Microsoft.Extensions.Logging;
using AppCarro.Services; 
using AppCarro.Views;   
using Microsoft.Maui.Controls.Maps; // Asegúrate de tener este using
using Microsoft.Maui.Maps; // Y este también

namespace AppCarro
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                }).UseMauiMaps();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Registrar el MqttService como Singleton
            // Esto asegura que haya una única instancia del servicio en toda la app.
            builder.Services.AddSingleton<MqttService>();

            // Registrar tus páginas/vistas si vas a usar inyección de dependencias en ellas.
            // Si las creas directamente con 'new', no siempre es necesario registrarlas aquí,
            // pero es buena práctica para consistencia, especialmente si tienen dependencias.
            builder.Services.AddTransient<Conduccion>(); // O AddSingleton si Conduccion debe ser singleton

            // Vamos a registrar la página de Ubicacion y el futuro GeolocationService aquí también
            builder.Services.AddTransient<Ubicacion>();
            // Cuando creemos GeolocationService, lo registraremos así:
            builder.Services.AddSingleton<GeolocationService>();
            builder.Services.AddTransient<Data>(); // O AddSingleton si es apropiado
            builder.Services.AddTransient<Temperatura>(); // O AddSingleton si es apropiado

            return builder.Build();
        }
    }
}
