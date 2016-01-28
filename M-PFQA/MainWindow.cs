using System;
using Gtk;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.IO.Ports;
using GLib;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;

using Mono.Unix.Native;
using Mono.Unix;
using System.Collections.Generic;
using EigenvectorInterpreter;
using EnumHelper;


public enum DISPLAY_KEYPAD_CODES : byte
{
	None = 0,
	ENTER = 76,
	F2 = 77,
	F1 = 82,
	UP = 81,
	RIGHT = 85,
	DOWN = 86,
	LEFT = 87
}

public enum FUEL_TYPES
{
	[DescriptionAttribute("Gasoline")]
	Gasoline,
	[DescriptionAttribute("Diesel")]
	Diesel,
	[DescriptionAttribute("AvGas")]
	Avgas,
	[DescriptionAttribute("JP-8")]
	JP8,
	[DescriptionAttribute("Kerosene")]
	Kerosene
}

public class SerialComm : IDisposable
{
	private int _fd = 0;
	private UnixStream _ss = null;
	private string _name = "";
	private int _baudRate = 115200;
	public delegate void ReadHandler(byte[] data, int count);
	private ReadHandler _readHandler = null;
	private System.Threading.Thread _readThread = null;
	private bool _stopReadThread = false;

	public SerialComm ()
	{
	}

	public SerialComm(string name, int nBaudRate)
	{
		_name = name;
		_baudRate = nBaudRate;
	}
 
	public bool Open ()
	{
		_fd = Syscall.open (_name, OpenFlags.O_RDWR);	// | OpenFlags.O_NONBLOCK | OpenFlags.O_NOCTTY);
		if (_fd != -1)
		{
			ForceSetBaudRate ();
			_ss = new UnixStream (_fd);
			var fndelay = (int)OpenFlags.O_NONBLOCK;
			Syscall.fcntl(_ss.Handle, FcntlCommand.F_SETFL, fndelay);
			if (_readThread != null)
			{
				_readThread.Start();
			}
			Clear();
			return true;
		}
		return false;
	}

	public bool Open(string name, int nBaudRate)
	{
		_name = name;
		_baudRate = nBaudRate;
		return Open ();
	}

	public void Close ()
	{
		if (_ss != null)
		{
			//Console.WriteLine("SerialComm::Close port = " + _name);
			_stopReadThread = true;
			System.Threading.Thread.Sleep(100);
			_ss.Close ();
			_ss = null;
			Syscall.close(_fd);
			_fd = 0;
		}
	}

	~SerialComm ()
	{
		Dispose ();
	}

	public void Dispose ()
	{
		Close();
	}

	private void ForceSetBaudRate()
	{
        if (System.Type.GetType ("Mono.Runtime") == null) return; //It is not mono === not linux! 
        string arg = String.Format("-F {0} speed {1}",_name , _baudRate);
        var proc = new System.Diagnostics.Process
        {
            EnableRaisingEvents = false,
            StartInfo = {FileName = @"stty", Arguments = arg}
        };
        proc.Start();
        proc.WaitForExit();
	}

	public ReadHandler GetReadHandler 
	{
		get
		{
			return _readHandler;
		}
		set
		{
			if (value != null)
			{
				_readHandler = value;
				if (_readThread == null)
				{
					_readThread = new System.Threading.Thread(ReadThread);
					if (_ss != null)
					{
						_stopReadThread = false;
						_readThread.Start();
					}
				}
			}
			else
			{
				if (_readHandler != null)
				{
					_stopReadThread = true;
					if (_readThread.IsAlive)
					{
						_readThread.Join();
					}
					_readThread = null;
				}
				_readHandler = value;
			}
		}
	}

	private void ReadThread ()
	{
		while (!_stopReadThread)
		{
			byte[] response = new byte[100];
			int nRead = 0;
			try
			{
				//Console.WriteLine("SerialComm::ReadThread call stream::Read");
				nRead = _ss.Read(response, 0, 100);
				//Console.WriteLine("stream::Read returned " + nRead.ToString());
			}
			catch (Mono.Unix.UnixIOException ex)
			{
				nRead = 0;
			}
			if (nRead > 0 )
			{
				Console.WriteLine("stream::Read returned " + nRead.ToString());
				if (_readHandler != null)
				{
					_readHandler(response, nRead);
				}
			}
			System.Threading.Thread.Sleep(100);
		}
	}

	public bool IsOpen ()
	{
		return _ss != null;
	}

	public void Write (byte[] cmdBytes)
	{
		_ss.Write(cmdBytes, 0, cmdBytes.Length);
	}

	public void Write (string cmd)
	{
		//Console.WriteLine("serialStream.Write " + cmd);
		byte[] cmdBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(cmd);
		_ss.Write(cmdBytes, 0, cmdBytes.Length);
		// omPort.WriteLine(cmd);
	}

	public void Clear ()
	{
		byte[] response = new byte[1000];
		int nDelay = 10;
		for (int i = 0; i < 100; i += nDelay)
		{
			try
			{
				_ss.Read (response, 0, 1000);
			}
			catch (Mono.Unix.UnixIOException)
			{
			}
			System.Threading.Thread.Sleep(nDelay);
		}
	}

	public bool Read (out byte[] response, int nMaxBytes, int nTimeoutMS)
	{
		response = null;
		byte[] buffer = new byte[nMaxBytes];
		int nDelay = 10;
		int nBytesToRead = nMaxBytes;
		int nOffset = 0;
		int nBytesRead = 0;
		int nConsecutiveEmptyReads = 0;
		// Read until nMaxBytes have been read or we timed out
		for (int i = 0; (i < nTimeoutMS) && (nBytesToRead > 0); i += nDelay)
		{
			//Console.WriteLine ("Try to read serialStream");
			/*
			if (serialStream.Length > 0)
			{
				Console.WriteLine("serialStream has " + serialStream.Length.ToString() + " bytes available");
			}
			*/
			int nRead = 0;
			try
			{
				nRead = _ss.Read (buffer, nOffset, nBytesToRead);
			}
			catch (Mono.Unix.UnixIOException ex)
			{
				nRead = 0;
			}
			if (nRead > 0)
			{
				/*
				Console.WriteLine ("serialStream.Read returned " + nRead.ToString ());
				for (int ii = nOffset; ii < nOffset + nRead; ii++)
				{
					Console.Write(buffer[ii].ToString() + " ");
				}
				Console.WriteLine();
				*/
				//string temp = System.Text.ASCIIEncoding.ASCII.GetString (response);
				//Console.WriteLine("read " + temp);
				nOffset += nRead;
				nBytesToRead -= nRead;
				nBytesRead += nRead;
				nConsecutiveEmptyReads = 0;
				//return true;
			}
			else
			{
				if (nBytesRead > 0)
				{
					nConsecutiveEmptyReads++;
					if (nConsecutiveEmptyReads >= 2)
					{
						break;
					}
				}
			}
			// else, wait for some bytes
			System.Threading.Thread.Sleep (nDelay);
			//Console.WriteLine ("Waiting for data");
		}
		if (nBytesRead > 0)
		{
			response = new byte[nBytesRead];
			for (int i = 0; i < nBytesRead; i++)
			{
				response[i] = buffer[i];
			}
		}
		return nBytesRead > 0;
	}

	public bool Read (out string response, int nMaxBytes, int nTimeoutMS)
	{
		response = null;
		byte[] respBytes;
		if (Read (out respBytes, nMaxBytes, nTimeoutMS))
		{
			response = System.Text.ASCIIEncoding.ASCII.GetString (respBytes);
			return true;
		}
		return false;
	}
}


public class SerialPortLib
{
	private Int32 _nHandle = -1;
	private string _portName = "";
	private UInt32 _nBaudRate = 115200;
	public delegate void ReadHandler(byte[] data, int count);
	private ReadHandler _readHandler = null;
	private System.Threading.Thread _readThread = null;
	private bool _stopReadThread = false;

	public SerialPortLib(string portName, int nBaudRate)
	{
		_portName = portName;
		_nBaudRate = (UInt32)nBaudRate;
	}

	~SerialPortLib ()
	{
		Dispose ();
	}

	public bool Open ()
	{
		_nHandle = SerialPort_Open (_portName, _nBaudRate, 0);
		bool bRet = _nHandle > 0;
		if (bRet)
		{
			//Clear ();
			if (_readThread != null)
			{
				_readThread.Start ();
			}
		}
		return bRet;
	}

	public void Close ()
	{
		if (_nHandle > 0)
		{
			_stopReadThread = true;
			System.Threading.Thread.Sleep(100);

			SerialPort_Close (_nHandle);
			_nHandle = -1;
		}
	}

	public void Dispose ()
	{
		Close();
	}

	public ReadHandler GetReadHandler 
	{
		get
		{
			return _readHandler;
		}
		set
		{
			if (value != null)
			{
				_readHandler = value;
				if (_readThread == null)
				{
					_readThread = new System.Threading.Thread(ReadThread);
					if (_nHandle > 0)
					{
						_stopReadThread = false;
						_readThread.Start();
					}
				}
			}
			else
			{
				if (_readHandler != null)
				{
					_stopReadThread = true;
					if (_readThread.IsAlive)
					{
						_readThread.Join();
					}
					_readThread = null;
				}
				_readHandler = value;
			}
		}
	}

	private void ReadThread ()
	{
		while (!_stopReadThread)
		{
			byte[] response = new byte[100];
			int nRead = SerialPort_Read_ByteArray(_nHandle, response, (UInt32)response.Length, 10);
			if (nRead > 0 )
			{
				if (_readHandler != null)
				{
					_readHandler(response, nRead);
				}
			}
			System.Threading.Thread.Sleep(50);
		}
	}

	public bool IsOpen ()
	{
		return _nHandle > 0;
	}

