﻿//*********************************************************************************************************************
//  ServiceHost.cs
//
//  Copyright(c) 2016, Electric Power Research Institute, Inc.
//  All rights reserved.
//  
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions are met:
//  
//  * Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//  
//  * Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//  
//  * Neither the name of copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
//  
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
//  AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//  DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
//  FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
//  DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
//  SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
//  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
//  OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
//  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
//  Code Modification History:
//  -------------------------------------------------------------------------------------------------------------------
//  09/10/2012 - Stephen C. Wills
//       Generated original version of source code.
//
//*********************************************************************************************************************

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using GSF;
using GSF.Console;
using GSF.IO;
using GSF.ServiceProcess;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using openEAS.Logging;

namespace openEAS
{
    public partial class ServiceHost : ServiceBase
    {
        #region [ Members ]

        // Fields
        private ServiceMonitors m_serviceMonitors;
        private SandBoxEngine m_extensibleDisturbanceAnalysisEngine;

        #endregion

        #region [ Constructors ]

        public ServiceHost()
        {
            InitializeComponent();

            // Register event handlers.
            m_serviceHelper.ServiceStarted += ServiceHelper_ServiceStarted;
            m_serviceHelper.ServiceStopping += ServiceHelper_ServiceStopping;
        }

        public ServiceHost(IContainer container)
            : this()
        {
            if (container != null)
                container.Add(this);
        }

        #endregion

        #region [ Methods ]

        private void ServiceHelper_ServiceStarted(object sender, EventArgs e)
        {
            ServiceHelperAppender serviceHelperAppender;
            RollingFileAppender fileAppender;
            ServiceProcess process;

            // Set current working directory to fix relative paths
            Directory.SetCurrentDirectory(FilePath.GetAbsolutePath(""));

            // Set up logging
            serviceHelperAppender = new ServiceHelperAppender(m_serviceHelper);

            fileAppender = new RollingFileAppender();
            fileAppender.StaticLogFileName = false;
            fileAppender.AppendToFile = true;
            fileAppender.RollingStyle = RollingFileAppender.RollingMode.Composite;
            fileAppender.MaxSizeRollBackups = 10;
            fileAppender.PreserveLogFileNameExtension = true;
            fileAppender.MaximumFileSize = "1MB";
            fileAppender.Layout = new PatternLayout("%date [%thread] %-5level %logger - %message%newline");

            try
            {
                if (!Directory.Exists("Debug"))
                    Directory.CreateDirectory("Debug");

                fileAppender.File = @"Debug\openEAS.log";
            }
            catch (Exception ex)
            {
                fileAppender.File = "openEAS.log";
                m_serviceHelper.ErrorLogger.Log(ex);
            }

            fileAppender.ActivateOptions();
            BasicConfigurator.Configure(serviceHelperAppender, fileAppender);

            // Set up heartbeat and client request handlers
            m_serviceHelper.AddScheduledProcess(ProcessLatestData, "ProcessLatestData", "* * * * *");
            m_serviceHelper.AddScheduledProcess(ServiceHeartbeatHandler, "ServiceHeartbeat", "* * * * *");
            m_serviceHelper.ClientRequestHandlers.Add(new ClientRequestHandler("ReloadSystemSettings", "Reloads system settings from the database", ReloadSystemSettingsRequestHandler));
            m_serviceHelper.ClientRequestHandlers.Add(new ClientRequestHandler("MsgServiceMonitors", "Sends a message to all service monitors", MsgServiceMonitorsRequestHandler));

            // Set up adapter loader to load service monitors
            m_serviceMonitors = new ServiceMonitors();
            m_serviceMonitors.AdapterCreated += ServiceMonitors_AdapterCreated;
            m_serviceMonitors.AdapterLoaded += ServiceMonitors_AdapterLoaded;
            m_serviceMonitors.AdapterUnloaded += ServiceMonitors_AdapterUnloaded;
            m_serviceMonitors.Initialize();

            // Set up the analysis engine
            m_extensibleDisturbanceAnalysisEngine = new SandBoxEngine();

            // Process latest data at startup
            process = m_serviceHelper.FindProcess("ProcessLatestData");

            if ((object)process != null)
                process.Start();
        }

