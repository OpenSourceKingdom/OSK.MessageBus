﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using OSK.Transmissions.Options;
using OSK.Transmissions.Ports;

namespace OSK.Transmissions.Internal.Services
{
    internal class MessageReceiverGroupBuilder<TReceiver>(IServiceProvider serviceProvider,
        IOptions<MessageBusConfigurationOptions> messageBusOptions)
        : IMessageReceiverGroupBuilder<TReceiver>
        where TReceiver : IMessageReceiver
    {
        #region Variables

        private readonly List<Action<IMessageReceiverBuilder>> _transmissionConfigurators = [];
        private readonly Dictionary<string, IMessageReceiverBuilder> _receiverBuilders = [];

        #endregion

        #region MessageReceiverGroupBuilder

        public IMessageReceiverGroupBuilder AddConfigurator(Action<IMessageReceiverBuilder> configurator)
        {
            if (configurator == null)
            {
                throw new ArgumentNullException(nameof(configurator));
            }

            _transmissionConfigurators.Add(configurator);
            return this;
        }

        public IMessageReceiverGroupBuilder<TReceiver> AddMessageReceiver(string receiverId, object[] parameters,
            Action<IMessageReceiverBuilder> receiverBuilderConfiguration)
        {
            AddReceiver(receiverId, typeof(TReceiver), parameters, receiverBuilderConfiguration);
            return this;
        }

        public IMessageReceiverGroupBuilder<TReceiver> AddMessageReceiver<TChildReceiver>(string receiverId, object[] parameters,
            Action<IMessageReceiverBuilder> receiverBuilderConfiguration)
            where TChildReceiver : TReceiver
        {
            AddReceiver(receiverId, typeof(TChildReceiver), parameters, receiverBuilderConfiguration);
            return this;
        }

        public IEnumerable<IMessageReceiver> BuildReceivers()
        {
            foreach (var builder in _receiverBuilders.Values)
            {
                foreach (var configurator in _transmissionConfigurators.Concat(messageBusOptions.Value.GlobalReceiverBuilderConfiguration))
                {
                    configurator.Invoke(builder);
                }

                yield return builder.BuildReceiver();
            }
        }

        #endregion

        #region Helpers

        private void AddReceiver(string receiverId, Type receiverType, object[] parameters,
            Action<IMessageReceiverBuilder> receiverBuilderConfiguration)
        {
            if (string.IsNullOrWhiteSpace(receiverId))
            {
                throw new ArgumentNullException(nameof(receiverId));
            }
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            if (receiverBuilderConfiguration is null)
            {
                throw new ArgumentNullException(nameof(receiverBuilderConfiguration));
            }
            if (_receiverBuilders.TryGetValue(receiverId, out _))
            {
                throw new InvalidOperationException($"Receiver id {receiverId} has already been added for receivers of type {receiverType.FullName}");
            }

            var descriptor = new MessageReceiverDescriptor(receiverId, receiverType, parameters);
            var builder = new MessageReceiverBuilder(serviceProvider, descriptor);
            receiverBuilderConfiguration(builder);

            _receiverBuilders.Add(receiverId, builder);
        }

        #endregion
    }
}
