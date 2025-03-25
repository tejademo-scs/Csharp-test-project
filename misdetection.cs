public void Connect(string strWorkstationIP, int iWorkstationIP, ClientCallBack callback)
  {
  try
  {
  nBadQualityCount = -1;
  CallBack = callback;
  m_SdiInterface.Connect(strWorkstationIP, iWorkstationIP, SDIEventCallback);
  m_ThreadsRunning = true;
  Thread OPCReceiveRTDataThread = new Thread(OPCReceiveRTDataThreadProc);
  OPCReceiveRTDataThread.Priority = ThreadPriority.Normal;
  OPCReceiveRTDataThread.Start();
  }
  catch (Exception e)
  {
  m_SdiInterface.LogMsg("Exception Occured while connecting to work station" + e.Message);
  _loggerService.Error("Exception Occured while connecting to work station" + e.Message, deviceNameToDisplay);
  }
  }
