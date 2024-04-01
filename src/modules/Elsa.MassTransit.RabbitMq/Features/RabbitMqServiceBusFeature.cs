using Elsa.Extensions;
using Elsa.Features.Abstractions;
using Elsa.Features.Attributes;
using Elsa.Features.Services;
using Elsa.Hosting.Management.Contracts;
using Elsa.Hosting.Management.Features;
using Elsa.MassTransit.Consumers;
using Elsa.MassTransit.Extensions;
using Elsa.MassTransit.Features;
using Elsa.MassTransit.Options;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elsa.MassTransit.RabbitMq.Features;

/// <summary>
/// Configures MassTransit to use the RabbitMQ transport.
/// </summary>
[DependsOn(typeof(MassTransitFeature))]
[DependsOn(typeof(InstanceManagementFeature))]
public class RabbitMqServiceBusFeature : FeatureBase
{
    /// <inheritdoc />
    public RabbitMqServiceBusFeature(IModule module) : base(module)
    {
    }

    /// A RabbitMQ connection string.
    public string? ConnectionString { get; set; }

    /// Configures the RabbitMQ transport options.
    public Action<RabbitMqTransportOptions>? TransportOptions { get; set; }

    /// <summary>
    /// Configures the RabbitMQ bus.
    /// </summary>
    public Action<IRabbitMqBusFactoryConfigurator>? ConfigureServiceBus { get; set; }

    /// <inheritdoc />
    public override void Configure()
    {
        Module.Configure<MassTransitFeature>(massTransitFeature =>
        {
            massTransitFeature.BusConfigurator = configure =>
            {
                var temporaryConsumers = massTransitFeature.GetConsumers()
                    .Where(c => c.IsTemporary)
                    .ToList();
                
                // Consumers need to be added before the UsingRabbitMq statement to prevent exceptions.
                foreach (var consumer in temporaryConsumers)
                    configure.AddConsumer(consumer.ConsumerType).ExcludeFromConfigureEndpoints();
                
                configure.UsingRabbitMq((context, configurator) =>
                {
                    var options = context.GetRequiredService<IOptions<MassTransitWorkflowDispatcherOptions>>().Value;
                    var instanceNameProvider = context.GetRequiredService<IApplicationInstanceNameProvider>();

                    if (!string.IsNullOrEmpty(ConnectionString))
                        configurator.Host(ConnectionString);

                    ConfigureServiceBus?.Invoke(configurator);

                    foreach (var consumer in temporaryConsumers)
                    {
                        configure.AddConsumer(consumer.ConsumerType).ExcludeFromConfigureEndpoints();
                        
                        configurator.ReceiveEndpoint($"{instanceNameProvider.GetName()}-{consumer.Name}",
                            endpointConfigurator =>
                            {
                                endpointConfigurator.QueueExpiration = options.TemporaryQueueTtl ?? TimeSpan.FromHours(1);
                                endpointConfigurator.ConcurrentMessageLimit = options.ConcurrentMessageLimit;
                                
                                endpointConfigurator.ConfigureConsumer(context, consumer.ConsumerType);
                            });
                    }

                    // Only configure the dispatcher endpoints if the Masstransit Workflow Dispatcher feature is enabled.
                    if (Module.HasFeature<MassTransitWorkflowDispatcherFeature>())
                        configurator.SetupWorkflowDispatcherEndpoints(context);
                    
                    configurator.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("Elsa", false));
                });
            };
        });
    }

    /// <inheritdoc />
    public override void Apply()
    {
        if (TransportOptions != null) Services.Configure(TransportOptions);
    }
}