using System;
using System.Collections.Generic;
using Gtk;

namespace MPFQA
{
	public partial class GetFuelTypeDialog : Gtk.Dialog
	{
		private List<string> listFuelTypes;
		private int selectedIndex = -1;

		public List<string> FuelTypes
		{
			set
			{
				listFuelTypes = new List<string> (value);
			}
		}

		public int SelectedIndex
		{
			get 
			{
				return selectedIndex;
			}
		}

		public GetFuelTypeDialog (List<string> fuelTypes)
		{
			this.Build ();

			listFuelTypes = new List<string> (fuelTypes);

			Gtk.TreeViewColumn tvc = new TreeViewColumn ();
			tvc.Title = "Fuel Type";

			CellRendererText crt = new CellRendererText ();
			tvc.PackStart (crt, true);

			treeviewFuelTypes.AppendColumn (tvc);
			tvc.AddAttribute (crt, "text", 0);

			Gtk.ListStore ls = new Gtk.ListStore (new System.Type[]{typeof(string)});
			treeviewFuelTypes.Model = ls;

			for (int i = 0; i < listFuelTypes.Count; i++)
			{
				ls.AppendValues (listFuelTypes [i]);
			}
			//ls.AppendValues (listFuelTypes.ToArray());

			treeviewFuelTypes.Selection.Changed += new EventHandler(OnTreeviewFuelTypesSelectionChanged);

			ShowAll();

			buttonOk.Sensitive = false;
		}

		protected void OnShown (object obj, EventArgs e)
		{
			base.OnShown ();
		}

		protected void OnButtonOkClicked (object sender, EventArgs e)
		{
			TreeIter iter;
			if (treeviewFuelTypes.Selection.GetSelected (out iter))
			{
				TreePath tp = treeviewFuelTypes.Model.GetPath (iter);
				int[] indices = tp.Indices;
				selectedIndex = indices[0];
				this.Respond(Gtk.ResponseType.Ok);
			}
		}

		protected void OnTreeviewFuelTypesSelectionChanged (object o, EventArgs args)
		{
			buttonOk.Sensitive = true;
		}

		protected void OnTreeviewFuelTypesSelectionGet (object o, SelectionGetArgs args)
		{
		}


	}
}

