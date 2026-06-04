using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.MetaSchema;

namespace Firefly.Mapping.MetaProgramming
{
    public class DummyType
    {
    }

    public class VariableInfo
    {
        public string Name;
        public Type Type;
    }
    public class FieldOrPropertyInfo
    {
        public MemberInfo Member;
        public Type Type;
    }
    public class ImmutableRecordInfo
    {
        public FieldOrPropertyInfo[] Members;
        public ConstructorInfo Constructor;
    }
    public class MutableRecordInfo
    {
        public FieldOrPropertyInfo[] Members;
    }
    public class MutableAliasInfo
    {
        public FieldOrPropertyInfo Member;
    }
    public class MutableTaggedUnionInfo
    {
        public FieldOrPropertyInfo TagMember;
        public FieldOrPropertyInfo[] Members;
    }
    public class ImmutableTupleInfo
    {
        public FieldOrPropertyInfo[] Members;
        public ConstructorInfo Constructor;
    }
    public class MutableTupleInfo
    {
        public FieldOrPropertyInfo[] Members;
    }

    public class DelegateExpressionContext
    {
        public ParameterExpression ClosureParam;
        public object[] Closure;
        public Expression[] DelegateExpressions;
    }

    [DebuggerNonUserCode()]
    public static class MetaProgramming
    {
        public static MemberExpression CreateFieldOrPropertyExpression(ParameterExpression Param, MemberInfo Member)
        {
            switch (Member.MemberType)
            {
                case MemberTypes.Field:
                    return Expression.Field(Param, (FieldInfo)Member);
                case MemberTypes.Property:
                    return Expression.Property(Param, (PropertyInfo)Member);
                default:
                    throw new ArgumentException();
            }
        }
        public static DelegateExpressionContext CreateDelegateExpressionContext(IEnumerable<KeyValuePair<Delegate, Expression[]>> DelegateCalls)
        {
            var DelegateCallsArray = DelegateCalls.ToArray();
            var ClosureFieldIndices = new Dictionary<int, int>();
            var ClosureObjects = new List<object>();
            {
                var k = 0;
                foreach (var DelegateCall in DelegateCallsArray)
                {
                    var d = DelegateCall.Key;
                    if (d.Target != null)
                    {
                        var n = ClosureObjects.Count;
                        ClosureFieldIndices.Add(k, n);
                        ClosureObjects.Add(d);
                    }
                    k += 1;
                }
            }
            ParameterExpression ClosureParam = null;
            object[] Closure = null;
            Func<int, Expression> AccessClosure = null;
            if (ClosureObjects.Count > 0)
            {
                Closure = ClosureObjects.ToArray();
                ClosureParam = Expression.Parameter(typeof(object[]), "<>_Closure");
                AccessClosure = n => Expression.ArrayIndex(ClosureParam, Expression.Constant(n));
            }
            var DelegateExpressions = new List<Expression>();
            {
                var k = 0;
                foreach (var DelegateCall in DelegateCallsArray)
                {
                    var d = DelegateCall.Key;
                    if (d.Target == null)
                    {
                        DelegateExpressions.Add(Expression.Call(d.Method, DelegateCall.Value));
                    }
                    else
                    {
                        var n = ClosureFieldIndices[k];
                        var DelegateType = d.GetType();
                        var DelegateFunc = Expression.ConvertChecked(AccessClosure(n), DelegateType);
                        DelegateExpressions.Add(Expression.Invoke(DelegateFunc, DelegateCall.Value));
                    }
                    k += 1;
                }
            }
            return new DelegateExpressionContext { ClosureParam = ClosureParam, Closure = Closure, DelegateExpressions = DelegateExpressions.ToArray() };
        }
        public static Delegate CreateDelegate(ParameterExpression ClosureParam, object[] Closure, LambdaExpression Expr)
        {
            var FunctionLambda = Expr;
            if (Closure != null)
            {
                FunctionLambda = Expression.Lambda(FunctionLambda, new ParameterExpression[] { ClosureParam });
            }

            var Compiled = FunctionLambda.Compile();
            if (Closure != null)
            {
                Compiled = (Delegate)((Func<object[], Delegate>)Compiled)(Closure);
            }
            return Compiled;
        }
    }

