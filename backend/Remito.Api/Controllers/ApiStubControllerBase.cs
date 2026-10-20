using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

public abstract class ApiStubControllerBase : ControllerBase
{
    protected IActionResult NotImplementedYet() =>
        Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented",
            detail: "This API endpoint is defined by the contract but is not implemented yet.");
}
