// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public partial class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
   RenderFragment<string> header = (context) => 

#line default
#line hidden
#nullable disable
            (__builder2) => {
                __builder2.OpenElement(0, "div");
#nullable restore
#line (1,56)-(1,82) 25 "x:\dir\subdir\Test\TestComponent.cshtml"
__builder2.AddContent(1, context.ToLowerInvariant());

#line default
#line hidden
#nullable disable
                __builder2.CloseElement();
            }
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                                                                       ; 

#line default
#line hidden
#nullable disable
            __builder.OpenComponent<Test.MyComponent>(2);
            __builder.AddAttribute(3, "Header", (Microsoft.AspNetCore.Components.RenderFragment<System.String>)(
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
                     header

#line default
#line hidden
#nullable disable
            ));
            __builder.AddAttribute(4, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)((__builder2) => {
                __builder2.AddMarkupContent(5, "\r\n    Some Content\r\n");
            }
            ));
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591