    [DebuggerNonUserCode()]
    public static class MetaProgrammingExtensions
    {
        public static bool IsProperCollectionType(this Type Type)
        {
            return Type.GetInterfaces().Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)).Count() == 1;
        }
        public static Type GetCollectionElementType(this Type Type)
        {
            return Type.GetInterfaces().Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)).Single().GetGenericArguments()[0];
        }

        /// <remarks>
        /// 不变记录 ::= 类或结构(构造函数(参数(简单类型)*), 公共只读字段(简单类型)*, 公共可写属性{0}) AND (参数(简单类型)* = 公共只读字段(简单类型)*)
        ///            | 类或结构(构造函数(参数(简单类型)*), 公共可写字段{0}, 公共只读属性(简单类型)*) AND (参数(简单类型)* = 公共只读属性(简单类型)*)
        /// </remarks>
        public static ImmutableRecordInfo TryGetImmutableRecordInfo(this Type Type)
        {
            if (!(Type.IsValueType || Type.IsClass)) return null;

            var ReadableAndWritableFields = Type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsInitOnly).ToArray();
            var WritableProperties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();
            var ReadableFields = Type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.IsInitOnly).ToArray();
            var ReadableProperties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && !p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();

            if (ReadableAndWritableFields.Count() > 0) return null;
            if (WritableProperties.Count() > 0) return null;
            if (ReadableFields.Length > 0 && ReadableProperties.Length > 0) return null;

            var FieldMembers = ReadableFields.Select(f => new FieldOrPropertyInfo { Member = (MemberInfo)f, Type = f.FieldType }).ToArray();
            var PropertyMembers = ReadableProperties.Select(f => new FieldOrPropertyInfo { Member = (MemberInfo)f, Type = f.PropertyType }).ToArray();
            var MemberToIndex = Type.GetMembers().Select((m, i) => new { Member = m, Index = i }).ToDictionary(p => p.Member, p => p.Index);
            var FieldsAndProperties = FieldMembers.Concat(PropertyMembers).OrderBy(f => MemberToIndex[f.Member]).ToArray();
            if (Type.IsValueType)
            {
                if (FieldsAndProperties.Length == 0)
                {
                    if (Type.GetCustomAttributes(typeof(RecordAttribute), false).Length == 0 && !Type.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "RecordAttribute")) return null;
                }
            }

            var FieldAndPropertyTypes = FieldsAndProperties.Select(f => f.Type).ToArray();
            var c = Type.GetConstructor(FieldAndPropertyTypes);
            if (c == null || !c.IsPublic) return null;

            return new ImmutableRecordInfo { Members = FieldsAndProperties, Constructor = c };
        }
        /// <remarks>
        /// 可变记录 ::= 类或结构(无参构造函数, 公共可读写字段(简单类型)*, 公共可写属性{0})
        ///            | 类或结构(无参构造函数, 公共可写字段{0}, 公共可读写属性(简单类型)*)
        /// </remarks>
        public static MutableRecordInfo TryGetMutableRecordInfo(this Type Type)
        {
            if (!(Type.IsValueType || Type.IsClass)) return null;

            if (Type.IsClass)
            {
                var c = Type.GetConstructor(new Type[] { });
                if (c == null || !c.IsPublic) return null;
            }

            var ReadableAndWritableFields = Type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsInitOnly).ToArray();
            var ReadableAndWritableProperties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();
            var WritableProperties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();
            if (!((ReadableAndWritableFields.Length >= 0 && WritableProperties.Length == 0) || (ReadableAndWritableFields.Length == 0 && ReadableAndWritableProperties.Length >= 0))) return null;

            var FieldMembers = ReadableAndWritableFields.Select(f => new FieldOrPropertyInfo { Member = (MemberInfo)f, Type = f.FieldType }).ToArray();
            var PropertyMembers = ReadableAndWritableProperties.Select(f => new FieldOrPropertyInfo { Member = (MemberInfo)f, Type = f.PropertyType }).ToArray();
            var MemberToIndex = Type.GetMembers().Select((m, i) => new { Member = m, Index = i }).ToDictionary(p => p.Member, p => p.Index);
            var FieldsAndProperties = FieldMembers.Concat(PropertyMembers).OrderBy(f => MemberToIndex[f.Member]).ToArray();
            if (Type.IsValueType)
            {
                if (FieldsAndProperties.Length == 0)
                {
                    if (Type.GetCustomAttributes(typeof(RecordAttribute), false).Length == 0 && !Type.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "RecordAttribute")) return null;
                }
            }

            return new MutableRecordInfo { Members = FieldsAndProperties };
        }

        public static MutableAliasInfo TryGetMutableAliasInfo(this Type Type)
        {
            if (Type.GetCustomAttributes(typeof(AliasAttribute), false).Length == 0 && !Type.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "AliasAttribute")) return null;

            var Info = Type.TryGetMutableRecordInfo();
            if (Info.Members.Length != 1) return null;
            return new MutableAliasInfo { Member = Info.Members.Single() };
        }

        public static MutableTaggedUnionInfo TryGetMutableTaggedUnionInfo(this Type Type)
        {
            if (Type.GetCustomAttributes(typeof(TaggedUnionAttribute), false).Length == 0 && !Type.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "TaggedUnionAttribute")) return null;

            var Info = Type.TryGetMutableRecordInfo();
            var TagMembers = Info.Members.Where(m => (m.Member.GetCustomAttributes(typeof(TagAttribute), false).Length > 0) || (m.Member.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "TagAttribute"))).ToArray();
            if (TagMembers.Length != 1) return null;
            var TagMember = TagMembers.Single();
            if (!TagMember.Type.IsEnum) return null;
            var NumTag = TagMember.Type.GetEnumNames().Length;
            var Members = Info.Members.Except(new[] { TagMember }).ToArray();
            if (NumTag != Members.Length) return null;

            return new MutableTaggedUnionInfo { TagMember = TagMember, Members = Members };
        }

        public static ImmutableTupleInfo TryGetImmutableTupleInfo(this Type Type)
        {
            if (Type.GetCustomAttributes(typeof(TupleAttribute), false).Length == 0 && !Type.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "TupleAttribute") && !(Type.Namespace == "System" && Type.Name.StartsWith("Tuple`"))) return null;

            var Info = Type.TryGetImmutableRecordInfo();
            if (Info == null) return null;
            return new ImmutableTupleInfo { Members = Info.Members, Constructor = Info.Constructor };
        }
        public static MutableTupleInfo TryGetMutableTupleInfo(this Type Type)
        {
            if (Type.GetCustomAttributes(typeof(TupleAttribute), false).Length == 0 && !Type.GetCustomAttributes(false).OfType<Attribute>().Any(a => a.GetType().Name == "TupleAttribute") && !(Type.Namespace == "System" && Type.Name.StartsWith("Tuple`"))) return null;

            var Info = Type.TryGetMutableRecordInfo();
            if (Info == null) return null;
            return new MutableTupleInfo { Members = Info.Members };
        }

        public static Type MakeArrayTypeFromRank(this Type ElementType, int n)
        {
            if (n == 1) return ElementType.MakeArrayType();
            return ElementType.MakeArrayType(n);
        }

        public static Type MakeGenericTypeFromDummy(this Type Type, Func<Type, Type> Mapping)
        {
            var Mapped = Mapping(Type);
            if (!object.ReferenceEquals(Mapped, Type)) return Mapped;
            if (Type.IsGenericTypeDefinition) throw new ArgumentException();
            if (Type.IsGenericType)
            {
                return Type.GetGenericTypeDefinition().MakeGenericType(Type.GetGenericArguments().Select(t => t.MakeGenericTypeFromDummy(Mapping)).ToArray());
            }
            if (Type.IsArray)
            {
                return Type.GetElementType().MakeGenericTypeFromDummy(Mapping).MakeArrayTypeFromRank(Type.GetArrayRank());
            }
            return Type;
        }
        public static Type MakeGenericTypeFromDummy(this Type Type, Type DummyType, Type RealType)
        {
            Func<Type, Type> Mapping = t =>
            {
                if (object.ReferenceEquals(t, DummyType)) return RealType;
                return t;
            };
            return MakeGenericTypeFromDummy(Type, Mapping);
        }
        public static Type MakeGenericTypeFromDummy(this Type Type, Type RealType)
        {
            return MakeGenericTypeFromDummy(Type, typeof(DummyType), RealType);
        }
        public static MethodInfo MakeGenericMethodFromDummy(this MethodInfo Method, Func<Type, Type> Mapping)
        {
            if (!Method.IsGenericMethod) throw new ArgumentException();
            if (Method.IsGenericMethodDefinition) throw new ArgumentException();
            return Method.GetGenericMethodDefinition().MakeGenericMethod(Method.GetGenericArguments().Select(t => t.MakeGenericTypeFromDummy(Mapping)).ToArray());
        }
        public static MethodInfo MakeGenericMethodFromDummy(this MethodInfo Method, Type DummyType, Type RealType)
        {
            if (!Method.IsGenericMethod) throw new ArgumentException();
            if (Method.IsGenericMethodDefinition) throw new ArgumentException();
            return Method.GetGenericMethodDefinition().MakeGenericMethod(Method.GetGenericArguments().Select(t => t.MakeGenericTypeFromDummy(DummyType, RealType)).ToArray());
        }
        public static Delegate MakeDelegateMethod(this Delegate m, Type[] GenericParams, Type MethodType)
        {
            var Target = m.Target;
            var Method = m.Method;
            if (!Method.IsGenericMethod) throw new ArgumentException();
            if (Method.IsGenericMethodDefinition) throw new ArgumentException();
            var GenericMethod = Method.GetGenericMethodDefinition().MakeGenericMethod(GenericParams);
            return Delegate.CreateDelegate(MethodType, Target, GenericMethod);
        }
        public static Delegate MakeDelegateMethodFromDummy(this Delegate m, Func<Type, Type> Mapping)
        {
            var Target = m.Target;
            var Method = m.Method;
            if (!Method.IsGenericMethod) throw new ArgumentException();
            if (Method.IsGenericMethodDefinition) throw new ArgumentException();
            var GenericMethod = Method.MakeGenericMethodFromDummy(Mapping);
            var MethodType = m.GetType().MakeGenericTypeFromDummy(Mapping);
            return Delegate.CreateDelegate(MethodType, Target, GenericMethod);
        }
        public static Delegate MakeDelegateMethodFromDummy(this Delegate m, Type DummyType, Type RealType)
        {
            Func<Type, Type> Mapping = t =>
            {
                if (object.ReferenceEquals(t, DummyType)) return RealType;
                return t;
            };
            return MakeDelegateMethodFromDummy(m, Mapping);
        }
        public static Delegate MakeDelegateMethodFromDummy(this Delegate m, Type RealType)
        {
            return MakeDelegateMethodFromDummy(m, typeof(DummyType), RealType);
        }

        /// <summary>获得委托的参数</summary>
        public static VariableInfo[] GetParameters(this Delegate d)
        {
            var m = d.Method;
            var Parameters = d.Method.GetParameters();
            if (d.Target == null)
            {
                if (d.Method.IsStatic)
                {
                    return Parameters.Select(p => new VariableInfo { Name = p.Name, Type = p.ParameterType }).ToArray();
                }
                else
                {
                    //此时应添加第一个参数
                    return (new VariableInfo[] { new VariableInfo { Name = "_This", Type = d.Method.DeclaringType } }).Concat(Parameters.Select(p => new VariableInfo { Name = p.Name, Type = p.ParameterType })).ToArray();
                }
            }
            else
            {
                if (d.Method.IsStatic)
                {
                    //A static method and a target object assignable to the first parameter of the method. The delegate is said to be closed over its first argument.
                    //此时应抛弃第一个参数
                    return Parameters.Skip(1).Select(p => new VariableInfo { Name = p.Name, Type = p.ParameterType }).ToArray();
                }
                else
                {
                    return Parameters.Select(p => new VariableInfo { Name = p.Name, Type = p.ParameterType }).ToArray();
                }
            }
        }
        public static Type ReturnType(this Delegate d)
        {
            return d.Method.ReturnType;
        }
        public static Delegate Compose(this Delegate InnerFunction, Delegate OuterMethod)
        {
            var D = InnerFunction.GetParameters().Single().Type;
            var MI = InnerFunction.ReturnType();
            var MO = OuterMethod.GetParameters().Single().Type;

            var iParam = Expression.Parameter(InnerFunction.GetType(), "<>_i");
            var oParam = Expression.Parameter(OuterMethod.GetType(), "<>_o");

            var vParam = Expression.Parameter(D, "<>_v");
            LambdaExpression InnerLambda;
            if (object.ReferenceEquals(MI, MO))
            {
                InnerLambda = Expression.Lambda(Expression.Invoke(oParam, Expression.Invoke(iParam, vParam)), vParam);
            }
            else
            {
                InnerLambda = Expression.Lambda(Expression.Invoke(oParam, Expression.ConvertChecked(Expression.Invoke(iParam, vParam), MO)), vParam);
            }
            var OuterLambda = Expression.Lambda(InnerLambda, iParam, oParam);
            var OuterDelegate = OuterLambda.Compile();
            return OuterDelegate.StaticDynamicInvoke<Delegate, Delegate, Delegate>(InnerFunction, OuterMethod);
        }
        public static Delegate Curry(this Delegate Method, params object[] Parameters)
        {
            var ProvidedParameters = Method.GetParameters().Take(Parameters.Length).Select(p => Expression.Parameter(p.Type, p.Name)).ToArray();
            var NotProvidedParameters = Method.GetParameters().Skip(Parameters.Length).Select(p => Expression.Parameter(p.Type, p.Name)).ToArray();
            var AllParameters = ProvidedParameters.Concat(NotProvidedParameters).ToArray();
            var mParam = Expression.Parameter(Method.GetType(), "<>_m");
            var OuterLambdaParameters = (new ParameterExpression[] { mParam }).Concat(ProvidedParameters).ToArray();
            var InnerLambda = Expression.Lambda(Expression.Invoke(mParam, AllParameters), NotProvidedParameters);
            var OuterLambda = Expression.Lambda(InnerLambda, OuterLambdaParameters);
            var OuterDelegate = OuterLambda.Compile();
            var ParamObjects = (new object[] { Method }).Concat(Parameters).ToArray();
            return OuterDelegate.StaticDynamicInvokeWithObjects<Delegate>(ParamObjects);
        }
        public static Delegate AdaptFunction(this Delegate Method, Type ReturnType, params Type[] RequiredParameterTypes)
        {
            var Parameters = Method.GetParameters().ZipStrict(RequiredParameterTypes, (p, r) => new { InnerType = p.Type, OuterType = r, OuterParamExpr = Expression.Parameter(r, p.Name) }).ToArray();

            ParameterExpression ClosureParam = null;
            ClosureParam = Expression.Parameter(typeof(Delegate), "<>_Closure");
            var ConvertExpressions = new List<Expression>();
            foreach (var p in Parameters)
            {
                ConvertExpressions.Add(Expression.ConvertChecked(p.OuterParamExpr, p.InnerType));
            }
            var Ret = Expression.ConvertChecked(Expression.Invoke(Expression.ConvertChecked(ClosureParam, Method.GetType()), ConvertExpressions), ReturnType);
            var InnerLambda = Expression.Lambda(Ret, Parameters.Select(p => p.OuterParamExpr).ToArray());
            var OuterLambda = Expression.Lambda(Expression.ConvertChecked(InnerLambda, typeof(Delegate)), ClosureParam);

            var OuterDelegate = (Func<Delegate, Delegate>)OuterLambda.Compile();
            return OuterDelegate(Method);
        }
        public static Func<TReturn> AdaptFunction<TReturn>(this Delegate Method)
        {
            return (Func<TReturn>)AdaptFunction(Method, typeof(TReturn));
        }
        public static Func<T, TReturn> AdaptFunction<T, TReturn>(this Delegate Method)
        {
            return (Func<T, TReturn>)AdaptFunction(Method, typeof(TReturn), typeof(T));
        }
        public static Func<T1, T2, TReturn> AdaptFunction<T1, T2, TReturn>(this Delegate Method)
        {
            return (Func<T1, T2, TReturn>)AdaptFunction(Method, typeof(TReturn), typeof(T1), typeof(T2));
        }
        public static Func<T1, T2, T3, TReturn> AdaptFunction<T1, T2, T3, TReturn>(this Delegate Method)
        {
            return (Func<T1, T2, T3, TReturn>)AdaptFunction(Method, typeof(TReturn), typeof(T1), typeof(T2), typeof(T3));
        }
        public static Func<T1, T2, T3, T4, TReturn> AdaptFunction<T1, T2, T3, T4, TReturn>(this Delegate Method)
        {
            return (Func<T1, T2, T3, T4, TReturn>)AdaptFunction(Method, typeof(TReturn), typeof(T1), typeof(T2), typeof(T3));
        }
        public static Delegate AdaptFunctionWithObjects(this Delegate Method, Type ReturnType)
        {
            var Parameters = Method.GetParameters().Select(p => new { InnerType = p.Type }).ToArray();

            ParameterExpression ClosureParam = null;
            ClosureParam = Expression.Parameter(typeof(Delegate), "<>_Closure");

            var ObjectsParam = Expression.Parameter(typeof(object[]));

            var ConvertExpressions = new List<Expression>();
            var n = 0;
            foreach (var p in Parameters)
            {
                ConvertExpressions.Add(Expression.ConvertChecked(Expression.ArrayIndex(ObjectsParam, Expression.Constant(n)), p.InnerType));
                n += 1;
            }
            var Ret = Expression.ConvertChecked(Expression.Invoke(Expression.ConvertChecked(ClosureParam, Method.GetType()), ConvertExpressions), ReturnType);
            var InnerLambda = Expression.Lambda(Ret, ObjectsParam);
            var OuterLambda = Expression.Lambda(Expression.ConvertChecked(InnerLambda, typeof(Delegate)), ClosureParam);

            var OuterDelegate = (Func<Delegate, Delegate>)OuterLambda.Compile();
            return OuterDelegate(Method);
        }
        public static Func<object[], TReturn> AdaptFunctionWithObjects<TReturn>(this Delegate Method)
        {
            return (Func<object[], TReturn>)Method.AdaptFunctionWithObjects(typeof(TReturn));
        }
        public static TReturn StaticDynamicInvoke<TReturn>(this Delegate Method)
        {
            return Method.AdaptFunction<TReturn>()();
        }
        public static TReturn StaticDynamicInvoke<T, TReturn>(this Delegate Method, T v)
        {
            return Method.AdaptFunction<T, TReturn>()(v);
        }
        public static TReturn StaticDynamicInvoke<T1, T2, TReturn>(this Delegate Method, T1 v1, T2 v2)
        {
            return Method.AdaptFunction<T1, T2, TReturn>()(v1, v2);
        }
        public static TReturn StaticDynamicInvoke<T1, T2, T3, TReturn>(this Delegate Method, T1 v1, T2 v2, T3 v3)
        {
            return Method.AdaptFunction<T1, T2, T3, TReturn>()(v1, v2, v3);
        }
        public static TReturn StaticDynamicInvoke<T1, T2, T3, T4, TReturn>(this Delegate Method, T1 v1, T2 v2, T3 v3, T4 v4)
        {
            return Method.AdaptFunction<T1, T2, T3, T4, TReturn>()(v1, v2, v3, v4);
        }
        public static TReturn StaticDynamicInvokeWithObjects<TReturn>(this Delegate Method, object[] o)
        {
            return Method.AdaptFunctionWithObjects<TReturn>()(o);
        }
    }
}
