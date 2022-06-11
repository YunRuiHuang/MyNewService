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
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;


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
            /*
            foreach (KeyValuePair<int, string> keyValues in hash)
            {
                if (keyValues.Value.Equals("MyNewService"))
                {
                    eventLog1.WriteEntry(keyValues.Key + ":" + keyValues.Value);
                    KillProcessByID(keyValues.Key);
                }
            }
            */

            Send(eventLog1);
            //Receive();

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

        //Service Bus Part

        // connection string to your Service Bus namespace
        static string connectionString = "Endpoint=sb://busofazure.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ALZIn0YTuNENlVDOQsuVFi8zk0F+224+zZv5Q9P9HsI=";

        // name of your Service Bus queue
        static string queueName = "myqueue";

        // the client that owns the connection and can be used to create senders and receivers
        static ServiceBusClient senderClient;
        static ServiceBusClient receiveClient;

        // the sender used to publish messages to the queue
        static ServiceBusSender sender;

        // the processor that reads and processes messages from the queue
        static ServiceBusProcessor processor;

        // number of messages to be sent to the queue
        private const int numOfMessages = 3;

        static async Task Send(EventLog eventLog1)
        {
            // The Service Bus client types are safe to cache and use as a singleton for the lifetime
            // of the application, which is best practice when messages are being published or read
            // regularly.
            //
            // Create the clients that we'll use for sending and processing messages.
            senderClient = new ServiceBusClient(connectionString);
            sender = senderClient.CreateSender(queueName);

            // create a batch 
            ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

            for (int i = 1; i <= numOfMessages; i++)
            {
                // try adding a message to the batch
                if (!messageBatch.TryAddMessage(new ServiceBusMessage($"Message {i}")))
                {
                    // if it is too large for the batch
                    throw new Exception($"The message {i} is too large to fit in the batch.");
                }
            }

            try
            {
                // Use the producer client to send the batch of messages to the Service Bus queue
                await sender.SendMessagesAsync(messageBatch);
                eventLog1.WriteEntry($"A batch of {numOfMessages} messages has been published to the queue.");
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                await sender.DisposeAsync();
                await senderClient.DisposeAsync();
            }

            //eventLog1.WriteEntry("Press any key to end the application");
            //Console.ReadKey();
        }


        //receive part

        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine($"Received: {body}");

            // complete the message. message is deleted from the queue. 
            await args.CompleteMessageAsync(args.Message);
        }

        // handle any errors when receiving messages
        static Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }

        static async Task Receive(EventLog eventLog1)
        {
            // The Service Bus client types are safe to cache and use as a singleton for the lifetime
            // of the application, which is best practice when messages are being published or read
            // regularly.
            //

            // Create the client object that will be used to create sender and receiver objects
            receiveClient = new ServiceBusClient(connectionString);

            // create a processor that we can use to process the messages
            processor = receiveClient.CreateProcessor(queueName, new ServiceBusProcessorOptions());

            try
            {
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();

                //Console.WriteLine("Wait for a minute and then press any key to end the processing");
                //Console.ReadKey();

                // stop processing 
                //Console.WriteLine("\nStopping the receiver...");
                await processor.StopProcessingAsync();
                //Console.WriteLine("Stopped receiving messages");
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                await processor.DisposeAsync();
                await receiveClient.DisposeAsync();
            }
        }

    }
}