	public bool Write (byte[] cmdBytes)
	{
		return SerialPort_Write_ByteArray(_nHandle, cmdBytes, (UInt32)cmdBytes.Length) == cmdBytes.Length;
	}

	public bool Write (string cmd)
	{
		return SerialPort_Write_StringA(_nHandle, cmd) == cmd.Length;
		/*
		byte[] cmdBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(cmd);
		SerialPort_Write_ByteArray(_nHandle, cmdBytes, (UInt32)cmdBytes.Length);
		*/
	}

	public void Clear ()
	{
		byte[] response = new byte[1000];
		int nDelay = 10;
		for (int i = 0; i < 10; i += nDelay)
		{
			SerialPort_Read_ByteArray(_nHandle, response, (UInt32)response.Length, (UInt32)nDelay);
		}
	}

	public bool Read (out byte[] response, int nMaxBytes, int nTimeoutMS)
	{
		response = null;
		byte[] buffer = new byte[nMaxBytes];
		Int32 nRead = SerialPort_Read_ByteArray(_nHandle, buffer, (UInt32)buffer.Length, (UInt32)nTimeoutMS);
		if (nRead > 0)
		{
			response = new byte[nRead];
			for (int i = 0; i < nRead; i++)
			{
				response[i] = buffer[i];
			}
		}
		return nRead > 0;
	}

	public bool Read (out string response, int nMaxBytes, int nTimeoutMS)
	{
		response = null;
		byte[] respBytes;
		if (Read (out respBytes, nMaxBytes, nTimeoutMS))
		{
			response = System.Text.ASCIIEncoding.ASCII.GetString (respBytes);
			return true;
		}
		return false;
	}

	[DllImport("./libSerialPortLib.so", CharSet = CharSet.Ansi)]
//	[DllImport("/home/pi/Dev/RTA/SerialLib/bin/Release/libSerialPortLib.so", CharSet = CharSet.Ansi)]
	extern public static Int32 SerialPort_Open( string pszPortName, UInt32 nBaudRate, UInt32 nParity );

	[DllImport("./libSerialPortLib.so", CharSet = CharSet.Ansi)]
//	[DllImport("/home/pi/Dev/RTA/SerialLib/bin/Release/libSerialPortLib.so")]
	extern public static void SerialPort_Close( Int32 fd );

	[DllImport("./libSerialPortLib.so", CharSet = CharSet.Ansi)]
//	[DllImport("/home/pi/Dev/RTA/SerialLib/bin/Release/libSerialPortLib.so")]
	extern public static Int32 SerialPort_Write_ByteArray( Int32 fd, byte[] cmdBytes, UInt32 siz );

	[DllImport("./libSerialPortLib.so", CharSet = CharSet.Ansi)]
//	[DllImport("/home/pi/Dev/RTA/SerialLib/bin/Release/libSerialPortLib.so", CharSet = CharSet.Ansi)]
	extern public static Int32 SerialPort_Write_StringA( Int32 fd, string pszCmd );

	[DllImport("./libSerialPortLib.so", CharSet = CharSet.Ansi)]
//	[DllImport("/home/pi/Dev/RTA/SerialLib/bin/Release/libSerialPortLib.so")]
	extern public static Int32 SerialPort_Read_ByteArray( Int32 fd, byte[] respBytes, UInt32 nMaxBytes, UInt32 nTimeoutMS );
}


public class MessageBox 
{ 
	public delegate void DoneHandler(int response);
	private static DoneHandler _doneHandler = null;
	private static MessageDialog _md = null;

	public static void Show(Gtk.Window parent_window, DialogFlags flags, MessageType msgtype, ButtonsType btntype, string msg) 
    { 
        MessageDialog md = new MessageDialog (parent_window, flags, msgtype, btntype, msg); 
        md.Run (); 
        md.Destroy(); 
    } 
            
    public static void Show(string msg) 
    { 
        MessageDialog md = new MessageDialog (null, DialogFlags.Modal, MessageType.Other, ButtonsType.Ok, msg); 
        md.Run (); 
        md.Destroy(); 
    } 

	public static void ShowModeless(string msg, DoneHandler dh)
	{
		_doneHandler = dh;
		_md = new MessageDialog (null, DialogFlags.DestroyWithParent, MessageType.Other, ButtonsType.Ok, msg);
		_md.Response += new ResponseHandler(RespHandler);
		_md.ShowNow();
		//_md.Run();
	}

	public static void RespHandler (object sender, ResponseArgs ra)
	{
		if (_doneHandler != null)
		{
			_doneHandler((int)ra.ResponseId);
		}
	}

	public static void Destroy ()
	{
		if (_md != null)
		{
			_md.Response -= new ResponseHandler(RespHandler);

			_md.Destroy ();
			_md = null;
			_doneHandler = null;
		}
	}
}



public class Scan
{
	public double[] xVals = null;
	public double[] yVals = null;

	public Scan ()
	{
	}

	public Scan (Scan source)
	{
		xVals = new double[source.xVals.Length];
		yVals = new double[source.yVals.Length];
		source.xVals.CopyTo(xVals, 0);
		source.yVals.CopyTo(yVals, 0);
	}

	public Scan(double[] x, double[] y)
	{
		xVals = new double[x.Length];
		yVals = new double[y.Length];
		x.CopyTo(xVals, 0);
		y.CopyTo(yVals, 0);
	}

	public Scan(double[] x, UInt16[] y)
	{
		xVals = new double[x.Length];
		yVals = new double[y.Length];
		x.CopyTo(xVals, 0);
		for (int i = 0; i < y.Length; i++)
		{
			yVals[i] = (double)y[i];
		}
	}

	public Scan (List<double> x, List<double> y)
	{
		xVals = new double[x.Count];
		yVals = new double[y.Count];
		x.CopyTo(xVals);
		y.CopyTo(yVals);
	}

	public Scan (List<UInt16> x, List<UInt16> y)
	{
		xVals = new double[x.Count];
		yVals = new double[y.Count];
		for (int i = 0; i < x.Count; i++)
		{
			xVals[i] = (double)x[i];
			yVals[i] = (double)y[i];
		}
	}

	public Scan (List<double> x, List<UInt16> y)
	{
		xVals = new double[x.Count];
		yVals = new double[y.Count];
		for (int i = 0; i < x.Count; i++)
		{
			xVals[i] = x[i];
			yVals[i] = (double)y[i];
		}
	}

	public Scan (double[] x, List<UInt16> y)
	{
		xVals = new double[x.Length];
		yVals = new double[y.Count];
		x.CopyTo(xVals, 0);
		for (int i = 0; i < y.Count; i++)
		{
			yVals[i] = (double)y[i];
		}
	}

	public string ToString(string sep = "\r")
	{
		//string retStr = "";
		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		for (int i = 0; i < xVals.Length; i++)
		{
			sb.AppendFormat("{0} X: {1:0.00} Y: {2:0.000000}{3}", i, xVals[i], yVals[i], sep);
			//retStr += String.Format("{0} X: {1:0.00} Y: {2:0.000000}{3}", i, xVals[i], yVals[i], sep);
		}
		return sb.ToString();
		//return retStr;
	}
}

namespace MPFQA
{
	public class Display
	{
		private List<string> displayBuffer = new List<string>();
		private const int numRows = 2;
		private const int numCols = 20;
		private int curRow = 0;
		private int curCol = 0;
		private int maxTextWidth = 0;
		private int scrollAnchorRow = 0;
		public SerialPortLib comm = null;
		public delegate void DisplayKeyCodeHandler(DISPLAY_KEYPAD_CODES kc);
		private DisplayKeyCodeHandler _displayKeyCodeHandler = null;
		private List<string> listMenuItems = null;
		private int curMenuItem = 0;
		private DISPLAY_KEYPAD_CODES userMenuSelection = DISPLAY_KEYPAD_CODES.None;
		private SerialPortLib.ReadHandler _previousReadHandler = null;
		private DisplayKeyCodeHandler _modelessKeyCodeHandler = null;

		public Display (SerialPortLib sp)
		{
			comm = sp;
			Clear();
		}

		~Display()
		{
			Close();
		}

		public void Close ()
		{
			if (comm != null)
			{
				comm.Close ();
			}
		}

		public void Clear ()
		{
			displayBuffer.Clear ();
			curRow = 0;
			curCol = 0;
			maxTextWidth = numCols;
			comm.Write (new byte[]{0xFE, 0x58});
		}

		public void SetScrollAnchor (int nRow)
		{
			scrollAnchorRow = nRow;
		}

		private void UpdateTextWidths ()
		{
			for (int i = 0; i < displayBuffer.Count; i++)
			{
				if (displayBuffer[i].Length < maxTextWidth)
				{
					displayBuffer[i] = displayBuffer[i].PadRight(maxTextWidth);
				}
			}
		}

		public int Add (string text, bool bScrollToNew)
		{
			displayBuffer.Add (text);
			maxTextWidth = Math.Max(maxTextWidth, text.Length);
			UpdateTextWidths();
			//Console.WriteLine("Display::Add " + text + " count = " + displayBuffer.Count.ToString() + " curRow = " + curRow.ToString());
			if (displayBuffer.Count - curRow <= numRows)
			{
				int nRowStart = displayBuffer.Count - curRow;
				comm.Write (new byte[]{0xFE, 0x47, 1, (byte)nRowStart});
				//string t = text.Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols);
				//Console.WriteLine("Writing " + t + " to display at row " + nRowStart.ToString());
				comm.Write (text.Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols));
			}
			else if (bScrollToNew)
			{
				comm.Write (new byte[]{0xFE, 0x58});
				comm.Write (new byte[]{0xFE, 0x47, 1, 1});
				string t = displayBuffer[displayBuffer.Count - 2];
				comm.Write (t.Substring(curCol, Math.Min(numCols, t.Length)));
				comm.Write (new byte[]{0xFE, 0x47, 1, 2});
				t = displayBuffer[displayBuffer.Count - 1];
				comm.Write (t.Substring(curCol, Math.Min(numCols, t.Length)));
				curRow = displayBuffer.Count - 2;
			}
			return displayBuffer.Count;
		}

