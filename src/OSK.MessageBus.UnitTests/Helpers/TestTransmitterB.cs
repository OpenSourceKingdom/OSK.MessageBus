﻿using OSK.MessageBus.Abstractions;
using OSK.MessageBus.Events.Abstractions;

namespace OSK.MessageBus.UnitTests.Helpers
{
    public class TestTransmitterB : IMessageTransmitter
    {
        public Exception ExceptionToThrow { get; set; }

        public Task TransmitAsync<TMessage>(TMessage message, MessageTransmissionOptions options, CancellationToken cancellationToken = default)
            where TMessage : IMessage
        {
            return ExceptionToThrow is null
                ? Task.CompletedTask
                : throw ExceptionToThrow;
        }
    }
}
