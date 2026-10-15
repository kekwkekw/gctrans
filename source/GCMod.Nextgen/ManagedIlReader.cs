#nullable enable
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;

namespace GCMod.Nextgen;

/// <summary>Reads managed instruction boundaries; operand bytes are never treated as opcodes.</summary>
public static class ManagedIlReader
{
    static readonly IReadOnlyDictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);

    public static ImmutableArray<FieldInfo> StaticHandleFields(MethodInfo method, string fieldPrefix)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray() ?? throw new InvalidDataException("MANAGED_METHOD_BODY_UNAVAILABLE");
        var fields = ImmutableArray.CreateBuilder<FieldInfo>();
        int offset = 0;
        while (offset < bytes.Length)
        {
            short value = bytes[offset++];
            if (value == 0xfe)
            {
                RequireRemaining(bytes, offset, 1);
                value = unchecked((short)(0xfe00 | bytes[offset++]));
            }
            if (!opcodes.TryGetValue(value, out var opcode)) throw new InvalidDataException("INVALID_MANAGED_OPCODE");
            int size = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineType or OperandType.InlineI or OperandType.InlineMethod
                    or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => SwitchSize(bytes, offset),
                _ => throw new InvalidDataException("UNSUPPORTED_MANAGED_OPERAND")
            };
            RequireRemaining(bytes, offset, size);
            if (opcode == OpCodes.Ldsfld)
            {
                var field = method.Module.ResolveField(BitConverter.ToInt32(bytes, offset),
                    method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (field is { IsStatic: true } && field.FieldType == typeof(IntPtr)
                    && field.Name.StartsWith(fieldPrefix, StringComparison.Ordinal)) fields.Add(field);
            }
            offset += size;
        }
        return fields.Distinct().ToImmutableArray();
    }

    static int SwitchSize(byte[] bytes, int offset)
    {
        RequireRemaining(bytes, offset, 4);
        int count = BitConverter.ToInt32(bytes, offset);
        if (count < 0 || count > (bytes.Length - offset - 4) / 4) throw new InvalidDataException("INVALID_MANAGED_SWITCH_BOUNDS");
        return 4 + count * 4;
    }
    static void RequireRemaining(byte[] bytes, int offset, int size)
    {
        if (size < 0 || size > bytes.Length - offset) throw new InvalidDataException("TRUNCATED_MANAGED_OPERAND");
    }
}
