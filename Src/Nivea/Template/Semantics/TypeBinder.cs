//==========================================================================
//
//  File:        TypeBinder.cs
//  Location:    Nivea <Visual C#>
//  Description: 类型绑定器
//  Version:     2016.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Nivea.Template.Semantics
{
    public class BindedTypeSpec
    {
        public List<String> NamespaceParts;
        public String Name;
        public String Version;
        public List<TypeSpec> GenericParameters;
        public Optional<BindedTypeSpec> Parent;
        public Optional<TypeDef> TypeDef;
        public Optional<Mono.Cecil.TypeDefinition> TypeDefinition;

        public static readonly BindedTypeSpec Void = new BindedTypeSpec { };
        public static readonly BindedTypeSpec Any = new BindedTypeSpec { };
    }

    public static class TypeBinder
    {
        public static List<BindedTypeSpec> BindType(TypeSpec t, List<String> Namespace, List<List<String>> Imports, TypeProvider tp)
        {
            if (t.OnMember)
            {
                var MemberChainList = new LinkedList<TypeSpec>();
                AddToMemberChain(MemberChainList, t.Member);
                return BindTypeFromMemberChain(MemberChainList.ToList(), Namespace, Imports, tp);
            }
            else
            {
                return BindTypeFromMemberChain(new List<TypeSpec> { t }, Namespace, Imports, tp);
            }
        }

        private static void AddToMemberChain(LinkedList<TypeSpec> MemberChain, TypeMemberSpec tms)
        {
            if (tms.Parent.OnMember)
            {
                AddToMemberChain(MemberChain, tms.Parent.Member);
            }
            else
            {
                MemberChain.AddLast(tms.Parent);
            }
            if (tms.Child.OnMember)
            {
                AddToMemberChain(MemberChain, tms.Child.Member);
            }
            else
            {
                MemberChain.AddLast(tms.Child);
            }
        }

        private static List<BindedTypeSpec> BindTypeFromMemberChain(List<TypeSpec> MemberChain, List<String> Namespace, List<List<string>> Imports, TypeProvider tp)
        {
            if (MemberChain.Count == 0)
            {
                return new List<BindedTypeSpec> { };
            }
            var LeadingNamespaceCandidateChain = new LinkedList<TypeSpec>(MemberChain.TakeWhile(ts => ts.OnTypeRef && (ts.TypeRef.Version == "")));
            if (LeadingNamespaceCandidateChain.Count == MemberChain.Count)
            {
                LeadingNamespaceCandidateChain.RemoveLast();
            }

            var Candidates = new List<BindedTypeSpec> { };
            while (LeadingNamespaceCandidateChain.Count > 0)
            {
                var ns = LeadingNamespaceCandidateChain.Select(ts => ts.TypeRef.Name).ToList();
                var tCandidate = MemberChain[ns.Count];
                TypeRef r;
                List<TypeSpec> gp;
                if (tCandidate.OnTypeRef)
                {
                    r = tCandidate.TypeRef;
                    gp = new List<TypeSpec> { };
                }
                else if (tCandidate.OnGenericTypeSpec && tCandidate.GenericTypeSpec.TypeSpec.OnTypeRef)
                {
                    r = tCandidate.GenericTypeSpec.TypeSpec.TypeRef;
                    gp = tCandidate.GenericTypeSpec.ParameterValues;
                }
                else
                {
                    return new List<BindedTypeSpec> { };
                }

                var l = tp.GetTypeDefs(ns, r.Name, r.Version, gp.Count);
                if (l.Count > 0)
                {
                    Candidates = l.Select(d => new BindedTypeSpec { NamespaceParts = ns, Name = r.Name, Version = r.Version, GenericParameters = gp, Parent = Optional<BindedTypeSpec>.Empty, TypeDef = d, TypeDefinition = Optional<Mono.Cecil.TypeDefinition>.Empty }).ToList();
                    break;
                }
                var l2 = tp.GetTypeDefinitions(ns, r.Name, r.Version, gp.Count);
                if (l2.Count > 0)
                {
                    Candidates = l2.Select(d => new BindedTypeSpec { NamespaceParts = ns, Name = r.Name, Version = r.Version, GenericParameters = gp, Parent = Optional<BindedTypeSpec>.Empty, TypeDef = Optional<TypeDef>.Empty, TypeDefinition = d }).ToList();
                    break;
                }
                LeadingNamespaceCandidateChain.RemoveLast();
            }
            if (Candidates.Count == 0)
            {
                var tCandidate = MemberChain[0];
                TypeRef r = null;
                List<TypeSpec> gp = null;
                if (tCandidate.OnTypeRef)
                {
                    r = tCandidate.TypeRef;
                    gp = new List<TypeSpec> { };
                }
                else if (tCandidate.OnGenericParameterRef)
                {
                    throw new InvalidOperationException();
                }
                else if (tCandidate.OnGenericTypeSpec && tCandidate.GenericTypeSpec.TypeSpec.OnTypeRef)
                {
                    r = tCandidate.GenericTypeSpec.TypeSpec.TypeRef;
                    gp = tCandidate.GenericTypeSpec.ParameterValues;
                }
                else if (tCandidate.OnTuple)
                {
                    var ns = new List<String> { "System" };
                    var l = tp.GetTypeDefinitions(ns, "Tuple", "", tCandidate.Tuple.Count);
                    if (l.Count > 0)
                    {
                        Candidates = new List<BindedTypeSpec> { new BindedTypeSpec { NamespaceParts = ns, Name = "Tuple", Version = "", GenericParameters = tCandidate.Tuple, Parent = Optional<BindedTypeSpec>.Empty } };
                    }
                    else
                    {
                        return new List<BindedTypeSpec> { };
                    }
                }
                else
                {
                    return new List<BindedTypeSpec> { };
                }

                if (Candidates.Count == 0)
                {
                    foreach (var k in Enumerable.Range(0, Namespace.Count + 1))
                    {
                        var ns = Namespace.Take(Namespace.Count - k).ToList();
                        var l = tp.GetTypeDefs(ns, r.Name, r.Version, gp.Count);
                        if (l.Count > 0)
                        {
                            Candidates = l.Select(d => new BindedTypeSpec { NamespaceParts = ns, Name = r.Name, Version = r.Version, GenericParameters = gp, Parent = Optional<BindedTypeSpec>.Empty, TypeDef = d, TypeDefinition = Optional<Mono.Cecil.TypeDefinition>.Empty }).ToList();
                            break;
                        }
                        var l2 = tp.GetTypeDefinitions(ns, r.Name, r.Version, gp.Count);
                        if (l2.Count > 0)
                        {
                            Candidates = l2.Select(d => new BindedTypeSpec { NamespaceParts = ns, Name = r.Name, Version = r.Version, GenericParameters = gp, Parent = Optional<BindedTypeSpec>.Empty, TypeDef = Optional<TypeDef>.Empty, TypeDefinition = d }).ToList();
                            break;
                        }
                    }
                }

                if (Candidates.Count == 0)
                {
                    foreach (var Import in Imports)
                    {
                        var l = BindTypeFromMemberChain(Import.Select(i => TypeSpec.CreateTypeRef(new TypeRef { Name = i, Version = "" })).Concat(new List<TypeSpec> { tCandidate }).ToList(), new List<String> { }, new List<List<String>> { }, tp);
                        Candidates.AddRange(l);
                    }
                }
            }
            if (LeadingNamespaceCandidateChain.Count + 1 == MemberChain.Count)
            {
                return Candidates;
            }
            if (Candidates.Count != 1)
            {
                return new List<BindedTypeSpec> { };
            }

            var Current = Candidates.Single();
            var oParentTypeDefinition = Current.TypeDefinition;
            if (oParentTypeDefinition.OnNotHasValue)
            {
                return new List<BindedTypeSpec> { };
            }
            var CurrentTypeDefinition = oParentTypeDefinition.Value;

            foreach (var tCandidate in MemberChain.Skip(LeadingNamespaceCandidateChain.Count + 1))
            {
                if (tCandidate.OnTypeRef)
                {
                    var r = tCandidate.TypeRef;
                    var od = tp.GetNestTypeDefinition(CurrentTypeDefinition, r.Name, r.Version, 0);
                    if (od.OnHasValue)
                    {
                        Current = new BindedTypeSpec { NamespaceParts = new List<String> { }, Name = r.Name, Version = r.Version, GenericParameters = new List<TypeSpec> { }, Parent = Current, TypeDef = Optional<TypeDef>.Empty, TypeDefinition = od.Value };
                        CurrentTypeDefinition = od.Value;
                    }
                    else
                    {
                        return new List<BindedTypeSpec> { };
                    }
                }
                else if (tCandidate.OnGenericTypeSpec && tCandidate.GenericTypeSpec.TypeSpec.OnTypeRef)
                {
                    var r = tCandidate.GenericTypeSpec.TypeSpec.TypeRef;
                    var od = tp.GetNestTypeDefinition(CurrentTypeDefinition, r.Name, r.Version, tCandidate.GenericTypeSpec.ParameterValues.Count);
                    if (od.OnHasValue)
                    {
                        Current = new BindedTypeSpec { NamespaceParts = new List<String> { }, Name = r.Name, Version = r.Version, GenericParameters = tCandidate.GenericTypeSpec.ParameterValues, Parent = Current, TypeDef = Optional<TypeDef>.Empty, TypeDefinition = od.Value };
                        CurrentTypeDefinition = od.Value;
                    }
                    else
                    {
                        return new List<BindedTypeSpec> { };
                    }
                }
                else
                {
                    return new List<BindedTypeSpec> { };
                }
            }

            return new List<BindedTypeSpec> { Current };
        }
    }
}
