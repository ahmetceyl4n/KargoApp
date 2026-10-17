using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace KargoApp.Branches;

// Id = şube kodu ("IST01"). Ayrı bir Guid'e gerek yok, kod zaten benzersiz.
public class Branch : BasicAggregateRoot<string>
{
    public string Name { get; private set; } = default!;
    public string City { get; private set; } = default!;

    protected Branch() { }

    public Branch(string code, string name, string city)
        : base(Check.NotNullOrWhiteSpace(code, nameof(code), BranchConsts.MaxCodeLength))
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BranchConsts.MaxNameLength);
        City = Check.NotNullOrWhiteSpace(city, nameof(city), BranchConsts.MaxCityLength);
    }
}