		public void SetText (List<string> textLines)
		{
			Clear();
			foreach (string text in textLines)
			{
				Add(text, false);
			}
		}

		public void Remove (int nIndex)
		{
			if (nIndex < displayBuffer.Count)
			{
				displayBuffer.RemoveAt(nIndex);
				if ((nIndex >= curRow) && (nIndex < (curRow + numRows)))
				{
					comm.Write (new byte[]{0xFE, 0x47, 1, (byte)((nIndex - curRow) + 1) });
					if (nIndex < displayBuffer.Count)
					{
						string t = displayBuffer[nIndex];
						comm.Write (t.Substring(curCol, Math.Min(numCols, t.Length)).PadRight(numCols));
					}
					else
					{
						comm.Write (new string(' ', numCols));
					}
				}
			}
		}

		public void Replace (int nIndex, string text)
		{
			if (nIndex < displayBuffer.Count)
			{
				displayBuffer[nIndex] = text;
				if ((nIndex >= curRow) && (nIndex < (curRow + numRows)))
				{
					comm.Write (new byte[]{0xFE, 0x47, 1, (byte)((nIndex - curRow) + 1) });
					comm.Write (displayBuffer[nIndex].Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols));
				}
			}
			else
			{
				Add (text, true);
			}
		}

		public void ScrollUp ()
		{
			if (displayBuffer.Count - curRow > numRows)
			{
				curRow++;

				comm.Write (new byte[]{0xFE, 0x58});

				comm.Write (new byte[]{0xFE, 0x47, 1, 1});
				string text = displayBuffer [curRow];
				comm.Write (text.Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols));
				if (curRow + 1 < displayBuffer.Count)
				{
					comm.Write (new byte[]{0xFE, 0x47, 1, 2});
					text = displayBuffer [curRow + 1];
					comm.Write (text.Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols));
				}
			}
		}

		public void ScrollDown ()
		{
			if (curRow > 0)
			{
				curRow--;

				comm.Write (new byte[]{0xFE, 0x58});

				comm.Write (new byte[]{0xFE, 0x47, 1, 1});
				string text = displayBuffer [curRow];
				comm.Write (text.Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols));
				comm.Write (new byte[]{0xFE, 0x47, 1, 2});
				text = displayBuffer [curRow + 1];
				comm.Write (text.Substring(curCol, Math.Min(numCols, text.Length)).PadRight(numCols));

			}
		}

		public static bool GetDisplayKeypadCode (byte b, out DISPLAY_KEYPAD_CODES code)
		{
			code = DISPLAY_KEYPAD_CODES.ENTER;
			foreach (DISPLAY_KEYPAD_CODES e in Enum.GetValues(typeof(DISPLAY_KEYPAD_CODES)))
			{
				if (e == (DISPLAY_KEYPAD_CODES)b)
				{
					code = (DISPLAY_KEYPAD_CODES)e;
					return true;
				}
			}
			return false;
		}

		private void KeypadHandler (byte[] response, int count)
		{
			for (int i = 0; i < count; i++)
			{
				DISPLAY_KEYPAD_CODES code;
				if (GetDisplayKeypadCode (response [i], out code))
				{
					if (_displayKeyCodeHandler != null)
					{
						_displayKeyCodeHandler (code);
					}
					else
					{
						switch (code)
						{
						case DISPLAY_KEYPAD_CODES.DOWN:
							ScrollUp ();
							break;

						case DISPLAY_KEYPAD_CODES.UP:
							ScrollDown ();
							break;
						}
					}
				}
			}
		}

		private void MenuKeypadHandler (DISPLAY_KEYPAD_CODES code)
		{
			switch (code)
			{
			case DISPLAY_KEYPAD_CODES.DOWN:
				if (curMenuItem + 1 < listMenuItems.Count)
				{
					curMenuItem++;
					Replace(1, listMenuItems[curMenuItem]);
				}
				break;

			case DISPLAY_KEYPAD_CODES.UP:
				if (curMenuItem > 0)
				{
					curMenuItem--;
					Replace(1, listMenuItems[curMenuItem]);
				}
				break;

			case DISPLAY_KEYPAD_CODES.ENTER:
				userMenuSelection = code;
				break;
			}
		}

		public int GetMenuSelection (string caption, List<string> listOptions)
		{
			int selectedItem = -1;
			listMenuItems = new List<string> (listOptions);
			curMenuItem = 0;
			userMenuSelection = DISPLAY_KEYPAD_CODES.None;

			//Console.WriteLine("Display.GetMenuSelection: At start display comm handler = " + comm.GetReadHandler.ToString());

			SerialPortLib.ReadHandler rh = comm.GetReadHandler;
			comm.GetReadHandler = KeypadHandler;

			_displayKeyCodeHandler += new DisplayKeyCodeHandler (MenuKeypadHandler);

			Clear ();
			Add (caption, false);
			Add (listOptions[0], false);

			bool bDone = false;

			while (userMenuSelection == DISPLAY_KEYPAD_CODES.None)
			{
				System.Threading.Thread.Sleep (50);
			}

			if (userMenuSelection == DISPLAY_KEYPAD_CODES.ENTER)
			{
				selectedItem = curMenuItem;
			}

			_displayKeyCodeHandler -= new DisplayKeyCodeHandler(MenuKeypadHandler);
			comm.GetReadHandler = rh;

			//Console.WriteLine("Display.GetMenuSelection: At start display comm handler = " + comm.GetReadHandler.ToString());

			return selectedItem;
		}

		private void ModelessMenuKeypadHandler (DISPLAY_KEYPAD_CODES code)
		{
			switch (code)
			{
			case DISPLAY_KEYPAD_CODES.DOWN:
				if (curMenuItem + 1 < listMenuItems.Count)
				{
					curMenuItem++;
					Replace(1, listMenuItems[curMenuItem]);
				}
				break;

			case DISPLAY_KEYPAD_CODES.UP:
				if (curMenuItem > 0)
				{
					curMenuItem--;
					Replace(1, listMenuItems[curMenuItem]);
				}
				break;

			case DISPLAY_KEYPAD_CODES.ENTER:
			case DISPLAY_KEYPAD_CODES.F1:
				_modelessKeyCodeHandler (code);
				break;
			}
		}


		public void ShowMenuModeless (string caption, List<string> listOptions, DisplayKeyCodeHandler kh)
		{
			int selectedItem = -1;
			listMenuItems = new List<string> (listOptions);
			curMenuItem = 0;

			//Console.WriteLine("Display.GetMenuSelection: At start display comm handler = " + comm.GetReadHandler.ToString());

			_previousReadHandler = comm.GetReadHandler;
			comm.GetReadHandler = KeypadHandler;

			_modelessKeyCodeHandler = kh;
			_displayKeyCodeHandler += new DisplayKeyCodeHandler (ModelessMenuKeypadHandler);

			Clear ();
			Add (caption, false);
			Add (listOptions [0], false);
		}

		public int SelectedMenuItem
		{
			get 
			{
				return curMenuItem;
			}
		}

		public void KillMenuModeless()
		{
			_displayKeyCodeHandler -= new DisplayKeyCodeHandler(ModelessMenuKeypadHandler);
			comm.GetReadHandler = _previousReadHandler;
		}

	}


	public class MessageBoxEx
	{ 
		private static MessageDialog _md = null;

		private static int _response = 0;

		public static int Show (string screenMsg, MPFQA.Display display, List<string> displayMsg)
		{
			_response = 0;
			_md = new MessageDialog (null, DialogFlags.DestroyWithParent, MessageType.Other, ButtonsType.OkCancel, screenMsg);
			_md.Response += new ResponseHandler (MessageBoxRespHandler);
			SerialPortLib.ReadHandler rh = display.comm.GetReadHandler;
			display.comm.GetReadHandler = DisplayRespHandler;
			display.SetText (displayMsg);

			_md.ShowNow ();

			while (_response == 0)
			{
		         while (Gtk.Application.EventsPending ())
		              Gtk.Application.RunIteration ();

				System.Threading.Thread.Sleep(50);
			}
			Destroy();

			display.comm.GetReadHandler = rh;

			return _response;
		}

		private static void MessageBoxRespHandler (object sender, ResponseArgs ra)
		{
			_response = (int)ra.ResponseId;
		}

		private static void DisplayRespHandler (byte[] response, int count)
		{
			DISPLAY_KEYPAD_CODES code;
			if (MPFQA.Display.GetDisplayKeypadCode(response[0], out code))
			{
				switch (code)
				{
					case DISPLAY_KEYPAD_CODES.ENTER:
						_response = (int)Gtk.ResponseType.Ok;
					break;

					case DISPLAY_KEYPAD_CODES.F1:
						_response = (int)Gtk.ResponseType.Cancel;
					break;
				}
			}
		}

		public static void Destroy ()
		{
			if (_md != null)
			{
				_md.Response -= new ResponseHandler(MessageBoxRespHandler);

				_md.Destroy ();
				_md = null;
			}
		}
	}

}


public partial class MainWindow: Gtk.Window
{
	private class SerialPortInfo
	{
		public string portName = "";
		public int baudRate = 115200;
		public SerialPortLib comm = null;

		public SerialPortInfo()
		{
		}

		public SerialPortInfo(string name, int baud)
		{
			portName = name;
			baudRate = baud;
		}
	}

