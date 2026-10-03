using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Samples.Commands.CreateSample;
using Meshtrail.Core.Application.UseCases.Samples.Commands.DeleteSample;
using Meshtrail.Core.Application.UseCases.Samples.Commands.UpdateSample;
using Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleById;
using Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleFilterOptions;
using Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleGrid;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Samples;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>
/// Reference controller: it only turns HTTP into messages and back. No logic, no try/catch
/// (the global exception handler maps errors to status codes).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/samples")]
public sealed class SamplesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SampleGridItemDto>>> GetGrid([FromQuery] SampleGridRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSampleGridQuery(request), cancellationToken));

    [HttpGet("filter-options")]
    public async Task<ActionResult<SampleFilterOptionsDto>> GetFilterOptions(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSampleFilterOptionsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SampleDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSampleByIdQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<SampleDto>> Create(CreateSampleRequest request, CancellationToken cancellationToken)
    {
        var sample = await sender.Send(new CreateSampleCommand(request.Name, request.Description), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = sample.Id, version = "1" }, sample);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SampleDto>> Update(Guid id, UpdateSampleRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateSampleCommand(id, request.Name, request.Description, request.RowVersion), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSampleCommand(id), cancellationToken);
        return NoContent();
    }
}
