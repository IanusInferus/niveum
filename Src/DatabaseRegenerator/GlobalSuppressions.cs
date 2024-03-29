﻿// 代码分析使用此文件来维护应用到此项目的 SuppressMessage 
// 特性。
// 项目级禁止显示或者没有目标，或者已给定 
// 一个特定目标且其范围为命名空间、类型和成员等。
//
// 若要向此文件中添加禁止显示，请右击 
// 代码分析结果中的消息，指向“禁止显示消息”，然后单击 
//“在禁止显示文件中”。
// 无需手动向此文件添加禁止显示。

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:检查 SQL 查询是否存在安全漏洞", Scope = "member", Target = "Yuki.DatabaseRegenerator.Program.#RegenSqlServer(Yuki.RelationSchema.Schema,System.String,System.String,System.String[])")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:检查 SQL 查询是否存在安全漏洞", Scope = "member", Target = "Yuki.DatabaseRegenerator.Program.#RegenPostgreSQL(Yuki.RelationSchema.Schema,System.String,System.String,System.String[])")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:检查 SQL 查询是否存在安全漏洞", Scope = "member", Target = "Yuki.DatabaseRegenerator.Program.#RegenMySQL(Yuki.RelationSchema.Schema,System.String,System.String,System.String[])")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:检查 SQL 查询是否存在安全漏洞", Scope = "member", Target = "Yuki.DatabaseRegenerator.TableOperations.#ImportTable(System.Collections.Generic.Dictionary`2<System.String,Yuki.RelationSchema.EntityDef>,System.Collections.Generic.Dictionary`2<System.String,System.String>,System.Data.IDbConnection,System.Data.IDbTransaction,System.Collections.Generic.KeyValuePair`2<System.String,Yuki.RelationValue.TableVal>,Yuki.DatabaseRegenerator.DatabaseType)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:检查 SQL 查询是否存在安全漏洞", Scope = "member", Target = "Yuki.DatabaseRegenerator.TableOperations.#ExportTable(System.Collections.Generic.Dictionary`2<System.String,Yuki.RelationSchema.EntityDef>,System.Collections.Generic.Dictionary`2<System.String,System.String>,System.Data.IDbConnection,System.Data.IDbTransaction,System.String,Yuki.DatabaseRegenerator.DatabaseType)")]
