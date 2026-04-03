using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.Codes;
using AFH.Common.Errors.Exceptions;
using AFH.Common.Errors.Mapping;
using AFH.Common.Errors.Models;
using AFH.Location.Domain.Errors;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AFH.Location.Function.Middleware;

public sealed class LocationExceptionMapper : IExceptionMapper
{
    private static readonly ErrorCode ValidationError = new(
        "VALIDATION_ERROR",
        ErrorCategory.Validation,
        ErrorSeverity.Warning,
        "Invalid JSON payload.");

    private static readonly ErrorCode InternalError = new(
        "INTERNAL_ERROR",
        ErrorCategory.Unknown,
        ErrorSeverity.Error,
        "Something went wrong.");

    public ExceptionMappingResult Map(Exception exception, ErrorContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return TryMap(exception, context).MappingResult;
    }

    internal LocationHandledException TryMap(Exception exception, ErrorContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is DestinationResolveException destinationResolveException)
        {
            var errorCode = new ErrorCode(
                destinationResolveException.Code,
                ErrorCategory.Validation,
                ErrorSeverity.Warning,
                destinationResolveException.Message);

            return new LocationHandledException(
                new ExceptionMappingResult
                {
                    Exception = exception,
                    ErrorCode = errorCode,
                    Message = destinationResolveException.Message,
                    StatusCode = (int)HttpStatusCode.UnprocessableEntity,
                    Context = context
                },
                "DestinationResolution",
                LogLevel.Warning);
        }

        if (LooksLikeDeserializationFailure(exception))
        {
            const string message = "Invalid JSON payload.";
            var validationErrors = new[]
            {
                new ValidationErrorDetail("body", message, ValidationErrorCodes.InvalidFormat.Value)
            };

            return new LocationHandledException(
                new ExceptionMappingResult
                {
                    Exception = new ValidationException(validationErrors, message, exception),
                    ErrorCode = ValidationError,
                    Message = message,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Context = context,
                    ValidationErrors = validationErrors
                },
                "RequestDeserialization",
                LogLevel.Warning);
        }

        return new LocationHandledException(
            new ExceptionMappingResult
            {
                Exception = exception,
                ErrorCode = InternalError,
                Message = "Something went wrong.",
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Context = context
            },
            "UnhandledException",
            LogLevel.Error);
    }

    private static bool LooksLikeDeserializationFailure(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                if (LooksLikeDeserializationFailure(inner))
                    return true;
            }
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is JsonException || current is FormatException)
                return true;
        }

        return false;
    }

    internal sealed record LocationHandledException(
        ExceptionMappingResult MappingResult,
        string FailureSource,
        LogLevel Level);
}
