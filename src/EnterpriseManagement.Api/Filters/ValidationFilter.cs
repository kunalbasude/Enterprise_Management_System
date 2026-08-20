using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ApplicationValidationException = EnterpriseManagement.Application.Common.Exceptions.ValidationException;

namespace EnterpriseManagement.Api.Filters;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument
/// before the action executes.
/// </summary>
/// <remarks>
/// <para>
/// A filter rather than a call at the top of each action: validation that must
/// be remembered is validation that will eventually be forgotten, and the first
/// endpoint that forgets is the one that takes unvalidated input.
/// </para>
/// <para>
/// It throws <see cref="ApplicationValidationException"/> rather than building a
/// response, so the failure travels through the same exception middleware as
/// everything else and produces one consistent error shape.
/// </para>
/// </remarks>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>();

        // Model binding failures first.
        //
        // ApiBehaviorOptions.SuppressModelStateInvalidFilter is enabled so that
        // MVC's built-in 400 does not bypass the exception middleware and return
        // a different error shape. The catch is that suppressing it also
        // discards BINDING errors, not just validation ones -- so a request like
        // ?status=abc or ?status=99 would silently bind to null and return
        // unfiltered results, with the caller never told their filter was
        // ignored. Surfacing them here keeps one error shape without losing them.
        if (!context.ModelState.IsValid)
        {
            foreach (var entry in context.ModelState)
            {
                if (entry.Value.Errors.Count == 0)
                {
                    continue;
                }

                var key = string.IsNullOrEmpty(entry.Key) ? "request" : entry.Key;

                errors[key] = entry.Value.Errors
                    // Exception messages from a binder can expose type internals,
                    // so a generic message is used when one is absent.
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "The supplied value is not valid for this field."
                        : e.ErrorMessage)
                    .ToArray();
            }
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            // Resolve IValidator<TArgument> for the runtime type. Endpoints with
            // no validator simply skip, so adding one is opt-in by existing.
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid)
            {
                continue;
            }

            foreach (var group in result.Errors.GroupBy(e => e.PropertyName))
            {
                errors[group.Key] = group.Select(e => e.ErrorMessage).ToArray();
            }
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }

        await next();
    }
}