        private void ProcessLatestData(string arg1, object[] arg2)
        {
            try
            {
                m_extensibleDisturbanceAnalysisEngine.ProcessLatestData();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void ServiceHelper_ServiceStopping(object sender, EventArgs e)
        {
            // Dispose of adapter loader for service monitors
            m_serviceMonitors.AdapterLoaded -= ServiceMonitors_AdapterLoaded;
            m_serviceMonitors.AdapterUnloaded -= ServiceMonitors_AdapterUnloaded;
            m_serviceMonitors.Dispose();
        }

        private void ServiceHeartbeatHandler(string s, object[] args)
        {
            // Go through all service monitors to notify of the heartbeat
            foreach (IServiceMonitor serviceMonitor in m_serviceMonitors.Adapters)
            {
                try
                {
                    // If the service monitor is enabled, notify it of the heartbeat
                    if (serviceMonitor.Enabled)
                        serviceMonitor.HandleServiceHeartbeat();
                }
                catch (Exception ex)
                {
                    // Handle each service monitor's exceptions individually
                    HandleException(ex);
                }
            }
        }

        // Reloads system settings from the database
        private void ReloadSystemSettingsRequestHandler(ClientRequestInfo requestInfo)
        {
            m_extensibleDisturbanceAnalysisEngine.ReloadSystemSettings();
        }

        // Send a message to the service monitors on request
        private void MsgServiceMonitorsRequestHandler(ClientRequestInfo requestInfo)
        {
            Arguments arguments = requestInfo.Request.Arguments;

            if (arguments.ContainsHelpRequest)
            {
                StringBuilder helpMessage = new StringBuilder();

                helpMessage.Append("Sends a message to all service monitors.");
                helpMessage.AppendLine();
                helpMessage.AppendLine();
                helpMessage.Append("   Usage:");
                helpMessage.AppendLine();
                helpMessage.Append("       MsgServiceMonitors [Options] [Args...]");
                helpMessage.AppendLine();
                helpMessage.AppendLine();
                helpMessage.Append("   Options:");
                helpMessage.AppendLine();
                helpMessage.Append("       -?".PadRight(20));
                helpMessage.Append("Displays this help message");

                DisplayResponseMessage(requestInfo, helpMessage.ToString());
            }
            else
            {
                string[] args = Enumerable.Range(1, arguments.OrderedArgCount)
                    .Select(arg => arguments[arguments.OrderedArgID + arg])
                    .ToArray();

                // Go through all service monitors and handle the message
                foreach (IServiceMonitor serviceMonitor in m_serviceMonitors.Adapters)
                {
                    try
                    {
                        // If the service monitor is enabled, notify it of the message
                        if (serviceMonitor.Enabled)
                            serviceMonitor.HandleClientMessage(args);
                    }
                    catch (Exception ex)
                    {
                        // Handle each service monitor's exceptions individually
                        HandleException(ex);
                    }
                }

                SendResponse(requestInfo, true);
            }
        }

        // Send the error to the service helper, error logger, and each service monitor
        private void HandleException(Exception ex)
        {
            string newLines = string.Format("{0}{0}", Environment.NewLine);

            m_serviceHelper.ErrorLogger.Log(ex);
            m_serviceHelper.UpdateStatus(UpdateType.Alarm, ex.Message + newLines);

            foreach (IServiceMonitor serviceMonitor in m_serviceMonitors.Adapters)
            {
                try
                {
                    if (serviceMonitor.Enabled)
                        serviceMonitor.HandleServiceError(ex);
                }
                catch (Exception ex2)
                {
                    // Exceptions encountered while handling exceptions can be tricky,
                    // so we just log them rather than risk a recursive loop
                    m_serviceHelper.ErrorLogger.Log(ex2);
                    m_serviceHelper.UpdateStatus(UpdateType.Alarm, ex2.Message + newLines);
                }
            }
        }

        #region [ Service Monitor Handlers ]

        // Ensure that service monitors save their settings to the configuration file
        private void ServiceMonitors_AdapterCreated(object sender, EventArgs<IServiceMonitor> e)
        {
            e.Argument.PersistSettings = true;
        }

        // Display a message when service monitors are loaded
        private void ServiceMonitors_AdapterLoaded(object sender, EventArgs<IServiceMonitor> e)
        {
            m_serviceHelper.UpdateStatusAppendLine(UpdateType.Information, "{0} has been loaded", e.Argument.GetType().Name);
        }

        // Display a message when service monitors are unloaded
        private void ServiceMonitors_AdapterUnloaded(object sender, EventArgs<IServiceMonitor> e)
        {
            m_serviceHelper.UpdateStatusAppendLine(UpdateType.Information, "{0} has been unloaded", e.Argument.GetType().Name);
        }

        #endregion

        #region [ Broadcast Message Handling ]

        /// <summary>
        /// Sends an actionable response to client.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="success">Flag that determines if this response to client request was a success.</param>
        protected virtual void SendResponse(ClientRequestInfo requestInfo, bool success)
        {
            SendResponseWithAttachment(requestInfo, success, null, null);
        }

        /// <summary>
        /// Sends an actionable response to client with a formatted message and attachment.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="success">Flag that determines if this response to client request was a success.</param>
        /// <param name="attachment">Attachment to send with response.</param>
        /// <param name="status">Formatted status message to send with response.</param>
        /// <param name="args">Arguments of the formatted status message.</param>
        protected virtual void SendResponseWithAttachment(ClientRequestInfo requestInfo, bool success, object attachment, string status, params object[] args)
        {
            try
            {
                // Send actionable response
                m_serviceHelper.SendActionableResponse(requestInfo, success, attachment, status, args);

                // Log details of client request as well as response
                if (m_serviceHelper.LogStatusUpdates && m_serviceHelper.StatusLog.IsOpen)
                {
                    string responseType = requestInfo.Request.Command + (success ? ":Success" : ":Failure");
                    string arguments = requestInfo.Request.Arguments.ToString();
                    string message = responseType + (string.IsNullOrWhiteSpace(arguments) ? "" : "(" + arguments + ")");

                    if (status != null)
                    {
                        if (args.Length == 0)
                            message += " - " + status;
                        else
                            message += " - " + string.Format(status, args);
                    }

                    m_serviceHelper.StatusLog.WriteTimestampedLine(message);
                }
            }
            catch (Exception ex)
            {
                string message = string.Format("Failed to send client response due to an exception: {0}", ex.Message);
                HandleException(new InvalidOperationException(message, ex));
            }
        }

        /// <summary>
        /// Displays a response message to client requestor.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="status">Formatted status message to send to client.</param>
        /// <param name="args">Arguments of the formatted status message.</param>
        protected virtual void DisplayResponseMessage(ClientRequestInfo requestInfo, string status, params object[] args)
        {
            try
            {
                m_serviceHelper.UpdateStatus(requestInfo.Sender.ClientID, UpdateType.Information, string.Format("{0}\r\n\r\n", status), args);
            }
            catch (Exception ex)
            {
                string message = string.Format("Failed to update client status \"{0}\" due to an exception: {1}", status.ToNonNullString(), ex.Message);
                HandleException(new InvalidOperationException(message, ex));
            }
        }

        #endregion

        #endregion
    }
}
