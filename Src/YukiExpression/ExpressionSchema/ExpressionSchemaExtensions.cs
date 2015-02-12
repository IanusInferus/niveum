using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Firefly;
using Firefly.Mapping.Binary;
using Firefly.Streaming;

namespace Yuki.ExpressionSchema
{
    public static class ExpressionSchemaExtensions
    {
        public static Byte[] GetUnifiedBinaryRepresentation(this Schema s)
        {
            var Modules = s.Modules.Select(m => new ModuleDecl { Name = m.Name, Functions = m.Functions.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList(), Description = "" }).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var ss = new Schema { Modules = Modules, Imports = new List<String> { } };

            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();
            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Write(ss, ms);
                ms.Position = 0;

                var Bytes = ms.Read((int)(ms.Length));
                return Bytes;
            }
        }

        public static UInt64 Hash(this Schema s)
        {
            var Bytes = GetUnifiedBinaryRepresentation(s);
            var sha = new SHA1CryptoServiceProvider();
            Byte[] result;

            using (var ms = Streams.CreateMemoryStream())
            {
                ms.Write(Bytes);
                ms.Position = 0;

                result = sha.ComputeHash(ms.ToUnsafeStream());
            }

            using (var ms = Streams.CreateMemoryStream())
            {
                ms.Write(result.Skip(result.Length - 8).ToArray());
                ms.Position = 0;

                return ms.ReadUInt64B();
            }
        }

        public static void Verify(this Schema s)
        {
            VerifyDuplicatedNames(s);
        }

        public static void VerifyDuplicatedNames(this Schema s)
        {
            CheckDuplicatedNames(s.Modules, m => m.Name, m => String.Format("DuplicatedName {0}", m.Name));

            foreach (var m in s.Modules)
            {
                CheckDuplicatedNames(m.Functions, f => f.Name, f => String.Format("DuplicatedFunction {0}: module {1}", f.Name, m.Name));
            }
        }

        private static void CheckDuplicatedNames<T>(IEnumerable<T> Values, Func<T, String> NameSelector, Func<T, String> ErrorMessageSelector)
        {
            var TypeNames = Values.Select(NameSelector).Distinct(StringComparer.OrdinalIgnoreCase);
            var DuplicatedNames = new HashSet<String>(Values.GroupBy(NameSelector, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);

            if (DuplicatedNames.Count > 0)
            {
                var l = new List<String>();
                foreach (var tp in Values.Where(p => DuplicatedNames.Contains(NameSelector(p))))
                {
                    l.Add(ErrorMessageSelector(tp));
                }
                var Message = String.Concat(l.Select(Line => Line + Environment.NewLine));
                throw new AggregateException(Message);
            }
        }
    }
}
