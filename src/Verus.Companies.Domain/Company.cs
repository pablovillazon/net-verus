namespace Verus.Companies.Domain;

/// <summary>
/// Core domain entity. Immutable by design: once a company has been validated and
/// created, it never changes in place (updates will produce a new instance, which
/// keeps the in-memory store safe to share across threads without locking).
/// </summary>
public sealed class Company
{
    public Company(Guid id, string name, Uri websiteUrl, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        WebsiteUrl = websiteUrl;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string Name { get; }
    public Uri WebsiteUrl { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
