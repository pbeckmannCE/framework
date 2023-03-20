using Microsoft.AspNetCore.Builder;
using Signum.API;

namespace Signum.Templating;

public static class TemplatingServer
{
    public static Func<bool>? TemplateTokenMessageAllowed; 

    public static void Start(IApplicationBuilder app)
    {
        SignumControllerFactory.RegisterArea(MethodInfo.GetCurrentMethod());

        ReflectionServer.RegisterLike(typeof(TemplateTokenMessage), () => TemplateTokenMessageAllowed.GetInvocationListTyped().Any(f => f()));
    }
}
