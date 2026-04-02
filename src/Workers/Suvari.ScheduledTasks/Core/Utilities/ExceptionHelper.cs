using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Suvari.ScheduledTasks.Core.Utilities;

/// <summary>
/// Exception yönetimi için kullanılır.
/// </summary>
public class Exceptions
{
    /// <summary>
    /// Exception loglamak için kullanılır.
    /// </summary>
    /// <param name="exception">Exception objesi.</param>
    public static void NewException(Exception exception, bool sendTelegramLog = true, bool sendAILog = true)
    {
        try
        {
            string assemblyName = "";

            try
            {
                assemblyName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
            }
            catch { }

            if (sendAILog)
            {
                try
                {
                    var telemetryClient = new TelemetryClient(new TelemetryConfiguration());
                    telemetryClient.TrackException(exception);
                }
                catch { }
            }

            if (sendTelegramLog)
            {
                try
                {
                    if (!string.IsNullOrEmpty(assemblyName))
                    {
                        Integrations.Telegram.SendMessage($"Assembly: {assemblyName}{Environment.NewLine}{FlattenException(exception)}", Integrations.Telegram.Channel.ExceptionLogs);
                    }
                    else
                    {
                        Integrations.Telegram.SendMessage(FlattenException(exception), Integrations.Telegram.Channel.ExceptionLogs);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>
    /// Exception nesnesini string formatında detaylarıyla birlikte döner.
    /// </summary>
    /// <param name="exception">Exception</param>
    /// <returns>String formatında exception detayları</returns>
    public static string FlattenException(Exception exception)
    {
        var currentCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                try
                {
                    stringBuilder.AppendLine($"Source: {exception.Source}");
                }
                catch
                {
                    stringBuilder.AppendLine("Source: Unknown");
                }

                stringBuilder.AppendLine(exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);
                stringBuilder.AppendLine($"Exception Info: {exception.ExceptionInfo()}");
                stringBuilder.AppendLine("---------------------");

                exception = exception.InnerException;
            }

            string exceptionSource = string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(Environment.MachineName))
                {
                    exceptionSource = Environment.MachineName;
                }
                else if (!string.IsNullOrEmpty(Environment.MachineName))
                {
                    exceptionSource = Environment.MachineName;
                }
                else if (!string.IsNullOrEmpty(System.Net.Dns.GetHostName()))
                {
                    exceptionSource = System.Net.Dns.GetHostName();
                }
                else
                {
                    exceptionSource = "Unknown PC";
                }
            }
            catch
            {
                exceptionSource = "Unknown PC";
            }

            stringBuilder.AppendLine("Exception Source: " + exceptionSource);
            stringBuilder.AppendLine("Environment: " + Globals.CurrentBrand);

            return stringBuilder.ToString();
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = currentCulture;
            Thread.CurrentThread.CurrentCulture = currentCulture;
        }
    }
}

public static class ExceptionExtensions
{
    public static string ExceptionInfo(this Exception exception)
    {
        try
        {
            StackFrame stackFrame = (new StackTrace(exception, true)).GetFrame(0);
            if (stackFrame == null)
                return string.Empty;

            return string.Format("At line {0} column {1} in {2}: {3} {4}{3}{5}  ",
               stackFrame.GetFileLineNumber(), stackFrame.GetFileColumnNumber(),
               stackFrame.GetMethod(), Environment.NewLine, stackFrame.GetFileName(),
               exception.Message);
        }
        catch
        {
            return string.Empty;
        }
    }
}