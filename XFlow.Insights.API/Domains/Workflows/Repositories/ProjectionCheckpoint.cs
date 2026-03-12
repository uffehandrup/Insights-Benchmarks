using EventStore.Client;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public readonly record struct ProjectionPosition(ulong CommitPosition, ulong PreparePosition) : IComparable<ProjectionPosition>
{
    public static ProjectionPosition From(Position position) => new(position.CommitPosition, position.PreparePosition);

    public static ProjectionPosition FromDatabase(long commitPosition, long preparePosition) =>
        new(checked((ulong)commitPosition), checked((ulong)preparePosition));

    public Position ToEventStorePosition() => new(CommitPosition, PreparePosition);

    public long CommitPositionInt64 => checked((long)CommitPosition);

    public long PreparePositionInt64 => checked((long)PreparePosition);

    public int CompareTo(ProjectionPosition other)
    {
        var commitComparison = CommitPosition.CompareTo(other.CommitPosition);
        if (commitComparison != 0)
        {
            return commitComparison;
        }

        return PreparePosition.CompareTo(other.PreparePosition);
    }
}

public sealed record ProjectionCheckpoint(
    string ProjectionName,
    ProjectionPosition Position,
    DateTime UpdatedAt);