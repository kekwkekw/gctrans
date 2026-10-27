using System;
using System.Reflection;

[assembly: AssemblyCompany("Jitsu")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("7.0.0.0")]
[assembly: AssemblyInformationalVersion("7.0.0-ko-localparity+0cdcd9e84fffa438dfdac63176685a6c47a07318")]
[assembly: AssemblyProduct("GCMod")]
[assembly: AssemblyTitle("GCMod")]
[assembly: AssemblyVersion("7.0.0.0")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;
        public NullableAttribute(byte flag) => NullableFlags = new byte[] { flag };
        public NullableAttribute(byte[] flags) => NullableFlags = flags;
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public readonly byte Flag;
        public NullableContextAttribute(byte flag) => Flag = flag;
    }
}
