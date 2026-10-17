#nullable enable
using System.Security.Cryptography;
using System.Text;
namespace GCMod.Nextgen;
/// Stable role tag for bounded source observations, never an availability proof.
public static class Ui6SourceDiagnosticCodes
{
    public static string RoleTag(string identity) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..12];
    public static string Create(string identity,long owner,string reason) => $"UI6S_{RoleTag(identity)}_O{owner}_{reason}";
}
