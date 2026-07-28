using Domus.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Domus.Api.Common;

/// <summary>
/// Traduz excecoes de dominio para ProblemDetails com o status correto.
/// O dominio nunca conhece HTTP; a traducao acontece so aqui.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (status, title) = Map(exception);

            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);
            }
            else
            {
                logger.LogInformation("Requisição recusada em {Path}: {Message}", context.Request.Path, exception.Message);
            }

            if (context.Response.HasStarted) throw;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Algo deu errado. Tente novamente em instantes."
                    : exception.Message,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int Status, string Title) Map(Exception exception) => exception switch
    {
        DomainValidationException => (StatusCodes.Status400BadRequest, "Dados invalidos"),
        DomainRuleException => (StatusCodes.Status409Conflict, "Operação não permitida"),
        NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado"),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Sem permissão"),
        _ => (StatusCodes.Status500InternalServerError, "Erro interno")
    };
}
