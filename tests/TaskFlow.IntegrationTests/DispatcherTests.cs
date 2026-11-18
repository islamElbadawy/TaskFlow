using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Dispatchers;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.IntegrationTests;

/// <summary>The dispatchers on their own (no API, no database): validation step, handler call, missing handler.</summary>
public sealed class DispatcherTests
{
    [Fact]
    public async Task Invalid_command_returns_the_field_errors_and_never_reaches_the_handler()
    {
        var handler = new EchoHandler();
        var dispatcher = CreateDispatcher(handler, withValidator: true);

        var result = await dispatcher.DispatchAsync(new EchoCommand { Text = "" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(new[] { "Text is required" }, result.ValidationErrors!["Text"]);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Valid_command_reaches_the_handler()
    {
        var handler = new EchoHandler();
        var dispatcher = CreateDispatcher(handler, withValidator: true);

        var result = await dispatcher.DispatchAsync(new EchoCommand { Text = "hello" });

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Data);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Command_without_validators_reaches_the_handler()
    {
        var handler = new EchoHandler();
        var dispatcher = CreateDispatcher(handler, withValidator: false);

        var result = await dispatcher.DispatchAsync(new EchoCommand { Text = "" });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Missing_handler_throws_a_clear_error()
    {
        var dispatcher = new CommandDispatcher(new ServiceCollection().BuildServiceProvider());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(new EchoCommand { Text = "x" }));
        Assert.Contains("No handler is registered for EchoCommand", error.Message);
    }

    private static CommandDispatcher CreateDispatcher(EchoHandler handler, bool withValidator)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<EchoCommand, Result<string>>>(handler);
        if (withValidator)
            services.AddSingleton<IValidator<EchoCommand>, EchoValidator>();
        return new CommandDispatcher(services.BuildServiceProvider());
    }

    public sealed class EchoCommand : ICommand<Result<string>>
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class EchoValidator : AbstractValidator<EchoCommand>
    {
        public EchoValidator() => RuleFor(x => x.Text).NotEmpty().WithMessage("Text is required");
    }

    private sealed class EchoHandler : ICommandHandler<EchoCommand, Result<string>>
    {
        public int Calls { get; private set; }

        public Task<Result<string>> HandleAsync(EchoCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result<string>.Success(command.Text));
        }
    }
}
