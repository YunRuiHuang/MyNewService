using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;


namespace MyNewService
{
    public partial class MyNewService : ServiceBase
    {
        public MyNewService()
        {
            InitializeComponent();
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("MySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "MySource", "MyNewLog");
            }
            eventLog1.Source = "MySource";
            eventLog1.Log = "MyNewLog";
        }

        private int eventId = 1;
        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            eventLog1.WriteEntry("In OnStart.");

            // Print the Process and ID into EventLog
            Dictionary<int, string> hash = GetAllProcess();
            string dictionaryString = "{";
            foreach (KeyValuePair<int, string> keyValues in hash)
            {
                dictionaryString += keyValues.Key + " : " + keyValues.Value + ", ";
            }
            eventLog1.WriteEntry(dictionaryString);

            //find the "MyNewService" Process, print to EventLog and kill
            foreach (KeyValuePair<int, string> keyValues in hash)
            {
                if (keyValues.Value.Equals("MyNewService"))
                {
                    eventLog1.WriteEntry(keyValues.Key + ":" + keyValues.Value);
                    KillProcessByID(keyValues.Key);
                }
            }

            Timer timer = new Timer();
            timer.Interval = 60000; // 60 seconds
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("In OnStop.");
        }

        protected override void OnContinue()
        {
            eventLog1.WriteEntry("In OnContinue.");
        }

        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.
            eventLog1.WriteEntry("Monitoring the System", EventLogEntryType.Information, eventId++);
        }

        public Dictionary<int, string> GetAllProcess()
        {
            Dictionary<int, string> hash = new Dictionary<int, string>();
            //Process currentProcess = Process.GetCurrentProcess();
            //return currentProcess.ToString();
            //String str = "";
            Process[] localAll = Process.GetProcesses();
            
            foreach(Process process in localAll)
            {
                hash.Add(process.Id, process.ProcessName);
                //str = str + process.Id + ":" + process.ProcessName + "; ";
            }
            

            return hash;
        }

        public Boolean KillProcessByID(int id)
        {
            
            Process process = Process.GetProcessById(id);
            process.Kill();
            process.WaitForExit();
            process.Dispose();

            return true;
        }


    }
}
