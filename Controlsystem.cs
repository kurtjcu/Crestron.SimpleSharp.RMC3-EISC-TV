

/** Project Summary ***********************************************************
 * 
 * Hacked together from other projects by Kurt Schoenhoff.
 * 
 * This Project is a simple EISC controlled RMC3 that turns a samsung screen 
 * On/off, as well as switching from HDMI1 to HDMI2 Via RS232.
 * 
 * EISC is on IPID 0A or 10 in decimal notation.
 * EISC Joins
 * D10 Power high = on / low = off
 * D11 Switch to HDMI1
 * D12 Switch to HDMI2
 * 
 * Serial on S10 is sent back to the other connected processor as to let you 
 * know it is there. :)
 * 
 * 
 * This program was produced from Posts on crestron labs including but not 
 * limited to:
 * 
 *  http://www.crestronlabs.com/showthread.php?7277-eisc-simpl-pro 
 *  
 * 
 * 
 * 
 *****************************************************************************/



namespace EXXON_RMC_TV_EISC
{
	public class ControlSystem : CrestronControlSystem
	{
		#region local objects

		// Define local variables ...

        public EthernetIntersystemCommunications myEISC;
        public ComPort MyCOMPort;

        private string eISCIP = "192.168.1.48";     //ip to connect to via eisc
        private uint eISCIPID = 10;                 //IPID for EISC

        private CrestronQueue<String> RxQueue = new CrestronQueue<string>();
        private Thread RxHandler;

		#endregion

		#region required for start up

		/// <summary>
		/// Constructor of the Control System Class. Make sure the constructor always exists.
		/// If it doesn't exit, the code will not run on your 3-Series processor.
		/// </summary>
		public ControlSystem() : base()
		{
			// subscribe to control system events
			CrestronEnvironment.SystemEventHandler += new SystemEventHandler(CrestronEnvironment_SystemEventHandler);
			CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
			CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);
			
			// Set the number of threads which you want to use in your program - At this point the threads cannot be created but we should
			// define the max number of threads which we will use in the system.
			// the right number depends on your project; do not make this number unnecessarily large
			Thread.MaxNumberOfUserThreads = 20;


            if (this.SupportsComPort)
            {
                MyCOMPort = this.ComPorts[1];
                MyCOMPort.SerialDataReceived += new ComPortDataReceivedEvent(myComPort_SerialDataReceived);

                if (MyCOMPort.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("COM Port couldn't be registered. Cause: {0}", MyCOMPort.DeviceRegistrationFailureReason);

                if (MyCOMPort.Registered)
                    MyCOMPort.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                             ComPort.eComDataBits.ComspecDataBits8,
                                             ComPort.eComParityType.ComspecParityNone,
                                             ComPort.eComStopBits.ComspecStopBits1,
                                             ComPort.eComProtocolType.ComspecProtocolRS232,
                                             ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                             ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
            }

			// ensure this processor has ethernet then setup the EISC
			if (this.SupportsEthernet)
			{
				myEISC = new EthernetIntersystemCommunications(eISCIPID, eISCIP, this);
				if (myEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
				{
					ErrorLog.Error(">>> The EISC was not registered: {0}", myEISC.RegistrationFailureReason);
				}
				else
				{
					// Device has been registered. Now register for the event handlers
					myEISC.OnlineStatusChange += new OnlineStatusChangeEventHandler(myEISC_OnlineStatusChange);
					myEISC.SigChange += new SigEventHandler(myEISC_SigChange);
					ErrorLog.Notice(">>> The EISC has been registered successfully");
				}
			}
			else
			{
				ErrorLog.Error(">>> This processor does not support ethernet, so this program will not run");
			}


		}

		/// <summary>
		/// Overridden function... Invoked before any traffic starts flowing back and forth between the devices and the 
		/// user program. 
		/// This is used to start all the user threads and create all events / mutexes etc.
		/// This function should exit ... If this function does not exit then the program will not start
		/// </summary>
		public override void InitializeSystem()
		{
            if (this.SupportsComPort && MyCOMPort.Registered)
                RxHandler = new Thread(RxMethod, null, Thread.eThreadStartOptions.Running);
		}

		#endregion

		#region myEISC  methods	

		void myEISC_SigChange(BasicTriList currentDevice, SigEventArgs args)
		{
			// What kind of event ??
			switch (args.Event)
			{
				case eSigEvent.BoolChange:
				// Bool change event
				if (args.Sig == myEISC.BooleanOutput[10])
				{
                    if (args.Sig.BoolValue == true)
                    {
                        myEISC.StringInput[10].StringValue = "EISC says Turning unit on";
                        setDisplayPower(true);
                    }
                    else
                    {
                        myEISC.StringInput[10].StringValue = "EISC says Turning unit off";
                        setDisplayPower(false);
                    }
				}
                else if (args.Sig == myEISC.BooleanOutput[11])
                {
                    if (args.Sig.BoolValue == true)
                    {
                        myEISC.StringInput[10].StringValue = " EISC says Switching to \"HDMI1\"";
                        setDisplayInput("HDMI1");
                    }
                }
                else if (args.Sig == myEISC.BooleanOutput[12])
                {
                    if (args.Sig.BoolValue == true)
                    {
                        myEISC.StringInput[10].StringValue = " EISC says Switching to \"HDMI2\"";
                        setDisplayInput("HDMI2");
                    }
                }

				break;
				case eSigEvent.StringChange:
				// String change event
				break;
				case eSigEvent.UShortChange:
				// UShort change event
				break;
			}
		}

        void myEISC_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            if (currentDevice == myEISC)
            {
                if (args.DeviceOnLine == true)
                {
                    // Device is ONLINE... Go ahead and start sending data to it..
                    myEISC.StringInput[1].StringValue = " Hello From Remote via EISC";
                    CrestronConsole.PrintLine("Connected to remote via EISC");
                }
                else
                {
                    // Device just went offline
                    CrestronConsole.PrintLine("Connection to remote EISC had been dropped!!! ");
                }
            }
        } 
		
