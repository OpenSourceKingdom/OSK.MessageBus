﻿using Microsoft.Extensions.DependencyInjection;
using OSK.Functions.Outputs.Abstractions;
using OSK.Functions.Outputs.Logging.Abstractions;
using OSK.Transmissions.Abstractions;
using OSK.Transmissions.Internal;
using OSK.Transmissions.Messages.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OSK.Transmissions.Internal.Services
{
    internal class MessageBroadcaster(IEnumerable<MessageTransmitterDescriptor> transmitterDescriptors,
        IServiceProvider serviceProvider,
        IOutputFactory<MessageBroadcaster> outputFactory)
        : IMessageBroadcaster
    {
        #region Variables

        private readonly static string OriginationSource = "OSK.Transmissions";

        #endregion

        #region IMessageBroadcaster

        public async Task<IOutput<BroadcastResult>> BroadcastMessageAsync<TMessage>(TMessage message, Action<MessageBroadcastOptions> broadcastConfiguration,
            CancellationToken cancellationToken = default)
            where TMessage : IMessage
        {
            if (broadcastConfiguration is null)
            {
                throw new ArgumentNullException(nameof(broadcastConfiguration));
            }

            MessageBroadcastOptions options = new();
            broadcastConfiguration(options);

            var targetedDescriptors = options.TargetTransmitterIds is null
                ? transmitterDescriptors
                : transmitterDescriptors.Where(transmitter => options.TargetTransmitterIds.Contains(transmitter.TransmitterId));

            var transmissionResults = new List<MessageTransmissionResult>();
            foreach (var descriptor in targetedDescriptors)
            {
                var transmissionResult = new MessageTransmissionResult()
                {
                    TransmitterId = descriptor.TransmitterId
                };

                var transmitter = (IMessageTransmitter)serviceProvider.GetRequiredService(descriptor.TransmitterType);
                try
                {
                    await transmitter.TransmitAsync(message, options.TransmissionOptions, cancellationToken);
                }
                catch (Exception ex)
                {
                    transmissionResult.Exception = ex;
                }

                transmissionResults.Add(transmissionResult);
            }

            var errorCount = transmissionResults.Count(transmission => transmission.Exception is not null);

            if (errorCount == 0)
            {
                return outputFactory.Success(new BroadcastResult()
                {
                    TransmissionResults = transmissionResults
                });
            }
            if (errorCount == transmissionResults.Count)
            {
                return outputFactory.Exception<BroadcastResult>
                    (new AggregateException(transmissionResults.Select(transmission => transmission.Exception)),
                    OriginationSource);
            }

            return outputFactory.Create(new BroadcastResult()
            {
                TransmissionResults = transmissionResults
            }, new OutputStatusCode(HttpStatusCode.MultiStatus, DetailCode.DownStreamError, OriginationSource));
        }

        #endregion
    }
}
