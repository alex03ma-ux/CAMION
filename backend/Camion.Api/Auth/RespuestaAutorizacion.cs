using Camion.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Camion.Api.Auth;

// RN-SE-006: el 403 del middleware (token válido sin el rol requerido) se escribe con el formato común { error }.
// El 401 (sin token, token inválido o sesión no vigente) sigue el comportamiento por defecto, sin cuerpo.
public sealed class RespuestaAutorizacion : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _porDefecto = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(MensajesError.SinPermiso));
            return;
        }
        await _porDefecto.HandleAsync(next, context, policy, authorizeResult);
    }
}
