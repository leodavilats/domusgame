namespace Domus.Domain.Common;

/// <summary>
/// Base de toda entidade identificada por <see cref="Guid"/>.
/// Usamos GUID v7 (sequencial no tempo) para manter localidade nos indices do Postgres.
/// </summary>
public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    // Construtor sem parametros exigido pelo materializador do EF Core.
    protected Entity() { }

    public Guid Id { get; protected set; }

    public static Guid NewId() => Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
