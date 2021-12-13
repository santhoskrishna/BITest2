// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// Options to configure <see cref="SystemTextJsonInputFormatter"/> and <see cref="SystemTextJsonOutputFormatter"/>.
/// </summary>
public class JsonOptions
{
    /// <summary>
    /// Gets or sets a flag to determine whether error messages from JSON deserialization by the
    /// <see cref="SystemTextJsonInputFormatter"/> will be added to the <see cref="ModelStateDictionary"/>. If
    /// <see langword="false"/>, a generic error message will be used instead.
    /// </summary>
    /// <value>
    /// The default value is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Error messages in the <see cref="ModelStateDictionary"/> are often communicated to clients, either in HTML
    /// or using <see cref="BadRequestObjectResult"/>. In effect, this setting controls whether clients can receive
    /// detailed error messages about submitted JSON data.
    /// </remarks>
    public bool AllowInputFormatterExceptionMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets the flag which causes the <see cref="ValidationProblemDetails.Errors"/> be produces based on the ModelName
    /// obtained by the <see cref="JsonProperty.Name"/> <c>*/*</c>. <see langword="false"/> by default.
    /// </summary>
    public bool RespectModelNameInValidationErrors { get; set; }

    /// <summary>
    /// Gets the <see cref="System.Text.Json.JsonSerializerOptions"/> used by <see cref="SystemTextJsonInputFormatter"/> and
    /// <see cref="SystemTextJsonOutputFormatter"/>.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        // Limit the object graph we'll consume to a fixed depth. This prevents stackoverflow exceptions
        // from deserialization errors that might occur from deeply nested objects.
        // This value is the same for model binding and Json.Net's serialization.
        MaxDepth = MvcOptions.DefaultMaxModelBindingRecursionDepth,
    };
}
