
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;


namespace voice_recognition.cs
{
	public partial class MainForm : Form
	{

		string ipString = "127.0.0.1";
		int port = 50377;

		public const string UnityExecName = "Robotis_vsido_connect";
		string UnityExecNameFullPath = null;
		Process UnityProcess = null;
		string stCurrentDir = null;

		string DEBUG_STR = null;


		System.IO.MemoryStream ms = null;
		System.Net.Sockets.NetworkStream ns = null;
		System.Net.Sockets.TcpListener listener = null;
		System.Net.Sockets.TcpClient client = null;
		System.Text.Encoding enc = null;

		Thread ServerThread = null;
		Thread ServerWaitingThread = null;
		string resMsg = "ready";
		string HostIP_Port = null;
		string ClientIP_Port = null;
		string ClientSendMsg = null;
		string[] FromClientMessage = null;

		string LineTest;

		int ClientCommandNo = 0;

		private PXCMSession session;
		private Dictionary<ToolStripMenuItem, Int32> modules = new Dictionary<ToolStripMenuItem, int>();
		private Dictionary<ToolStripMenuItem, PXCMAudioSource.DeviceInfo> devices = new Dictionary<ToolStripMenuItem, PXCMAudioSource.DeviceInfo>();

		public string g_file; //SM: ToDo function for return the file
		public string v_file; //SM: ToDo function for return the file
		string[] OrderWords = {"走れ", "こんにちは", "伸びろ", "ぐるぐる","グルグル","のびろ" };

		public MainForm(PXCMSession session)
		{
			InitializeComponent();

			this.session = session;
			PopulateSource();
			PopulateModule();
			PopulateLanguage();
			dictationToolStripMenuItem_Click(null, null);

			Console2.AfterLabelEdit += new NodeLabelEditEventHandler(Console2_AfterLabelEdit);
			Console2.KeyDown += new KeyEventHandler(Console2_KeyDown);
			FormClosing += new FormClosingEventHandler(MainForm_FormClosing);

			label1.Text = null;
			label3.Text = "0000";
			stCurrentDir = System.Environment.CurrentDirectory;

			UnityExecNameFullPath = stCurrentDir + @"\Unity\" + UnityExecName;
			label6.Text = null;

		}

		public delegate void myLabel(string Mes);
		public void PutLabel1Text(string Mes)
		{
			if (this.label1.InvokeRequired)
			{
				myLabel d = new myLabel(PutLabel1Text);
				this.Invoke(d, new object[] { Mes });
			}
			else
			{
				this.label1.Text = Mes;
			}
		}