	//private UnixStream serialStream = null;
	private SerialPort comPort = new SerialPort ();
	private Dictionary<string, SerialPortInfo> dictSerialPorts = new Dictionary<string, SerialPortInfo>();
	private string specPort = null;
	private string tecPort = null;
	private string displayPort = null;
	private SerialPortLib specComm = null;
	private SerialPortLib displayComm = null;
	private SerialPortLib tecComm = null;
	private MPFQA.Display display = null;
	private double[] calibCoeffs = new double[5];
	private int pixelCount = 256;
	private int tecSetTemp = 12;
	private double tecTemp = 25.0;
	private double[] xAxis = null;
	private double[] xAxisInterp = null;
	private int integrationTime = 200;	// Hard-code this for now
	private int coaddCount = 8;	// Hard-code this for now
	private double clippingStart = 1050;
	private double clippingEnd = 1550;
	private IniFile iniFile = null;
	private Scan darkScan = null;
	private Scan lightScan = null;
	private Scan sampleScan = null;
	private Scan absorbanceSpectrum = null;
	private Scan absorbanceSpectrumSplined = null;
	private Scan absorbanceSpectrumClipped = null;
	private MessageBox _messageBox = null;
	public byte ACK = 0x06;
	public byte NACK = 0x15;
	public byte BELL = 0x07;
	private string SPEC = "Spec. Board";
	private string TEC = "TEC board";
	private string DISPLAY = "Display";
	private delegate void DisplayKeyCodeHandler(DISPLAY_KEYPAD_CODES kc);
	private DisplayKeyCodeHandler _displayKeyCodeHandler = null;


	public MainWindow (): base (Gtk.WindowType.Toplevel)
	{
		Build ();
		GLib.Timeout.Add (10000, new GLib.TimeoutHandler (ReadTimeout));
		//this.textviewOutput.SizeAllocated += new SizeAllocatedHandler(Scroll2);
		dictSerialPorts.Add (SPEC, new SerialPortInfo ());
		dictSerialPorts.Add (TEC, new SerialPortInfo ());
		dictSerialPorts.Add (DISPLAY, new SerialPortInfo ("", 19200));

		LoadSettings();

		//this.buttonMeasure.Clicked += new global::System.EventHandler (this.OnButtonMeasureClicked);
	}

	private void LoadSettings()
	{
		string codeName = System.Reflection.Assembly.GetExecutingAssembly ().GetName ().CodeBase;
		string appName = System.IO.Path.GetFileNameWithoutExtension (codeName);
		string iniFileName = appName + ".ini";
//		string iniFileName = System.IO.Path.GetDirectoryName (codeName) + System.IO.Path.DirectorySeparatorChar + appName + ".ini";

		iniFile = new IniFile (iniFileName);

		if (iniFile.Load ())
		{
			IniFile.IniSection sec = iniFile.GetSection("General");
			IniFile.IniSection.IniKey key = null;

			key = sec.GetKey("IntegrationTime");
			if (key != null)
			{
				integrationTime = Convert.ToInt32(key.Value);
			}
			key = sec.GetKey("Coadds");
			if (key != null)
			{
				coaddCount = Convert.ToInt32(key.Value);
			}
			key = sec.GetKey("TecTemp");
			if (key != null)
			{
				tecSetTemp = Convert.ToInt32(key.Value);
			}
		}
	}

	private void SaveSettings ()
	{
		IniFile.IniSection sec = iniFile.AddSection("General");
		sec.AddKey("IntegrationTime").Value = Convert.ToString(integrationTime);
		sec.AddKey("Coadds").Value = Convert.ToString(coaddCount);
		sec.AddKey("TecTemp").Value = Convert.ToString(tecSetTemp);

		iniFile.Save();
	}

	private void SetConnectedUI (bool bConnected)
	{
		buttonInfo.Sensitive = bConnected;
		buttonManual.Sensitive = bConnected;
		buttonStart.Label = bConnected ? "Stop" : "Start";
		buttonScan.Sensitive = bConnected;
		buttonCollectBackground.Sensitive = bConnected;
	}

	protected override void OnShown ()
	{
		base.OnShown ();

		SetConnectedUI(false);
	}

	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		Console.WriteLine ("OnDeleteEvent");

		Shutdown();

