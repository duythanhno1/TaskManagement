using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.Messaging
{
    public static class MassTransitExtensions
    {
        public static IServiceCollection AddSharedMassTransit(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<IBusRegistrationConfigurator>? configureConsumers = null)
        {
            services.AddMassTransit(busConfig =>
            {
                configureConsumers?.Invoke(busConfig);

                busConfig.UsingRabbitMq((context, cfg) =>
                {
                    var host = configuration.GetValue("RabbitMQ:Host", "localhost");
                    var port = configuration.GetValue("RabbitMQ:Port", 5672);
                    var username = configuration.GetValue("RabbitMQ:Username", "guest");
                    var password = configuration.GetValue("RabbitMQ:Password", "guest");

                    cfg.Host(host, (ushort)port, "/", h =>
                    {
                        h.Username(username!);
                        h.Password(password!);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }
    }
}
