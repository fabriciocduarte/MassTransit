﻿// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.RabbitMqTransport.Configurators
{
    using System;
    using System.Collections.Generic;
    using Builders;
    using BusConfigurators;
    using GreenPipes;
    using MassTransit.Builders;
    using Topology;
    using Transport;
    using Transports;


    public class RabbitMqBusFactoryConfigurator :
        BusFactoryConfigurator<IBusBuilder>,
        IRabbitMqBusFactoryConfigurator,
        IBusFactory
    {
        readonly BusHostCollection<RabbitMqHost> _hosts;
        readonly RabbitMqReceiveSettings _settings;

        public RabbitMqBusFactoryConfigurator()
        {
            _hosts = new BusHostCollection<RabbitMqHost>();

            var queueName = ((IRabbitMqHost)null).GetTemporaryQueueName("bus-");
            _settings = new RabbitMqReceiveSettings
            {
                QueueName = queueName,
                AutoDelete = true,
                Durable = false
            };

            _settings.QueueArguments["x-expires"] = 60000;
            _settings.ExchangeArguments["x-expires"] = 60000;
        }

        public IBusControl CreateBus()
        {
            var builder = new RabbitMqBusBuilder(_hosts, ConsumePipeFactory, SendPipeFactory, PublishPipeFactory, _settings);

            ApplySpecifications(builder);

            return builder.Build();
        }

        public override IEnumerable<ValidationResult> Validate()
        {
            foreach (var result in base.Validate())
                yield return result;

            if (_hosts.Count == 0)
                yield return this.Failure("Host", "At least one host must be defined");
            if (string.IsNullOrWhiteSpace(_settings.QueueName))
                yield return this.Failure("Bus", "The bus queue name must not be null or empty");
        }

        public ushort PrefetchCount
        {
            set { _settings.PrefetchCount = value; }
        }

        public bool Durable
        {
            set { _settings.Durable = value; }
        }

        public bool Exclusive
        {
            set { _settings.Exclusive = value; }
        }

        public bool AutoDelete
        {
            set { _settings.AutoDelete = value; }
        }

        public string ExchangeType
        {
            set { _settings.ExchangeType = value; }
        }

        public bool PurgeOnStartup
        {
            set { _settings.PurgeOnStartup = value; }
        }

        public bool Lazy
        {
            set { SetQueueArgument("x-queue-mode", value ? "lazy" : "default"); }
        }

        public void SetQueueArgument(string key, object value)
        {
            _settings.SetQueueArgument(key, value);
        }

        public void SetExchangeArgument(string key, object value)
        {
            _settings.SetExchangeArgument(key, value);
        }

        public void EnablePriority(byte maxPriority)
        {
            _settings.EnablePriority(maxPriority);
        }

        public bool PublisherConfirmation
        {
            set { }
        }

        public IRabbitMqHost Host(RabbitMqHostSettings settings)
        {
            var host = new RabbitMqHost(settings);
            _hosts.Add(host);

            return host;
        }

        public void OverrideDefaultBusEndpointQueueName(string value)
        {
            _settings.QueueName = value;
        }

        public void ReceiveEndpoint(string queueName, Action<IReceiveEndpointConfigurator> configureEndpoint)
        {
            if (_hosts.Count == 0)
                throw new ArgumentException("At least one host must be configured before configuring a receive endpoint");

            ReceiveEndpoint(_hosts[0], queueName, configureEndpoint);
        }

        public void ReceiveEndpoint(IRabbitMqHost host, string queueName, Action<IRabbitMqReceiveEndpointConfigurator> configure)
        {
            if (host == null)
                throw new EndpointNotFoundException("The host address specified was not configured.");

            var specification = new RabbitMqReceiveEndpointSpecification(host, queueName);

            specification.ConnectConsumerConfigurationObserver(this);
            specification.ConnectSagaConfigurationObserver(this);

            AddReceiveEndpointSpecification(specification);

            configure?.Invoke(specification);
        }
    }
}