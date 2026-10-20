using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Remito.Api.Controllers;

namespace Remito.ContractTests;

public sealed class StubEndpointContractTests
{
    [Fact]
    public void Currency_list_route_returns_problem_details_with_not_implemented_status()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var serviceProvider = services.BuildServiceProvider();
        var controller = new CurrenciesController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = serviceProvider }
            }
        };

        var result = Assert.IsType<ObjectResult>(controller.List(CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
        Assert.Equal(StatusCodes.Status501NotImplemented, problem.Status);
    }
}
