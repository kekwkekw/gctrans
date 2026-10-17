using System.Security.Cryptography;
using System.Text;

namespace GCMod.Nextgen;

/// Codes describing a finite requested signature, never native addresses or display data.
public static class HookDiagnosticCodes
{
    public static string Identity(string owner, string member, IEnumerable<string> parameters) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + "::" + member + "(" + string.Join(",", parameters) + ")")))[..12];

    public static string Symbol(string value, int maximum) => new(value.ToUpperInvariant()
        .Select(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' ? c : '_')
        .Take(Math.Clamp(maximum, 0, 64)).ToArray());

    public static string Discovery(string owner, string member, int arity, int candidates)
    {
        string id = Identity(owner, member, new[] { "arity:" + arity });
        string reason = candidates == 0 ? "MISSING" : candidates == 1 ? "UNIQUE" : "AMBIGUOUS";
        return "HOOK_DISC_" + id + "_" + Symbol(owner, 27) + "_" + Symbol(member, 18) + "_A" + arity + "_" + reason;
    }

    public static IEnumerable<string> Signature(string owner, string member, IReadOnlyList<string> parameters)
    {
        string id = Identity(owner, member, parameters);
        yield return "HOOK_REQ_" + id + "_" + Symbol(owner, 32) + "_" + Symbol(member, 22) + "_A" + parameters.Count;
        for (int i = 0; i < Math.Min(parameters.Count, 16); i++)
            yield return "HOOK_ARG_" + id + "_I" + i + "_" + Symbol(parameters[i], 54);
    }

    public static string Outcome(string owner, string member, IReadOnlyList<string> parameters, string outcome, string reason) =>
        "HOOK_RESULT_" + Identity(owner, member, parameters) + "_" + Symbol(outcome, 12) + "_" + Symbol(reason, 47);
}
