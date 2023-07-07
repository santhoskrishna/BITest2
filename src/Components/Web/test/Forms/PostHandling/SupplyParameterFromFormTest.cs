// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Forms.ModelBinding;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Test.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Components.Forms.PostHandling;

public class SupplyParameterFromFormTest
{
    [Fact]
    public async Task FindCascadingParameters_HandlesSupplyParameterFromFormValues()
    {
        // Arrange
        var renderer = CreateRendererWithFormValueModelBinder();
        var cascadingModelBinder = new CascadingModelBinder
        {
            Navigation = new TestNavigationManager(),
            FormValueModelBinder = new TestFormModelValueBinder(),
            ChildContent = modelBindingContext => builder =>
            {
                builder.OpenComponent<FormParametersComponent>(0);
                builder.CloseComponent();
            }
        };

        // Act
        var componentId = renderer.AssignRootComponentId(cascadingModelBinder);
        await renderer.RenderRootComponentAsync(componentId);
        var formComponentState = renderer.Batches.Single()
            .GetComponentFrames<FormParametersComponent>().Single()
            .ComponentState;

        var result = CascadingParameterState.FindCascadingParameters(formComponentState);

        // Assert
        var supplier = Assert.Single(result);
        Assert.Equal(cascadingModelBinder, supplier.ValueSupplier);
    }

    [Fact]
    public async Task FindCascadingParameters_HandlesSupplyParameterFromFormValues_WithName()
    {
        // Arrange
        var renderer = CreateRendererWithFormValueModelBinder();
        var cascadingModelBinder = new CascadingModelBinder
        {
            Navigation = new TestNavigationManager(),
            FormValueModelBinder = new TestFormModelValueBinder("some-name"),
            ChildContent = modelBindingContext => builder =>
            {
                builder.OpenComponent<FormParametersComponentWithName>(0);
                builder.CloseComponent();
            }
        };

        // Act
        var componentId = renderer.AssignRootComponentId(cascadingModelBinder);
        await renderer.RenderRootComponentAsync(componentId);
        var formComponentState = renderer.Batches.Single()
            .GetComponentFrames<FormParametersComponentWithName>().Single()
            .ComponentState;

        var result = CascadingParameterState.FindCascadingParameters(formComponentState);

        // Assert
        var supplier = Assert.Single(result);
        Assert.Equal(cascadingModelBinder, supplier.ValueSupplier);
    }

    static TestRenderer CreateRendererWithFormValueModelBinder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFormValueModelBinder, TestFormModelValueBinder>();
        return new TestRenderer(services.BuildServiceProvider());
    }

    class FormParametersComponent : TestComponentBase
    {
        [SupplyParameterFromForm] public string FormParameter { get; set; }
    }

    class FormParametersComponentWithName : TestComponentBase
    {
        [SupplyParameterFromForm(Handler = "some-name")] public string FormParameter { get; set; }
    }

    class TestFormModelValueBinder(string FormName = "") : IFormValueModelBinder
    {
        public void Bind(FormValueModelBindingContext context) { }

        public bool CanBind(Type valueType, string formName = null)
            => formName is null || formName == FormName;
    }

    class TestComponentBase : IComponent
    {
        public void Attach(RenderHandle renderHandle)
        {
        }

        public Task SetParametersAsync(ParameterView parameters)
            => Task.CompletedTask;
    }

    class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://localhost:85/subdir/", "https://localhost:85/subdir/path?query=value#hash");
        }
    }
}