		Application.Quit ();
		a.RetVal = true;
	}

	private void OnDestroyEvent(object o, EventArgs args)
    {
        Console.WriteLine("OnDestroy");
		Shutdown();
		Console.WriteLine("OnDestroy done");
    }

	protected override void OnDestroyed ()
	{
		Console.WriteLine("OnDestroyed");
		Shutdown();
		Console.WriteLine("OnDestroyed done");
	}

	protected void Shutdown()
	{
		ShutdownConnections ();

		SaveSettings();
	}

	protected void ShutdownConnections ()
	{
		foreach (SerialPortInfo spi in dictSerialPorts.Values)
		{
			if (spi.comm != null)
			{
				spi.comm.Close ();
			}
		}

		if (specComm != null)
		{
			specComm.Close ();
		}
		if (display != null)
		{
			display.Close ();
			display = null;
		}
		if (displayComm != null)
		{
			displayComm.Close ();
		}
		if (tecComm != null)
		{
			tecComm.Close ();
		}
	}

	bool ReadTimeout ()
	{
		GetTecTemp();
		Gtk.Application.Invoke (delegate {
			var contextId = statusbarTECTemp.GetContextId("TEC Temp");
			statusbarTECTemp.Push(contextId, String.Format("TEC Temp: {0:0.000}", tecTemp));
			}
			);
		/*
		if (comPort.IsOpen && comPort.BytesToRead > 0) {
			string msg = comPort.ReadExisting ();
			Console.WriteLine ("Read " + msg + " from " + comPort.PortName);
			Gtk.Application.Invoke (delegate {
				textviewOutput.Buffer.Text += msg;
			}
			);
		}
		*/
		return true;

	}

	protected void AddOutputText (string text)
	{
		//textviewOutput.Buffer.Text += text;
		Gtk.TextIter ti = textviewOutput.Buffer.EndIter;
		textviewOutput.Buffer.Insert(ref ti, text);
		RefreshUI();
		//ti = textviewOutput.Buffer.EndIter;
		textviewOutput.ScrollToIter (ti, 0.0, false, 0.0, 0.0);
	}

	public void Scroll2(object sender, Gtk.SizeAllocatedArgs e)
    {
    	textviewOutput.ScrollToIter(textviewOutput.Buffer.EndIter, 0, false, 0, 0);
	}

	protected void SpecBoardReadHandler (byte[] response, int count)
	{
		byte[] respBytes = new byte[count];
		for (int i = 0; i < count; i++)
		{
			respBytes [i] = response [i];
		}
		string text = System.Text.ASCIIEncoding.ASCII.GetString (respBytes);
		if (!text.EndsWith ("\r"))
		{
			text += "\r";
		}
		Gtk.Application.Invoke (delegate {
			AddOutputText(text);
              //textviewOutput.Buffer.Text += text;
        });
		//textviewOutput.Buffer.Text += System.Text.ASCIIEncoding.ASCII.GetString (respBytes) + "\r";
	}

	protected void DisplayKeypadHandler (byte[] response, int count)
	{
		string text = "Display sent ";
		DISPLAY_KEYPAD_CODES code;
		for (int i = 0; i < count; i++)
//		foreach (byte b in response)
		{
			byte b = response [i];
			string t = b.ToString ();
			text += t + " ";
			if (MPFQA.Display.GetDisplayKeypadCode (b, out code))
			{
				text += "= " + code.ToString () + " ";
			}
		}
		Gtk.Application.Invoke (delegate {
			AddOutputText (text + "\r");
			//textviewOutput.Buffer.Text += text + "\r";
		}
		);
		if (MPFQA.Display.GetDisplayKeypadCode (response[0], out code))
		{
			if (_displayKeyCodeHandler != null)
			{
				_displayKeyCodeHandler(code);
			}
			else
			{
				switch(code)
				{
					case DISPLAY_KEYPAD_CODES.DOWN:
						display.ScrollUp();
					break;

					case DISPLAY_KEYPAD_CODES.UP:
						display.ScrollDown();
					break;
				}
			}
		}
	}

	protected void OnButtonStartClicked (object sender, EventArgs e)
	{
		if (specComm == null || !specComm.IsOpen ())
		{
			UpdateSystemStatus("Initializing");

			DeterminePortConnections ();

			InitDisplay ();

			InitSpectrometer ();

			InitTEC ();

			UpdateSystemStatus("Ready");
		}
		else
		{
			ShutdownConnections ();
		}
		if (specComm != null)
		{
			SetConnectedUI (specComm.IsOpen ());
		} 
		else
		{
			SetConnectedUI (false);
		}
	}

	protected void OnButtonSettingsClicked (object sender, EventArgs e)
	{
		MPFQA.SettingsDialog dlg = new MPFQA.SettingsDialog ();
		dlg.Modal = true;
		dlg.IntegrationTime = integrationTime;
		dlg.CoaddCount = coaddCount;
		dlg.TecSetTemp = tecSetTemp;
		int nResp = dlg.Run ();
		if (nResp == (int)Gtk.ResponseType.Ok)	// OK button pressed
		{
			integrationTime = dlg.IntegrationTime;
			coaddCount = dlg.CoaddCount;
			if (tecSetTemp != dlg.TecSetTemp)
			{
				tecSetTemp = dlg.TecSetTemp;
				SetTecTemp();
			}
			SaveSettings();
			//AddOutputText("TEC Set temp is now " + tecSetTemp.ToString() + "\r");
		}
		dlg.Destroy();
	}

	protected void OnButtonInfoClicked (object sender, EventArgs e)
	{
		string response, text = "";
		specComm.Write ("*VERS?\r");
		if (specComm.Read (out response, 70, 200))
		{
			text += response + "\r";
			//textviewOutput.Buffer.Text += response + "\r";
		}
		if (tecComm != null && tecComm.IsOpen ())
		{
			tecComm.Write ("*para:tecall?\r");
			if (tecComm.Read (out response, 500, 400))
			{
				text += response + "\r";
				//textviewOutput.Buffer.Text += response + "\r";
			}
		}
		if (displayComm != null && displayComm.IsOpen())
		{
			// Temporarily turn off the asynch read handler
			SerialPortLib.ReadHandler rh = displayComm.GetReadHandler;
			if (rh != null)
			{
				displayComm.GetReadHandler = null;
			}

			// Get display firmware version
			displayComm.Write(new byte[]{0xFE, 0x36});
			byte[] resp;
			if (displayComm.Read(out resp, 1, 400))
			{
				byte majorRev = (byte)(resp[0] >> 4);
				byte minorRev = (byte)(resp[0] & 0x0F);
				text += string.Format("Display firmware version: {0}.{1}\r", majorRev, minorRev);
			}
			// Get display module number
			displayComm.Write(new byte[]{0xFE, 0x37});
			if (displayComm.Read(out resp, 1, 400))
			{
				text += string.Format("Display module number: {0}\r", resp[0]);
			}
			if (rh != null)
			{
				displayComm.GetReadHandler = rh;
			}
		}

		if (text.Length > 0)
		{
			AddOutputText (text);
		}
	}

	protected bool gotFuelTypeResponse = false;
	protected bool fuelTypeResponse = false;
	protected bool fuelTypeResponseIsFromDialog = false;

	protected void GetFuelTypeDialogResponseHandler (object o, ResponseArgs ra)
	{
		fuelTypeResponse = (ra.ResponseId == Gtk.ResponseType.Ok);
		fuelTypeResponseIsFromDialog = true;
		gotFuelTypeResponse = true;
	}

	protected void GetFuelTypeDisplayMenuResponseHandler (DISPLAY_KEYPAD_CODES code)
	{
		fuelTypeResponse = (code == DISPLAY_KEYPAD_CODES.ENTER);
		fuelTypeResponseIsFromDialog = false;
		gotFuelTypeResponse = true;
	}

	protected bool GetFuelType (out FUEL_TYPES fuelTypeSelected)
	{
		fuelTypeSelected = FUEL_TYPES.Gasoline;

		List<string> listOptions = EnumHelper.EnumHelper.EnumDescriptionList<FUEL_TYPES>();// new List<string> ();

		/*
		foreach (Enum value in Enum.GetValues(typeof(FUEL_TYPES)))
		{
		    listOptions.Add(value.ToString());
		}
		*/
		/*
		listOptions.Add ("Gasoline");
		listOptions.Add ("Diesel");
		listOptions.Add ("JP-8");
		listOptions.Add ("Kerosene");
		*/
		gotFuelTypeResponse = false;
		fuelTypeResponse = false;

		MPFQA.GetFuelTypeDialog dlg = new MPFQA.GetFuelTypeDialog (listOptions);
		dlg.Response += new ResponseHandler (GetFuelTypeDialogResponseHandler);
		dlg.Show ();

		if (display != null)
		{
			display.ShowMenuModeless("Select Fuel Type", listOptions, GetFuelTypeDisplayMenuResponseHandler);
		}

		while (!gotFuelTypeResponse)
		{
	         while (Gtk.Application.EventsPending ())
	              Gtk.Application.RunIteration ();
			System.Threading.Thread.Sleep (50);
		}
		 
		if (fuelTypeResponse)
		{
			if (fuelTypeResponseIsFromDialog)
			{
				fuelTypeSelected = (FUEL_TYPES)dlg.SelectedIndex;
			}
			else
			{
				fuelTypeSelected = (FUEL_TYPES)display.SelectedMenuItem;
			}
		}

		dlg.Destroy();

		if (display != null)
		{
			display.KillMenuModeless();
			display.Clear ();
			display.Add ("     RTA M-PFQA", false);
		}
		return fuelTypeResponse;
	}

	protected void TestMessageBoxDoneHandler (int response)
	{
		AddOutputText("Received " + response.ToString() + " from modeless MessageBox\r");
		MessageBox.Destroy();
		_displayKeyCodeHandler -= new DisplayKeyCodeHandler(InsertKeyCodeHandler);
	}

	protected void OnButtonManualClicked (object sender, EventArgs e)
	{
		FUEL_TYPES fuelTypeSelected;
		if (GetFuelType (out fuelTypeSelected))
		{
			AddOutputText("User selected " + fuelTypeSelected.ToString() + "\r");
		}

		/*
		List<string> listOptions = new List<string> ();
		listOptions.Add ("Gasoline");
		listOptions.Add ("Diesel");
		listOptions.Add ("JP-4");
		listOptions.Add ("Kerosene");

		MPFQA.GetFuelTypeDialog dlg = new MPFQA.GetFuelTypeDialog (listOptions);
		int nDlgResp = dlg.Run ();
		AddOutputText ("Received " + nDlgResp.ToString () + " from GetFuelTypeDialog\r");
		if (nDlgResp == (int)Gtk.ResponseType.Ok)
		{
			AddOutputText ("User selected " + listOptions[dlg.SelectedIndex] + " from GetFuelTypeDialog\r");
		}

		dlg.Destroy();

		if (display != null)
		{
			int nSelected = display.GetMenuSelection("Select Fuel Type", listOptions);
			AddOutputText("Fuel Selection Menu returned " + nSelected.ToString() + "\r");

			display.Clear();
			display.Add("     RTA M-PFQA", false);
		}
		*/

		int nResp = MPFQA.MessageBoxEx.Show("Insert sample vial\nPress Ok to proceed", display, new List<string>(new string[] {"Insert sample vial", "Hit Enter to proceed"}));
		AddOutputText("Received " + nResp.ToString() + " from MessageBoxEx\r");

		display.Clear();
		display.Add("     RTA M-PFQA", false);

		/*
		_displayKeyCodeHandler += new DisplayKeyCodeHandler(InsertKeyCodeHandler);

		MessageBox.ShowModeless("Manual Mode not yet implemented", TestMessageBoxDoneHandler);
		AddOutputText("Exiting OnButtonManualClicked\r");
		*/

		//MessageBox.Show("Manual Mode not yet implemented");
		//throw new System.NotImplementedException ();
	}

	protected void OnButtonClearResultsClicked (object sender, EventArgs e)
	{
		textviewOutput.Buffer.Clear();
	}

	protected void InitSpectrometer ()
	{
		if (dictSerialPorts [SPEC].portName != "")
		{
			specComm = new SerialPortLib (dictSerialPorts [SPEC].portName, dictSerialPorts [SPEC].baudRate);
			if (!specComm.Open ())
			{
				MessageBox.Show ("Failed to open Spec Board port " + dictSerialPorts [SPEC].portName);
				return;
			}
		}

		if (specComm != null && specComm.IsOpen ())
		{
			string response;
			System.Text.StringBuilder sb = new System.Text.StringBuilder();

			Console.WriteLine ("Reset device");
			specComm.Write ("*RST\r");
			System.Threading.Thread.Sleep (2000);
			if (specComm.Read (out response, 100, 1000))
			{
				// returns something like Softwarereset PIC24H !!PIC_Versa256 VERSION 2.610 26.06.12
				Console.WriteLine (response);
			}
			specComm.Clear ();

			System.Threading.Thread.Sleep (500);
			Console.WriteLine ("Get device ID");
			specComm.Write ("*IDN?\r");
			if (specComm.Read (out response, 100, 200))
			{
				// returns something like JETI_PIC_VERSA
				Console.WriteLine ("Read " + response);
			}
			specComm.Clear ();

			// Get the number of pixels (Y data points) the board returns
			specComm.Write ("*PARA:PIX?\r");
			if (specComm.Read (out response, 50, 200))
			{
				string[] words = response.Split (new char[]{':'});
				if (words.Length == 2)
				{
					pixelCount = Convert.ToInt32 (words [1]);
					sb.AppendFormat("Pixel Count = {0}\r", pixelCount);
					//textviewOutput.Buffer.Text += "Pixel Count = " + pixelCount.ToString () + "\r";
				}
			}

			// Read the calibration coefficients
			for (int i = 0; i < 5; i++)
			{
				specComm.Write ("*para:fit" + i.ToString () + "?\r");
				if (specComm.Read (out response, 60, 200))
				{
					string[] words = response.Split (new char[]{':'});
					if (words.Length == 2)
					{
						calibCoeffs [i] = Convert.ToDouble (words [1]);
						sb.AppendFormat("Calibration Coeff {0} = {1}\r", i, calibCoeffs[i]);
						//textviewOutput.Buffer.Text += "Calibration Coeff " + i.ToString () + " = " + calibCoeffs [i].ToString () + "\r";
					}
				}
			}

			// Calculate the X Axis using the calibration coefficients just read
			CalculateXAxis ();

			if (xAxis.Length > 0)
			{
				sb.AppendFormat("X Axis starts with {0} and ends with {1}\r", xAxis[0], xAxis[xAxis.Length - 1]);
				//textviewOutput.Buffer.Text += String.Format("X Axis starts with {0} and ends with {1}\r", xAxis[0], xAxis[xAxis.Length - 1]); 
			}
			/*
			System.Text.StringBuilder sb = new System.Text.StringBuilder("X Axis\r");

			for (int i = 0; i < pixelCount; i++)
			{
				sb.AppendFormat("X[{0}] = {1}\r", i, xAxis[i]);
				//textviewOutput.Buffer.Text += "X[" + i.ToString () + "] = " + xAxis [i].ToString () + "\r";
			}
			textviewOutput.Buffer.Text += sb.ToString();
			*/

			// Set integration time
			sb.AppendFormat("Setting integration time to {0}\r", integrationTime);
			//textviewOutput.Buffer.Text += "Setting integration time to " + integrationTime.ToString() + "\r";
			specComm.Write ("*PARA:TINT " + integrationTime.ToString () + "\r");
			specComm.Read (out response, 20, 200);

			// Set the scan delay to 0
			sb.Append("Setting scan delay to 0\r");
			//textviewOutput.Buffer.Text += "Setting scan delay to 0\r";
			specComm.Write ("*PARA:SDELAY 0\r");
			specComm.Read (out response, 20, 200);

			if (sb.Length > 0)
			{
				AddOutputText(sb.ToString());
			}
			/*
			// TESTING ONLY!!!
			{
				SplineInterpolateFile("/home/pi/Dev/RTA/TestSpectra/PQ-0111.txt");
			}
			*/
		}
	}


	protected void InitTEC ()
	{
		if (dictSerialPorts [TEC].portName != "")
		{
			tecComm = new SerialPortLib (dictSerialPorts [TEC].portName, dictSerialPorts [TEC].baudRate);
			if (!tecComm.Open ())
			{
				MessageBox.Show ("Failed to open TEC board port " + dictSerialPorts [TEC].portName);
				return;
			}
		}

		if (tecComm != null && tecComm.IsOpen ())
		{
			SetTecTemp();
		}
	}

	protected void InitDisplay ()
	{
		if (dictSerialPorts [DISPLAY].portName != "")
		{
			displayComm = new SerialPortLib (dictSerialPorts [DISPLAY].portName, dictSerialPorts [DISPLAY].baudRate);
			if (!displayComm.Open ())
			{
				MessageBox.Show ("Failed to open Display port " + dictSerialPorts [DISPLAY].portName);
				return;
			}
		}

		if (displayComm != null && displayComm.IsOpen ())
		{
			display = new MPFQA.Display(displayComm);
			display.Add("     RTA M-PFQA", true);
			display.Add("Initializing", true);
			/*
			// Clear the display
			displayComm.Write (new byte[]{0xFE, 0x58});
			displayComm.Write ("RTA M-PFQA\nReady");
			*/
			displayComm.GetReadHandler += new SerialPortLib.ReadHandler (DisplayKeypadHandler);
		}
	}

	protected void RefreshUI ()
	{
         // Flush pending events to keep the GUI reponsive
         while (Gtk.Application.EventsPending ())
              Gtk.Application.RunIteration ();
	}

	protected void UpdateSystemStatus (string status)
	{
		var contextId = statusbarSystemStatus.GetContextId ("System Status");
		statusbarSystemStatus.Push (contextId, status);
		RefreshUI ();
		if (display != null)
		{
			display.Replace (1, status);
		}
	}

	protected void OnButtonCollectBackgroundClicked (object sender, EventArgs e)
	{
		CollectBackground ();
	}

	protected void OnButtonScanClicked (object sender, EventArgs e)
	{
		CollectScan(false);
	}

	protected void DeterminePortConnections ()
	{
		/*
		 * There can be up to 3 serial devices connected:
		 * Spectrometer board
		 * TEC board
		 * Display
		 * 
		 * This code attempts to determine which device is
		 * on which port (/dev/ttyUSBn where n = 0, 1 or 2
		 * 
		 * The Spectrometer board defaults to 921600 baud, but
		 * we want to run it at 115200 baud.
		 * To change the baud rate to 115200, we use the following
		 * Open the port at 921600 baud
		 * Send *PARA:BAUD 115\r
		 * Send *PARA:SAVE\r
		 * Send *RST\r
		 * open up the port at 115200 baud
		 * 
		 * The TEC board runs at 115200 baud
		 * The display board runs at 19200 baud
		 * 
		 * To test a serial port for a device, we can use the following algorithm:
		 * Open the port at 115200 baud
		 * Send *para:tectemp?\r
		 * Read the response
		 * If there is a response, it can be one of the following:
		 * 1) a NACK which indicates the board is the Spectrometer board
		 * 2) the TEC temperature which indicates the board is the TEC board
		 * If there is no response, this indicates one of the following scenarios:
		 * 1) The device is the display, which will interpret the command as text to display and not
		 *    return anything, plus it runs at 19200 baud
		 * 2) The device is the Spectrometer board, running at 921600 baud
		 * 
		 * The C# code is very flaky at 921600 baud, so we have to use C++ or Java code to 
		 * test and reprogram the Spectrometer board for 115200 baud
		 */

		foreach (SerialPortInfo spi in dictSerialPorts.Values)
		{
			spi.portName = "";
		}

		string[] portNames = SerialPort.GetPortNames ();
		Dictionary<string, bool> dictPorts = new Dictionary<string, bool> (portNames.Length);

		foreach (string name in portNames)
		{
			SerialPortLib sc = new SerialPortLib (name, 115200);
			if (sc.Open ())
			{
				dictPorts [name] = false;
				//System.Threading.Thread.Sleep (100);
				for (int i = 0; (i < 5) && (dictPorts[name] == false); i++)
				{
					sc.Write ("*para:tectemp?\r");
					byte[] resp;
					if (sc.Read (out resp, 60, 300))
					{
						if (resp [0] == NACK)
						{
							dictSerialPorts[SPEC].portName = name;
							specPort = name;
							dictPorts [name] = true;
							Console.WriteLine ("Found spec board on port " + name);
						}
						else
						{
							try
							{
								string text = System.Text.ASCIIEncoding.ASCII.GetString (resp);
								Console.WriteLine ("Received " + text);
								string[] words = text.Split (new char[]{' '});
								for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
								{
									string wordUC = words[wordIndex].ToUpper();
									if (wordUC.Contains("TEC"))
									{
										if (wordIndex + 1 < words.Length)
										{
											wordUC = words[wordIndex + 1].ToUpper();
											if (wordUC.Contains("TEMPERATURE"))
											{
												dictSerialPorts[TEC].portName = name;
												tecPort = name;
												dictPorts [name] = true;
												Console.WriteLine ("Found tec board on port " + name);
												break;
											}
										}
									}
								}
							} 
							catch (System.FormatException)
							{
							}
						}
					} 
					else
					{
						Console.WriteLine ("No response");
						// No response.  Could be a spectrometer board running at 921600 baud
						// or the display, which wouldn't return anything
					}
				}
				sc.Close();

				if (dictPorts [name] == false)
				{
					// The port wasn't assigned to a Spectrometer or TEC board in the above code
					// This means the port could be attached to a pectrometer board running at 921600 baud
					// or the display, which wouldn't return anything
					// See if it is the spectrometer board running at 921600

					SerialPortLib port = new SerialPortLib (name, 921600);
					if (port.Open ())
					{
						port.Write ("*IDN?\r");
						string buf;
						if (port.Read (out buf, 50, 200))
						{
							Console.WriteLine ("SerialPortLib Read returned " + buf);
							if (buf.Contains ("JETI_PIC_VERSA"))
							{
								Console.WriteLine ("Found spec board on port " + name + " running at 921600 baud");
								dictSerialPorts[SPEC].portName = name;
								specPort = name;
								dictPorts [name] = true;
								// We now want to reprogram the spec board to run at 115200 baud
								port.Write ("*PARA:BAUD 115\r");
								port.Read (out buf, 50, 200);
								port.Write ("*PARA:SAVE\r");
								port.Read (out buf, 50, 500);
								port.Write ("*RST\r");
							}
						}
						port.Close ();
					}
				}
				/*
				string response;
				if (sc.ReadPort(out response, 40, 200))
				{
					if (response.Contains("JETI_PIC_VERSA"))
					{
						Console.WriteLine("Found spec board on port " + name);
						specPort = name;
					}
				}
				*/
			}
		}
		foreach (string name in dictPorts.Keys)
		{
			if (dictPorts [name] == false)
			{
				dictSerialPorts[DISPLAY].portName = name;
				displayPort = name;
				Console.WriteLine ("Assigning " + name + " to the display");
			}
		}

		System.Text.StringBuilder sb = new System.Text.StringBuilder ();

		foreach (string key in dictSerialPorts.Keys)
		{
			//Console.WriteLine("Next key in dictSerialPorts is " + key);
			SerialPortInfo spi = dictSerialPorts[key];
			if (spi.portName != "")
			{
				sb.AppendFormat ("{0} found on {1}\r", key, spi.portName);
			}
		}
		/*
		if (specPort != null)
		{
			sb.AppendFormat ("Spec. Board found on {0}\r", specPort);
		}
		if (tecPort != null)
		{
			sb.AppendFormat ("TEC Board found on {0}\r", tecPort);
		}
		if (displayPort != null)
		{
			sb.AppendFormat ("Display found on {0}\r", displayPort);
		}
		*/
		if (sb.Length > 0)
		{
			AddOutputText(sb.ToString());
			//textviewOutput.Buffer.Text += sb.ToString();
		}
	}

	protected void CalculateXAxis ()
	{
		xAxis = new double[pixelCount];
		for (int i = 0; i < pixelCount; i++)
		{
			double x = calibCoeffs[0];
			double xMult = i;
			for( int ii = 1; ii < calibCoeffs.Length; ii++)
			{
				x += (calibCoeffs[ii] * xMult);
				xMult *= (double)i;
			}
			xAxis[i] = x;
		}
	}

	protected void SetTecTemp ()
	{
		if (tecComm != null)
		{
			tecComm.Write ("*para:tecref " + tecSetTemp.ToString () + "\r");
			byte[] resp;
			tecComm.Read (out resp, 20, 200);
		}
	}

	protected void GetTecTemp ()
	{
		if (tecComm != null && tecComm.IsOpen() )
		{
			tecComm.Write ("*para:tectemp?\r");
			string resp;
			if (tecComm.Read (out resp, 40, 200))
			{
				string[] words = resp.Split (new char[]{' '});
				if (words.Length == 3)
				{
					tecTemp = Convert.ToDouble (words [2]);
					//textviewOutput.Buffer.Text += "Temp: " + tecTemp.ToString () + "\r";
				}
			}
		}
	}

	protected void TurnOnLamp ()
	{
		if (tecComm != null)
		{
			tecComm.Write ("*para:tecfan 0\r");
			byte[] resp;
			tecComm.Read (out resp, 20, 200);
		}
		/*
		specComm.WritePort ("*CONTR:LAMP 1\r");
		byte[] resp;
		specComm.ReadPort(out resp, 20, 200);
		*/
		System.Threading.Thread.Sleep (2000);
	}

	protected void TurnOffLamp ()
	{
		if (tecComm != null)
		{
			tecComm.Write ("*para:tecfan 1\r");
			byte[] resp;
			tecComm.Read (out resp, 20, 200);
		}
		/*
		specComm.WritePort ("*CONTR:LAMP 0\r");
		byte[] resp;
		specComm.ReadPort(out resp, 20, 200);
		*/
		System.Threading.Thread.Sleep (100);
	}

	protected bool ReadMeasurementFormat6 (out UInt16[] data)
	{
		//textviewOutput.Buffer.Text += "Taking measurement\r";
		data = null;
		specComm.Write("*MEASURE:DARK " + integrationTime.ToString() + " 1 6\r");
		byte[] resp = new byte[2];
		bool bInvalidCount = false;

		do
		{
			// Wait for command ACK (or NACK)
			if (!specComm.Read (out resp, 1, 1000))
			{
				AddOutputText("Measure: Failed to read ACK or NACK\r");
				//textviewOutput.Buffer.Text += "Measure: Failed to read ACK or NACK\r";
				break;
			}
			// Good response is an ACK (0x06)
			if (resp[0] != 0x06)
			{
				AddOutputText("Measure: Received NACK\r");
				//textviewOutput.Buffer.Text += "Measure: Received NACK\r";
				break;
			}
			// Wait for measurement done
			if (!specComm.Read (out resp, 1, integrationTime + 1000))
			{
				AddOutputText("Measure: Timeout waiting for BELL\r");
				//textviewOutput.Buffer.Text += "Measure: Timeout waiting for BELL\r";
				break;
			}
			// Good response is a BELL (0x07) indicating measurement is done
			if (resp[0] != 0x07)
			{
				break;
			}
			// Read the data length in next.  This has to be twice the pixel count 
			UInt16 count = 0;
			if (!specComm.Read (out resp, 2, 100))
			{
				AddOutputText("Measure: Failed to read data length\r");
				//textviewOutput.Buffer.Text += "Measure: Failed to read data length\r";
				break;
			}
			count = (UInt16)(resp[0] << 8);
			count |= (UInt16)resp[1];
			if (count != pixelCount * 2)
			{
				AddOutputText(string.Format("Measure: Invalid data length {0}\r", count));
				//textviewOutput.Buffer.Text += "Measure: Invalid data length " + count.ToString() + "\r";
				bInvalidCount = true;
			}

			resp = new byte[count];

			if (!specComm.Read(out resp, count, 200))
			{
				AddOutputText("Measure: Failed to read data\r");
				//textviewOutput.Buffer.Text += "Measure: Failed to read data\r";
				break;
			}
			if (resp.Length != count)
			{
				AddOutputText(string.Format("Measure: Failed to read {0} data bytes\r", count));
				//textviewOutput.Buffer.Text += "Measure: Failed to read " + count.ToString() + " data bytes\r";
			}
			UInt16 checksum = 0;
			if (bInvalidCount != true)
			{
				data = new UInt16[pixelCount];
				//System.Text.StringBuilder sb = new System.Text.StringBuilder("Data:\r");

				int nIndex = 0;
				for( int i = 0; i < pixelCount; i++)
				{
					byte b1 = resp[nIndex];
					byte b2 = resp[nIndex + 1];
					data[i] = (UInt16)(b1 << 8);
					data[i] |= (UInt16)b2;
					//sb.AppendFormat("{0}\r", data[i]);
					checksum += (UInt16)b1;
					checksum += (UInt16)b2;
					nIndex += 2;
				}
				//textviewOutput.Buffer.Text += sb.ToString();
			}
			// Read the checksum
			//System.Threading.Thread.Sleep(125);
			if (!specComm.Read(out resp, 2, 100))
			{
				AddOutputText("Measure: Failed to read checksum\r");
				//textviewOutput.Buffer.Text += "Measure: Failed to read checksum\r";
				data = null;
				break;
			}
			if (resp.Length != 2)
			{
				AddOutputText("Measure: Failed to read both checksum bytes\r");
				//textviewOutput.Buffer.Text += "Measure: Failed to read both checksum bytes\r";
				data = null;
				break;
			}
			UInt16 msgChecksum = (UInt16)(resp[0] << 8);
			msgChecksum |= (UInt16)resp[1];
			if (msgChecksum != checksum)
			{
				Console.WriteLine("Checksum mismatch! Msg checksum = " + msgChecksum.ToString() + " calc checksum = " + checksum.ToString());
				//textviewOutput.Buffer.Text += "Measure: Checksum mismatch\r";
				//textviewOutput.Buffer.Text += String.Format("Msg checksum = {0} calc checksum = {1}\r", msgChecksum, checksum);
				//data = null;
			}
		}
		while(1 == 0);

		return data != null;
	}

	protected bool GetCoaddedScans (out UInt16[] coaddedData)
	{
		bool bRet = false;
		coaddedData = null;
		double[] yVals = new double[pixelCount];
		UInt16[] data;

		// Throw out the first scan
		ReadMeasurementFormat6 (out data);

		for (int i = 0; i < coaddCount; i++)
		{
			AddOutputText(string.Format("Getting coadd {0} of {1}\r", i + 1, coaddCount));
			RefreshUI();

			bRet = ReadMeasurementFormat6 (out data);
			if (bRet)
			{
				for (int ii = 0; ii < pixelCount; ii++)
				{
					yVals[ii] += data[ii];
				}
			}
			else
			{
				break;
			}
		}
		// Finish the averaging
		if (bRet)
		{
			coaddedData = new ushort[pixelCount];
			for (int i = 0; i < pixelCount; i++)
			{
				yVals[i] /= coaddCount;
				//Console.WriteLine("yVals[" + i.ToString() + "] = " + yVals[i].ToString());
				coaddedData[i] = Convert.ToUInt16(yVals[i]);
			}
		}
		return bRet;
	}

	protected bool SplineInterpolate (double[] x, double[] y, double firstX, double lastX, double spacing, out double[] newX, out double[] newY)
	{
		int siz = Convert.ToInt32 (((lastX - firstX) / spacing) + 1);
		newX = new double[siz];
		newY = new double[siz];

		double curX = firstX;
		for (int i = 0; i < siz; i++)
		{
			newX[ i ] = curX;
			curX += spacing;
		}

		bool bRet = false;
		int handle = DLSAlg_dCreateSpline (x, y, x.Length);
		if (handle > 0)
		{
			double firstInBoundsX = 0, lastInBoundsX = 0;
			int nRet = DLSAlg_dSplineArrayEvenlySpaced (handle, firstX, lastX, siz, 0, newY, ref firstInBoundsX, ref lastInBoundsX);
			if (nRet != 0)
			{
				Console.WriteLine("SplineEvenlySpaced firstInBoundsX = " + firstInBoundsX.ToString() + " lastInBoundsX = " + lastInBoundsX.ToString());
				bRet = true;
			}
			DLSAlg_DestroySpline(handle);
		}
		return bRet;
	}

	protected bool GetDarkScan ()
	{
		bool bRet = false;

		TurnOffLamp ();

		AddOutputText("Getting Dark Scan\r");
		RefreshUI();

		UInt16[] data;

		if (GetCoaddedScans (out data))
		{
			bRet = true;
			darkScan = new Scan(xAxis, data);
		}
		return bRet;
	}

	protected void RemoveDark (ref Scan scan)
	{
		for( int i = 0; i < scan.yVals.Length; i++)
		{
			if (scan.yVals[i] > darkScan.yVals[i])
			{
				scan.yVals[i] -= darkScan.yVals[i];
			}
			else
			{
				scan.yVals[i] = 0;
			}
		}
	}

	protected bool GetLightScan ()
	{
		bool bRet = false;
		TurnOnLamp ();

		AddOutputText("Getting Light Scan\r");
		RefreshUI();

		UInt16[] data;
		if (GetCoaddedScans (out data))
		{
			bRet = true;
			lightScan = new Scan(xAxis, data);
			RemoveDark(ref lightScan);
		}
		TurnOffLamp();
		return bRet;
	}

	protected void InsertKeyCodeHandler (DISPLAY_KEYPAD_CODES kc)
	{
		if (kc == DISPLAY_KEYPAD_CODES.ENTER)
		{
			Gtk.Application.Invoke (delegate {
				MessageBox.Destroy();
			}
			);
		}
	}

	protected bool GetNormalScans ()
	{
		int nResp = MPFQA.MessageBoxEx.Show ("Insert sample vial\nPress Ok to proceed", display, new List<string> (new string[] {
			"Insert sample vial",
			"Hit Enter to proceed"
		}));

		display.Clear();
		display.Add("     RTA M-PFQA", false);

		if (nResp != (int)Gtk.ResponseType.Ok)
		{
			UpdateSystemStatus("Ready");
			return false;
		}

		bool bRet = false;

		UpdateSystemStatus("Collecting sample");

		/*
         // Flush pending events to keep the GUI reponsive
         while (Gtk.Application.EventsPending ())
              Gtk.Application.RunIteration ();

		if (displayComm != null && displayComm.IsOpen ())
		{
			displayComm.Write ("\rCollect sample     ");
		}
		*/

		TurnOnLamp ();

		AddOutputText("Getting Normal Scan\r");
		RefreshUI();

		UInt16[] data;
		if (GetCoaddedScans (out data))
		{
			bRet = true;
			sampleScan = new Scan(xAxis, data);
			RemoveDark(ref sampleScan);
		}
		TurnOffLamp();

		UpdateSystemStatus("Ready");

		return bRet;
	}

	protected void ComputeAbsorbance ()
	{
		double[] yVals = new double[pixelCount];
		for (int i = 0; i < pixelCount; i++)
		{
			if (sampleScan.yVals[i] != 0)
			{
				yVals[i] = Math.Log(lightScan.yVals[i] / sampleScan.yVals[i]);
			}
			else
			{
				yVals[i] = 0;
			}
		}
		absorbanceSpectrum = new Scan(xAxis, yVals);
	}

	protected bool CollectBackground ()
	{
		int nResp = MPFQA.MessageBoxEx.Show ("Insert blank vial\nPress Ok to proceed", display, new List<string> (new string[] {
			"Insert blank vial",
			"Hit Enter to proceed"
		}));

		display.Clear();
		display.Add("     RTA M-PFQA", false);

		if (nResp != (int)Gtk.ResponseType.Ok)
		{
			UpdateSystemStatus("Ready");
			return false;
		}

		bool bRet = false;

		UpdateSystemStatus("Collecting backgnd");

		/*
         // Flush pending events to keep the GUI reponsive
         while (Gtk.Application.EventsPending ())
              Gtk.Application.RunIteration ();

		if (displayComm != null && displayComm.IsOpen())
		{
			displayComm.Write("\rCollect background ");
		}
		*/

		AddOutputText("Getting Background\r");
		RefreshUI();

		bRet = GetDarkScan ();
		if (bRet)
		{
			bRet = GetLightScan ();
		}
		if (bRet)
		{
			string text = string.Format("Dark Scan:\r{0}\rLight Scan:\r{1}\r", darkScan.ToString("\r"), lightScan.ToString("\r"));
			AddOutputText(text);
			/*
			string scanStr = darkScan.ToString("\r");
			textviewOutput.Buffer.Text += "Dark Scan:\r" + scanStr + "\r";

			scanStr = lightScan.ToString("\r");
			textviewOutput.Buffer.Text += "Light Scan:\r" + scanStr + "\r";
			*/
		}

		UpdateSystemStatus("Ready");

		return bRet;
	}

	protected bool CollectScan (bool collectBkgnd)
	{
		bool bRet = true;
		if (collectBkgnd || (darkScan == null) || (lightScan == null))
		{
			bRet = CollectBackground();
		}
		if (bRet)
		{
			bRet = GetNormalScans ();
		}
		if (bRet)
		{
			UpdateSystemStatus("Analyzing data");

			AddOutputText(string.Format("Sample Scan:\r{0}\r", sampleScan.ToString("\r")));
			/*
			string scanStr = sampleScan.ToString("\r");
			textviewOutput.Buffer.Text += "Sample Scan:\r" + scanStr + "\r";
			*/

			AddOutputText("Computing absorbance\r");
			RefreshUI();

			ComputeAbsorbance ();

			AddOutputText("Performing spline interpolation\r");
			RefreshUI();

			double[] newX, newY;
			bRet = SplineInterpolate (xAxis, absorbanceSpectrum.yVals, (double)Math.Ceiling(xAxis[0]), Math.Floor(xAxis[xAxis.Length - 1]), 1.0, out newX, out newY);

			if (bRet)
			{
				AddOutputText("Clipping spectra\r");
				RefreshUI();

				xAxisInterp = new double[newX.Length];
				newX.CopyTo(xAxisInterp, 0);
				absorbanceSpectrumSplined = new Scan(xAxisInterp, newY);
				// Now clip the spectra
				int siz = (int)(clippingEnd - clippingStart) + 1;	// this assumes spacing is 1
				double[] x = new double[siz];
				double[] y = new double[siz];
				double[,] yMatrix = new double[1, siz];
				int nIndex = 0;
				for (int i = 0; i < xAxisInterp.Length; i++)
				{
					if (xAxisInterp[i] >= clippingStart && xAxisInterp[i] <= clippingEnd)
					{
						x[nIndex] = xAxisInterp[i];
						y[nIndex] = newY[i];
						yMatrix[0,nIndex] = newY[i];
						nIndex++;
					}
				}
				absorbanceSpectrumClipped = new Scan(x, y);
				System.Text.StringBuilder sb =  new System.Text.StringBuilder("Absorbance spectra after spline and clip\r");
				//AddOutputText("Absorbance spectra after spline and clip\r");
				for( int i = 0; i < x.Length; i++)
				{
					sb.AppendFormat("X: {0} Y: {1}\r", x[i], y[i]);
					//textviewOutput.Buffer.Text += "X: " + x[i].ToString() + " Y: " + y[i].ToString() + "\r";
				}
				AddOutputText(sb.ToString());

				RunEigenvectorModel("./01_Density.xml", yMatrix);
				RunEigenvectorModel("./02_FlashPoint.xml", yMatrix);
				RunEigenvectorModel("./03_FreezePoint.xml", yMatrix);
			}
		}
		UpdateSystemStatus("Ready");
		return bRet;
	}

	protected void RunEigenvectorModel (string file, double[,] yMatrix)
	{
		try
		{
			EigenvectorInterpreter.ModelInterpreter evim = new EigenvectorInterpreter.ModelInterpreter(file);
			evim.inputdata = new MatrixLibrary.Matrix(yMatrix);
			evim.apply();
			EigenvectorInterpreter.Workspace ws = evim.results;
			List<string> varList = ws.varList;
			/*
			System.Text.StringBuilder sb = new System.Text.StringBuilder("EigenvectorInterpreter Workspace variables\r");
			foreach (string s in varList)
			{
				sb.AppendFormat("{0}\r", s);
			}
			AddOutputText(sb.ToString());
			*/
			string property = System.IO.Path.GetFileNameWithoutExtension(file);
			string[] words = property.Split(new char[]{'_'});
			if (words.Length > 1)
			{
				property = words[words.Length - 1];
			}
			if (varList.Contains("yhat"))
			{
				MatrixLibrary.Matrix m = ws.getVar("yhat");
				AddOutputText(string.Format("{0} yhat = {1}\r", property, m.ToString()));
			}
			if( varList.Contains("T2"))
			{
				MatrixLibrary.Matrix m = ws.getVar("T2");
				AddOutputText(string.Format("{0} T2 = {1}\r", property, m.ToString()));
			}
			if (varList.Contains("Q"))
			{
				MatrixLibrary.Matrix m = ws.getVar("Q");
				AddOutputText(string.Format("{0} Q = {1}\r", property, m.ToString()));
			}
		}
		catch(EigenvectorInterpreter.EigenvectorInterpreterExceptions e)
		{
			AddOutputText("EigenvectorInterpreter exception: " + e.Message + "\r");
		}
	}

	protected void SplineInterpolateFile (string file)
	{
		try
		{
			string[] lines = System.IO.File.ReadAllLines (file);
			if (lines.Length > 0)
			{
				double[] x = new double[lines.Length];
				double[] y = new double[lines.Length];
				for (int i = 0; i < lines.Length; i++)
				{
					string[] words = lines [i].Split (new char[]{','});
					if (words.Length == 2)
					{
						x [i] = Convert.ToDouble (words [0]);
						y [i] = Convert.ToDouble (words [1]);
					}
				}
				double[] newX, newY;
				if (SplineInterpolate (x, y, (double)Math.Ceiling(x[0]), Math.Floor(x[x.Length - 1]), 1.0, out newX, out newY))
				{
					textviewOutput.Buffer.Text += "Spline Interpolate successful!\r";
					string fname = System.IO.Path.GetDirectoryName(file) + System.IO.Path.DirectorySeparatorChar.ToString() +
						System.IO.Path.GetFileNameWithoutExtension(file) + "_CubicSplineInter.csv";
					lines = new string[newX.Length];
					for (int i = 0; i < newX.Length; i++)
					{
						lines[i] = newX[i].ToString() + "," + String.Format("{0:0.000000}", newY[i]) + "\r";
						//textviewOutput.Buffer.Text += " X: " + newX [i].ToString () + " Y: " + newY [i].ToString () + "\r";
					}

					System.IO.File.WriteAllLines(fname, lines);
				}
			}
		} 
		catch (System.IO.FileNotFoundException ex)
		{
			MessageBox.Show("Could not find " + ex.FileName);
		}
	}


    [DllImport("./libDLSAlg.so")]
