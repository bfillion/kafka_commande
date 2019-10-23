using System;
using Autofac;
using kafka.commande.messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kafka.commande
{
    public class AutofacModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new KafkaService(c.Resolve<IConfiguration>(), c.Resolve<ILogger<KafkaService>>()))
                .As<IKafkaService>()
                .InstancePerLifetimeScope();
        }
    }
}
