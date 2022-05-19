// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Shared;
using Type = System.Type;

namespace Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal.Json;

internal sealed class AnyConverter<TMessage> : SettingsConverterBase<TMessage> where TMessage : IMessage, new()
{
    internal const string AnyTypeUrlField = "@type";
    internal const string AnyWellKnownTypeValueField = "value";

    public AnyConverter(JsonContext context) : base(context)
    {
    }

    public override TMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var d = JsonDocument.ParseValue(ref reader);
        if (!d.RootElement.TryGetProperty(AnyTypeUrlField, out var urlField))
        {
            throw new InvalidOperationException("Any message with no @type.");
        }

        var typeUrl = urlField.GetString();
        var typeName = Any.GetTypeName(typeUrl);

        var descriptor = Context.TypeRegistry.Find(typeName);
        if (descriptor == null)
        {
            throw new InvalidOperationException($"Type registry has no descriptor for type name '{typeName}'.");
        }

        IMessage data;
        if (ServiceDescriptorHelpers.IsWellKnownType(descriptor))
        {
            if (!d.RootElement.TryGetProperty(AnyWellKnownTypeValueField, out var valueField))
            {
                throw new InvalidOperationException($"Expected '{AnyWellKnownTypeValueField}' property for well-known type Any body.");
            }

            data = (IMessage)JsonSerializer.Deserialize(valueField, descriptor.ClrType, options)!;
        }
        else
        {
            data = (IMessage)JsonSerializer.Deserialize(d.RootElement, descriptor.ClrType, options)!;
        }

        var message = new TMessage();
        message.Descriptor.Fields[Any.TypeUrlFieldNumber].Accessor.SetValue(message, typeUrl);
        message.Descriptor.Fields[Any.ValueFieldNumber].Accessor.SetValue(message, data.ToByteString());

        return message;
    }

    public override void Write(Utf8JsonWriter writer, TMessage value, JsonSerializerOptions options)
    {
        var typeUrl = (string)value.Descriptor.Fields[Any.TypeUrlFieldNumber].Accessor.GetValue(value);
        var data = (ByteString)value.Descriptor.Fields[Any.ValueFieldNumber].Accessor.GetValue(value);
        var typeName = Any.GetTypeName(typeUrl);
        var descriptor = Context.TypeRegistry.Find(typeName);
        if (descriptor == null)
        {
            throw new InvalidOperationException($"Type registry has no descriptor for type name '{typeName}'.");
        }
        var valueMessage = descriptor.Parser.ParseFrom(data);
        writer.WriteStartObject();
        writer.WriteString(AnyTypeUrlField, typeUrl);

        if (ServiceDescriptorHelpers.IsWellKnownType(descriptor))
        {
            writer.WritePropertyName(AnyWellKnownTypeValueField);
            if (ServiceDescriptorHelpers.IsWrapperType(descriptor))
            {
                var wrappedValue = valueMessage.Descriptor.Fields[JsonConverterHelper.WrapperValueFieldNumber].Accessor.GetValue(valueMessage);
                JsonSerializer.Serialize(writer, wrappedValue, wrappedValue.GetType(), options);
            }
            else
            {
                JsonSerializer.Serialize(writer, valueMessage, valueMessage.GetType(), options);
            }
        }
        else
        {
            MessageConverter<Any>.WriteMessageFields(writer, valueMessage, Context.Settings, options);
        }

        writer.WriteEndObject();
    }
}
