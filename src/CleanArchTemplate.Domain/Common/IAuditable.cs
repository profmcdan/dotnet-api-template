namespace CleanArchTemplate.Domain.Common;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    Guid? CreatedBy { get; set; }

    Guid? UpdatedBy { get; set; }
}