		private void PopulateSource()
		{
			ToolStripMenuItem sm = new ToolStripMenuItem("マイク");
			devices.Clear();

			PXCMAudioSource source = session.CreateAudioSource();
			if (source != null)
			{
				source.ScanDevices();

				for (int i = 0; ; i++)
				{
					PXCMAudioSource.DeviceInfo dinfo;
					if (source.QueryDeviceInfo(i, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

					ToolStripMenuItem sm1 = new ToolStripMenuItem(dinfo.name, null, new EventHandler(Source_Item_Click));
					devices[sm1] = dinfo;
					sm.DropDownItems.Add(sm1);
				}

				source.Dispose();
			}

			if (sm.DropDownItems.Count > 0)
				(sm.DropDownItems[0] as ToolStripMenuItem).Checked = true;
			MainMenu.Items.RemoveAt(0);
			MainMenu.Items.Insert(0, sm);
		}

		private void PopulateModule()
		{
			ToolStripMenuItem mm = new ToolStripMenuItem("モジュール");
			modules.Clear();

			PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
			desc.cuids[0] = PXCMSpeechRecognition.CUID;
			for (int i = 0; ; i++)
			{
				PXCMSession.ImplDesc desc1;
				if (session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
				ToolStripMenuItem mm1 = new ToolStripMenuItem(desc1.friendlyName, null, new EventHandler(Module_Item_Click));
				modules[mm1] = desc1.iuid;
				mm.DropDownItems.Add(mm1);
			}

			if (mm.DropDownItems.Count > 0)
				(mm.DropDownItems[0] as ToolStripMenuItem).Checked = true;
			MainMenu.Items.RemoveAt(1);
			MainMenu.Items.Insert(1, mm);
		}

		private void PopulateLanguage()
		{
			PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
			desc.cuids[0] = PXCMSpeechRecognition.CUID;
			desc.iuid = GetCheckedModule();
			int k = 0;

			PXCMSpeechRecognition vrec;
			if (session.CreateImpl<PXCMSpeechRecognition>(desc, out vrec) < pxcmStatus.PXCM_STATUS_NO_ERROR) return;

			ToolStripMenuItem lm = new ToolStripMenuItem("言語");
			for (int i = 0; ; i++)
			{
				PXCMSpeechRecognition.ProfileInfo pinfo;
				if (vrec.QueryProfile(i, out pinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
				ToolStripMenuItem lm1 = new ToolStripMenuItem(LanguageToString(pinfo.language), null, new EventHandler(Language_Item_Click));
				lm.DropDownItems.Add(lm1);

				if (pinfo.language == PXCMSpeechRecognition.LanguageType.LANGUAGE_JP_JAPANESE)
				{
					k = i;
				}
			}
			vrec.Dispose();

			if (lm.DropDownItems.Count > 0)
				(lm.DropDownItems[k] as ToolStripMenuItem).Checked = true;
			MainMenu.Items.RemoveAt(2);
			MainMenu.Items.Insert(2, lm);
		}

		public PXCMAudioSource.DeviceInfo GetCheckedSource()
		{
			foreach (ToolStripMenuItem m in MainMenu.Items)
			{
				if (!m.Text.Equals("Source")) continue;
				foreach (ToolStripMenuItem e in m.DropDownItems)
				{
					if (e.Checked) return devices[e];
				}
			}
			return null;
		}

		public Int32 GetCheckedModule()
		{
			foreach (ToolStripMenuItem m in MainMenu.Items)
			{
				if (!m.Text.Equals("Module")) continue;
				foreach (ToolStripMenuItem e in m.DropDownItems)
				{
					if (e.Checked) return modules[e];
				}
			}
			return 0;
		}

		public int GetCheckedLanguage()
		{
			foreach (ToolStripMenuItem m in MainMenu.Items)
			{
				if (!m.Text.Equals("言語")) continue;
				for (int i = 0; i < m.DropDownItems.Count; i++)
				{
					if (m.DropDownItems[i] == null)
						continue;
					if ((m.DropDownItems[i] as ToolStripMenuItem).Checked)
						return i;
				}
			}
			return 0;
		}

		public bool IsCommandControl()
		{
			return commandControlToolStripMenuItem.Checked;
		}

		private void RadioCheck(object sender, string name)
		{
			foreach (ToolStripMenuItem m in MainMenu.Items)
			{
				if (!m.Text.Equals(name)) continue;
				foreach (ToolStripMenuItem e1 in m.DropDownItems)
				{
					e1.Checked = (sender == e1);
				}
			}
		}

		private void Source_Item_Click(object sender, EventArgs e)
		{
			RadioCheck(sender, "Source");
		}

		private void Module_Item_Click(object sender, EventArgs e)
		{
			RadioCheck(sender, "Module");
			PopulateLanguage();
		}

		private void Language_Item_Click(object sender, EventArgs e)
		{
			RadioCheck(sender, "Language");
		}

		private void commandControlToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Status2.Nodes.Clear();
//			ConsoleMode.Text = "Command Control:";
			commandControlToolStripMenuItem.Checked = true;
			dictationToolStripMenuItem.Checked = false;
			Console2.Nodes.Clear();
			Console2.LabelEdit = true;
			setGrammarFromFileToolStripMenuItem.Enabled = true;
			addVocabularyFromFileToolStripMenuItem.Enabled = false;
			AlwaysAddNewCommand();
		}

		private void dictationToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Status2.Nodes.Clear();
//			ConsoleMode.Text = "Dictation:";
			commandControlToolStripMenuItem.Checked = false;
			dictationToolStripMenuItem.Checked = true;
			Console2.LabelEdit = false;
			setGrammarFromFileToolStripMenuItem.Enabled = false;
			addVocabularyFromFileToolStripMenuItem.Enabled = true;
			Console2.Nodes.Clear();
		}

		private void Start_Click(object sender, EventArgs e)
		{
//			Start.Enabled = false;
			Stop.Enabled = true;
			MainMenu.Enabled = false;

			stop = false;
			System.Threading.Thread thread = new System.Threading.Thread(DoVoiceRecognition);
			thread.Start();
			PutLabel1Text("初期化中...");

//			System.Threading.Thread.Sleep(5);
		}

		private delegate void VoiceRecognitionCompleted();
		private void DoVoiceRecognition()
		{
			VoiceRecognition vr = new VoiceRecognition();
			vr.DoIt(this, session);

			this.Invoke(new VoiceRecognitionCompleted(
				delegate
				{
					//Start.Enabled = true;
					Stop.Enabled = false;
					MainMenu.Enabled = true;
					if (closing) Close();
				}
			));
		}

		private void Stop_Click(object sender, EventArgs e)
		{
			stop = true;
			this.Close();
		}

		private void Console2_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (e.Label == null) return;
			if (e.Label.Length == 0) return;
			e.Node.EndEdit(false);
			if (e.Node.Text.Equals("[Enter New Command]"))
				Console2.Nodes.Add("[Enter New Command]");
		}

		public static string TrimScore(string s)
		{
			s = s.Trim();
			int x = s.IndexOf('[');
			if (x < 0) return s;
			return s.Substring(0, x);
		}

		private string LanguageToString(PXCMSpeechRecognition.LanguageType language)
		{
			switch (language)
			{
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_US_ENGLISH: return "US English";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_GB_ENGLISH: return "British English";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_DE_GERMAN: return "Deutsch";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_IT_ITALIAN: return "Italiano";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_BR_PORTUGUESE: return "BR Português";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_CN_CHINESE: return "中文";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_FR_FRENCH: return "Français";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_JP_JAPANESE: return "日本語";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_US_SPANISH: return "US Español";
				case PXCMSpeechRecognition.LanguageType.LANGUAGE_LA_SPANISH: return "LA Español";
			}
			return null;
		}

		private delegate void TreeViewCleanDelegate();

		public void CleanConsole()
		{
			Console2.Invoke(new TreeViewCleanDelegate(delegate { Console2.Nodes.Clear(); }));
		}

		private delegate void TreeViewUpdateDelegate(string line);
		public void PrintConsole(string line)
		{
			///////////////////////////////////////////
			LineTest = line;
//			Invoke(new PrintMessage(PrintRecognized));
			
			Console2.Invoke(new TreeViewUpdateDelegate(delegate(string line1) { Console2.Nodes.Add(line1).EnsureVisible(); }), new object[] { line });

			if(checkBox1.Checked)
			{
				PerformClickButton1();
			}
		}

		delegate void PrintMessage();
		void PrintRecognized()
		{
			textBox1.Text = LineTest;
		}

		public void PrintStatus(string line)
		{
			Status2.Invoke(new TreeViewUpdateDelegate(delegate(string line1) { Status2.Nodes.Add(line1).EnsureVisible(); }), new object[] { line });
		}

		private delegate void ConsoleReplaceTextDelegate(TreeNode tn1, string text);

		public void ClearScores()
		{
			foreach (TreeNode n in Console2.Nodes)
			{
				string s = TrimScore(n.Text);
				if (s.Length > 0)
					Console2.Invoke(new ConsoleReplaceTextDelegate(delegate(TreeNode tn1, string text) { tn1.Text = text; }), new object[] { n, s });
			}
		}

		public void SetScore(int label, int confidence)
		{
			for (int i = 0; i < Console2.Nodes.Count; i++)
			{
				string s = TrimScore(Console2.Nodes[i].Text);
				if (s.Length == 0) continue;
				if ((label--) != 0) continue;
				Console2.Invoke(new ConsoleReplaceTextDelegate(delegate(TreeNode tn1, string text) { tn1.Text = text; }), new object[] { Console2.Nodes[i], Console2.Nodes[i].Text + " [" + confidence + "%]" });
				break;
			}
		}

		public string[] GetCommands()
		{
			int ncmds = 0;
			foreach (TreeNode tn in Console2.Nodes)
				if (TrimScore(tn.Text).Length > 0) ncmds++;
			if (ncmds == 0) return null;
			string[] cmds = new string[ncmds];
			for (int i = 0, k = 0; i < Console2.Nodes.Count; i++)
			{
				string cmd = TrimScore(Console2.Nodes[i].Text);
				if (cmd.Length <= 0) continue;
				cmds[k++] = cmd;
			}
			return cmds;
		}

		public void FillCommandListConsole(string filename)
		{
			string line;

			CleanConsole();
			PrintConsole("[Enter New Command]");

			System.IO.StreamReader file = new System.IO.StreamReader(filename);
			try
			{
				while ((line = file.ReadLine()) != null)
				{

					LineTest = line;
					textBox1.Text = LineTest;

					PrintConsole(line);
				}
				file.Close();
			}
			catch
			{
				file.Close();
			}

		}

		public string AlertToString(PXCMSpeechRecognition.AlertType label)
		{
			switch (label)
			{
				case PXCMSpeechRecognition.AlertType.ALERT_SNR_LOW: return "SNR_LOW";
				case PXCMSpeechRecognition.AlertType.ALERT_SPEECH_UNRECOGNIZABLE: return "SPEECH_UNRECOGNIZABLE";
				case PXCMSpeechRecognition.AlertType.ALERT_VOLUME_HIGH: return "VOLUME_HIGH";
				case PXCMSpeechRecognition.AlertType.ALERT_VOLUME_LOW: return "VOLUME_LOW";
				case PXCMSpeechRecognition.AlertType.ALERT_SPEECH_BEGIN: return "SPEECH_BEGIN";
				case PXCMSpeechRecognition.AlertType.ALERT_SPEECH_END: return "SPEECH_END";
				case PXCMSpeechRecognition.AlertType.ALERT_RECOGNITION_ABORTED: return "REC_ABORT";
				case PXCMSpeechRecognition.AlertType.ALERT_RECOGNITION_END: return "REC_END";
			}
			return "Unknown";
		}

		private volatile bool stop = true;
		private bool closing = false;

		public bool IsStop()
		{
			return stop;
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (ServerThread != null && ServerThread.IsAlive)
			{
				ns.Close();
				client.Close();
				listener.Stop();
				ServerThread.Abort();
				UnityProcess.Kill();
				UnityProcess.Close();
				UnityProcess.Dispose();
			}

			stop = true;
			e.Cancel = Stop.Enabled;
			closing = true;

		}

		private void AlwaysAddNewCommand()
		{
			foreach (TreeNode tn in Console2.Nodes)
				if (tn.Text.Equals("[Enter New Command]")) return;
			Console2.Nodes.Add("[Enter New Command]").EnsureVisible();
		}

		private void Console2_KeyDown(Object sender, KeyEventArgs e)
		{
			if (!IsCommandControl()) return;
			if (e.KeyCode != Keys.Delete) return;
			if (Console2.SelectedNode != null)
				Console2.Nodes.Remove(Console2.SelectedNode);
			AlwaysAddNewCommand();
		}

		private void setGrammarFromFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GrammarFileDialog.Filter = "jsgf files (*.jsgf)|*.jsgf|list files (*.list)|*.list|All files (*.*)|*.*";
			GrammarFileDialog.FilterIndex = 1;
			GrammarFileDialog.RestoreDirectory = true;

			if (GrammarFileDialog.ShowDialog() == DialogResult.OK)
			{
				try
				{
					g_file = GrammarFileDialog.FileName;
					setGrammarFromFileToolStripMenuItem.Checked = true;
					Console2.Nodes.Clear();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
				}
			}
			else
			{
				setGrammarFromFileToolStripMenuItem.Checked = false;
				g_file = null;

				Console2.Nodes.Clear();
				AlwaysAddNewCommand();

			}

		}

		private void addVocabularyFromFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			VocabFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
			VocabFileDialog.FilterIndex = 1;
			VocabFileDialog.RestoreDirectory = true;

			if (VocabFileDialog.ShowDialog() == DialogResult.OK)
			{
				try
				{
					v_file = VocabFileDialog.FileName;
					addVocabularyFromFileToolStripMenuItem.Checked = true;
					Console2.Nodes.Clear();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
				}
			}
			else
			{
				addVocabularyFromFileToolStripMenuItem.Checked = false;
				v_file = null;
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			textBox1.Text = LineTest;
			AnalyseWords(textBox1.Text);
            timer1.Enabled = true;
		}


		private void AnalyseWords(string Order)
		{
			string Cmd;
			string Mes;

			if (Order == OrderWords[0])
			{
				ClientCommandNo = 1;
			}
			else if (Order == OrderWords[1])
			{
				ClientCommandNo = 2;
			}
			else if (Order == OrderWords[2])
			{
				ClientCommandNo = 3;
			}
			else if (Order == OrderWords[3])
			{
				ClientCommandNo = 4;
			}
            else if (Order == OrderWords[4]) {
                ClientCommandNo = 4;
            }
            else if (Order == OrderWords[5])
            {
                ClientCommandNo = 3;
            }
            else 
			{
				ClientCommandNo = 0;
			}


			if (ClientCommandNo == 0)
			{
				Cmd = "0000";
				Mes = null;
			}
			else if (ClientCommandNo == 1)
			{
				Cmd = "0001";
				Mes = "うつよっ  ばーーん";
			}
			else if (ClientCommandNo == 2)
			{
				Cmd = "0002";
				Mes = "やっほーー";
			}
			else if (ClientCommandNo == 3)
			{
				Cmd = "0003";
				Mes = "えっ   なんだろー";
			}
			else if (ClientCommandNo == 4)
			{
				Cmd = "0004";
				Mes = "ぐるぐる";
			}
			else
			{
				Cmd = "0000";
				Mes = null;
			}

			ClientSendMsg = Cmd + ";" + Mes;

			///////////////////////////////////////////////
			//			if (ClientCommandNo != PreviousClientCommandNo && resMsg != "busy")
			if (resMsg != "busy")
			{
				label3.Text = Cmd;
				toClientSend();
				//pictureBox1.BackColor = Color.White;
			}
			else
			{
				label3.Text = Cmd;
			//	pictureBox1.BackColor = Color.MistyRose;
			}
		}

        private void timer_tick(object sender, EventArgs e)
        {
            if (resMsg != "busy")
            {
                pictureBox1.BackColor = Color.White;
            }
            else
            {
                pictureBox1.BackColor = Color.MistyRose;
            }
        }

		void toClientSend()
		{
			byte[] sendBytes = enc.GetBytes(ClientSendMsg + '\n');
			ns.Write(sendBytes, 0, sendBytes.Length);
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			//Start.Enabled = false;
			Stop.Enabled = true;
			MainMenu.Enabled = true;

			button1.Enabled = false;
			checkBox1.Enabled = false;

			stop = false;

			enc = System.Text.Encoding.UTF8;

			//			System.Threading.Thread thread = new System.Threading.Thread(DoVoiceRecognition);
			//			thread.Start();
			//			PutLabel1Text("初期化中...");
		}

		delegate void PerformClickButton();
		void PerformClickButton1()
		{
			if (this.button1.InvokeRequired)
			{
				PerformClickButton d = new PerformClickButton(PerformClickButton1);
				this.Invoke(d, new object[] { });
			}
			else
			{
				this.button1.PerformClick();
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			ServerWaitingThread = new Thread(ServerWaiting);
			ServerWaitingThread.Priority = ThreadPriority.Lowest;
			ServerWaitingThread.Start();
			button2.Enabled = false;
			UnityProcess = Process.Start(UnityExecNameFullPath, "-popupwindow");

		}
		void ServerWaiting()
		{
			System.Net.IPAddress ipAdd = System.Net.IPAddress.Parse(ipString);
			listener = new System.Net.Sockets.TcpListener(ipAdd, port);
			listener.Start();
			HostIP_Port = ((IPEndPoint)listener.LocalEndpoint).Address.ToString() + " : " + ((IPEndPoint)listener.LocalEndpoint).Port.ToString();
			client = listener.AcceptTcpClient();
			ClientIP_Port = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() + " : " + ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Port.ToString();
			DispIPInfo();

			DEBUG_STR = "Client: Connect\n" + ClientIP_Port;
			DispIPInfo();

			ServerThread = new Thread(ServerReadWrite);
			ServerThread.Priority = ThreadPriority.Lowest;
			ServerThread.Start();

			Thread thread = new System.Threading.Thread(DoVoiceRecognition);
			thread.Start();
			PutLabel1Text("初期化中...");
		}

		delegate void MyText();
		private void DispIPInfo()
		{
			if (this.label6.InvokeRequired)
			{
				MyText d = new MyText(DispIPInfo);
				this.Invoke(d);
			}
			else
			{
				//				label32.Text = "Client: Connected" + ClientIP_Port;
				label6.Text = DEBUG_STR;
				button1.Enabled = true;
				Stop.Enabled = true;
				checkBox1.Enabled = true;
				MainMenu.Enabled = false;
			}
		}

		void ServerReadWrite()
		{
			while (true)
			{
				//				resMsg = null;
				ns = client.GetStream();
				ns.ReadTimeout = Timeout.Infinite;
				ns.WriteTimeout = Timeout.Infinite;

				bool disconnected = false;
				ms = new System.IO.MemoryStream();
				byte[] resBytes = new byte[256];
				int resSize = 0;
				do
				{
					try
					{
						resSize = ns.Read(resBytes, 0, resBytes.Length);
						if (resSize == 0)
						{
							disconnected = true;
							Console.WriteLine("クライアントが切断しました。");
							ServerThread.Abort();
							break;
						}
						ms.Write(resBytes, 0, resSize);
					}
					catch
					{
						;
					}
				} while (ns.DataAvailable || resBytes[resSize - 1] != '\n');

				resMsg = enc.GetString(ms.GetBuffer(), 0, (int)ms.Length);
				ms.Close();
				resMsg = resMsg.TrimEnd('\n');

				FromClientMessage = resMsg.Split(':');
				//				string rcvMsgNo = FromClientMessage[0] + ";" + FromClientMessage[1];
				//				string rcvMsg = FromClientMessage[2];
				//				PutServerText();
				if (!disconnected)
				{
					string sendMsg = resMsg.Length.ToString();
					byte[] sendBytes = enc.GetBytes(sendMsg + '\n');
					Console.WriteLine(sendMsg);
				}
			}
		}

	}
}
