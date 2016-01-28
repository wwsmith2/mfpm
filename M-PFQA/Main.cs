using System;
using Gtk;

namespace MPFQA
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init ();
			foreach (string s in args)
			{
				Console.WriteLine ("Cmd line arg: " + s);
			}
			MainWindow win = new MainWindow ();
			win.Show ();
			Application.Run ();
		}
	}
}
