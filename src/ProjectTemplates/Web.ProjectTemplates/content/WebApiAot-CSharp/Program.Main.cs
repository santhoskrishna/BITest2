using System.Text.Json.Serialization;

namespace Company.WebApplication1;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        var app = builder.Build();

        var sampleTodos = new Todo[] {
            new(1, "Walk the dog"),
            new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
            new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
            new(4, "Clean the bathroom"),
            new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
        };

        var todosApi = app.MapGroup("/todos");
        #if (EnableOpenAPI)
        todosApi.MapGet("/", () => sampleTodos)
                .WithName("GetTodos");

        todosApi.MapGet("/{id}", Results<Ok<Todo>, NotFound> (int id) =>
            sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
                ? TypedResults.Ok(todo)
                : TypedResults.NotFound())
            .WithName("GetTodoById");
        #else
        todosApi.MapGet("/", () => sampleTodos);

        todosApi.MapGet("/{id}", Results<Ok<Todo>, NotFound> (int id) =>
            sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
                ? TypedResults.Ok(todo)
                : TypedResults.NotFound());
        #endif

        app.Run();
    }
}

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
