using System.Threading.Tasks;

namespace KargoApp.Data;

public interface IKargoAppDbSchemaMigrator
{
    Task MigrateAsync();
}
