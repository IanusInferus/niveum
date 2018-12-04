'==========================================================================
'
'  File:        Program.vb
'  Location:    Niveum.Examples <Visual Basic .Net>
'  Description: 数据转换工具
'  Version:     2012.04.07.
'  Author:      F.R.C.
'  Copyright(C) Public Domain
'
'==========================================================================

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.IO
Imports Firefly
Imports Firefly.Streaming
Imports Firefly.TextEncoding
Imports Firefly.Texting.TreeFormat
Imports Yuki
Imports Yuki.ObjectSchema

Public Module Program
    Public Function Main() As Integer
        If System.Diagnostics.Debugger.IsAttached Then
            Return MainInner()
        Else
            Try
                Return MainInner()
            Catch ex As Exception
                Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex))
                Return -1
            End Try
        End If
    End Function

    Public Function MainInner() As Integer
        TextEncoding.WritingDefault = TextEncoding.UTF8

        Dim CmdLine = CommandLine.GetCmdLine()
        Dim argv = CmdLine.Arguments

        If CmdLine.Arguments.Length <> 0 Then
            DisplayInfo()
            Return -1
        End If

        If CmdLine.Options.Length = 0 Then
            DisplayInfo()
            Return 0
        End If

        For Each opt In CmdLine.Options
            Select Case opt.Name.ToLower
                Case "?", "help"
                    DisplayInfo()
                    Return 0
                Case "t2b"
                    Dim args = opt.Arguments
                    If args.Length = 2 Then
                        TreeToBinary(args(0), args(1))
                    Else
                        DisplayInfo()
                        Return -1
                    End If
                Case "b2t"
                    Dim args = opt.Arguments
                    If args.Length = 2 Then
                        BinaryToTree(args(0), args(1))
                    Else
                        DisplayInfo()
                        Return -1
                    End If
                Case Else
                    Throw New ArgumentException(opt.Name)
            End Select
        Next
        Return 0
    End Function

    Public Sub DisplayInfo()
        Console.WriteLine("数据转换工具")
        Console.WriteLine("DataConv，Public Domain")
        Console.WriteLine("F.R.C.")
        Console.WriteLine("")
        Console.WriteLine("用法:")
        Console.WriteLine("DataConv (/<Command>)*")
        Console.WriteLine("将Tree格式数据转化为二进制数据")
        Console.WriteLine("/t2b:<TreeFile>,<BinaryFile>")
        Console.WriteLine("将二进制数据转化为Tree格式数据")
        Console.WriteLine("/b2t:<BinaryFile>,<TreeFile>")
        Console.WriteLine("TreeFile Tree文件路径。")
        Console.WriteLine("BinaryFile 二进制文件路径。")
        Console.WriteLine("")
        Console.WriteLine("示例:")
        Console.WriteLine("DataConv /t2b:..\..\Data\WorldData.tree,..\Data\WorldData.bin")
        Console.WriteLine("将WorldData.tree转化为WorldData.bin。")
    End Sub

    Public Sub TreeToBinary(ByVal TreePath As String, ByVal BinaryPath As String)
        Dim tbc = New TreeBinaryConverter()
        Dim Data = TreeFile.ReadFile(TreePath)
        Dim b = tbc.TreeToBinary(Of World.World)(Data)
        Using s = Streams.CreateWritable(BinaryPath)
            s.Write(b)
        End Using
    End Sub

    Public Sub BinaryToTree(ByVal BinaryPath As String, ByVal TreePath As String)
        Dim tbc = New TreeBinaryConverter()
        Dim Data As Byte()
        Using s = Streams.OpenReadable(BinaryPath)
            Data = s.Read(CInt(s.Length))
        End Using
        Dim x = tbc.BinaryToTree(Of World.World)(Data)
        TreeFile.WriteFile(TreePath, x)
    End Sub
End Module
