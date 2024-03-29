﻿// 代码分析使用此文件来维护应用到此项目的 SuppressMessage 
// 特性。
// 项目级禁止显示或者没有目标，或者已给定 
// 一个特定目标且其范围为命名空间、类型和成员等。
//
// 若要向此文件中添加禁止显示，请右击 
// 代码分析结果中的消息，指向“禁止显示消息”，然后单击 
//“在禁止显示文件中”。
// 无需手动向此文件添加禁止显示。

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Scope = "type", Target = "Communication.BaseSystem.AsyncConsumer`1")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:不要多次释放对象", Scope = "member", Target = "Communication.Net.TcpServer`2.#Start()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:不要多次释放对象", Scope = "member", Target = "Communication.Net.TcpServer`2.#Stop()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly", Scope = "member", Target = "Communication.Net.TcpServer`2.#MaxConnectionsExceeded")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly", Scope = "member", Target = "Communication.Net.TcpServer`2.#MaxConnectionsPerIPExceeded")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Scope = "member", Target = "Communication.Net.TcpServer`2.#Dispose()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:不要多次释放对象", Scope = "member", Target = "Communication.Net.TcpSession`2.#Stop()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Scope = "member", Target = "Communication.Net.TcpSession`2.#Dispose()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Scope = "member", Target = "Communication.BaseSystem.AsyncConsumer`1.#Dispose()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Scope = "type", Target = "TcpSendReceive.MainWindow")]
