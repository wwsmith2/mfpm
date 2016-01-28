using System;

namespace MPFQA
{
	public partial class SettingsDialog : Gtk.Dialog
	{
		private int integrationTime = 200;
		private int coaddCount = 8;
		private int tecSetTemp = 12;

		public int IntegrationTime {
			get {
				return integrationTime;
			}
			set {
				integrationTime = value;
			}
		}

		public int CoaddCount {
			get {
				return coaddCount;
			}
			set {
				coaddCount = value;
			}
		}

		public int TecSetTemp {
			get {
				return tecSetTemp;
			}
			set {
				tecSetTemp = value;
			}
		}

		public SettingsDialog ()
		{
			this.Build ();
			//this.Resize(300, this.DefaultHeight);
			//this.DefaultWidth = 100;
			entryIntegrationTime.Text = Convert.ToString(integrationTime);
			entryCoaddCount.Text = Convert.ToString(coaddCount);
			entryTECSetTemp.Text = Convert.ToString(tecSetTemp);
		}

		protected void OnButtonOkClicked (object sender, EventArgs e)
		{
			integrationTime = Convert.ToInt32(entryIntegrationTime.Text);
			coaddCount = Convert.ToInt32(entryCoaddCount.Text);
			tecSetTemp = Convert.ToInt32(entryTECSetTemp.Text);
			this.Respond(Gtk.ResponseType.Ok);
			//MessageBox.Show("OK button Clicked");
		}

		protected void OnButtonCancelClicked (object sender, EventArgs e)
		{
			//MessageBox.Show("Cancel button Clicked");
		}


	}
}

