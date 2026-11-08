// Narrow native-object adapters for production patch tests. They do not execute Unity,
// install Harmony hooks, or prove the real TMP fallback/material lifecycle is correct.
namespace UnityEngine
{
    public class Object
    {
        protected class Native { internal static int Next; internal int Id = ++Next; internal bool Destroyed; }
        protected Native Handle = new();
        public int GetInstanceID() => Handle.Id;
        public static int DestroyCalls;
        public static void Destroy(Object value) { DestroyCalls++; value.Handle.Destroyed = true; }
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.Handle.Destroyed, bn = ReferenceEquals(b, null) || b.Handle.Destroyed;
            return an || bn ? an == bn : a.Handle.Id == b.Handle.Id;
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object value) => ReferenceEquals(this, value); // wrappers are different managed references
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public struct Color(float red, float green, float blue, float alpha = 1)
    {
        public float r = red, g = green, b = blue, a = alpha;
    }
    public sealed class Material : Object
    {
        private sealed class Data : Native
        {
            internal Dictionary<string, float> Floats = new();
            internal Dictionary<string, Color> Colors = new();
            internal HashSet<string> Keywords = new();
            internal string Shader = "SDF";
        }
        private Data Value => (Data)Handle;
        public static int Instances;
        public Material() { Handle = new Data(); }
        public Material(Material source) : this() { Instances++; CopyPropertiesFromMaterial(source); shader = source.shader; }
        private Material(Data value) { Handle = value; }
        public Material Alias() => new(Value);
        public string shader { get => Value.Shader; set => Value.Shader = value; }
        public void AssertAlive() { if (Handle.Destroyed) throw new InvalidOperationException("Material referenced by fallback was destroyed"); }
        public bool HasProperty(string name) { AssertAlive(); return Value.Floats.ContainsKey(name) || Value.Colors.ContainsKey(name); }
        public float GetFloat(string name) { AssertAlive(); return Value.Floats[name]; }
        public void SetFloat(string name, float value) { AssertAlive(); Value.Floats[name] = value; }
        public Color GetColor(string name) { AssertAlive(); return Value.Colors[name]; }
        public void SetColor(string name, Color value) { AssertAlive(); Value.Colors[name] = value; }
        public string[] shaderKeywords => Value.Keywords.ToArray();
        public bool IsKeywordEnabled(string name) => throw new MissingMethodException("Installed interop ReadOnlySpan.GetPinnableReference is unavailable");
        public void EnableKeyword(string name) => Value.Keywords.Add(name);
        public void DisableKeyword(string name) => Value.Keywords.Remove(name);
        public void CopyPropertiesFromMaterial(Material source)
        {
            source.AssertAlive(); AssertAlive();
            Value.Floats = new(source.Value.Floats); Value.Colors = new(source.Value.Colors); Value.Keywords = new(source.Value.Keywords);
        }
    }
    public sealed class GameObject : Object
    {
        public readonly List<TMPro.TextMeshProUGUI> Texts = new();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Texts.Cast<T>().ToArray();
    }
}
namespace TMPro
{
    public sealed class TextMeshProUGUI : UnityEngine.Object
    {
        private UnityEngine.Material shared, instance;
        public UnityEngine.Color color;
        public bool havePropertiesChanged;
        public int VertexDirty, MaterialDirty, PaddingUpdates, ParsedOrAdvanced;
        public UnityEngine.Material fontSharedMaterial { get => shared?.Alias(); set => shared = value; }
        public UnityEngine.Material m_fontMaterial => instance?.Alias();
        public UnityEngine.Material fontMaterial
        {
            get
            {
                if (instance == null || instance != shared) instance = new UnityEngine.Material(shared);
                shared = instance;
                return instance.Alias();
            }
        }
        public void UpdateMeshPadding() => PaddingUpdates++;
        public void SetVerticesDirty() => VertexDirty++;
        public void SetMaterialDirty() => MaterialDirty++;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.Empty<T>();
        public UnityEngine.Material FallbackReference;
        public void RenderCanvas()
        {
            FallbackReference?.AssertAlive(); // keep prior frame reference until the normal canvas rebuild
            fontSharedMaterial.AssertAlive();
            FallbackReference = fontSharedMaterial;
            havePropertiesChanged = false;
        }
    }
}
namespace DMM.OLG.Unity.Extensions.Novel
{
    public sealed class EventText { public IntPtr Pointer = new(100); public float fontSpacing; }
    public sealed class EventMessage { public TMPro.TextMeshProUGUI MessageName; }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string name) { } }
    public sealed class HarmonyPrefix : Attribute { }
    public sealed class HarmonyPostfix : Attribute { }
}
namespace GCMod.Services
{
    public static class DialogueSettings { public static bool ReadabilityEnabled; }
}
namespace GCMod
{
    public sealed class Entry<T>(T value) { public T Value = value; }
    public static class Config
    {
        public static Entry<float> FaceDilate = new(0.3f), OutlineWidth = new(0.3f), OutlineSoftness = new(0.01f), CharacterSpacing = new(0);
        public static UnityEngine.Color OutlineColor = new(0.2f, 0.2f, 0.2f), MessageTextColor = new(1, 1, 1), NameTextColor = new(0.8f, 0.9f, 1);
    }
}
