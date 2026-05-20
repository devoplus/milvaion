using Milvaion.Application.Dtos.ScheduledJobDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;

namespace Milvaion.Application.Features.ScheduledJobs.GetJobOccurenceListCursor;

/// <summary>
/// Data transfer object for scheduledjob list.
/// </summary>
public record GetJobOccurenceListCursorQuery : CursorListRequest, IQuery<CursorListResponse<JobOccurrenceListDto>>
{
    /// <summary>
    /// Search term to filter job occurrences.
    /// </summary>
    public string SearchTerm { get; set; }
}