//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.TcpSendReceive <Visual C#>
//  Description: TCP发送接收器
//  Version:     2018.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Firefly;

namespace TcpSendReceive
{
    public class Program
    {
        private static Window MainWindow;

        public static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (MainWindow == null)
            {
                MessageBox.Show(ExceptionInfo.GetExceptionInfo(e.Exception, new StackTrace(3, true)), "Error");
            }
            else
            {
                MessageBox.Show(MainWindow, ExceptionInfo.GetExceptionInfo(e.Exception, new StackTrace(3, true)), "Error");
            }
            e.Handled = true;
        }

        [STAThread]
        public static int Main(String[] args)
        {
            if (!Debugger.IsAttached)
            {
                try
                {
                    var a = new App();
                    MainWindow = new MainWindow();
                    a.DispatcherUnhandledException += App_DispatcherUnhandledException;
                    a.Run(MainWindow);
                    Environment.Exit(0);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (MainWindow == null)
                    {
                        MessageBox.Show(ExceptionInfo.GetExceptionInfo(ex), "Error");
                    }
                    else
                    {
                        MessageBox.Show(MainWindow, ExceptionInfo.GetExceptionInfo(ex), "Error");
                    }
                    Environment.Exit(-1);
                    return -1;
                }
            }
            else
            {
                var Success = false;
                try
                {
                    var a = new App();
                    MainWindow = new MainWindow();
                    a.Run(MainWindow);
                    Success = true;
                    Environment.Exit(0);
                    return 0;
                }
                finally
                {
                    if (!Success)
                    {
                        Environment.Exit(-1);
                    }
                }
            }
        }
    }
}