//    [DllImport("/home/pi/Dev/Agilent/DLS_DLL_for_Agilent/Trunk/3rd Party/DLS_DLL/Release/Linux/libDLSAlg.so")]
	extern public static int DLSAlg_dCreateSpline(double[] xx, double[] yy, int siz);

    [DllImport("./libDLSAlg.so")]
//    [DllImport("/home/pi/Dev/Agilent/DLS_DLL_for_Agilent/Trunk/3rd Party/DLS_DLL/Release/Linux/libDLSAlg.so")]
	extern public static int DLSAlg_dCreateSplineEvenlySpaced(double firstX, double lastX, double[] yy, int siz);

    [DllImport("./libDLSAlg.so")]
//    [DllImport("/home/pi/Dev/Agilent/DLS_DLL_for_Agilent/Trunk/3rd Party/DLS_DLL/Release/Linux/libDLSAlg.so")]
	extern public static int DLSAlg_dSplineArrayEvenlySpaced(int spHandle, double firstX, double lastX, int siz, double outofBoundsFillValue, double[] yAtX, ref double firstInBoundsX, ref double lastInBoundsX);

    [DllImport("./libDLSAlg.so")]
//    [DllImport("/home/pi/Dev/Agilent/DLS_DLL_for_Agilent/Trunk/3rd Party/DLS_DLL/Release/Linux/libDLSAlg.so")]
	extern public static int DLSAlg_DestroySpline(int spHandle);


}
