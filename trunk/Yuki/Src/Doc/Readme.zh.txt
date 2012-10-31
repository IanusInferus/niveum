Yummy Ultimate Kata Infrastructure (YUKI类型框架库)

地狱门神(F.R.C.)


1 概论

本框架的目标是建立一个跨平台跨语言的类型系统，并提供对应的序列化支持。
本框架的主要用途是将定义好的元数据文件通过代码生成得到各个语言的类型定义和序列化代码。

支持的代码生成分成两类：基于对象数据结构的代码生成和基于关系数据结构的代码生成。


2 基于对象数据结构的代码生成

基于对象的数据结构是用于存储和通讯用的数据结构。

基于对象的数据结构是指的由
基元类型(Primitive)
别名(Alias)
记录(Record)
标签联合(TaggedUnion)
枚举(Enum)
客户端方法(ClientCommand)
服务端事件(ServerCommand)
组成的数据结构。

基元类型 Primitive
没有泛型参数，指由所有相关外部系统均理解的基础类型，如32位有符号整数、字符串等。

别名 Alias
可有泛型参数，用以表示一个基元类型、别名、记录、标签联合或其泛型特化，以及多元组。

记录 Record
可有泛型参数，用以表示一个有多个字段的顺序结构。

标签联合 TaggedUnion
可有泛型参数，用以表示一个有标签和多个字段的选择结构，其实例只表示多个字段中的一个。

枚举 Enum
没有泛型参数，相当于一个所有字段的类型都是空类型(Unit)的标签联合。

客户端方法 ClientCommand
没有泛型参数，在通讯中表示由客户端发出的方法，由一个多个字段的顺序结构表示客户端传给服务端的参数，由一个有标签和多个字段的选择结构表示服务端返回给客户端的返回值。

服务端事件 ServerCommand
没有泛型参数，在通讯中表示由服务端发出的方法，由一个多个字段的顺序结构表示服务端传给客户端的参数。

元组 Tuple
一个匿名的有多个匿名字段的顺序结构，类型的一种组合方法。

泛型特化 GenericTypeSpec
一个匿名的数据结构，表示将某个有泛型参数的类型代入参数的结果。

这部分的代码生成主要分成四个部分：类型定义、二进制序列化、JSON序列化、通讯。
下面列出各语言代码生成支持的部分。

                    类型定义        二进制序列化        JSON序列化      二进制通讯  JSON通讯
VB.Net              √              Firefly库动态支持   ×              ×          ×
C#                  √              Firefly库动态支持   √              √          √
Java                √              √                  ×              ×          ×
C++2011             √              √                  ×              √          ×
ActionScript        √              √                  √              √          √
Xhtml               √              无需                无需            无需        无需

其中，VB.Net、Java、C++2011的通讯功能，将会在今后逐步增加。


3 基于关系数据结构的代码生成

基于关系的数据结构用于生成数据库创建和数据导入代码。

基于关系的数据结构是指由
基元类型(Primitive)
记录(Record)
枚举(Enum)
组成的数据结构。

基元类型 Primitive
没有泛型参数，指由所有相关外部系统均理解的基础类型，如32位有符号整数、字符串等。

记录 Record
没有泛型参数，表示一个数据表，有多个字段。

枚举 Enum
没有泛型参数，由若干标签名和对应的整数组成。

这部分的代码生成主要是SQL的代码生成，目前只支持SQL Server。


4 环境要求

本框架使用 Visual C# 3.0 编写，开发时需要 Microsoft .Net Framework 4.0 编译器 或 Visual Studio 2012 支持。
本框架运行时需要 Microsoft .Net Framework 4 或 Microsoft .Net Framework 4 Client Profile 运行库支持。
Microsoft .Net Framework 4 (x86/x64，48.1MB)
http://download.microsoft.com/download/9/5/A/95A9616B-7A37-4AF6-BC36-D6EA96C8DAAE/dotNetFx40_Full_x86_x64.exe
Microsoft .NET Framework 4 Client Profile (x86，28.8MB)
http://download.microsoft.com/download/3/1/8/318161B8-9874-48E4-BB38-9EB82C5D6358/dotNetFx40_Client_x86.exe


5 用户使用协议

以下协议不针对示例(Examples文件夹)：
本框架是免费自由软件，所有源代码和可执行程序按照BSD许可证授权，详见License.zh.txt。
本框架的所有文档不按照BSD许可证授权，你可以不经修改的复制、传播这些文档，你还可以引用、翻译这些文档，其他一切权利保留。

以下协议针对示例(Examples文件夹)：
本框架的示例进入公有领域，可以随意修改使用。

本框架所依赖的Firefly框架的使用协议请参见对应文档。

本框架示例所依赖的Json.NET库的使用协议请参见对应文档。


6 相关软件

本框架与Google Protocol Buffers和Facebook Thrift功能类似。


7 备注

如果发现了BUG，或者有什么意见或建议，请到以下网址与我联系。
http://www.cnblogs.com/Rex/Contact.aspx?id=1
