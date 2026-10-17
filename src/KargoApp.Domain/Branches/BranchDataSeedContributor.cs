using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KargoApp.Branches;

public class BranchDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Branch, string> _branchRepository;

    public BranchDataSeedContributor(IRepository<Branch, string> branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _branchRepository.GetCountAsync() > 0)
        {
            return; // Zaten eklenmiş, DbMigrator tekrar çalışınca kopya oluşmasın.
        }

        await _branchRepository.InsertManyAsync(new[]
        {
            new Branch("IST01", "İstanbul Avrupa", "İstanbul"),
            new Branch("IST02", "İstanbul Anadolu", "İstanbul"),
            new Branch("ANK01", "Ankara Merkez", "Ankara"),
            new Branch("IZM01", "İzmir Merkez", "İzmir"),
            new Branch("ESK01", "Eskişehir Merkez", "Eskişehir"),
            new Branch("BRS01", "Bursa Merkez", "Bursa"),
        }, autoSave: true);
    }
}