		#endregion



        #region myComPort methods
		
        object RxMethod(object obj)
        {
            StringBuilder RxData = new StringBuilder();
            int Pos = -1;

            String MatchString = String.Empty;
            // the Dequeue method will wait, making this an acceptable
            // while (true) implementation.
            while (true)
            {
                try
                {
                    // removes string from queue, blocks until an item is queued
                    string tmpString = RxQueue.Dequeue();

                    if (tmpString == null)
                        return null; // terminate the thread

                    RxData.Append(tmpString); //Append received data to the COM buffer
                    MatchString = RxData.ToString();

                    //find the delimiter
                    Pos = MatchString.IndexOf(Convert.ToChar("\n"));
                    if (Pos >= 0)
                    {
                        // delimiter found
                        // create temporary string with matched data.
                        MatchString = MatchString.Substring(0, Pos + 1);
                        RxData.Remove(0, Pos + 1); // remove data from COM buffer

                        // parse data here
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("Exception in thread: {0}", ex.Message);
                }
            }
        }

        void myComPort_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            RxQueue.Enqueue(args.SerialData);
        }

        #endregion




		#region display methods


		/// <summary>
		/// Method to send the power commands to the display via the dm rmc com port.
		/// </summary>
		/// <param name="power">Power state: TRUE = send on command, FALSE = send off command.</param>
		private void setDisplayPower(bool power)
		{
			if (power)
			{
				CrestronConsole.PrintLine("Display power on command");

				// Samsung power on command
                MyCOMPort.Send("\x6B\x61\x20\x30\x30\x20\x30\x31\x0D");

				// wait for one second before sending input command
				Thread.Sleep(2000);

				// samsung power on command 
                MyCOMPort.Send("\x6B\x61\x20\x30\x30\x20\x30\x31\x0D");
			}
			else
			{
				CrestronConsole.PrintLine("Display power off command");

				// samsung power off command
                MyCOMPort.Send("\x6B\x61\x20\x30\x30\x20\x30\x30\x0D");
			}
		}
        /// <summary>
        /// Method to set the input on the display
        /// </summary>
        /// <param name="input">HDMI1 or HDMI2 to select inputs on the display</param>
        private void setDisplayInput(string input)
        {
            if (input == "HDMI1")
            {
                CrestronConsole.PrintLine("Display switching to \"HDMI1\"");

                // Samsung HDMI1 command
                MyCOMPort.Send("\x78\x62\x20\x30\x30\x20\x41\x30\x0D");
            }
            else if (input == "HDMI2")
            {
                CrestronConsole.PrintLine("Display switching to \"HDMI2\"");

                // Samsung HDMI2 command
                MyCOMPort.Send("\x78\x62\x20\x30\x30\x20\x41\x31\x0D");
            }

        }

		#endregion




		#region system event handlers

		/// <summary>
		/// Method to handle the processor's ethernet adapter events.
		/// </summary>
		/// <param name="ethernetEventArgs">Information about the event being raised.</param>
		void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs ethernetEventArgs)
		{
			// only process the main ehternet adapter's events
			if (ethernetEventArgs.EthernetAdapter != EthernetAdapterType.EthernetLANAdapter)
				return;

			// determine what type of event has been raised
			switch (ethernetEventArgs.EthernetEventType)
			{
				case eEthernetEventType.LinkUp:
					// get the processor's ip address
					var enetInfo = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0);

				break;

				case eEthernetEventType.LinkDown:
				default:
					break;
			}
		}



		/// <summary>
		/// Method to handle program events on this processor.
		/// </summary>
		/// <param name="programEventType">Information about the event being raised.</param>
		void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
		{
			switch (programEventType)
			{
				case eProgramStatusEventType.Paused:
					
					break;

				case eProgramStatusEventType.Resumed:

					break;

				case eProgramStatusEventType.Stopping:
					
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle system events on this processor.
		/// </summary>
		/// <param name="systemEventType">Information about the event being raised.</param>
		void CrestronEnvironment_SystemEventHandler(eSystemEventType systemEventType)
		{
			switch (systemEventType)
			{
				case eSystemEventType.Rebooting:
					
					break;

				case eSystemEventType.DiskInserted:
				case eSystemEventType.DiskRemoved:
				default:
					break;
			}
		}

		#endregion
	}
}
