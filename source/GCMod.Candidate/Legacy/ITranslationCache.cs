using System.Threading;
using System.Threading.Tasks;
namespace GCMod.Services;

/// <summary>Narrow provider refactor of the sealed donor cache; parser and session contracts are retained.</summary>
public interface ITranslationCache
{
    Task<Manifest> FetchManifestAsync(CancellationToken cancellationToken = default);
    Task<T> LoadAsync<T>(string type, string id = null, CancellationToken cancellationToken = default) where T : class;
    Task<T> LoadLocalAsync<T>(string type, string id = null, CancellationToken cancellationToken = default) where T : class;
}
