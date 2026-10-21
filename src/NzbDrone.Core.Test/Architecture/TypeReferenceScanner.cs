using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NzbDrone.Core.Test.Architecture
{
    // Finds every type each top-level type in an assembly references: signatures, attributes,
    // locals, and the type, method, and field tokens in method bodies. Nested and compiler-generated
    // types (lambdas, async state machines) count toward their outermost declaring type.
    public static class TypeReferenceScanner
    {
        private const BindingFlags AllDeclared = BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.Static |
                                                 BindingFlags.DeclaredOnly;

        private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null))
            .ToDictionary(o => o.Value);

        public static Dictionary<Type, HashSet<Type>> Scan(Assembly assembly)
        {
            var result = new Dictionary<Type, HashSet<Type>>();

            foreach (var type in assembly.GetTypes())
            {
                var owner = OuterMost(type);

                if (owner.Name.StartsWith('<'))
                {
                    continue;
                }

                if (!result.TryGetValue(owner, out var references))
                {
                    references = new HashSet<Type>();
                    result.Add(owner, references);
                }

                var found = new HashSet<Type>();
                CollectFromType(type, found);

                foreach (var reference in found)
                {
                    if (reference.Assembly == assembly &&
                        !reference.IsGenericParameter &&
                        !reference.HasElementType &&
                        (!reference.IsGenericType || reference.IsGenericTypeDefinition))
                    {
                        var referenceOwner = OuterMost(reference);

                        if (referenceOwner != owner)
                        {
                            references.Add(referenceOwner);
                        }
                    }
                }
            }

            return result;
        }

        public static Type OuterMost(Type type)
        {
            while (type.DeclaringType != null)
            {
                type = type.DeclaringType;
            }

            return type;
        }

        private static void CollectFromType(Type type, HashSet<Type> found)
        {
            Add(type.BaseType, found);

            foreach (var item in type.GetInterfaces())
            {
                Add(item, found);
            }

            if (type.IsGenericTypeDefinition)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    foreach (var constraint in argument.GetGenericParameterConstraints())
                    {
                        Add(constraint, found);
                    }
                }
            }

            AddAttributes(type.CustomAttributes, found);

            foreach (var field in type.GetFields(AllDeclared))
            {
                Add(field.FieldType, found);
                AddAttributes(field.CustomAttributes, found);
            }

            foreach (var property in type.GetProperties(AllDeclared))
            {
                Add(property.PropertyType, found);
                AddAttributes(property.CustomAttributes, found);
            }

            foreach (var item in type.GetEvents(AllDeclared))
            {
                Add(item.EventHandlerType, found);
            }

            foreach (var method in type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared)))
            {
                CollectFromMethod(method, found);
            }
        }

        private static void CollectFromMethod(MethodBase method, HashSet<Type> found)
        {
            if (method is MethodInfo info)
            {
                Add(info.ReturnType, found);

                if (info.IsGenericMethodDefinition)
                {
                    foreach (var argument in info.GetGenericArguments())
                    {
                        foreach (var constraint in argument.GetGenericParameterConstraints())
                        {
                            Add(constraint, found);
                        }
                    }
                }
            }

            foreach (var parameter in method.GetParameters())
            {
                Add(parameter.ParameterType, found);
            }

            AddAttributes(method.CustomAttributes, found);

            var body = method.GetMethodBody();

            if (body == null)
            {
                return;
            }

            foreach (var local in body.LocalVariables)
            {
                Add(local.LocalType, found);
            }

            foreach (var clause in body.ExceptionHandlingClauses)
            {
                if (clause.Flags == ExceptionHandlingClauseOptions.Clause)
                {
                    Add(clause.CatchType, found);
                }
            }

            var il = body.GetILAsByteArray();

            if (il != null)
            {
                CollectFromIl(method, il, found);
            }
        }

        private static void CollectFromIl(MethodBase method, byte[] il, HashSet<Type> found)
        {
            var module = method.Module;
            var typeArguments = method.DeclaringType != null && method.DeclaringType.IsGenericType
                ? method.DeclaringType.GetGenericArguments()
                : null;
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

            var position = 0;

            while (position < il.Length)
            {
                short value = il[position++];

                if (value == 0xFE)
                {
                    value = (short)(0xFE00 | il[position++]);
                }

                if (!OpCodesByValue.TryGetValue(value, out var opCode))
                {
                    throw new InvalidOperationException($"Unknown IL opcode 0x{value:X} in {method.DeclaringType}.{method.Name}");
                }

                switch (opCode.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        position += 1;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        position += 8;
                        break;
                    case OperandType.InlineSwitch:
                        var count = BitConverter.ToInt32(il, position);
                        position += 4 + (4 * count);
                        break;
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                        var token = BitConverter.ToInt32(il, position);
                        position += 4;
                        AddMember(module, token, typeArguments, methodArguments, found);
                        break;
                    default:
                        position += 4;
                        break;
                }
            }
        }

        private static void AddMember(Module module, int token, Type[] typeArguments, Type[] methodArguments, HashSet<Type> found)
        {
            MemberInfo member;

            try
            {
                member = module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (Exception ex) when (ex is ArgumentException or TypeLoadException or BadImageFormatException)
            {
                return;
            }

            switch (member)
            {
                case Type type:
                    Add(type, found);
                    break;
                case FieldInfo field:
                    Add(field.DeclaringType, found);
                    Add(field.FieldType, found);
                    break;
                case MethodBase target:
                    Add(target.DeclaringType, found);

                    if (target is MethodInfo targetInfo)
                    {
                        Add(targetInfo.ReturnType, found);

                        if (targetInfo.IsGenericMethod)
                        {
                            foreach (var argument in targetInfo.GetGenericArguments())
                            {
                                Add(argument, found);
                            }
                        }
                    }

                    foreach (var parameter in target.GetParameters())
                    {
                        Add(parameter.ParameterType, found);
                    }

                    break;
            }
        }

        private static void AddAttributes(IEnumerable<CustomAttributeData> attributes, HashSet<Type> found)
        {
            foreach (var attribute in attributes)
            {
                Add(attribute.AttributeType, found);

                foreach (var argument in attribute.ConstructorArguments.Concat(attribute.NamedArguments.Select(a => a.TypedValue)))
                {
                    if (argument.Value is Type type)
                    {
                        Add(type, found);
                    }
                }
            }
        }

        private static void Add(Type type, HashSet<Type> found)
        {
            if (type == null || type.IsGenericParameter || !found.Add(type))
            {
                return;
            }

            if (type.HasElementType)
            {
                Add(type.GetElementType(), found);
            }

            if (type.IsGenericType)
            {
                Add(type.GetGenericTypeDefinition(), found);

                foreach (var argument in type.GetGenericArguments())
                {
                    Add(argument, found);
                }
            }
        }
    }
}
