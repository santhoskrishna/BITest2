// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Xunit;

namespace Microsoft.AspNetCore.Components
{
    public class ElementReferenceJsonConverterTest
    {
        private readonly IJSRuntime TestRuntime;

        private readonly ElementReferenceJsonConverter Converter;

        public ElementReferenceJsonConverterTest()
        {
            TestRuntime = new TestJsRuntime();
            Converter = new ElementReferenceJsonConverter(TestRuntime);
        }

        [Fact]
        public void Serializing_Works()
        {
            // Arrange
            var elementReference = ElementReference.CreateWithUniqueId(TestRuntime);
            var expected = $"{{\"__internalId\":\"{elementReference.Id}\"}}";
            var memoryStream = new MemoryStream();
            var writer = new Utf8JsonWriter(memoryStream);

            // Act
            Converter.Write(writer, elementReference, new JsonSerializerOptions());
            writer.Flush();

            // Assert
            var json = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Deserializing_Works()
        {
            // Arrange
            var id = ElementReference.CreateWithUniqueId(TestRuntime).Id;
            var json = $"{{\"__internalId\":\"{id}\"}}";
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes);
            reader.Read();

            // Act
            var elementReference = Converter.Read(ref reader, typeof(ElementReference), new JsonSerializerOptions());

            // Assert
            Assert.Equal(id, elementReference.Id);
        }

        [Fact]
        public void Deserializing_WithFormatting_Works()
        {
            // Arrange
            var id = ElementReference.CreateWithUniqueId(TestRuntime).Id;
            var json =
@$"{{
    ""__internalId"": ""{id}""
}}";
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes);
            reader.Read();

            // Act
            var elementReference = Converter.Read(ref reader, typeof(ElementReference), new JsonSerializerOptions());

            // Assert
            Assert.Equal(id, elementReference.Id);
        }

        [Fact]
        public void Deserializing_Throws_IfUnknownPropertyAppears()
        {
            // Arrange
            var json = "{\"id\":\"some-value\"}";
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act
            var ex = Assert.Throws<JsonException>(() =>
            {
                var reader = new Utf8JsonReader(bytes);
                reader.Read();
                Converter.Read(ref reader, typeof(ElementReference), new JsonSerializerOptions());
            });

            // Assert
            Assert.Equal("Unexpected JSON property 'id'.", ex.Message);
        }

        [Fact]
        public void Deserializing_Throws_IfIdIsNotSpecified()
        {
            // Arrange
            var json = "{}";
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act
            var ex = Assert.Throws<JsonException>(() =>
            {
                var reader = new Utf8JsonReader(bytes);
                reader.Read();
                Converter.Read(ref reader, typeof(ElementReference), new JsonSerializerOptions());
            });

            // Assert
            Assert.Equal("__internalId is required.", ex.Message);
        }

        private class TestJsRuntime : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args) =>
                ValueTask.FromResult(default(TValue));

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object[] args) =>
                ValueTask.FromResult(default(TValue));
        }
    }
}
