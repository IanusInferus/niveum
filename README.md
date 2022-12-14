# Niveum类型框架

## 概述

本框架是由一组代码生成工具和功能相关的一些库组成的一个系统。

主要的功能有三个：

1. NiveumObject语言代码生成器(SchemaManipulator)

    NiveumObject是一个类型定义语言，主要用于描述存储和通讯用的基于对象的数据结构，以生成各个语言的类型定义和序列化代码。详见[NiveumObject](Src/Doc/NiveumObject/NiveumObject.md)。

2. NiveumRelation语言代码生成器(RelationSchemaManipulator)和数据导入工具(DatabaseRegenerator)

    NiveumRelation是一个类型定义语言，主要用于描述关系数据库用的基于关系的数据结构，以生成各个数据库的创建和数据导入代码。详见[NiveumRelation](Src/Doc/NiveumRelation/NiveumRelation.md)。

3. NiveumExpression语言编译器和运行时生成器(ExpressionManipulator)

    NiveumExpression是一个微型纯数值表达式计算语言，主要用于提供一种可配置的公式，并在各个语言中执行。详见[NiveumExpression](Src/Doc/NiveumExpression/NiveumExpression.md)。

次要的功能还包括：

4. NiveumTemplate语言代码生成器(Nivea)

    NiveumTemplate语言是一个模板语言，主要用于描述代码生成时的模板，可以生成C#代码，并支持直接嵌入C#代码。NiveumTemplate在Niveum各功能的代码生成器开发中进行了使用。详见[NiveumTemplate](Src/Doc/NiveumTemplate/NiveumTemplate.md)。

5. NiveumJson

    NiveumJson是一个[JSON](https://www.json.org/)读写库。由于[Newtonsoft.Json](https://www.newtonsoft.com/)发展过程中引入了一些不符合标准的行为(例如Date类型的处理和反序列化漏洞)，因此从头编写了JSON的解析。

6. Krustallos数据库

    Krustallos是一个单机内存事务数据库，支持只读事务多版本镜像访问(MVCC)、读写事务两阶段锁定(2PL)、悲观并发控制(Pessimistic Concurrency Control)。可以嵌入到C#程序中使用，方便部署。可以导出一个特定时间点的不包括未提交变更的一致的数据备份。

7. TcpSendReceive

    NiveumObject语言的服务器测试工具，可以提交TCP通讯请求查看服务器响应。

## 环境要求

本框架使用 C# 编写，开发时需要 Visual Studio 2022 或 BuildTool 支持。
本框架运行需要 [Microsoft .Net Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) 或 [.Net 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) 运行时。

## 用户使用协议

### 以下协议不针对示例(Examples文件夹)：

本框架是免费自由软件，所有源代码和可执行程序按照三条款BSD许可证授权，详见[License.zh.txt](Src/Doc/License.zh.txt)。

本框架的所有文档不按照BSD许可证授权，你可以不经修改的复制、传播这些文档，你还可以引用、翻译这些文档，其他一切权利保留。

### 以下协议针对示例(Examples文件夹)：

本框架的示例进入公有领域，可以随意修改使用。

## 相关软件

本框架的NiveumObject语言与[Google Protocol Buffers](https://developers.google.com/protocol-buffers)和Facebook Thrift功能类似。
