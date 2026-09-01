namespace CleanArchTemplate.Domain.Common;

public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }

    bool IsDeleted => DeletedAt is not null;
}
