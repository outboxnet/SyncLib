namespace SyncLib.Abstractions;

/// <summary>
/// Base contract for entities persisted by a <see cref="ISyncRepository{TEntity}"/>.
/// </summary>
public interface IEntity
{
    /// <summary>Stable primary key.</summary>
    Guid Id { get; set; }

    /// <summary>UTC time the entity was first persisted locally.</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>UTC time the entity was last updated locally.</summary>
    DateTime? UpdatedAt { get; set; }
}
